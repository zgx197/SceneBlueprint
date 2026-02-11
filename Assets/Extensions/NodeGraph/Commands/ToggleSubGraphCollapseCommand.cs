#nullable enable
using NodeGraph.Core;

namespace NodeGraph.Commands
{
    /// <summary>
    /// 切换子图框折叠/展开状态的命令。支持 Undo/Redo。
    /// </summary>
    public class ToggleSubGraphCollapseCommand : ICommand
    {
        private readonly string _frameId;
        private bool _previousState;

        public string Description { get; }

        public ToggleSubGraphCollapseCommand(string frameId)
        {
            _frameId = frameId;
            Description = $"切换子图框折叠状态 {frameId}";
        }

        public void Execute(Graph graph)
        {
            var frame = graph.FindSubGraphFrame(_frameId);
            if (frame == null) return;

            _previousState = frame.IsCollapsed;
            frame.IsCollapsed = !frame.IsCollapsed;
        }

        public void Undo(Graph graph)
        {
            var frame = graph.FindSubGraphFrame(_frameId);
            if (frame == null) return;

            frame.IsCollapsed = _previousState;
        }
    }
}
