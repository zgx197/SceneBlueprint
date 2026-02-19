#nullable enable

namespace NodeGraph.Core
{
    /// <summary>
    /// 可携带节点描述文本的标记接口。
    /// 实现此接口的 <see cref="INodeData"/> 将在编辑器节点标题栏下方显示描述文字。
    /// </summary>
    public interface IDescribableNode
    {
        /// <summary>节点描述文本（空或 null 表示不显示）</summary>
        string? Description { get; }
    }
}
