#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 场景绑定协调器（从 SceneBlueprintWindow 提取）。
    /// 持有 BindingContext 和 ISceneBindingStore，负责绑定的恢复、同步、导出收集和一致性验证。
    /// <para>
    /// 注意：SaveBlueprint() 不在本类职责范围，窗口应在调用 SyncToScene() 前自行确保资产已保存。
    /// </para>
    /// </summary>
    public class SceneBindingCoordinator
    {
        // ── 依赖 ──
        private readonly IBlueprintEditorContext _ctx;
        private readonly ISceneBindingStore      _store;

        // ── 延迟加载 ──
        private IEditorSpatialModeDescriptor? _spatialDescriptor;

        /// <summary>运行时内存绑定映射（nodeId/bindingKey → GameObject）。Inspector UI 仍需直接访问。</summary>
        public BindingContext BindingContext { get; }

        // ── 构造 ──

        public SceneBindingCoordinator(
            IBlueprintEditorContext ctx,
            BindingContext          bindingContext,
            ISceneBindingStore      store)
        {
            _ctx           = ctx;
            BindingContext = bindingContext;
            _store         = store;
        }

        // ══════════════════════════════════════════════
        //  公开 API
        // ══════════════════════════════════════════════

        /// <summary>
        /// 蓝图加载后从场景恢复绑定到 BindingContext。
        /// 策略1：SceneBindingStore → 策略2：PropertyBag MarkerId 反查。
        /// </summary>
        public void RestoreFromScene()
        {
            var vm    = _ctx.ViewModel;
            var asset = _ctx.CurrentAsset;
            if (vm == null || asset == null) return;

            BindingContext.Clear();

            // 策略1：从持久化存储恢复
            if (_store.TryLoadBindingGroups(asset, out var groups))
            {
                foreach (var g in groups)
                    foreach (var b in g.Bindings)
                        if (!string.IsNullOrEmpty(b.BindingKey) && b.BoundObject != null)
                            BindingContext.Set(b.BindingKey, b.BoundObject);
            }

            // 策略2：对未恢复的绑定用 MarkerId 在场景中回退查找
            var registry = _ctx.GetActionRegistry();
            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    string scopedKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (BindingContext.Get(scopedKey) != null) continue;

                    var storedId = data.Properties.Get<string>(prop.Key);
                    if (string.IsNullOrEmpty(storedId)) continue;

                    var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
                    if (marker != null) BindingContext.Set(scopedKey, marker.gameObject);
                }
            }

            int restored = BindingContext.BoundCount;
            if (restored > 0)
                SBLog.Info(SBLogTags.Binding, $"已从场景恢复 {restored} 个绑定");
        }

        /// <summary>
        /// 将当前绑定同步持久化到场景的 SceneBlueprintManager。
        /// 调用方需确保 CurrentAsset 已保存（非 null）。
        /// </summary>
        public void SyncToScene()
        {
            var vm    = _ctx.ViewModel;
            var asset = _ctx.CurrentAsset;
            if (vm == null || asset == null) return;

            var graph    = vm.Graph;
            var registry = _ctx.GetActionRegistry();
            var groups   = new List<SubGraphBindingGroup>();

            // 按子蓝图分组构建绑定数据
            foreach (var sgf in graph.SubGraphFrames)
            {
                var group = new SubGraphBindingGroup
                {
                    SubGraphFrameId = sgf.Id,
                    SubGraphTitle   = sgf.Title
                };
                var seen = new HashSet<string>();
                foreach (var nodeId in sgf.ContainedNodeIds)
                {
                    var node = graph.FindNode(nodeId);
                    if (node?.UserData is not ActionNodeData ad) continue;
                    if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;

                    foreach (var prop in def.Properties)
                    {
                        if (prop.Type != PropertyType.SceneBinding) continue;
                        string sk = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                        if (!seen.Add(sk)) continue;
                        group.Bindings.Add(new SceneBindingSlot
                        {
                            BindingKey         = sk,
                            BindingType        = prop.SceneBindingType ?? BindingType.Transform,
                            DisplayName        = prop.DisplayName,
                            SourceActionTypeId = ad.ActionTypeId,
                            BoundObject        = BindingContext.Get(sk)
                        });
                    }
                }
                if (group.Bindings.Count > 0) groups.Add(group);
            }

            // 收集顶层节点绑定
            var topLevel = CollectTopLevelBindings(graph, registry);
            if (topLevel?.Bindings.Count > 0) groups.Add(topLevel);

            _store.SaveBindingGroups(asset, groups);

            int total = groups.Sum(g => g.Bindings.Count);
            int bound = groups.Sum(g => g.Bindings.Count(b => b.IsBound));
            SBLog.Info(SBLogTags.Binding,
                $"已同步到场景: 分组={groups.Count}, 绑定={bound}/{total}");
        }

        /// <summary>
        /// 为导出收集绑定数据。优先读持久化存储，降级读 BindingContext 内存数据。
        /// </summary>
        public List<BlueprintExporter.SceneBindingData>? CollectForExport()
        {
            var asset    = _ctx.CurrentAsset;
            var registry = _ctx.GetActionRegistry();

            // 优先从持久化存储读取
            if (asset != null
                && _store.TryLoadBindingGroups(asset, out var groups)
                && groups.Count > 0)
            {
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var g in groups)
                    foreach (var b in g.Bindings)
                    {
                        EncodeBinding(b.BoundObject, b.BindingType,
                            out var sid, out var at, out var spj);
                        list.Add(new BlueprintExporter.SceneBindingData
                        {
                            BindingKey          = b.BindingKey,
                            BindingType         = b.BindingType.ToString(),
                            StableObjectId      = sid,
                            AdapterType         = at,
                            SpatialPayloadJson  = spj,
                            SourceSubGraph      = g.SubGraphTitle,
                            SourceActionTypeId  = b.SourceActionTypeId
                        });
                    }
                return list.Count > 0 ? list : null;
            }

            // 降级：从 BindingContext 读取
            if (BindingContext.Count > 0)
            {
                var typeMap = BuildBindingTypeMap(registry);
                var list    = new List<BlueprintExporter.SceneBindingData>();
                foreach (var kvp in BindingContext.All)
                {
                    string resolvedKey = ResolveBindingKey(kvp.Key, typeMap);
                    var    bindingType = typeMap.TryGetValue(resolvedKey, out var bt)
                        ? bt : BindingType.Transform;
                    EncodeBinding(kvp.Value, bindingType,
                        out var sid, out var at, out var spj);
                    list.Add(new BlueprintExporter.SceneBindingData
                    {
                        BindingKey         = resolvedKey,
                        StableObjectId     = sid,
                        AdapterType        = at,
                        SpatialPayloadJson = spj
                    });
                }
                return list.Count > 0 ? list : null;
            }

            return null;
        }

        /// <summary>执行标记绑定一致性验证（缺失/孤立/类型不匹配），结果输出到 Console。</summary>
        public ValidationReport RunBindingValidation()
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return new ValidationReport();
            var report = MarkerBindingValidator.Validate(vm.Graph, _ctx.GetActionRegistry());
            MarkerBindingValidator.LogReport(report);
            return report;
        }

        // ══════════════════════════════════════════════
        //  私有辅助
        // ══════════════════════════════════════════════

        private SubGraphBindingGroup? CollectTopLevelBindings(Graph graph, ActionRegistry registry)
        {
            var contained = new HashSet<string>(
                graph.SubGraphFrames.SelectMany(f => f.ContainedNodeIds));

            var group = new SubGraphBindingGroup
            {
                SubGraphFrameId = "__toplevel__",
                SubGraphTitle   = "顶层节点"
            };
            var seen = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (contained.Contains(node.Id)) continue;
                if (node.UserData is not ActionNodeData ad) continue;
                if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;

                foreach (var prop in def.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding) continue;
                    string sk = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (!seen.Add(sk)) continue;
                    group.Bindings.Add(new SceneBindingSlot
                    {
                        BindingKey         = sk,
                        BindingType        = prop.SceneBindingType ?? BindingType.Transform,
                        DisplayName        = prop.DisplayName,
                        SourceActionTypeId = ad.ActionTypeId,
                        BoundObject        = BindingContext.Get(sk)
                    });
                }
            }
            return group.Bindings.Count > 0 ? group : null;
        }

        private Dictionary<string, BindingType> BuildBindingTypeMap(ActionRegistry registry)
        {
            var map = new Dictionary<string, BindingType>();
            var vm  = _ctx.ViewModel;
            if (vm == null) return map;

            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData ad) continue;
                if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;
                foreach (var prop in def.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding || string.IsNullOrEmpty(prop.Key)) continue;
                    map[BindingScopeUtility.BuildScopedKey(node.Id, prop.Key)] =
                        prop.SceneBindingType ?? BindingType.Transform;
                }
            }
            return map;
        }

        private static string ResolveBindingKey(string key, Dictionary<string, BindingType> typeMap)
        {
            if (string.IsNullOrEmpty(key) || BindingScopeUtility.IsScopedKey(key)) return key;
            string? matched = null;
            foreach (var sk in typeMap.Keys)
            {
                if (BindingScopeUtility.ExtractRawBindingKey(sk) != key) continue;
                if (matched != null) return key; // 多个作用域同名，不猜测
                matched = sk;
            }
            return matched ?? key;
        }

        private void EncodeBinding(
            GameObject?  obj,
            BindingType  type,
            out string   stableObjectId,
            out string   adapterType,
            out string   spatialPayloadJson)
        {
            _spatialDescriptor ??= SpatialModeRegistry.GetProjectModeDescriptor();

            if (obj == null)
            {
                stableObjectId    = "";
                adapterType       = _spatialDescriptor.AdapterType;
                spatialPayloadJson = "{}";
                return;
            }

            var payload        = _spatialDescriptor.BindingCodec.Encode(obj, type);
            stableObjectId    = payload.StableObjectId;
            adapterType       = string.IsNullOrEmpty(payload.AdapterType)
                ? _spatialDescriptor.AdapterType : payload.AdapterType;
            spatialPayloadJson = string.IsNullOrEmpty(payload.SerializedSpatialData)
                ? "{}" : payload.SerializedSpatialData;
        }
    }
}

