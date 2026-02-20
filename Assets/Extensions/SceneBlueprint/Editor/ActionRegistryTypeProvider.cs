#nullable enable
using NodeGraph.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// INodeTypeProvider 的实现，通过 NodeTypeRegistry 查找节点类型定义。
    /// 用于 JsonGraphSerializer 在反序列化时从 TypeDefinition 重建端口结构（S4）。
    /// </summary>
    public class ActionRegistryTypeProvider : INodeTypeProvider
    {
        private readonly NodeTypeRegistry _nodeTypeRegistry;

        public ActionRegistryTypeProvider(NodeTypeRegistry nodeTypeRegistry)
        {
            _nodeTypeRegistry = nodeTypeRegistry;
        }

        public NodeTypeDefinition? GetNodeType(string typeId)
        {
            return _nodeTypeRegistry.GetDefinition(typeId);
        }
    }
}
