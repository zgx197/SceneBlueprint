#nullable enable
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 添加节点命令。
    /// </summary>
    public class AddNodeCommand : ICommand
    {
        private readonly string _typeId;
        private readonly Vec2 _position;

        // Execute 后记录实际创建的节点 ID，供 Undo 使用
        private string? _createdNodeId;

        public string Description { get; }

        public AddNodeCommand(string typeId, Vec2 position)
        {
            _typeId = typeId;
            _position = position;
            Description = $"添加节点 {typeId}";
        }

        /// <summary>获取执行后创建的节点 ID</summary>
        public string? CreatedNodeId => _createdNodeId;

        public void Execute(Graph graph)
        {
            var node = graph.AddNode(_typeId, _position);
            _createdNodeId = node?.Id;
        }

        public void Undo(Graph graph)
        {
            if (_createdNodeId != null)
            {
                graph.RemoveNode(_createdNodeId);
            }
        }
    }
}
