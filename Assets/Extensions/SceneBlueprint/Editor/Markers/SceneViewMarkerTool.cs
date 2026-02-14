#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Definitions;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// Scene View 标记创建工具。
    /// <para>
    /// 职责：
    /// <list type="bullet">
    ///   <item>在 Scene View 中提供右键菜单，按 Action 类型列出可创建的标记</item>
    ///   <item>处理标记的多步创建流程（如 Spawn = 先画区域 → 再放点位）</item>
    ///   <item>创建标记后通知蓝图编辑器创建对应节点并绑定</item>
    /// </list>
    /// </para>
    /// <para>
    /// 使用方式：由 <see cref="SceneBlueprintWindow"/> 在打开时启用，关闭时禁用。
    /// 通过 <see cref="SceneView.duringSceneGui"/> 回调注入 Scene View 事件处理。
    /// </para>
    /// </summary>
    public static class SceneViewMarkerTool
    {
        // ─── 状态 ───

        private static bool _enabled;
        private static IActionRegistry? _registry;
        private static Vector3 _lastRightClickWorldPos;

        /// <summary>标记创建完成时的回调——蓝图编辑器订阅此事件来创建节点并绑定</summary>
        public static event System.Action<MarkerCreationResult>? OnMarkerCreated;

        // ─── 启用/禁用 ───

        /// <summary>
        /// 启用 Scene View 标记工具。
        /// <para>由蓝图编辑器窗口在打开时调用。</para>
        /// </summary>
        /// <param name="registry">Action 注册表（用于获取 SceneRequirements）</param>
        public static void Enable(IActionRegistry registry)
        {
            if (_enabled) return;
            _registry = registry;
            _enabled = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// 禁用 Scene View 标记工具。
        /// <para>由蓝图编辑器窗口在关闭时调用。</para>
        /// </summary>
        public static void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            _registry = null;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ─── Scene View 事件处理 ───

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_enabled || _registry == null) return;

            var evt = Event.current;

            // 右键点击（MouseUp 避免与 Unity 原生右键冲突）
            if (evt.type == EventType.MouseDown && evt.button == 1 && evt.modifiers == EventModifiers.Shift)
            {
                // Shift + 右键 → 标记创建菜单（避免覆盖 Unity 原生右键菜单）
                if (TryRaycastGround(evt.mousePosition, sceneView, out var worldPos))
                {
                    _lastRightClickWorldPos = worldPos;
                    evt.Use();
                    ShowCreateMenu(worldPos);
                }
            }
        }

        /// <summary>
        /// 从鼠标位置射线投射到场景几何体，获取世界坐标。
        /// <para>
        /// 三层检测策略（兼容无 Collider 的白模地形）：
        /// 1. Physics.Raycast — 有 Collider 的物体优先
        /// 2. HandleUtility.PickGameObject + Renderer bounds — 无 Collider 的 MeshRenderer
        /// 3. Y=0 平面回退 — 最终兜底
        /// </para>
        /// </summary>
        private static bool TryRaycastGround(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);

            // 策略 1：优先检测有 Collider 的物体
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                worldPos = hit.point;
                return true;
            }

            // 策略 2：检测无 Collider 的 MeshRenderer（白模地形等）
            //   使用 HandleUtility.PickGameObject 找到鼠标下的可见物体，
            //   然后用射线与该物体 Renderer bounds 的顶面 Y 平面求交，
            //   得到一个近似的表面位置。
            var pickedGO = HandleUtility.PickGameObject(mousePos, false);
            if (pickedGO != null)
            {
                var renderer = pickedGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 用 bounds 顶面 Y 作为"地面高度"
                    float surfaceY = renderer.bounds.max.y;
                    var surfacePlane = new Plane(Vector3.up, new Vector3(0, surfaceY, 0));
                    if (surfacePlane.Raycast(ray, out float surfaceEnter))
                    {
                        worldPos = ray.GetPoint(surfaceEnter);
                        return true;
                    }
                }
            }

            // 策略 3：回退到 Y=0 平面
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }

        // ─── 右键菜单 ───

        private static void ShowCreateMenu(Vector3 worldPos)
        {
            if (_registry == null) return;

            var menu = new GenericMenu();

            // 按 Category 分组列出有 SceneRequirements 的 Action
            var actionsWithMarkers = _registry.GetAll()
                .Where(a => a.SceneRequirements != null && a.SceneRequirements.Length > 0)
                .GroupBy(a => a.Category)
                .OrderBy(g => GetCategoryOrder(g.Key));

            foreach (var group in actionsWithMarkers)
            {
                string categoryIcon = GetCategoryIcon(group.Key);
                foreach (var action in group)
                {
                    string menuPath = $"{categoryIcon} {group.Key}/{action.DisplayName}";
                    var actionCopy = action; // 闭包捕获
                    menu.AddItem(new GUIContent(menuPath), false, () =>
                    {
                        CreateMarkersForAction(actionCopy, worldPos);
                    });
                }
            }

            // 分隔线
            menu.AddSeparator("");

            // 仅创建标记——按预设分组显示
            var presets = MarkerPresetRegistry.GetAll();
            if (presets.Count > 0)
            {
                var grouped = presets
                    .GroupBy(p => string.IsNullOrEmpty(p.Category) ? "未分类" : p.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    foreach (var preset in group.OrderBy(p => p.DisplayName))
                    {
                        string label = $"仅创建标记/{group.Key}/{preset.DisplayName} ({preset.BaseMarkerTypeId})";
                        var presetCopy = preset;
                        menu.AddItem(new GUIContent(label), false, () =>
                        {
                            CreateStandaloneMarkerFromPreset(presetCopy, worldPos);
                        });
                    }
                }

                menu.AddSeparator("仅创建标记/");
            }

            // 基础标记类型（无预设的裸创建）
            menu.AddItem(new GUIContent("仅创建标记/空白 Point"), false, () =>
            {
                CreateStandaloneMarker<PointMarker>("新点位", worldPos, "");
            });
            menu.AddItem(new GUIContent("仅创建标记/空白 Area (Box)"), false, () =>
            {
                var marker = CreateStandaloneMarker<AreaMarker>("新区域", worldPos, "");
                marker.Shape = AreaShape.Box;
            });
            menu.AddItem(new GUIContent("仅创建标记/空白 Entity"), false, () =>
            {
                CreateStandaloneMarker<EntityMarker>("新实体", worldPos, "");
            });

            menu.ShowAsContext();
        }

        private static string GetCategoryIcon(string category)
        {
            return category switch
            {
                "Combat" => "⚔️",
                "Trigger" => "🎯",
                "Presentation" => "🎬",
                "Environment" => "💡",
                _ => "📍"
            };
        }

        private static int GetCategoryOrder(string category)
        {
            return category switch
            {
                "Combat" => 0,
                "Trigger" => 1,
                "Presentation" => 2,
                "Environment" => 3,
                _ => 99
            };
        }

        // ─── 标记创建 ───

        /// <summary>
        /// 为指定 Action 创建所有需要的场景标记。
        /// <para>
        /// 按 SceneRequirements 中的顺序逐个创建标记。
        /// 对于 AllowMultiple 的需求，首次只创建 MinCount 个（至少 1 个）。
        /// </para>
        /// </summary>
        private static void CreateMarkersForAction(ActionDefinition action, Vector3 basePos)
        {
            var result = new MarkerCreationResult
            {
                ActionTypeId = action.TypeId,
                ActionDisplayName = action.DisplayName,
                CreatedMarkers = new List<MarkerBindingEntry>()
            };

            float offset = 0f;

            foreach (var req in action.SceneRequirements)
            {
                int count = req.AllowMultiple ? System.Math.Max(req.MinCount, 1) : 1;

                // 优先按 PresetId 精确查找，回退到 Tag 模糊匹配
                var preset = ResolvePreset(req);

                var markerDef = MarkerDefinitionRegistry.Get(req.MarkerTypeId);
                if (markerDef == null)
                {
                    SBLog.Warn(SBLogTags.Marker,
                        $"未找到标记类型定义: {req.MarkerTypeId}，跳过创建");
                    continue;
                }

                for (int i = 0; i < count; i++)
                {
                    var markerPos = basePos + Vector3.right * offset;

                    // 名称优先级：PresetSO.NamePrefix > PresetSO.DisplayName > req.DisplayName > 标记类型
                    string baseName = ResolveDisplayName(req, preset);
                    string markerName = count > 1
                        ? $"{baseName}_{i + 1:D2}"
                        : baseName;

                    // Tag 优先级：PresetSO.DefaultTag > req.DefaultTag
                    string tag = !string.IsNullOrEmpty(preset?.DefaultTag)
                        ? preset!.DefaultTag
                        : req.DefaultTag;

                    var marker = MarkerHierarchyManager.CreateMarker(
                        markerDef.ComponentType, markerName, markerPos, tag: tag);

                    // 应用预设默认值（颜色、尺寸等）
                    ApplyPreset(marker, preset);

                    markerDef.Initializer?.Invoke(marker);
                    offset += markerDef.DefaultSpacing;

                    result.CreatedMarkers.Add(new MarkerBindingEntry
                    {
                        BindingKey = req.BindingKey,
                        MarkerId = marker.MarkerId,
                        MarkerGameObject = marker.gameObject
                    });

                    Selection.activeGameObject = marker.gameObject;
                }
            }

            // 通知蓝图编辑器
            OnMarkerCreated?.Invoke(result);

            SBLog.Info(SBLogTags.Marker, $"为 {action.DisplayName} 创建了 {result.CreatedMarkers.Count} 个标记");
        }

        /// <summary>
        /// 解析标记预设：优先 PresetId 精确匹配，回退到 (MarkerTypeId, DefaultTag) 模糊匹配。
        /// </summary>
        private static MarkerPresetSO? ResolvePreset(MarkerRequirement req)
        {
            // 优先按 PresetId 精确查找
            if (!string.IsNullOrEmpty(req.PresetId))
            {
                var preset = MarkerPresetRegistry.FindByPresetId(req.PresetId);
                if (preset != null) return preset;
                SBLog.Warn(SBLogTags.Template,
                    $"未找到预设 '{req.PresetId}'（BindingKey='{req.BindingKey}'），尝试 Tag 匹配回退");
            }

            // 回退：按 Tag 模糊匹配
            return MarkerPresetRegistry.FindMatch(req.MarkerTypeId, req.DefaultTag);
        }

        /// <summary>
        /// 解析标记显示名称：PresetSO > MarkerRequirement > 默认
        /// </summary>
        private static string ResolveDisplayName(MarkerRequirement req, MarkerPresetSO? preset)
        {
            if (preset != null)
            {
                if (!string.IsNullOrEmpty(preset.NamePrefix)) return preset.NamePrefix;
                if (!string.IsNullOrEmpty(preset.DisplayName)) return preset.DisplayName;
            }
            if (!string.IsNullOrEmpty(req.DisplayName)) return req.DisplayName;
            return req.MarkerTypeId;
        }

        /// <summary>
        /// 将预设默认值应用到刚创建的标记上。
        /// <para>预设为 null 时不做任何操作。</para>
        /// </summary>
        private static void ApplyPreset(SceneMarker marker, MarkerPresetSO? preset)
        {
            if (preset == null) return;

            // 颜色覆盖：记录到 marker 上，供 Gizmo 渲染阶段直接读取。
            marker.UseCustomGizmoColor = preset.UseGizmoColor;
            if (preset.UseGizmoColor)
                marker.CustomGizmoColor = preset.GizmoColor;

            // Area 特有属性
            if (marker is AreaMarker area)
            {
                area.Shape = preset.DefaultAreaShape;
                area.BoxSize = preset.DefaultBoxSize;
                area.Height = preset.DefaultHeight;
            }

            // Entity 特有属性
            if (marker is EntityMarker entity && preset.DefaultPrefab != null)
            {
                entity.PrefabRef = preset.DefaultPrefab;
            }

            SBLog.Debug(SBLogTags.Template,
                $"应用预设 '{preset.DisplayName}' 到标记 '{marker.MarkerName}'");
        }

        /// <summary>
        /// 根据预设创建独立标记（不关联 Action 节点）。
        /// </summary>
        private static void CreateStandaloneMarkerFromPreset(MarkerPresetSO preset, Vector3 position)
        {
            var markerDef = MarkerDefinitionRegistry.Get(preset.BaseMarkerTypeId);
            if (markerDef == null)
            {
                SBLog.Warn(SBLogTags.Marker,
                    $"未找到标记类型定义: {preset.BaseMarkerTypeId}，跳过创建");
                return;
            }

            string name = !string.IsNullOrEmpty(preset.NamePrefix) ? preset.NamePrefix
                : !string.IsNullOrEmpty(preset.DisplayName) ? preset.DisplayName
                : preset.BaseMarkerTypeId;

            string tag = !string.IsNullOrEmpty(preset.DefaultTag) ? preset.DefaultTag : "";

            var marker = MarkerHierarchyManager.CreateMarker(
                markerDef.ComponentType, name, position, tag: tag);

            ApplyPreset(marker, preset);
            markerDef.Initializer?.Invoke(marker);

            Selection.activeGameObject = marker.gameObject;
            EditorGUIUtility.PingObject(marker.gameObject);

            SBLog.Info(SBLogTags.Marker, $"从预设 '{preset.DisplayName}' 创建了独立标记");
        }

        /// <summary>
        /// 创建独立标记（不关联 Action 节点）。
        /// </summary>
        private static T CreateStandaloneMarker<T>(string name, Vector3 position, string tag) where T : SceneMarker
        {
            var marker = MarkerHierarchyManager.CreateMarker<T>(name, position, tag: tag);
            Selection.activeGameObject = marker.gameObject;
            EditorGUIUtility.PingObject(marker.gameObject);
            return marker;
        }
    }

    // ─── 创建结果数据 ───

    /// <summary>
    /// 标记创建结果——通知蓝图编辑器需要创建节点并绑定。
    /// </summary>
    public class MarkerCreationResult
    {
        /// <summary>Action 类型 ID</summary>
        public string ActionTypeId { get; set; } = "";

        /// <summary>Action 显示名称</summary>
        public string ActionDisplayName { get; set; } = "";

        /// <summary>创建的标记列表及其绑定信息</summary>
        public List<MarkerBindingEntry> CreatedMarkers { get; set; } = new();
    }

    /// <summary>
    /// 单条标记绑定信息——关联 BindingKey 和 MarkerId。
    /// </summary>
    public class MarkerBindingEntry
    {
        /// <summary>绑定键名（对应 MarkerRequirement.BindingKey）</summary>
        public string BindingKey { get; set; } = "";

        /// <summary>标记唯一 ID</summary>
        public string MarkerId { get; set; } = "";

        /// <summary>标记 GameObject 引用（编辑器内直接访问）</summary>
        public GameObject? MarkerGameObject { get; set; }
    }
}
