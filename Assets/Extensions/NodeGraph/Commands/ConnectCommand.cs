#nullable enable
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 连线命令。连接两个端口。
    /// </summary>
    public class ConnectCommand : ICommand
    {
        private readonly string _sourcePortId;
        private readonly string _targetPortId;

        // Execute 后记录实际创建的连线 ID，供 Undo 使用
        private string? _createdEdgeId;

        // 如果目标端口是 Single 容量且已有连线，记录被替换的旧连线
        private string? _replacedEdgeId;
        private string? _replacedSourcePortId;
        private string? _replacedTargetPortId;
        private IEdgeData? _replacedEdgeData;

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
            // 记录可能被替换的旧连线（Single 容量端口）
            var targetPort = graph.FindPort(_targetPortId);
            if (targetPort != null && targetPort.Capacity == PortCapacity.Single)
            {
                foreach (var existingEdge in graph.GetEdgesForPort(_targetPortId))
                {
                    if (existingEdge.TargetPortId == _targetPortId)
                    {
                        _replacedEdgeId = existingEdge.Id;
                        _replacedSourcePortId = existingEdge.SourcePortId;
                        _replacedTargetPortId = existingEdge.TargetPortId;
                        _replacedEdgeData = existingEdge.UserData;
                        break;
                    }
                }
            }

            var edge = graph.Connect(_sourcePortId, _targetPortId);
            _createdEdgeId = edge?.Id;
        }

        public void Undo(Graph graph)
        {
            // 断开新建的连线
            if (_createdEdgeId != null)
            {
                graph.Disconnect(_createdEdgeId);
            }

            // 恢复被替换的旧连线
            if (_replacedEdgeId != null && _replacedSourcePortId != null && _replacedTargetPortId != null)
            {
                var restoredEdge = new Edge(_replacedEdgeId, _replacedSourcePortId, _replacedTargetPortId)
                {
                    UserData = _replacedEdgeData
                };
                graph.AddEdgeDirect(restoredEdge);
            }
        }
    }
}
