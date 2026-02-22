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
    /// 图的核心配置——拓扑策略与节点类型体系（「是什么」）。
    /// 行为策略（连接规则、类型兼容性）由 <see cref="GraphBehavior"/> 单独承载（「怎么做」）。
    /// </summary>
    public class GraphSettings
    {
        /// <summary>图拓扑策略</summary>
        public GraphTopologyPolicy Topology { get; set; } = GraphTopologyPolicy.DAG;

        /// <summary>节点类型目录（可替换为任意 INodeTypeCatalog 实现）</summary>
        public INodeTypeCatalog NodeTypes { get; set; }

        /// <summary>行为策略集合（连接规则、类型兼容性）</summary>
        public GraphBehavior Behavior { get; set; }

        public GraphSettings()
        {
            NodeTypes = new NodeTypeRegistry();
            Behavior  = new GraphBehavior();
        }
    }
}
