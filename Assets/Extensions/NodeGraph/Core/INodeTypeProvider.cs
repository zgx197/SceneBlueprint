#nullable enable

namespace NodeGraph.Core
{
    /// <summary>
    /// 节点类型提供者接口。
    /// <para>
    /// JsonGraphSerializer 在反序列化时通过此接口按 TypeId 查找 NodeTypeDefinition，
    /// 从而用 DefaultPorts 重建端口结构，而无需从 JSON 中读取可能过期的端口元数据。
    /// </para>
    /// </summary>
    public interface INodeTypeProvider
    {
        /// <summary>根据 TypeId 返回节点类型定义（含 DefaultPorts）。找不到时返回 null。</summary>
        NodeTypeDefinition? GetNodeType(string typeId);
    }
}
