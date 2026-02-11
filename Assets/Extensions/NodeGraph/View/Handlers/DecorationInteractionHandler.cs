#nullable enable
using NodeGraph.Abstraction;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.View.Handlers
{
    /// <summary>
    /// 装饰元素交互处理器。处理 SubGraphFrame 折叠按钮点击等装饰层交互。
    /// 优先级 15，高于 NodeDragHandler(20)，确保标题栏点击优先于节点拖拽。
    /// </summary>
    public class DecorationInteractionHandler : IInteractionHandler
    {
        public int Priority => 15;
        public bool IsActive => false;

        public bool HandleInput(GraphViewModel viewModel, IPlatformInput input)
        {
            // 仅处理左键单击
            if (!input.IsMouseDown(MouseButton.Left)) return false;
            if (input.IsAltHeld) return false;

            Vec2 canvasPos = viewModel.ScreenToCanvas(input.MousePosition);

            // 检测 SubGraphFrame 标题栏点击（折叠/展开切换）
            var sgf = viewModel.HitTestSubGraphCollapseButton(canvasPos);
            if (sgf != null)
            {
                viewModel.Commands.Execute(new ToggleSubGraphCollapseCommand(sgf.Id));
                viewModel.RequestRepaint();
                return true; // 事件已消费
            }

            return false;
        }

        public OverlayFrame? GetOverlay() => null;
    }
}
