#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 标记预设注册表——查找和缓存项目中的 MarkerPresetSO 资产。
    /// <para>
    /// 提供按 (MarkerTypeId, Tag) 匹配预设的能力，供 SceneViewMarkerTool 在创建标记时使用。
    /// </para>
    /// </summary>
    public static class MarkerPresetRegistry
    {
        static MarkerPresetRegistry()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        private static List<MarkerPresetSO>? _cache;
        private static bool _dirty = true;

        /// <summary>标记缓存为脏，下次访问时重新加载</summary>
        public static void Invalidate() => _dirty = true;

        /// <summary>获取所有预设</summary>
        public static IReadOnlyList<MarkerPresetSO> GetAll()
        {
            EnsureLoaded();
            return _cache!;
        }

        /// <summary>
        /// 按 PresetId 精确查找预设（推荐方式）。
        /// </summary>
        public static MarkerPresetSO? FindByPresetId(string presetId)
        {
            if (string.IsNullOrEmpty(presetId)) return null;

            EnsureLoaded();
            foreach (var preset in _cache!)
            {
                if (preset.PresetId.Equals(presetId, System.StringComparison.OrdinalIgnoreCase))
                    return preset;
            }
            return null;
        }

        /// <summary>
        /// [向后兼容] 按 (MarkerTypeId, Tag) 模糊匹配预设。
        /// <para>匹配规则：BaseMarkerTypeId 匹配 且 MatchTags 包含 tag。</para>
        /// </summary>
        /// <returns>匹配的预设，未找到返回 null</returns>
        public static MarkerPresetSO? FindMatch(string markerTypeId, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;

            EnsureLoaded();
            foreach (var preset in _cache!)
            {
                if (preset.Matches(markerTypeId, tag))
                    return preset;
            }
            return null;
        }

        /// <summary>按分类分组获取所有预设</summary>
        public static Dictionary<string, List<MarkerPresetSO>> GetGrouped()
        {
            EnsureLoaded();
            return _cache!
                .GroupBy(p => string.IsNullOrEmpty(p.Category) ? "未分类" : p.Category)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static void EnsureLoaded()
        {
            if (!_dirty && _cache != null) return;
            _dirty = false;

            _cache = new List<MarkerPresetSO>();
            var guids = AssetDatabase.FindAssets("t:MarkerPresetSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<MarkerPresetSO>(path);
                if (preset != null)
                    _cache.Add(preset);
            }

            SBLog.Debug(SBLogTags.Template, $"MarkerPresetRegistry: 加载 {_cache.Count} 个预设");
        }
    }
}
