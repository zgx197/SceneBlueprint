#nullable enable
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 编辑器启动时检查 MarkerPresetSO 资产，不存在则自动创建默认预设。
    /// <para>
    /// 默认预设对应 C# ActionDef 中引用的 3 个 PresetId：
    /// <list type="bullet">
    ///   <item>Combat.SpawnArea — 刷怪区域（Area 标记）</item>
    ///   <item>Combat.SpawnPoint — 刷怪点（Point 标记）</item>
    ///   <item>Combat.PresetPoint — 预设放置点（Point 标记）</item>
    /// </list>
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class MarkerPresetInitializer
    {
        private const string PresetDir = "Assets/Extensions/SceneBlueprint/Templates/MarkerPresets";

        static MarkerPresetInitializer()
        {
            EditorApplication.delayCall += EnsureDefaultPresets;
        }

        private static void EnsureDefaultPresets()
        {
            // 已有预设则跳过
            var guids = AssetDatabase.FindAssets("t:MarkerPresetSO");
            if (guids.Length > 0) return;

            EnsureDirectory(PresetDir);

            CreatePreset(new PresetConfig
            {
                PresetId = "Combat.SpawnArea",
                DisplayName = "刷怪区域",
                Category = "Combat",
                Description = "刷怪 Action 的区域标记——怪物在此范围内随机生成",
                BaseMarkerTypeId = "Area",
                DefaultTag = "Combat.SpawnArea",
                NamePrefix = "刷怪区域",
                GizmoColor = new Color(0.9f, 0.3f, 0.2f),
                UseGizmoColor = true,
                DefaultAreaShape = AreaShape.Box,
                DefaultBoxSize = new Vector3(10f, 3f, 10f),
                DefaultHeight = 3f,
                MatchTags = new[] { "Combat.SpawnArea" },
                FileName = "Combat_SpawnArea"
            });

            CreatePreset(new PresetConfig
            {
                PresetId = "Combat.SpawnPoint",
                DisplayName = "刷怪点",
                Category = "Combat",
                Description = "刷怪 Action 的点位标记——精确的怪物生成位置",
                BaseMarkerTypeId = "Point",
                DefaultTag = "Combat.SpawnPoint",
                NamePrefix = "刷怪点",
                GizmoColor = new Color(1f, 0.5f, 0.2f),
                UseGizmoColor = true,
                MatchTags = new[] { "Combat.SpawnPoint" },
                FileName = "Combat_SpawnPoint"
            });

            CreatePreset(new PresetConfig
            {
                PresetId = "Combat.PresetPoint",
                DisplayName = "放置点",
                Category = "Combat",
                Description = "放置预设怪 Action 的点位标记——Boss 出场点、守卫站位等",
                BaseMarkerTypeId = "Point",
                DefaultTag = "Combat.PresetPoint",
                NamePrefix = "放置点",
                GizmoColor = new Color(0.7f, 0.3f, 0.9f),
                UseGizmoColor = true,
                MatchTags = new[] { "Combat.PresetPoint" },
                FileName = "Combat_PresetPoint"
            });

            AssetDatabase.SaveAssets();
            MarkerPresetRegistry.Invalidate();

            SBLog.Info(SBLogTags.Template, $"已自动创建 3 个默认标记预设到 {PresetDir}");
        }

        private static void CreatePreset(PresetConfig cfg)
        {
            var preset = ScriptableObject.CreateInstance<MarkerPresetSO>();
            preset.PresetId = cfg.PresetId;
            preset.DisplayName = cfg.DisplayName;
            preset.Category = cfg.Category;
            preset.Description = cfg.Description;
            preset.BaseMarkerTypeId = cfg.BaseMarkerTypeId;
            preset.DefaultTag = cfg.DefaultTag;
            preset.NamePrefix = cfg.NamePrefix;
            preset.GizmoColor = cfg.GizmoColor;
            preset.UseGizmoColor = cfg.UseGizmoColor;
            preset.DefaultAreaShape = cfg.DefaultAreaShape;
            preset.DefaultBoxSize = cfg.DefaultBoxSize;
            preset.DefaultHeight = cfg.DefaultHeight;
            preset.MatchTags = cfg.MatchTags;

            string path = $"{PresetDir}/{cfg.FileName}.asset";
            AssetDatabase.CreateAsset(preset, path);
        }

        private static void EnsureDirectory(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = System.IO.Path.GetDirectoryName(dir)!.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>手动触发创建（菜单入口）</summary>
        [MenuItem("SceneBlueprint/创建默认标记预设", false, 211)]
        public static void CreateDefaultPresetsMenu()
        {
            var guids = AssetDatabase.FindAssets("t:MarkerPresetSO");
            if (guids.Length > 0)
            {
                SBLog.Info(SBLogTags.Template, $"已存在 {guids.Length} 个标记预设，跳过创建");
                // 选中第一个
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<MarkerPresetSO>(path);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
                return;
            }

            EnsureDefaultPresets();
        }

        private struct PresetConfig
        {
            public string PresetId;
            public string DisplayName;
            public string Category;
            public string Description;
            public string BaseMarkerTypeId;
            public string DefaultTag;
            public string NamePrefix;
            public Color GizmoColor;
            public bool UseGizmoColor;
            public AreaShape DefaultAreaShape;
            public Vector3 DefaultBoxSize;
            public float DefaultHeight;
            public string[] MatchTags;
            public string FileName;
        }
    }
}
