#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NodeGraph.Abstraction;
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.Serialization
{
    /// <summary>
    /// JSON 图序列化器。实现 IGraphSerializer，用于跨引擎导入导出和剪贴板。
    /// 使用手写的轻量级 JSON 解析/生成（避免依赖第三方库）。
    /// </summary>
    public class JsonGraphSerializer : IGraphSerializer
    {
        private readonly IUserDataSerializer? _userDataSerializer;

        public JsonGraphSerializer(IUserDataSerializer? userDataSerializer = null)
        {
            _userDataSerializer = userDataSerializer;
        }

        // ══════════════════════════════════════
        //  序列化：Graph → JSON string
        // ══════════════════════════════════════

        public string Serialize(Graph graph)
        {
            var model = GraphToModel(graph);
            return SimpleJson.Serialize(model);
        }

        public Graph Deserialize(string json)
        {
            var model = SimpleJson.Deserialize<JsonGraphModel>(json);
            if (model == null)
                throw new InvalidOperationException("无法解析 JSON 数据");
            return ModelToGraph(model);
        }

        public string SerializeSubGraph(Graph graph, IEnumerable<string> nodeIds)
        {
            var nodeIdSet = new HashSet<string>(nodeIds);
            var model = new JsonGraphModel { id = "" };

            foreach (var node in graph.Nodes.Where(n => nodeIdSet.Contains(n.Id)))
                model.nodes.Add(NodeToModel(node));

            // 只保留两端都在选中集合中的连线
            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp != null && tp != null && nodeIdSet.Contains(sp.NodeId) && nodeIdSet.Contains(tp.NodeId))
                    model.edges.Add(EdgeToModel(edge));
            }

            return SimpleJson.Serialize(model);
        }

        public IEnumerable<Node> DeserializeSubGraphInto(Graph target, string data, Vec2 offset)
        {
            var model = SimpleJson.Deserialize<JsonGraphModel>(data);
            if (model == null) return Enumerable.Empty<Node>();

            // 生成新 ID 映射（避免冲突）
            var idMap = new Dictionary<string, string>();
            foreach (var nm in model.nodes)
            {
                idMap[nm.id] = IdGenerator.NewId();
                foreach (var pm in nm.ports)
                    idMap[pm.id] = IdGenerator.NewId();
            }
            foreach (var em in model.edges)
                idMap[em.id] = IdGenerator.NewId();

            // 创建节点（使用新 ID + 偏移）
            var newNodes = new List<Node>();
            foreach (var nm in model.nodes)
            {
                Enum.TryParse<NodeDisplayMode>(nm.displayMode, out var displayMode);
                var node = new Node(idMap[nm.id], nm.typeId, ModelToVec2(nm.position) + offset)
                {
                    Size = ModelToVec2(nm.size),
                    DisplayMode = displayMode,
                    AllowDynamicPorts = nm.allowDynamicPorts
                };

                foreach (var pm in nm.ports)
                {
                    Enum.TryParse<PortDirection>(pm.direction, out var dir);
                    Enum.TryParse<PortKind>(pm.kind, out var kind);
                    Enum.TryParse<PortCapacity>(pm.capacity, out var cap);
                    var port = new Port(idMap[pm.id], node.Id, pm.name, dir, kind, pm.dataType, cap, pm.sortOrder);
                    node.AddPortDirect(port);
                }

                if (nm.userData != null && _userDataSerializer != null)
                    node.UserData = _userDataSerializer.DeserializeNodeData(nm.typeId, nm.userData);

                target.AddNodeDirect(node);
                newNodes.Add(node);
            }

            // 创建连线（使用新 ID）
            foreach (var em in model.edges)
            {
                string newSrcPort = idMap.TryGetValue(em.sourcePortId, out var s) ? s : em.sourcePortId;
                string newTgtPort = idMap.TryGetValue(em.targetPortId, out var t) ? t : em.targetPortId;
                var edge = new Edge(idMap[em.id], newSrcPort, newTgtPort);

                if (em.userData != null && _userDataSerializer != null)
                    edge.UserData = _userDataSerializer.DeserializeEdgeData(em.userData);

                target.AddEdgeDirect(edge);
            }

            return newNodes;
        }

        // ══════════════════════════════════════
        //  Graph ↔ Model 转换
        // ══════════════════════════════════════

        private JsonGraphModel GraphToModel(Graph graph)
        {
            var model = new JsonGraphModel
            {
                id = graph.Id,
                settings = new JsonSettingsModel
                {
                    topology = graph.Settings.Topology.ToString()
                }
            };

            foreach (var node in graph.Nodes)
                model.nodes.Add(NodeToModel(node));

            foreach (var edge in graph.Edges)
                model.edges.Add(EdgeToModel(edge));

            foreach (var group in graph.Groups)
            {
                model.groups.Add(new JsonGroupModel
                {
                    id = group.Id,
                    title = group.Title,
                    position = Vec2ToModel(new Vec2(group.Bounds.X, group.Bounds.Y)),
                    size = Vec2ToModel(new Vec2(group.Bounds.Width, group.Bounds.Height)),
                    color = ColorToModel(group.Color),
                    nodeIds = group.ContainedNodeIds.ToList()
                });
            }

            foreach (var comment in graph.Comments)
            {
                model.comments.Add(new JsonCommentModel
                {
                    id = comment.Id,
                    text = comment.Text,
                    position = Vec2ToModel(new Vec2(comment.Bounds.X, comment.Bounds.Y)),
                    size = Vec2ToModel(new Vec2(comment.Bounds.Width, comment.Bounds.Height)),
                    fontSize = comment.FontSize,
                    textColor = ColorToModel(comment.TextColor),
                    backgroundColor = ColorToModel(comment.BackgroundColor)
                });
            }

            foreach (var sgf in graph.SubGraphFrames)
            {
                model.subGraphFrames.Add(new JsonSubGraphFrameModel
                {
                    id = sgf.Id,
                    title = sgf.Title,
                    position = Vec2ToModel(new Vec2(sgf.Bounds.X, sgf.Bounds.Y)),
                    size = Vec2ToModel(new Vec2(sgf.Bounds.Width, sgf.Bounds.Height)),
                    nodeIds = sgf.ContainedNodeIds.ToList(),
                    representativeNodeId = sgf.RepresentativeNodeId,
                    isCollapsed = sgf.IsCollapsed,
                    sourceAssetId = sgf.SourceAssetId
                });
            }

            return model;
        }

        private Graph ModelToGraph(JsonGraphModel model)
        {
            var topology = GraphTopologyPolicy.DAG;
            if (Enum.TryParse<GraphTopologyPolicy>(model.settings.topology, out var parsed))
                topology = parsed;

            var settings = new GraphSettings { Topology = topology };
            var graph = new Graph(model.id, settings);

            foreach (var nm in model.nodes)
            {
                var node = ModelToNode(nm);
                graph.AddNodeDirect(node);
            }

            foreach (var em in model.edges)
            {
                var edge = ModelToEdge(em);
                graph.AddEdgeDirect(edge);
            }

            foreach (var gm in model.groups)
            {
                var pos = ModelToVec2(gm.position);
                var sz = ModelToVec2(gm.size);
                var group = new NodeGroup(gm.id, gm.title)
                {
                    Bounds = new Rect2(pos.X, pos.Y, sz.X, sz.Y),
                    Color = ModelToColor(gm.color)
                };
                foreach (var nid in gm.nodeIds)
                    group.ContainedNodeIds.Add(nid);
                graph.AddGroupDirect(group);
            }

            foreach (var cm in model.comments)
            {
                var cPos = ModelToVec2(cm.position);
                var cSz = ModelToVec2(cm.size);
                var comment = new GraphComment(cm.id, cm.text)
                {
                    Bounds = new Rect2(cPos.X, cPos.Y, cSz.X, cSz.Y),
                    FontSize = cm.fontSize,
                    TextColor = ModelToColor(cm.textColor),
                    BackgroundColor = ModelToColor(cm.backgroundColor)
                };
                graph.AddCommentDirect(comment);
            }

            foreach (var sm in model.subGraphFrames)
            {
                var sPos = ModelToVec2(sm.position);
                var sSz = ModelToVec2(sm.size);
                var sgf = new SubGraphFrame(sm.id, sm.title, sm.representativeNodeId)
                {
                    Bounds = new Rect2(sPos.X, sPos.Y, sSz.X, sSz.Y),
                    IsCollapsed = sm.isCollapsed,
                    SourceAssetId = sm.sourceAssetId
                };
                foreach (var nid in sm.nodeIds)
                    sgf.ContainedNodeIds.Add(nid);
                graph.AddSubGraphFrameDirect(sgf);
            }

            return graph;
        }

        // ══════════════════════════════════════
        //  Node / Edge / Port 转换
        // ══════════════════════════════════════

        private JsonNodeModel NodeToModel(Node node)
        {
            var nm = new JsonNodeModel
            {
                id = node.Id,
                typeId = node.TypeId,
                position = Vec2ToModel(node.Position),
                size = Vec2ToModel(node.Size),
                displayMode = node.DisplayMode.ToString(),
                allowDynamicPorts = node.AllowDynamicPorts
            };

            foreach (var port in node.Ports)
            {
                nm.ports.Add(new JsonPortModel
                {
                    id = port.Id,
                    name = port.Name,
                    direction = port.Direction.ToString(),
                    kind = port.Kind.ToString(),
                    dataType = port.DataType,
                    capacity = port.Capacity.ToString(),
                    sortOrder = port.SortOrder
                });
            }

            if (node.UserData != null && _userDataSerializer != null)
                nm.userData = _userDataSerializer.SerializeNodeData(node.UserData);

            return nm;
        }

        private Node ModelToNode(JsonNodeModel nm)
        {
            Enum.TryParse<NodeDisplayMode>(nm.displayMode, out var displayMode);

            var node = new Node(nm.id, nm.typeId, ModelToVec2(nm.position))
            {
                Size = ModelToVec2(nm.size),
                DisplayMode = displayMode,
                AllowDynamicPorts = nm.allowDynamicPorts
            };

            foreach (var pm in nm.ports)
            {
                Enum.TryParse<PortDirection>(pm.direction, out var dir);
                Enum.TryParse<PortKind>(pm.kind, out var kind);
                Enum.TryParse<PortCapacity>(pm.capacity, out var cap);

                var port = new Port(pm.id, node.Id, pm.name, dir, kind, pm.dataType, cap, pm.sortOrder);
                node.AddPortDirect(port);
            }

            if (nm.userData != null && _userDataSerializer != null)
                node.UserData = _userDataSerializer.DeserializeNodeData(nm.typeId, nm.userData);

            return node;
        }

        private JsonEdgeModel EdgeToModel(Edge edge)
        {
            var em = new JsonEdgeModel
            {
                id = edge.Id,
                sourcePortId = edge.SourcePortId,
                targetPortId = edge.TargetPortId
            };

            if (edge.UserData != null && _userDataSerializer != null)
                em.userData = _userDataSerializer.SerializeEdgeData(edge.UserData);

            return em;
        }

        private Edge ModelToEdge(JsonEdgeModel em)
        {
            var edge = new Edge(em.id, em.sourcePortId, em.targetPortId);

            if (em.userData != null && _userDataSerializer != null)
                edge.UserData = _userDataSerializer.DeserializeEdgeData(em.userData);

            return edge;
        }

        // ══════════════════════════════════════
        //  基本类型转换
        // ══════════════════════════════════════

        private static JsonVec2 Vec2ToModel(Vec2 v) => new JsonVec2 { x = v.X, y = v.Y };
        private static Vec2 ModelToVec2(JsonVec2 m) => new Vec2(m.x, m.y);
        private static JsonColor ColorToModel(Color4 c) => new JsonColor { r = c.R, g = c.G, b = c.B, a = c.A };
        private static Color4 ModelToColor(JsonColor m) => new Color4(m.r, m.g, m.b, m.a);
    }
}
