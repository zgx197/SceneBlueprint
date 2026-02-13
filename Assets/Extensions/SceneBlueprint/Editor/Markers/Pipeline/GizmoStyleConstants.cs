#nullable enable
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 样式常量——图层颜色、填充透明度、脉冲参数等。
    /// <para>
    /// 集中管理所有视觉常量，避免散落在各 Renderer 中。
    /// 修改颜色或动画参数只需改这一处。
    /// </para>
    /// </summary>
    public static class GizmoStyleConstants
    {
        // ─── 图层颜色 ───

        public static readonly Color CombatColor     = new Color(0.9f, 0.2f, 0.2f);  // 红色
        public static readonly Color TriggerColor     = new Color(0.2f, 0.4f, 0.9f);  // 蓝色
        public static readonly Color EnvironmentColor = new Color(0.9f, 0.8f, 0.2f);  // 黄色
        public static readonly Color CameraColor      = new Color(0.2f, 0.8f, 0.3f);  // 绿色
        public static readonly Color NarrativeColor   = new Color(0.7f, 0.3f, 0.9f);  // 紫色
        public static readonly Color DefaultColor     = new Color(0.7f, 0.7f, 0.7f);  // 灰色

        // ─── 填充透明度 ───

        /// <summary>普通状态下填充面的 alpha</summary>
        public const float FillAlpha = 0.15f;

        /// <summary>选中状态下填充面的 alpha</summary>
        public const float SelectedFillAlpha = 0.25f;

        // ─── 脉冲动画参数 ───

        /// <summary>脉冲频率（越大越快）</summary>
        public const float PulseSpeed = 5f;

        /// <summary>脉冲缩放最大倍数（1.0 + MaxPulseAmplitude）</summary>
        public const float MaxPulseAmplitude = 0.3f;

        /// <summary>脉冲透明度最小值</summary>
        public const float PulseAlphaMin = 0.4f;

        /// <summary>脉冲透明度最大值</summary>
        public const float PulseAlphaMax = 1.0f;

        // ─── 拾取参数 ───

        /// <summary>鼠标距离阈值（像素），小于此值判定为命中</summary>
        public const float PickDistanceThreshold = 20f;

        // ─── 颜色工具方法 ───

        /// <summary>根据标记的 Tag 前缀获取图层颜色</summary>
        public static Color GetLayerColor(SceneMarker marker)
        {
            return marker.GetLayerPrefix() switch
            {
                "Combat"      => CombatColor,
                "Trigger"     => TriggerColor,
                "Environment" => EnvironmentColor,
                "Camera"      => CameraColor,
                "Narrative"   => NarrativeColor,
                _             => DefaultColor,
            };
        }

        /// <summary>将颜色加亮（用于高亮效果）</summary>
        public static Color GetHighlightColor(Color baseColor)
        {
            var bright = Color.Lerp(baseColor, Color.white, 0.5f);
            bright.a = 1f;
            return bright;
        }

        /// <summary>生成半透明填充色</summary>
        public static Color GetFillColor(Color baseColor, bool selected = false)
        {
            float alpha = selected ? SelectedFillAlpha : FillAlpha;
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        /// <summary>计算当前帧的脉冲缩放值</summary>
        public static float CalcPulseScale(float time)
        {
            return 1f + MaxPulseAmplitude * (0.5f + 0.5f * Mathf.Sin(time * PulseSpeed));
        }

        /// <summary>计算当前帧的脉冲透明度值</summary>
        public static float CalcPulseAlpha(float time)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(time * PulseSpeed);
            return Mathf.Lerp(PulseAlphaMin, PulseAlphaMax, t);
        }
    }
}
