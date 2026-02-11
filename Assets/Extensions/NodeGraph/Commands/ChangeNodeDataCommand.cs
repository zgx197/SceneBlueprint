#nullable enable
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 修改节点业务数据命令。
    /// </summary>
    public class ChangeNodeDataCommand : ICommand
    {
        private readonly string _nodeId;
        private readonly INodeData? _newData;
        private INodeData? _oldData;

        public string Description { get; }

        public ChangeNodeDataCommand(string nodeId, INodeData? newData, string description = "修改节点数据")
        {
            _nodeId = nodeId;
            _newData = newData;
            Description = description;
        }

        public void Execute(Graph graph)
        {
            var node = graph.FindNode(_nodeId);
            if (node == null) return;

            _oldData = node.UserData;
            node.UserData = _newData;
        }

        public void Undo(Graph graph)
        {
            var node = graph.FindNode(_nodeId);
            if (node == null) return;

            node.UserData = _oldData;
        }
    }
}
