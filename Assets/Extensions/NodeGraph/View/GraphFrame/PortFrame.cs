#nullable enable
using NodeGraph.Core;
using NodeGraph.Math;

namespace NodeGraph.View
{
    /// <summary>单个端口的渲染描述</summary>
    public class PortFrame
    {
        /// <summary>端口 ID</summary>
        public string PortId { get; set; } = string.Empty;

        /// <summary>端口在画布中的绝对位置</summary>
        public Vec2 Position { get; set; }

        /// <summary>端口类型颜色</summary>
        public Color4 Color { get; set; }

        /// <summary>是否已连接（影响实心/空心绘制）</summary>
        public bool Connected { get; set; }

        /// <summary>端口显示名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>端口方向</summary>
        public PortDirection Direction { get; set; }

        /// <summary>端口类别（Control / Data）</summary>
        public PortKind Kind { get; set; }

        /// <summary>连接容量（Single / Multiple），影响渲染样式</summary>
        public PortCapacity Capacity { get; set; }

        /// <summary>当前已连接的边数（Multiple 端口用于堆叠圆圈渲染）</summary>
        public int ConnectedEdgeCount { get; set; }

        /// <summary>Multiple 端口的总槽位数（含已连接 + 空位 + "+"，由 FrameBuilder 计算）</summary>
        public int TotalSlots { get; set; }

        /// <summary>端口是否处于悬停状态（鼠标靠近时高亮）</summary>
        public bool Hovered { get; set; }

        /// <summary>悬停的槽位索引（Multiple 端口用，-1 表示非 Multiple 或整体悬停）</summary>
        public int HoveredSlotIndex { get; set; } = -1;
    }
}
