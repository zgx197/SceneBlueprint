#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.Unity.Persistence
{
    /// <summary>
    /// Graph ↔ GraphAsset 之间的转换器。
    /// 负责将内存中的 Graph 对象序列化为 Unity SO 可存储的格式，以及反序列化恢复。
    /// </summary>
    public static class GraphAssetConverter
    {
        /// <summary>将 Graph 写入 GraphAsset</summary>
        public static void SaveToAsset(Graph graph, GraphAsset asset)
        {
            asset.SetGraphId(graph.Id);
            asset.Topology = (int)graph.Settings.Topology;

            // 节点
            asset.Nodes.Clear();
            foreach (var node in graph.Nodes)
            {
                var sn = new SerializedNode
                {
                    id = node.Id,
                    typeId = node.TypeId,
                    position = node.Position.ToUnity(),
                    size = node.Size.ToUnity(),
                    displayMode = (int)node.DisplayMode,
                    allowDynamicPorts = node.AllowDynamicPorts,
                    userDataJson = "" // 业务层自行序列化
                };

                foreach (var port in node.Ports)
                {
                    sn.ports.Add(new SerializedPort
                    {
                        id = port.Id,
                        name = port.Name,
                        direction = (int)port.Direction,
                        kind = (int)port.Kind,
                        dataType = port.DataType,
                        capacity = (int)port.Capacity,
                        sortOrder = port.SortOrder
                    });
                }

                asset.Nodes.Add(sn);
            }

            // 连线
            asset.Edges.Clear();
            foreach (var edge in graph.Edges)
            {
                asset.Edges.Add(new SerializedEdge
                {
                    id = edge.Id,
                    sourcePortId = edge.SourcePortId,
                    targetPortId = edge.TargetPortId,
                    userDataJson = ""
                });
            }

            // 分组
            asset.Groups.Clear();
            foreach (var group in graph.Groups)
            {
                asset.Groups.Add(new SerializedGroup
                {
                    id = group.Id,
                    title = group.Title,
                    position = new Vector2(group.Bounds.X, group.Bounds.Y),
                    size = new Vector2(group.Bounds.Width, group.Bounds.Height),
                    color = group.Color.ToUnity(),
                    nodeIds = group.ContainedNodeIds.ToList()
                });
            }

            // 注释
            asset.Comments.Clear();
            foreach (var comment in graph.Comments)
            {
                asset.Comments.Add(new SerializedComment
                {
                    id = comment.Id,
                    text = comment.Text,
                    position = new Vector2(comment.Bounds.X, comment.Bounds.Y),
                    size = new Vector2(comment.Bounds.Width, comment.Bounds.Height),
                    fontSize = comment.FontSize,
                    textColor = comment.TextColor.ToUnity(),
                    backgroundColor = comment.BackgroundColor.ToUnity()
                });
            }

            // 子图框
            asset.SubGraphFrames.Clear();
            foreach (var sgf in graph.SubGraphFrames)
            {
                asset.SubGraphFrames.Add(new SerializedSubGraphFrame
                {
                    id = sgf.Id,
                    title = sgf.Title,
                    position = new Vector2(sgf.Bounds.X, sgf.Bounds.Y),
                    size = new Vector2(sgf.Bounds.Width, sgf.Bounds.Height),
                    nodeIds = sgf.ContainedNodeIds.ToList(),
                    representativeNodeId = sgf.RepresentativeNodeId,
                    isCollapsed = sgf.IsCollapsed,
                    sourceAssetId = sgf.SourceAssetId ?? ""
                });
            }
        }

        /// <summary>从 GraphAsset 恢复 Graph</summary>
        public static Graph LoadFromAsset(GraphAsset asset)
        {
            var settings = new GraphSettings
            {
                Topology = (GraphTopologyPolicy)asset.Topology
            };
            var graph = new Graph(asset.GraphId, settings);

            // 节点
            foreach (var sn in asset.Nodes)
            {
                var node = new Node(sn.id, sn.typeId, sn.position.ToNodeGraph())
                {
                    Size = sn.size.ToNodeGraph(),
                    DisplayMode = (NodeDisplayMode)sn.displayMode,
                    AllowDynamicPorts = sn.allowDynamicPorts
                };

                foreach (var sp in sn.ports)
                {
                    var port = new Port(
                        sp.id, node.Id, sp.name,
                        (PortDirection)sp.direction,
                        (PortKind)sp.kind,
                        sp.dataType,
                        (PortCapacity)sp.capacity,
                        sp.sortOrder);
                    node.AddPortDirect(port);
                }

                graph.AddNodeDirect(node);
            }

            // 连线
            foreach (var se in asset.Edges)
            {
                var edge = new Edge(se.id, se.sourcePortId, se.targetPortId);
                graph.AddEdgeDirect(edge);
            }

            // 分组
            foreach (var sg in asset.Groups)
            {
                var group = new NodeGroup(sg.id, sg.title)
                {
                    Bounds = new Rect2(sg.position.x, sg.position.y, sg.size.x, sg.size.y),
                    Color = sg.color.ToNodeGraph()
                };
                foreach (var nodeId in sg.nodeIds)
                    group.ContainedNodeIds.Add(nodeId);
                graph.AddGroupDirect(group);
            }

            // 注释
            foreach (var sc in asset.Comments)
            {
                var comment = new GraphComment(sc.id, sc.text)
                {
                    Bounds = new Rect2(sc.position.x, sc.position.y, sc.size.x, sc.size.y),
                    FontSize = sc.fontSize,
                    TextColor = sc.textColor.ToNodeGraph(),
                    BackgroundColor = sc.backgroundColor.ToNodeGraph()
                };
                graph.AddCommentDirect(comment);
            }

            // 子图框
            if (asset.SubGraphFrames != null)
            {
                foreach (var ss in asset.SubGraphFrames)
                {
                    var sgf = new SubGraphFrame(ss.id, ss.title, ss.representativeNodeId)
                    {
                        Bounds = new Rect2(ss.position.x, ss.position.y, ss.size.x, ss.size.y),
                        IsCollapsed = ss.isCollapsed,
                        SourceAssetId = string.IsNullOrEmpty(ss.sourceAssetId) ? null : ss.sourceAssetId
                    };
                    foreach (var nid in ss.nodeIds)
                        sgf.ContainedNodeIds.Add(nid);
                    graph.AddSubGraphFrameDirect(sgf);
                }
            }

            return graph;
        }
    }
}
