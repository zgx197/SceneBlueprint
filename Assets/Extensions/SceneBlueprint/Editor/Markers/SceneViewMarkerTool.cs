#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Markers;

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

            // 仅创建标记（不创建蓝图节点）
            menu.AddItem(new GUIContent("仅创建标记/点位标记"), false, () =>
            {
                CreateStandaloneMarker<PointMarker>("新点位", worldPos, "");
            });
            menu.AddItem(new GUIContent("仅创建标记/区域标记 (Box)"), false, () =>
            {
                var marker = CreateStandaloneMarker<AreaMarker>("新区域", worldPos, "");
                marker.Shape = AreaShape.Box;
            });
            menu.AddItem(new GUIContent("仅创建标记/实体标记"), false, () =>
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

                for (int i = 0; i < count; i++)
                {
                    var markerPos = basePos + Vector3.right * offset;
                    SceneMarker? marker = null;

                    switch (req.MarkerType)
                    {
                        case MarkerType.Point:
                            marker = MarkerHierarchyManager.CreateMarker<PointMarker>(
                                $"{req.DisplayName}{(count > 1 ? $"_{i + 1:D2}" : "")}",
                                markerPos,
                                tag: req.DefaultTag);
                            offset += 2f;
                            break;

                        case MarkerType.Area:
                            var areaMarker = MarkerHierarchyManager.CreateMarker<AreaMarker>(
                                req.DisplayName,
                                markerPos,
                                tag: req.DefaultTag);
                            areaMarker.Shape = AreaShape.Box;
                            areaMarker.BoxSize = new Vector3(8f, 3f, 8f);
                            marker = areaMarker;
                            offset += 10f;
                            break;

                        case MarkerType.Entity:
                            marker = MarkerHierarchyManager.CreateMarker<EntityMarker>(
                                req.DisplayName,
                                markerPos,
                                tag: req.DefaultTag);
                            offset += 2f;
                            break;
                    }

                    if (marker != null)
                    {
                        result.CreatedMarkers.Add(new MarkerBindingEntry
                        {
                            BindingKey = req.BindingKey,
                            MarkerId = marker.MarkerId,
                            MarkerGameObject = marker.gameObject
                        });

                        // 选中最后创建的标记
                        Selection.activeGameObject = marker.gameObject;
                    }
                }
            }

            // 通知蓝图编辑器
            OnMarkerCreated?.Invoke(result);

            SBLog.Info(SBLogTags.Marker, $"为 {action.DisplayName} 创建了 {result.CreatedMarkers.Count} 个标记");
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
