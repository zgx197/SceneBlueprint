#nullable enable
using UnityEngine;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>
    /// 标记预设——已有标记类型的语义变体。
    /// <para>
    /// 不创建新的 MarkerType，而是为已有类型提供预配置：
    /// <list type="bullet">
    ///   <item>"精英刷怪点" = PointMarker + 红色 + Tag="Combat.Elite"</item>
    ///   <item>"安全区域" = AreaMarker + 绿色 + Tag="Safe.Zone"</item>
    ///   <item>"摄像机位" = PointMarker + 蓝色 + Tag="Camera.Position"</item>
    /// </list>
    /// </para>
    /// <para>
    /// 创建标记时，如果 MarkerRequirement 的 DefaultTag 匹配到某个预设，
    /// 则自动应用该预设的默认值（名称前缀、区域尺寸等）。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewMarkerPreset",
        menuName = "SceneBlueprint/Marker Preset",
        order = 102)]
    public class MarkerPresetSO : ScriptableObject
    {
        // ─── 基础 ───

        [Header("── 基础 ──")]

        [Tooltip("预设 ID，全局唯一（如 'Combat.EliteSpawn'）")]
        public string PresetId = "";

        [Tooltip("显示名称（如 '精英刷怪点'）")]
        public string DisplayName = "";

        [Tooltip("分类（用于配置窗口分组）")]
        public string Category = "";

        [TextArea(1, 3)]
        [Tooltip("描述说明")]
        public string Description = "";

        // ─── 标记类型映射 ───

        [Header("── 标记类型 ──")]

        [Tooltip("基础标记类型 ID（Point / Area / Entity）")]
        public string BaseMarkerTypeId = "Point";

        // ─── 默认值 ───

        [Header("── 默认值 ──")]

        [Tooltip("创建时自动分配的 Tag（如 'Combat.Elite'）")]
        public string DefaultTag = "";

        [Tooltip("创建时自动分配的名称前缀（如 '精英刷怪点'）")]
        public string NamePrefix = "";

        [Tooltip("Gizmo 颜色覆盖（勾选 UseGizmoColor 时生效）")]
        public Color GizmoColor = Color.white;

        [Tooltip("是否使用自定义 Gizmo 颜色")]
        public bool UseGizmoColor = false;

        // ─── Area 特有 ───

        [Header("── Area 特有（仅 BaseMarkerTypeId=Area 时生效）──")]

        [Tooltip("默认区域形状")]
        public AreaShape DefaultAreaShape = AreaShape.Box;

        [Tooltip("默认 Box 尺寸")]
        public Vector3 DefaultBoxSize = new(8f, 3f, 8f);

        [Tooltip("默认区域高度")]
        public float DefaultHeight = 3f;

        // ─── Entity 特有 ───

        [Header("── Entity 特有（仅 BaseMarkerTypeId=Entity 时生效）──")]

        [Tooltip("默认 Prefab")]
        public GameObject? DefaultPrefab;

        // ─── 匹配规则 ───

        [Header("── 匹配规则 ──")]

        [Tooltip("匹配的 Tag 模式列表（MarkerRequirement.DefaultTag 匹配其中之一即应用此预设）")]
        public string[] MatchTags = System.Array.Empty<string>();

        /// <summary>
        /// 检查给定的 Tag 是否匹配此预设。
        /// <para>匹配规则：Tag 完全等于 MatchTags 中的任意一项，或 Tag 以 MatchTag + "." 开头。</para>
        /// </summary>
        public bool MatchesTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || MatchTags.Length == 0) return false;

            foreach (var pattern in MatchTags)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                if (TagExpressionMatcher.IsPatternMatch(tag, pattern)) return true;
            }
            return false;
        }

        /// <summary>
        /// 检查给定的标记类型和 Tag 是否匹配此预设。
        /// </summary>
        public bool Matches(string markerTypeId, string tag)
        {
            if (!BaseMarkerTypeId.Equals(markerTypeId, System.StringComparison.OrdinalIgnoreCase))
                return false;
            return MatchesTag(tag);
        }
    }
}
