#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Export;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 蓝图导出器。将编辑器中的 Graph 转换为运行时可消费的 SceneBlueprintData。
    ///
    /// 导出流程：
    /// 1. 展平子蓝图：跳过 __SubGraphBoundary 节点，合并穿过边界的连线
    /// 2. 遍历所有节点 → ActionEntry（属性扁平化为 PropertyValue[]）
    /// 3. 遍历所有连线 → TransitionEntry（含 ConditionData）
    /// 4. 合并场景绑定（从 Manager 或 BindingContext 读取）
    /// 5. 执行验证规则
    /// 6. 返回 SceneBlueprintData
    /// </summary>
    public static class BlueprintExporter
    {
        /// <summary>
        /// 导出选项。
        /// </summary>
        public sealed class ExportOptions
        {
            /// <summary>空间适配器类型标识（C2 默认 Unity3D）。</summary>
            public string AdapterType = "Unity3D";
        }

        /// <summary>
        /// 场景绑定数据（由调用方从 Manager 或 BindingContext 提供）。
        /// </summary>
        public class SceneBindingData
        {
            public string BindingKey = "";
            public string BindingType = "";
            public string StableObjectId = "";
            public string AdapterType = "";
            public string SpatialPayloadJson = "";
            public string SourceSubGraph = "";
            public string SourceActionTypeId = "";
        }

        /// <summary>
        /// 将 Graph 导出为 SceneBlueprintData（兼容旧接口，无场景绑定）。
        /// </summary>
        public static ExportResult Export(
            Graph graph,
            ActionRegistry registry,
            string? blueprintId = null,
            string? blueprintName = null)
        {
            return Export(graph, registry, null, blueprintId, blueprintName, null);
        }

        /// <summary>
        /// 将 Graph 导出为 SceneBlueprintData，合并场景绑定数据。
        /// </summary>
        /// <param name="graph">编辑器中的蓝图图</param>
        /// <param name="registry">ActionRegistry</param>
        /// <param name="sceneBindings">场景绑定数据（来自 Manager），可为 null</param>
        /// <param name="blueprintId">蓝图 ID（可选）</param>
        /// <param name="blueprintName">蓝图名称（可选）</param>
        public static ExportResult Export(
            Graph graph,
            ActionRegistry registry,
            List<SceneBindingData>? sceneBindings,
            string? blueprintId = null,
            string? blueprintName = null,
            ExportOptions? options = null)
        {
            var messages = new List<ValidationMessage>();
            var exportOptions = options ?? new ExportOptions();

            // ── Step 0: 收集边界节点 ID（用于展平过滤）──
            var boundaryNodeIds = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
                    boundaryNodeIds.Add(node.Id);
            }

            // ── Step 1: 节点 → ActionEntry（跳过边界节点）──
            var actions = new List<ActionEntry>();
            foreach (var node in graph.Nodes)
            {
                if (boundaryNodeIds.Contains(node.Id)) continue;

                var entry = ExportNode(node, registry, messages, exportOptions);
                if (entry != null)
                    actions.Add(entry);
            }

            // ── Step 2: 展平连线（合并穿过边界节点的连线）──
            var transitions = ExportEdgesFlattened(graph, boundaryNodeIds, registry, messages);

            // ── Step 3: 合并场景绑定（同时将 bindingKey 统一升级为 scoped key）──
            MergeSceneBindings(
                actions,
                sceneBindings ?? new List<SceneBindingData>(),
                messages,
                exportOptions,
                graph);

            // ── Step 3.5: 收集 Annotation 数据（后处理）──
            EnrichBindingsWithAnnotations(actions, registry, messages);

            // ── Step 4: 验证 ──
            Validate(graph, registry, actions, boundaryNodeIds, messages);

            // ── Step 5: 组装 ──
            var data = new SceneBlueprintData
            {
                BlueprintId = blueprintId ?? graph.Id,
                BlueprintName = blueprintName ?? "",
                Version = 2,
                ExportTime = DateTime.UtcNow.ToString("o"),
                Actions = actions.ToArray(),
                Transitions = transitions.ToArray()
            };

            return new ExportResult(data, messages);
        }

        // ══════════════════════════════════════
        //  节点导出
        // ══════════════════════════════════════

        private static ActionEntry? ExportNode(
            Node node,
            ActionRegistry registry,
            List<ValidationMessage> messages,
            ExportOptions options)
        {
            var nodeData = node.UserData as ActionNodeData;
            if (nodeData == null)
            {
                messages.Add(ValidationMessage.Warning(
                    $"节点 '{node.Id}' 无 ActionNodeData，已跳过"));
                return null;
            }

            var entry = new ActionEntry
            {
                Id = node.Id,
                TypeId = nodeData.ActionTypeId
            };

            // 导出属性
            if (registry.TryGet(nodeData.ActionTypeId, out var def))
            {
                var properties = new List<PropertyValue>();
                var sceneBindings = new List<SceneBindingEntry>();

                foreach (var prop in def.Properties)
                {
                    if (prop.Type == PropertyType.SceneBinding)
                    {
                        // SceneBinding 提升为独立条目
                        var value = nodeData.Properties.Get<string>(prop.Key) ?? "";
                        if (!string.IsNullOrEmpty(value))
                        {
                            sceneBindings.Add(new SceneBindingEntry
                            {
                                BindingKey = prop.Key,
                                BindingType = prop.SceneBindingType?.ToString() ?? "Transform",
                                // 节点属性里的值通常是业务侧标识（如 MarkerId），
                                // C2 起同时作为稳定 ID 回退值。
                                SceneObjectId = value,
                                StableObjectId = value,
                                AdapterType = options.AdapterType,
                                SpatialPayloadJson = "{}"
                            });
                        }
                    }
                    else
                    {
                        var pv = ExportPropertyValue(prop, nodeData.Properties);
                        if (pv != null)
                            properties.Add(pv);
                    }
                }

                entry.Properties = properties.ToArray();
                entry.SceneBindings = sceneBindings.ToArray();
            }
            else
            {
                messages.Add(ValidationMessage.Error(
                    $"节点 '{node.Id}' 的类型 '{nodeData.ActionTypeId}' 未在 ActionRegistry 中注册"));
            }

            return entry;
        }

        private static PropertyValue? ExportPropertyValue(
            PropertyDefinition prop, PropertyBag bag)
        {
            var raw = bag.GetRaw(prop.Key);
            if (raw == null) return null;

            string valueType = PropertyTypeToString(prop.Type);
            string value = SerializePropertyValue(prop.Type, raw);

            return new PropertyValue
            {
                Key = prop.Key,
                ValueType = valueType,
                Value = value
            };
        }

        private static string PropertyTypeToString(PropertyType type)
        {
            return type switch
            {
                PropertyType.Float => "float",
                PropertyType.Int => "int",
                PropertyType.Bool => "bool",
                PropertyType.String => "string",
                PropertyType.Enum => "enum",
                PropertyType.AssetRef => "assetRef",
                PropertyType.Vector2 => "vector2",
                PropertyType.Vector3 => "vector3",
                PropertyType.Color => "color",
                PropertyType.Tag => "tag",
                _ => "string"
            };
        }

        private static string SerializePropertyValue(PropertyType type, object value)
        {
            return type switch
            {
                PropertyType.Float => Convert.ToSingle(value).ToString("G", CultureInfo.InvariantCulture),
                PropertyType.Int => Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture),
                PropertyType.Bool => Convert.ToBoolean(value) ? "true" : "false",
                _ => value.ToString() ?? ""
            };
        }

        // ══════════════════════════════════════
        //  连线导出（展平子蓝图）
        // ══════════════════════════════════════

        /// <summary>
        /// 展平导出所有连线。对于穿过边界节点的连线，合并为直接连接。
        ///
        /// 边界节点（RepresentativeNode）充当中继：
        /// - 外部 A → Rep.inPort (edge1)，Rep.inPort → 内部 B (edge2) → 合并为 A → B
        /// - 内部 X → Rep.outPort (edge3)，Rep.outPort → 外部 Y (edge4) → 合并为 X → Y
        /// - 不涉及边界节点的连线直接导出
        /// </summary>
        private static TransitionEntry[] ExportEdgesFlattened(
            Graph graph, HashSet<string> boundaryNodeIds, ActionRegistry registry, List<ValidationMessage> messages)
        {
            if (boundaryNodeIds.Count == 0)
            {
                // 无子蓝图，直接导出所有连线
                var simple = new List<TransitionEntry>();
                foreach (var edge in graph.Edges)
                {
                    var entry = ExportEdgeDirect(edge, graph, registry, messages);
                    if (entry != null) simple.Add(entry);
                }
                return simple.ToArray();
            }

            // 1. 建立边界端口的入边和出边索引（支持多对多）
            //    incomingToPort[portId] = 所有连入此边界端口的 source port 列表
            //    outgoingFromPort[portId] = 所有从此边界端口连出的 target port 列表
            var incomingToPort = new Dictionary<string, List<NodeGraph.Core.Port>>();
            var outgoingFromPort = new Dictionary<string, List<NodeGraph.Core.Port>>();

            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null) continue;

                bool sourceIsBoundary = boundaryNodeIds.Contains(sp.NodeId);
                bool targetIsBoundary = boundaryNodeIds.Contains(tp.NodeId);

                if (!sourceIsBoundary && targetIsBoundary)
                {
                    if (!incomingToPort.TryGetValue(edge.TargetPortId, out var list))
                    {
                        list = new List<NodeGraph.Core.Port>();
                        incomingToPort[edge.TargetPortId] = list;
                    }
                    list.Add(sp);
                }
                else if (sourceIsBoundary && !targetIsBoundary)
                {
                    if (!outgoingFromPort.TryGetValue(edge.SourcePortId, out var list))
                    {
                        list = new List<NodeGraph.Core.Port>();
                        outgoingFromPort[edge.SourcePortId] = list;
                    }
                    list.Add(tp);
                }
            }

            // 2. 遍历所有连线，分类处理
            var transitions = new List<TransitionEntry>();

            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null)
                {
                    messages.Add(ValidationMessage.Warning(
                        $"连线 '{edge.Id}' 的端口未找到，已跳过"));
                    continue;
                }

                bool sourceIsBoundary = boundaryNodeIds.Contains(sp.NodeId);
                bool targetIsBoundary = boundaryNodeIds.Contains(tp.NodeId);

                if (!sourceIsBoundary && !targetIsBoundary)
                {
                    // 两端都不是边界节点 → 直接导出
                    transitions.Add(new TransitionEntry
                    {
                        FromActionId = sp.NodeId,
                        FromPortId = ResolvePortSemanticId(graph, sp, registry),
                        ToActionId = tp.NodeId,
                        ToPortId = ResolvePortSemanticId(graph, tp, registry),
                        Condition = new ConditionData { Type = "Immediate" }
                    });
                }
                else if (sourceIsBoundary && !targetIsBoundary)
                {
                    // 边界.outPort → 非边界：查找谁连入了这个边界端口
                    if (incomingToPort.TryGetValue(edge.SourcePortId, out var sources))
                    {
                        foreach (var realSource in sources)
                        {
                            transitions.Add(new TransitionEntry
                            {
                                FromActionId = realSource.NodeId,
                                FromPortId = ResolvePortSemanticId(graph, realSource, registry),
                                ToActionId = tp.NodeId,
                                ToPortId = ResolvePortSemanticId(graph, tp, registry),
                                Condition = new ConditionData { Type = "Immediate" }
                            });
                        }
                    }
                }
                else if (!sourceIsBoundary && targetIsBoundary)
                {
                    // 非边界 → 边界.inPort：查找这个边界端口连向谁
                    if (outgoingFromPort.TryGetValue(edge.TargetPortId, out var targets))
                    {
                        foreach (var realTarget in targets)
                        {
                            transitions.Add(new TransitionEntry
                            {
                                FromActionId = sp.NodeId,
                                FromPortId = ResolvePortSemanticId(graph, sp, registry),
                                ToActionId = realTarget.NodeId,
                                ToPortId = ResolvePortSemanticId(graph, realTarget, registry),
                                Condition = new ConditionData { Type = "Immediate" }
                            });
                        }
                    }
                }
                // sourceIsBoundary && targetIsBoundary → 跳过
            }

            return transitions.ToArray();
        }

        private static TransitionEntry? ExportEdgeDirect(
            Edge edge, Graph graph, ActionRegistry registry, List<ValidationMessage> messages)
        {
            var sourcePort = graph.FindPort(edge.SourcePortId);
            var targetPort = graph.FindPort(edge.TargetPortId);

            if (sourcePort == null || targetPort == null)
            {
                messages.Add(ValidationMessage.Warning(
                    $"连线 '{edge.Id}' 的端口未找到，已跳过"));
                return null;
            }

            return new TransitionEntry
            {
                FromActionId = sourcePort.NodeId,
                FromPortId = ResolvePortSemanticId(graph, sourcePort, registry),
                ToActionId = targetPort.NodeId,
                ToPortId = ResolvePortSemanticId(graph, targetPort, registry),
                Condition = new ConditionData { Type = "Immediate" }
            };
        }

        /// <summary>
        /// 从 Port.Name（显示名）反查 ActionDefinition 中的语义 ID。
        /// <para>适配器层将 SBPortDef.DisplayName 传给了 NGPortDef.Name，
        /// 导出时需要还原为原始的语义 ID（如 "in"/"out"）。</para>
        /// </summary>
        private static string ResolvePortSemanticId(
            Graph graph, NodeGraph.Core.Port port, ActionRegistry registry)
        {
            var node = graph.FindNode(port.NodeId);
            if (node?.UserData is ActionNodeData data
                && registry.TryGet(data.ActionTypeId, out var actionDef))
            {
                foreach (var sbPort in actionDef.Ports)
                {
                    // 通过显示名 + 方向匹配回语义 ID
                    var displayName = string.IsNullOrEmpty(sbPort.DisplayName) ? sbPort.Id : sbPort.DisplayName;
                    if (displayName == port.Name
                        && sbPort.Direction == port.Direction)
                    {
                        return sbPort.Id;
                    }
                }
            }
            // 回退：无法反查时使用原始 Name（边界节点等特殊情况）
            return port.Name;
        }

        // ══════════════════════════════════════
        //  场景绑定合并
        // ══════════════════════════════════════

        /// <summary>
        /// 将 Manager 中的场景绑定数据合并到对应 ActionEntry 的 SceneBindings 字段。
        /// </summary>
        private static void MergeSceneBindings(
            List<ActionEntry> actions,
            List<SceneBindingData> sceneBindings,
            List<ValidationMessage> messages,
            ExportOptions options,
            Graph graph)
        {
            // 建立 bindingKey → data 索引
            var bindingMap = new Dictionary<string, SceneBindingData>();
            foreach (var bd in sceneBindings)
            {
                if (!string.IsNullOrEmpty(bd.BindingKey))
                    bindingMap[bd.BindingKey] = bd;
            }

            var actionScopeMap = BuildActionScopeMap(graph);

            // 更新每个 ActionEntry 中已存在的 SceneBindingEntry
            foreach (var action in actions)
            {
                if (action.SceneBindings.Length == 0) continue;

                foreach (var sb in action.SceneBindings)
                {
                    string rawBindingKey = sb.BindingKey;
                    string scopeId = actionScopeMap.TryGetValue(action.Id, out var mappedScope)
                        ? mappedScope
                        : BindingScopeUtility.TopLevelScopeId;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(scopeId, rawBindingKey, action.Id);

                    // 导出统一使用 scopedBindingKey（C5）
                    sb.BindingKey = scopedBindingKey;

                    if (bindingMap.TryGetValue(scopedBindingKey, out var data)
                        || bindingMap.TryGetValue(rawBindingKey, out data))
                    {
                        // 仅使用 V2 语义：StableObjectId 作为唯一绑定标识。
                        var stableId = data.StableObjectId;
                        if (string.IsNullOrEmpty(stableId))
                            stableId = sb.StableObjectId;

                        sb.StableObjectId = stableId;
                        sb.AdapterType = !string.IsNullOrEmpty(data.AdapterType)
                            ? data.AdapterType
                            : options.AdapterType;
                        sb.SpatialPayloadJson = !string.IsNullOrEmpty(data.SpatialPayloadJson)
                            ? data.SpatialPayloadJson
                            : "{}";

                        // 为兼容运行时消费端，SceneObjectId 同步写入稳定 ID。
                        sb.SceneObjectId = stableId;

                        sb.SourceSubGraph = data.SourceSubGraph;
                        sb.SourceActionTypeId = data.SourceActionTypeId;

                        if (string.IsNullOrEmpty(stableId))
                        {
                            messages.Add(ValidationMessage.Warning(
                                $"场景绑定 '{sb.BindingKey}' (Action: {action.Id}) 未配置场景对象"));
                        }
                    }
                    else
                    {
                        // 无 Manager 绑定时，沿用节点导出的 StableObjectId，并补齐字段。
                        sb.SceneObjectId = sb.StableObjectId;

                        if (string.IsNullOrEmpty(sb.AdapterType))
                            sb.AdapterType = options.AdapterType;

                        if (string.IsNullOrEmpty(sb.SpatialPayloadJson))
                            sb.SpatialPayloadJson = "{}";

                        if (string.IsNullOrEmpty(sb.StableObjectId))
                        {
                            messages.Add(ValidationMessage.Warning(
                                $"场景绑定 '{sb.BindingKey}' (Action: {action.Id}) 未配置场景对象"));
                        }
                    }
                }
            }
        }

        private static Dictionary<string, string> BuildActionScopeMap(Graph graph)
        {
            var map = new Dictionary<string, string>();

            foreach (var node in graph.Nodes)
            {
                map[node.Id] = BindingScopeUtility.TopLevelScopeId;
            }

            foreach (var sgf in graph.SubGraphFrames)
            {
                foreach (var nodeId in sgf.ContainedNodeIds)
                {
                    map[nodeId] = sgf.Id;
                }
            }

            return map;
        }

        // ══════════════════════════════════════
        //  Annotation 数据收集（后处理）
        // ══════════════════════════════════════

        /// <summary>
        /// 遍历所有 ActionEntry 的 SceneBindings，通过 StableObjectId（MarkerId）
        /// 在场景中查找对应的 Marker，收集其上的 MarkerAnnotation 数据。
        /// <para>
        /// 特殊处理 AreaMarker 绑定：展开其子 PointMarker，为每个子点位生成
        /// 独立的 SceneBindingEntry（含 Annotation 数据）。
        /// </para>
        /// </summary>
        private static void EnrichBindingsWithAnnotations(
            List<ActionEntry> actions,
            ActionRegistry registry,
            List<ValidationMessage> messages)
        {
            foreach (var action in actions)
            {
                if (action.SceneBindings.Length == 0) continue;

                var expandedBindings = new List<SceneBindingEntry>();

                foreach (var sb in action.SceneBindings)
                {
                    var markerId = sb.StableObjectId;
                    if (string.IsNullOrEmpty(markerId))
                    {
                        expandedBindings.Add(sb);
                        continue;
                    }

                    var marker = AnnotationExportHelper.FindMarkerById(markerId);
                    if (marker == null)
                    {
                        expandedBindings.Add(sb);
                        continue;
                    }

                    // ── AreaMarker：展开子 PointMarker ──
                    if (marker is AreaMarker area)
                    {
                        var childPoints = AnnotationExportHelper.CollectChildPointMarkers(area);
                        if (childPoints.Count == 0)
                        {
                            messages.Add(ValidationMessage.Warning(
                                $"AreaMarker '{area.GetDisplayLabel()}' (ID: {area.MarkerId}) " +
                                $"没有子 PointMarker (Action: {action.Id})"));
                            expandedBindings.Add(sb);
                        }
                        else
                        {
                            // 为每个子 PointMarker 生成独立的 SceneBindingEntry
                            foreach (var pm in childPoints)
                            {
                                var pmStableId = "marker:" + pm.MarkerId;
                                var childSb = new SceneBindingEntry
                                {
                                    BindingKey = sb.BindingKey,
                                    BindingType = "Transform",
                                    SceneObjectId = pmStableId,
                                    StableObjectId = pmStableId,
                                    AdapterType = sb.AdapterType,
                                    SpatialPayloadJson = AnnotationExportHelper.BuildPointSpatialPayload(pm),
                                    SourceSubGraph = sb.SourceSubGraph,
                                    SourceActionTypeId = sb.SourceActionTypeId,
                                    Annotations = AnnotationExportHelper.CollectAnnotations(
                                        pm, action.TypeId)
                                };
                                expandedBindings.Add(childSb);
                            }

                            messages.Add(ValidationMessage.Info(
                                $"AreaMarker '{area.GetDisplayLabel()}' 展开为 {childPoints.Count} 个子点位 " +
                                $"(Action: {action.Id})"));
                        }
                    }
                    // ── PointMarker：直接收集 Annotation ──
                    else if (marker is PointMarker pm)
                    {
                        sb.Annotations = AnnotationExportHelper.CollectAnnotations(
                            pm, action.TypeId);
                        expandedBindings.Add(sb);
                    }
                    else
                    {
                        // 其他 Marker 类型：原样保留
                        expandedBindings.Add(sb);
                    }
                }

                action.SceneBindings = expandedBindings.ToArray();
            }
        }

        // ══════════════════════════════════════
        //  验证
        // ══════════════════════════════════════

        private static void Validate(
            Graph graph, ActionRegistry registry,
            List<ActionEntry> actions, HashSet<string> boundaryNodeIds,
            List<ValidationMessage> messages)
        {
            // 规则 1：必须有且仅有一个 Flow.Start
            var startNodes = actions.Where(a => a.TypeId == "Flow.Start").ToList();
            if (startNodes.Count == 0)
                messages.Add(ValidationMessage.Error("蓝图缺少 Flow.Start 入口节点"));
            else if (startNodes.Count > 1)
                messages.Add(ValidationMessage.Error($"蓝图有 {startNodes.Count} 个 Flow.Start 节点，应仅有一个"));

            // 规则 2：TypeId 已注册
            foreach (var action in actions)
            {
                if (!registry.TryGet(action.TypeId, out _))
                {
                    // 已在 ExportNode 中报告，此处跳过重复
                }
            }

            // 规则 3：孤立节点（无连入也无连出，跳过边界节点）
            var connectedNodeIds = new HashSet<string>();
            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp != null) connectedNodeIds.Add(sp.NodeId);
                if (tp != null) connectedNodeIds.Add(tp.NodeId);
            }

            foreach (var node in graph.Nodes)
            {
                if (boundaryNodeIds.Contains(node.Id)) continue;

                if (!connectedNodeIds.Contains(node.Id))
                {
                    messages.Add(ValidationMessage.Warning(
                        $"节点 '{node.Id}' (TypeId: {(node.UserData as ActionNodeData)?.ActionTypeId ?? "?"}) 是孤立节点（无连入也无连出）"));
                }
            }

            // 规则 4：端口连接方向合法性（Out → In，跳过边界节点相关连线）
            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null) continue;
                if (boundaryNodeIds.Contains(sp.NodeId) || boundaryNodeIds.Contains(tp.NodeId)) continue;

                if (sp.Direction != NodeGraph.Core.PortDirection.Output || tp.Direction != NodeGraph.Core.PortDirection.Input)
                {
                    messages.Add(ValidationMessage.Error(
                        $"连线 '{edge.Id}' 方向错误：{sp.Direction} → {tp.Direction}"));
                }
            }

            // 规则 5：子蓝图数量统计（信息级别）
            if (graph.SubGraphFrames.Count > 0)
            {
                messages.Add(ValidationMessage.Info(
                    $"展平了 {graph.SubGraphFrames.Count} 个子蓝图，" +
                    $"跳过了 {boundaryNodeIds.Count} 个边界节点"));
            }

            // 规则 6：执行 SO 配置的验证规则
            ValidateSOPRules(graph, registry, actions, boundaryNodeIds, messages);
        }

        /// <summary>
        /// 执行通过 ValidationRuleSO 配置的验证规则。
        /// 与 C# 内置规则合并，补充业务层面的验证。
        /// </summary>
        private static void ValidateSOPRules(
            Graph graph, ActionRegistry registry,
            List<ActionEntry> actions, HashSet<string> boundaryNodeIds,
            List<ValidationMessage> messages)
        {
            var rules = ValidationRuleRegistry.GetEnabled();
            if (rules.Count == 0) return;

            foreach (var rule in rules)
            {
                switch (rule.Type)
                {
                    case ValidationType.PropertyRequired:
                        ValidatePropertyRequired(rule, graph, registry, messages);
                        break;
                    case ValidationType.BindingRequired:
                        ValidateBindingRequired(rule, graph, registry, messages);
                        break;
                    case ValidationType.MinNodesInSubGraph:
                        ValidateMinNodesInSubGraph(rule, graph, boundaryNodeIds, messages);
                        break;
                }
            }
        }

        /// <summary>PropertyRequired：指定 Action 的指定属性必须非空</summary>
        private static void ValidatePropertyRequired(
            ValidationRuleSO rule, Graph graph, ActionRegistry registry,
            List<ValidationMessage> messages)
        {
            if (string.IsNullOrEmpty(rule.TargetActionTypeId) || string.IsNullOrEmpty(rule.TargetPropertyKey))
                return;

            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (data.ActionTypeId != rule.TargetActionTypeId) continue;

                var raw = data.Properties.GetRaw(rule.TargetPropertyKey);
                bool isEmpty = raw == null || (raw is string s && string.IsNullOrWhiteSpace(s));

                if (isEmpty)
                {
                    var msg = $"[{rule.RuleId}] 节点 '{node.Id}' ({data.ActionTypeId}) 的属性 '{rule.TargetPropertyKey}' 不能为空";
                    if (!string.IsNullOrEmpty(rule.Description)) msg += $" — {rule.Description}";
                    messages.Add(ToMessage(rule.Severity, msg));
                }
            }
        }

        /// <summary>BindingRequired：指定 Action 的所有 SceneBinding 必须已配置</summary>
        private static void ValidateBindingRequired(
            ValidationRuleSO rule, Graph graph, ActionRegistry registry,
            List<ValidationMessage> messages)
        {
            if (string.IsNullOrEmpty(rule.TargetActionTypeId)) return;
            if (!registry.TryGet(rule.TargetActionTypeId, out var actionDef)) return;

            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (data.ActionTypeId != rule.TargetActionTypeId) continue;

                foreach (var req in actionDef.SceneRequirements)
                {
                    var val = data.Properties.Get<string>(req.BindingKey);
                    if (string.IsNullOrEmpty(val))
                    {
                        var msg = $"[{rule.RuleId}] 节点 '{node.Id}' ({data.ActionTypeId}) 缺少场景绑定: {req.DisplayName}";
                        if (!string.IsNullOrEmpty(rule.Description)) msg += $" — {rule.Description}";
                        messages.Add(ToMessage(rule.Severity, msg));
                    }
                }
            }
        }

        /// <summary>MinNodesInSubGraph：子蓝图内至少 N 个节点</summary>
        private static void ValidateMinNodesInSubGraph(
            ValidationRuleSO rule, Graph graph, HashSet<string> boundaryNodeIds,
            List<ValidationMessage> messages)
        {
            foreach (var frame in graph.SubGraphFrames)
            {
                // 统计非边界节点的子图内节点数
                int count = 0;
                foreach (var node in graph.Nodes)
                {
                    if (frame.ContainedNodeIds.Contains(node.Id) && !boundaryNodeIds.Contains(node.Id))
                        count++;
                }

                if (count < rule.MinNodeCount)
                {
                    var msg = $"[{rule.RuleId}] 子蓝图 '{frame.Title}' 只有 {count} 个节点，最少需要 {rule.MinNodeCount} 个";
                    if (!string.IsNullOrEmpty(rule.Description)) msg += $" — {rule.Description}";
                    messages.Add(ToMessage(rule.Severity, msg));
                }
            }
        }

        /// <summary>将 ValidationSeverity 转为 ValidationMessage</summary>
        private static ValidationMessage ToMessage(Core.ValidationSeverity severity, string msg)
        {
            return severity switch
            {
                Core.ValidationSeverity.Error => ValidationMessage.Error(msg),
                Core.ValidationSeverity.Warning => ValidationMessage.Warning(msg),
                Core.ValidationSeverity.Info => ValidationMessage.Info(msg),
                _ => ValidationMessage.Info(msg)
            };
        }
    }

    // ══════════════════════════════════════
    //  导出结果
    // ══════════════════════════════════════

    /// <summary>导出结果，包含数据和验证消息</summary>
    public class ExportResult
    {
        public SceneBlueprintData Data { get; }
        public IReadOnlyList<ValidationMessage> Messages { get; }
        public bool HasErrors => Messages.Any(m => m.Level == ValidationLevel.Error);
        public bool HasWarnings => Messages.Any(m => m.Level == ValidationLevel.Warning);

        public ExportResult(SceneBlueprintData data, List<ValidationMessage> messages)
        {
            Data = data;
            Messages = messages;
        }
    }

    /// <summary>验证消息级别</summary>
    public enum ValidationLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>验证消息</summary>
    public class ValidationMessage
    {
        public ValidationLevel Level { get; }
        public string Message { get; }

        public ValidationMessage(ValidationLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public static ValidationMessage Info(string msg) => new(ValidationLevel.Info, msg);
        public static ValidationMessage Warning(string msg) => new(ValidationLevel.Warning, msg);
        public static ValidationMessage Error(string msg) => new(ValidationLevel.Error, msg);

        public override string ToString() => $"[{Level}] {Message}";
    }
}
