#nullable enable
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 连线命令。连接两个端口。
    /// </summary>
    public class ConnectCommand : IStructuralCommand
    {
        private readonly string _sourcePortId;
        private readonly string _targetPortId;

        // Execute 后由 ConnectResult 填充，供 Undo 使用
        private string? _createdEdgeId;
        private Edge? _displacedEdge;

        public string Description { get; }

        /// <summary>获取执行后创建的连线 ID</summary>
        public string? CreatedEdgeId => _createdEdgeId;

        public ConnectCommand(string sourcePortId, string targetPortId)
        {
            _sourcePortId = sourcePortId;
            _targetPortId = targetPortId;
            Description = "连接端口";
        }

        public void Execute(Graph graph)
        {
            var result = graph.Connect(_sourcePortId, _targetPortId);
            _createdEdgeId = result.CreatedEdge?.Id;
            _displacedEdge = result.DisplacedEdge;
        }

        public void Undo(Graph graph)
        {
            if (_createdEdgeId != null)
                graph.Disconnect(_createdEdgeId);

            if (_displacedEdge != null)
                graph.AddEdgeDirect(_displacedEdge);
        }
    }
}
