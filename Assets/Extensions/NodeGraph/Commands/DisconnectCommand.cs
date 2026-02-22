#nullable enable
using System.Linq;
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 断线命令。
    /// </summary>
    public class DisconnectCommand : IStructuralCommand
    {
        private readonly string _edgeId;

        // Undo 时恢复所需的快照
        private string? _sourcePortId;
        private string? _targetPortId;
        private IEdgeData? _edgeData;

        public string Description { get; }

        public DisconnectCommand(string edgeId)
        {
            _edgeId = edgeId;
            Description = "断开连线";
        }

        public void Execute(Graph graph)
        {
            var edge = graph.Edges.FirstOrDefault(e => e.Id == _edgeId);
            if (edge == null) return;

            // 保存快照
            _sourcePortId = edge.SourcePortId;
            _targetPortId = edge.TargetPortId;
            _edgeData = edge.UserData;

            graph.Disconnect(_edgeId);
        }

        public void Undo(Graph graph)
        {
            if (_sourcePortId == null || _targetPortId == null) return;

            var edge = new Edge(_edgeId, _sourcePortId, _targetPortId)
            {
                UserData = _edgeData
            };
            graph.AddEdgeDirect(edge);
        }
    }
}
