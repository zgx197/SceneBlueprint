#nullable enable
using UnityEngine;
using NodeGraph.Math;

namespace NodeGraph.Unity
{
    /// <summary>
    /// 画布坐标系统工具类。
    /// 解决 Handles + GUI.matrix + GUI.BeginClip 组合使用时
    /// Handles 渲染位置偏移的问题（clipOffset 被 GUI.matrix 的缩放因子影响）。
    ///
    /// 使用方式：
    /// 1. 调用 SetGraphAreaRect(graphRect) 设置画布区域
    /// 2. 不要使用 GUI.BeginClip — 改为将 graphRect 偏移合并到渲染矩阵中
    /// 3. 使用 CorrectedMousePosition 获取画布区域内的鼠标坐标
    /// 4. 将 CanvasCoordinateHelper 传入 UnityPlatformInput.Update 的重载
    ///
    /// 原理：
    /// - Handles 在 GUI.BeginClip 内配合 GUI.matrix（scale≠1）时，
    ///   clipOffset 会被缩放因子影响，导致渲染位置偏移
    /// - 移除 GUI.BeginClip，将 graphRect.position 偏移直接加到渲染矩阵的平移分量中，
    ///   确保 Handles/EditorGUI/GUI 使用完全相同的坐标变换
    /// </summary>
    public class CanvasCoordinateHelper
    {
        /// <summary>画布区域在窗口中的矩形</summary>
        private Rect _graphAreaRect;

        /// <summary>
        /// 设置画布区域矩形。
        /// 鼠标坐标将通过减去 graphRect.position 转换为画布区域相对坐标。
        /// </summary>
        public void SetGraphAreaRect(Rect graphAreaRect)
        {
            _graphAreaRect = graphAreaRect;
        }

        /// <summary>
        /// 画布区域相对鼠标位置（从窗口坐标减去 graphRect.position）。
        /// 等价于正确工作的 GUI.BeginClip 之后的 Event.current.mousePosition。
        ///
        /// 注意：在画布区域内获取鼠标坐标请使用此属性。
        /// </summary>
        public Vec2 CorrectedMousePosition
        {
            get
            {
                var mouse = Event.current.mousePosition;
                return new Vec2(mouse.x - _graphAreaRect.x, mouse.y - _graphAreaRect.y);
            }
        }

        /// <summary>鼠标是否在画布区域内</summary>
        public bool IsMouseInGraphArea
        {
            get
            {
                var mouse = Event.current.mousePosition;
                return _graphAreaRect.Contains(mouse);
            }
        }

        /// <summary>画布区域的屏幕偏移（用于渲染矩阵）</summary>
        public Vector2 ScreenOffset => _graphAreaRect.position;

        /// <summary>画布区域矩形</summary>
        public Rect GraphAreaRect => _graphAreaRect;
    }
}
