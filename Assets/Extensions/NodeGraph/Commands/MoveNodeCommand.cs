#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 移动节点命令。支持同时移动多个节点（批量拖拽）。
    /// </summary>
    public class MoveNodeCommand : ICommand
    {
        private readonly List<(string nodeId, Vec2 oldPos, Vec2 newPos)> _moves;

        public string Description { get; }

        /// <summary>移动单个节点</summary>
        public MoveNodeCommand(string nodeId, Vec2 oldPosition, Vec2 newPosition)
        {
            _moves = new List<(string, Vec2, Vec2)> { (nodeId, oldPosition, newPosition) };
            Description = "移动节点";
        }

        /// <summary>批量移动多个节点</summary>
        public MoveNodeCommand(IEnumerable<(string nodeId, Vec2 oldPos, Vec2 newPos)> moves)
        {
            _moves = new List<(string, Vec2, Vec2)>(moves);
            Description = $"移动 {_moves.Count} 个节点";
        }

        public void Execute(Graph graph)
        {
            foreach (var (nodeId, _, newPos) in _moves)
            {
                var node = graph.FindNode(nodeId);
                if (node != null)
                {
                    node.Position = newPos;
                    graph.Events.RaiseNodeMoved(node);
                }
            }
        }

        public void Undo(Graph graph)
        {
            foreach (var (nodeId, oldPos, _) in _moves)
            {
                var node = graph.FindNode(nodeId);
                if (node != null)
                {
                    node.Position = oldPos;
                    graph.Events.RaiseNodeMoved(node);
                }
            }
        }
    }
}
