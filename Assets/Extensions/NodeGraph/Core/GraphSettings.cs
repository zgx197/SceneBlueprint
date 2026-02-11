#nullable enable

namespace NodeGraph.Core
{
    /// <summary>图拓扑策略</summary>
    public enum GraphTopologyPolicy
    {
        /// <summary>有向无环图（刷怪蓝图、技能编辑器）</summary>
        DAG,
        /// <summary>有向图，允许环（状态机、对话树）</summary>
        DirectedGraph,
        /// <summary>无向图（关系图）</summary>
        Undirected
    }

    /// <summary>
    /// 图的全局配置。包含拓扑策略、连接策略、类型兼容性注册表和节点类型注册表。
    /// </summary>
    public class GraphSettings
    {
        /// <summary>图拓扑策略</summary>
        public GraphTopologyPolicy Topology { get; set; } = GraphTopologyPolicy.DAG;

        /// <summary>连接策略（可替换，默认为 DefaultConnectionPolicy）</summary>
        public IConnectionPolicy ConnectionPolicy { get; set; }

        /// <summary>类型兼容性注册表</summary>
        public TypeCompatibilityRegistry TypeCompatibility { get; }

        /// <summary>节点类型注册表</summary>
        public NodeTypeRegistry NodeTypes { get; }

        public GraphSettings()
        {
            TypeCompatibility = new TypeCompatibilityRegistry();
            NodeTypes = new NodeTypeRegistry();
            ConnectionPolicy = new DefaultConnectionPolicy();
        }
    }
}
