#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NodeGraph.Core;
using NodeGraph.Unity;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// Action 节点的 Inspector 面板绘制器。
    /// 根据 ActionDefinition 的 PropertyDefinition[] 自动生成 EditorGUILayout 控件。
    /// 支持条件可见性（VisibleWhen）、多种属性类型、范围约束等。
    ///
    /// 与 ActionContentRenderer 的职责区分：
    /// - ActionContentRenderer → 画布摘要（4行关键属性文本）
    /// - ActionNodeInspectorDrawer → Inspector 完整编辑（所有属性、可交互控件）
    /// </summary>
    public class ActionNodeInspectorDrawer : INodeInspectorDrawer
    {
        private readonly ActionRegistry _actionRegistry;
        private BindingContext? _bindingContext;

        public ActionNodeInspectorDrawer(ActionRegistry actionRegistry)
        {
            _actionRegistry = actionRegistry;
        }

        /// <summary>设置场景绑定上下文（由编辑器窗口管理生命周期）</summary>
        public void SetBindingContext(BindingContext? context)
        {
            _bindingContext = context;
        }

        /// <summary>设置当前 Graph 引用（用于子蓝图 Inspector 和关卡总览）</summary>
        public void SetGraph(Graph? graph)
        {
            _currentGraph = graph;
        }

        public bool CanInspect(Node node)
        {
            // Action 节点 或 子蓝图代表节点 都可以 Inspect
            return node.UserData is ActionNodeData
                || node.TypeId == SubGraphConstants.BoundaryNodeTypeId;
        }

        public string GetTitle(Node node)
        {
            // 子蓝图代表节点：显示所属子蓝图的标题
            if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
            {
                return "\U0001F4E6 子蓝图";
            }

            var data = node.UserData as ActionNodeData;
            if (data == null) return node.TypeId;

            if (_actionRegistry.TryGet(data.ActionTypeId, out var def))
                return def.DisplayName;

            return data.ActionTypeId;
        }

        /// <summary>缓存的 Graph 引用（由 InspectorPanel 通过 DrawBlueprintInspector 传入）</summary>
        private Graph? _currentGraph;

        public bool DrawInspector(Node node)
        {
            // 子蓝图代表节点：绘制子蓝图摘要
            if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
            {
                return DrawSubGraphInspector(node);
            }

            var data = node.UserData as ActionNodeData;
            if (data == null)
            {
                EditorGUILayout.HelpBox("节点数据为空", MessageType.Warning);
                return false;
            }

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
            {
                EditorGUILayout.HelpBox($"未知类型: {data.ActionTypeId}", MessageType.Error);
                return false;
            }

            bool changed = false;

            // ── 节点信息头 ──
            EditorGUILayout.LabelField("类型", def.DisplayName);
            if (!string.IsNullOrEmpty(def.Category))
                EditorGUILayout.LabelField("分类", def.Category);
            EditorGUILayout.Space(4);

            // ── 普通属性 ──
            bool hasSceneBindings = false;
            foreach (var prop in def.Properties)
            {
                if (!string.IsNullOrEmpty(prop.VisibleWhen))
                {
                    if (!VisibleWhenEvaluator.Evaluate(prop.VisibleWhen, data.Properties))
                        continue;
                }

                // SceneBinding 属性单独分组，先跳过
                if (prop.Type == PropertyType.SceneBinding)
                {
                    hasSceneBindings = true;
                    continue;
                }

                if (DrawPropertyField(prop, data.Properties))
                    changed = true;
            }

            // ── 场景绑定区（分组显示）──
            if (hasSceneBindings)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("── 场景绑定 ──", EditorStyles.boldLabel);

                foreach (var prop in def.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding) continue;

                    if (!string.IsNullOrEmpty(prop.VisibleWhen))
                    {
                        if (!VisibleWhenEvaluator.Evaluate(prop.VisibleWhen, data.Properties))
                            continue;
                    }

                    if (DrawPropertyField(prop, data.Properties))
                        changed = true;
                }
            }

            return changed;
        }

        // ── 属性控件绘制（使用 EditorGUILayout 自动布局）──

        private bool DrawPropertyField(PropertyDefinition prop, PropertyBag bag)
        {
            bool changed = false;

            switch (prop.Type)
            {
                case PropertyType.Float:
                    changed = DrawFloatField(prop, bag);
                    break;

                case PropertyType.Int:
                    changed = DrawIntField(prop, bag);
                    break;

                case PropertyType.Bool:
                    changed = DrawBoolField(prop, bag);
                    break;

                case PropertyType.String:
                    changed = DrawStringField(prop, bag);
                    break;

                case PropertyType.Enum:
                    changed = DrawEnumField(prop, bag);
                    break;

                case PropertyType.AssetRef:
                    changed = DrawStringField(prop, bag);
                    break;

                case PropertyType.SceneBinding:
                    changed = DrawSceneBindingField(prop, bag);
                    break;

                case PropertyType.Tag:
                    changed = DrawStringField(prop, bag);
                    break;

                default:
                    EditorGUILayout.LabelField(prop.DisplayName, $"(不支持的类型 {prop.Type})");
                    break;
            }

            return changed;
        }

        private bool DrawFloatField(PropertyDefinition prop, PropertyBag bag)
        {
            float current = bag.Get<float>(prop.Key);
            float result;

            if (prop.Min.HasValue && prop.Max.HasValue)
                result = EditorGUILayout.Slider(prop.DisplayName, current, prop.Min.Value, prop.Max.Value);
            else
                result = EditorGUILayout.FloatField(prop.DisplayName, current);

            if (!result.Equals(current))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawIntField(PropertyDefinition prop, PropertyBag bag)
        {
            int current = bag.Get<int>(prop.Key);
            int result;

            if (prop.Min.HasValue && prop.Max.HasValue)
                result = EditorGUILayout.IntSlider(prop.DisplayName, current,
                    (int)prop.Min.Value, (int)prop.Max.Value);
            else
                result = EditorGUILayout.IntField(prop.DisplayName, current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawBoolField(PropertyDefinition prop, PropertyBag bag)
        {
            bool current = bag.Get<bool>(prop.Key);
            bool result = EditorGUILayout.Toggle(prop.DisplayName, current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawStringField(PropertyDefinition prop, PropertyBag bag)
        {
            string current = bag.Get<string>(prop.Key) ?? "";
            string result = EditorGUILayout.TextField(prop.DisplayName, current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawSceneBindingField(PropertyDefinition prop, PropertyBag bag)
        {
            string bindingTypeStr = prop.SceneBindingType?.ToString() ?? "Unknown";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行：显示名称 + 绑定类型
            EditorGUILayout.LabelField(
                $"\U0001F517 {prop.DisplayName} ({bindingTypeStr})",
                EditorStyles.miniLabel);

            if (_bindingContext != null)
            {
                // 从 BindingContext 读取当前绑定
                var current = _bindingContext.Get(prop.Key);
                var result = (GameObject?)EditorGUILayout.ObjectField(
                    "场景对象", current, typeof(GameObject), true);

                if (result != current)
                {
                    _bindingContext.Set(prop.Key, result);
                    // 同时在 PropertyBag 中记录绑定对象名（用于画布摘要显示）
                    bag.Set(prop.Key, result != null ? result.name : "");
                    EditorGUILayout.EndVertical();
                    return true;
                }

                if (current == null)
                {
                    EditorGUILayout.HelpBox("未绑定场景对象", MessageType.Warning);
                }
            }
            else
            {
                // 无 BindingContext 时降级为只读文本
                string name = bag.Get<string>(prop.Key) ?? "";
                EditorGUILayout.LabelField("场景对象", string.IsNullOrEmpty(name) ? "(未绑定)" : name);
                EditorGUILayout.HelpBox("请先保存蓝图以启用场景绑定", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            return false;
        }

        // ── 子蓝图 Inspector ──

        private bool DrawSubGraphInspector(Node repNode)
        {
            if (_currentGraph == null) return false;

            // 找到此代表节点所属的 SubGraphFrame
            var sgf = _currentGraph.FindContainerSubGraphFrame(repNode.Id);
            if (sgf == null)
            {
                EditorGUILayout.HelpBox("未找到子蓝图框", MessageType.Warning);
                return false;
            }

            // ── 子蓝图信息 ──
            EditorGUILayout.LabelField("名称", sgf.Title);
            EditorGUILayout.LabelField("内部节点", sgf.ContainedNodeIds.Count.ToString());

            // 统计内部连线数
            int internalEdgeCount = 0;
            var containedSet = new HashSet<string>(sgf.ContainedNodeIds);
            foreach (var edge in _currentGraph.Edges)
            {
                var sp = _currentGraph.FindPort(edge.SourcePortId);
                var tp = _currentGraph.FindPort(edge.TargetPortId);
                if (sp != null && tp != null && containedSet.Contains(sp.NodeId) && containedSet.Contains(tp.NodeId))
                    internalEdgeCount++;
            }
            EditorGUILayout.LabelField("内部连线", internalEdgeCount.ToString());

            // ── 边界端口 ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── 边界端口 ──", EditorStyles.boldLabel);

            foreach (var port in repNode.Ports)
            {
                string arrow = port.Direction == NodeGraph.Core.PortDirection.Input ? "● " : "";
                string suffix = port.Direction == NodeGraph.Core.PortDirection.Output ? " ●" : "";
                EditorGUILayout.LabelField($"  {arrow}{port.Name}{suffix}",
                    $"({port.Direction}, {port.Kind})");
            }

            // ── 场景绑定汇总 ──
            var bindings = CollectSubGraphBindings(sgf);
            if (bindings.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("── 场景绑定汇总 ──", EditorStyles.boldLabel);

                bool changed = false;
                foreach (var (prop, nodeData) in bindings)
                {
                    if (DrawSceneBindingField(prop, nodeData.Properties))
                        changed = true;
                }
                return changed;
            }

            return false;
        }

        /// <summary>收集子蓝图内所有 SceneBinding 属性</summary>
        private List<(PropertyDefinition prop, ActionNodeData data)> CollectSubGraphBindings(SubGraphFrame sgf)
        {
            var result = new List<(PropertyDefinition, ActionNodeData)>();
            if (_currentGraph == null) return result;

            foreach (var nodeId in sgf.ContainedNodeIds)
            {
                var node = _currentGraph.FindNode(nodeId);
                if (node?.UserData is not ActionNodeData data) continue;
                if (!_actionRegistry.TryGet(data.ActionTypeId, out var def)) continue;

                foreach (var prop in def.Properties)
                {
                    if (prop.Type == PropertyType.SceneBinding)
                        result.Add((prop, data));
                }
            }
            return result;
        }

        // ── 关卡总览（无选中时）──

        public bool DrawBlueprintInspector(Graph graph)
        {
            _currentGraph = graph;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("\U0001F5FA\uFE0F 关卡总览", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 蓝图信息 ──
            int subGraphCount = graph.SubGraphFrames.Count;
            int totalNodes = graph.Nodes.Count;
            int totalEdges = graph.Edges.Count;

            EditorGUILayout.LabelField("子蓝图", subGraphCount.ToString());
            EditorGUILayout.LabelField("总节点", totalNodes.ToString());
            EditorGUILayout.LabelField("总连线", totalEdges.ToString());

            // ── 子蓝图列表 ──
            if (subGraphCount > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("── 子蓝图列表 ──", EditorStyles.boldLabel);

                foreach (var sgf in graph.SubGraphFrames)
                {
                    var bindings = CollectSubGraphBindings(sgf);
                    int bound = 0;
                    int total = bindings.Count;

                    if (_bindingContext != null)
                    {
                        foreach (var (prop, _) in bindings)
                        {
                            if (_bindingContext.Get(prop.Key) != null) bound++;
                        }
                    }

                    string status = total == 0 ? ""
                        : bound == total ? $"  绑定: {bound}/{total} \u2705"
                        : $"  绑定: {bound}/{total} \u26A0\uFE0F";

                    string icon = sgf.IsCollapsed ? "\U0001F4E6" : "\U0001F4C2";
                    EditorGUILayout.LabelField($"  {icon} {sgf.Title}{status}");
                }
            }

            // ── 全部未绑定项 ──
            if (_bindingContext != null)
            {
                var allUnbound = new List<(string subGraphTitle, PropertyDefinition prop)>();

                foreach (var sgf in graph.SubGraphFrames)
                {
                    var bindings = CollectSubGraphBindings(sgf);
                    foreach (var (prop, _) in bindings)
                    {
                        if (_bindingContext.Get(prop.Key) == null)
                            allUnbound.Add((sgf.Title, prop));
                    }
                }

                if (allUnbound.Count > 0)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("── 未绑定项 ──", EditorStyles.boldLabel);

                    foreach (var (title, prop) in allUnbound)
                    {
                        string bindingType = prop.SceneBindingType?.ToString() ?? "?";
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"\u26A0\uFE0F {title} / {prop.DisplayName} ({bindingType})",
                            EditorStyles.miniLabel);

                        var current = _bindingContext.Get(prop.Key);
                        var result = (GameObject?)EditorGUILayout.ObjectField(
                            "", current, typeof(GameObject), true);
                        if (result != current)
                        {
                            _bindingContext.Set(prop.Key, result);
                        }
                        EditorGUILayout.EndVertical();
                    }

                    return true; // 有可编辑内容
                }
            }

            return true; // 显示了总览信息
        }

        // ── 属性控件 ──

        private bool DrawEnumField(PropertyDefinition prop, PropertyBag bag)
        {
            if (prop.EnumOptions == null || prop.EnumOptions.Length == 0)
            {
                EditorGUILayout.LabelField(prop.DisplayName, "(无枚举选项)");
                return false;
            }

            string current = bag.Get<string>(prop.Key) ?? prop.EnumOptions[0];
            int selectedIndex = System.Array.IndexOf(prop.EnumOptions, current);
            if (selectedIndex < 0) selectedIndex = 0;

            int newIndex = EditorGUILayout.Popup(prop.DisplayName, selectedIndex, prop.EnumOptions);
            if (newIndex != selectedIndex && newIndex >= 0 && newIndex < prop.EnumOptions.Length)
            {
                bag.Set(prop.Key, prop.EnumOptions[newIndex]);
                return true;
            }
            return false;
        }
    }
}
