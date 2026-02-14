#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// SceneBlueprint 配置管理窗口。
    /// <para>
    /// 统一管理所有模板和配置：
    /// <list type="bullet">
    ///   <item>Action 模板标签页：查看 C# 定义（只读）+ 编辑 SO 模板</item>
    ///   <item>蓝图模板标签页：浏览/预览/删除子蓝图模板</item>
    /// </list>
    /// 后续 Phase B/C 可添加更多标签页（Gizmo 样式、标记预设、分类管理等）。
    /// </para>
    /// </summary>
    public class ConfigurationWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════
        //  常量 & 样式
        // ═══════════════════════════════════════════════════════════

        private static readonly string[] TabNames = { "Action 模板", "蓝图模板", "Gizmo 样式", "标记预设", "分类", "验证规则" };
        private const float LeftPanelMinWidth = 220f;
        private const float LeftPanelMaxWidth = 350f;
        private const string DefaultTemplateDir = "Assets/Extensions/SceneBlueprint/Templates";

        /// <summary>内置 Action 的分类列表（这些分类下的 C# 定义视为内置）</summary>
        private static readonly HashSet<string> BuiltInCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "Flow"
        };

        // ═══════════════════════════════════════════════════════════
        //  状态
        // ═══════════════════════════════════════════════════════════

        private int _selectedTab;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private float _leftPanelWidth = 250f;
        private bool _isResizing;
        private string _searchText = "";

        // Action 模板标签页状态
        private List<ActionEntryItem> _actionEntries = new();
        private ActionEntryItem? _selectedAction;
        private SerializedObject? _selectedActionSO;

        // 蓝图模板标签页状态
        private List<BlueprintTemplateSO> _blueprintTemplates = new();
        private BlueprintTemplateSO? _selectedBlueprint;

        // Gizmo 样式标签页状态
        private SerializedObject? _gizmoStyleSO;

        // 标记预设标签页状态
        private List<MarkerPresetSO> _markerPresets = new();
        private MarkerPresetSO? _selectedPreset;
        private SerializedObject? _selectedPresetSO;

        // 分类标签页状态
        private List<CategorySO> _categories = new();
        private CategorySO? _selectedCategory;
        private SerializedObject? _selectedCategorySO;

        // 验证规则标签页状态
        private List<ValidationRuleSO> _validationRules = new();
        private ValidationRuleSO? _selectedRule;
        private SerializedObject? _selectedRuleSO;

        // ═══════════════════════════════════════════════════════════
        //  菜单入口
        // ═══════════════════════════════════════════════════════════

        [MenuItem("SceneBlueprint/配置管理 %#&C", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigurationWindow>();
            window.titleContent = new GUIContent("SB 配置管理", EditorGUIUtility.IconContent("d_Settings").image);
            window.minSize = new Vector2(700, 400);
            window.Show();
        }

        // ═══════════════════════════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnFocus()
        {
            RefreshAll();
        }

        private void OnProjectChanged()
        {
            RefreshAll();
            Repaint();
        }

        private void RefreshAll()
        {
            RefreshActionEntries();
            RefreshBlueprintTemplates();
            RefreshGizmoStyle();
            RefreshMarkerPresets();
            RefreshCategories();
            RefreshValidationRules();
        }

        // ═══════════════════════════════════════════════════════════
        //  主绘制
        // ═══════════════════════════════════════════════════════════

        private void OnGUI()
        {
            DrawToolbar();

            // 标签页
            EditorGUI.BeginChangeCheck();
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(25));
            if (EditorGUI.EndChangeCheck())
            {
                _searchText = "";
                _selectedAction = null;
                _selectedActionSO = null;
                _selectedBlueprint = null;
                _selectedPreset = null;
                _selectedPresetSO = null;
                _selectedCategory = null;
                _selectedCategorySO = null;
                _selectedRule = null;
                _selectedRuleSO = null;
            }

            // 双栏布局
            EditorGUILayout.BeginHorizontal();
            {
                // 左侧列表
                DrawLeftPanel();

                // 分割条
                DrawSplitter();

                // 右侧编辑区
                DrawRightPanel();
            }
            EditorGUILayout.EndHorizontal();

            // 底部状态栏
            DrawStatusBar();
        }

        // ═══════════════════════════════════════════════════════════
        //  工具栏
        // ═══════════════════════════════════════════════════════════

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    RefreshAll();

                GUILayout.FlexibleSpace();

                // 搜索框
                _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField,
                    GUILayout.Width(200));
                if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? EditorStyles.toolbarButton))
                {
                    _searchText = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════
        //  左侧面板
        // ═══════════════════════════════════════════════════════════

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_leftPanelWidth));
            {
                switch (_selectedTab)
                {
                    case 0: DrawActionList(); break;
                    case 1: DrawBlueprintList(); break;
                    case 2: DrawGizmoStyleLeft(); break;
                    case 3: DrawMarkerPresetList(); break;
                    case 4: DrawCategoryList(); break;
                    case 5: DrawValidationRuleList(); break;
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ═══════════════════════════════════════════════════════════
        //  右侧面板
        // ═══════════════════════════════════════════════════════════

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();
            {
                switch (_selectedTab)
                {
                    case 0: DrawActionEditor(); break;
                    case 1: DrawBlueprintEditor(); break;
                    case 2: DrawGizmoStyleEditor(); break;
                    case 3: DrawMarkerPresetEditor(); break;
                    case 4: DrawCategoryEditor(); break;
                    case 5: DrawValidationRuleEditor(); break;
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ═══════════════════════════════════════════════════════════
        //  分割条（可拖拽）
        // ═══════════════════════════════════════════════════════════

        private void DrawSplitter()
        {
            var splitterRect = EditorGUILayout.GetControlRect(false, 4, GUILayout.Width(4));
            splitterRect.height = position.height;
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
                _isResizing = true;

            if (_isResizing)
            {
                _leftPanelWidth = Mathf.Clamp(Event.current.mousePosition.x, LeftPanelMinWidth, LeftPanelMaxWidth);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
                _isResizing = false;

            EditorGUI.DrawRect(splitterRect, new Color(0.15f, 0.15f, 0.15f));
        }

        // ═══════════════════════════════════════════════════════════
        //  状态栏
        // ═══════════════════════════════════════════════════════════

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                int builtInCount = _actionEntries.Count(e => e.IsBuiltIn);
                int customCodeCount = _actionEntries.Count(e => e.IsCode && !e.IsBuiltIn);
                int templateCount = _actionEntries.Count(e => !e.IsCode);
                string gizmoStatus = GizmoStyleConstants.HasOverride ? "已配置" : "默认";
                int enabledRules = _validationRules.Count(r => r.Enabled);
                EditorGUILayout.LabelField(
                    $"Action:{_actionEntries.Count} | 蓝图:{_blueprintTemplates.Count} | " +
                    $"Gizmo:{gizmoStatus} | 预设:{_markerPresets.Count} | " +
                    $"分类:{_categories.Count} | 规则:{enabledRules}/{_validationRules.Count}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════
        //  Action 模板标签页 — 左侧列表
        // ═══════════════════════════════════════════════════════════

        private void DrawActionList()
        {
            // 新建按钮
            if (GUILayout.Button("+ 新建 Action 模板", GUILayout.Height(24)))
                CreateNewActionTemplate();

            EditorGUILayout.Space(4);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                var filtered = _actionEntries.Where(e => MatchSearch(e)).ToList();
                var builtIn = filtered.Where(e => e.IsBuiltIn).ToList();
                var custom = filtered.Where(e => !e.IsBuiltIn).ToList();

                // ── 内置 Action ──
                if (builtIn.Count > 0)
                {
                    DrawSectionHeader($"内置 ({builtIn.Count})", new Color(0.4f, 0.7f, 1f));
                    DrawCategoryGroups(builtIn);
                    EditorGUILayout.Space(6);
                }

                // ── 自定义 Action ──
                DrawSectionHeader($"自定义 ({custom.Count})", new Color(0.5f, 1f, 0.5f));
                if (custom.Count > 0)
                    DrawCategoryGroups(custom);
                else
                    EditorGUILayout.LabelField("  暂无自定义 Action", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>绘制分隔标题（内置/自定义）</summary>
        private void DrawSectionHeader(string title, Color accentColor)
        {
            var rect = EditorGUILayout.GetControlRect(false, 20);
            // 底色
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            // 左侧色条
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), accentColor);
            // 文本
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = accentColor }
            };
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height), title, style);
        }

        /// <summary>按分类分组绘制条目</summary>
        private void DrawCategoryGroups(List<ActionEntryItem> entries)
        {
            var grouped = entries
                .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "未分类" : e.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                EditorGUILayout.LabelField(
                    $"  {group.Key} ({group.Count()})",
                    EditorStyles.boldLabel);

                foreach (var entry in group.OrderBy(e => e.DisplayName))
                {
                    DrawActionListItem(entry);
                }

                EditorGUILayout.Space(2);
            }
        }

        private void DrawActionListItem(ActionEntryItem entry)
        {
            bool isSelected = _selectedAction == entry;
            var bgColor = isSelected ? new Color(0.24f, 0.37f, 0.59f) : Color.clear;

            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, bgColor);

            // 左侧颜色条
            var colorBar = new Rect(rect.x, rect.y + 2, 3, rect.height - 4);
            EditorGUI.DrawRect(colorBar, entry.ThemeColor);

            // 标签
            var labelRect = new Rect(rect.x + 8, rect.y, rect.width - 50, rect.height);
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = isSelected ? Color.white : GUI.skin.label.normal.textColor }
            };
            EditorGUI.LabelField(labelRect, entry.DisplayName, style);

            // 来源标记
            var tagRect = new Rect(rect.xMax - 42, rect.y + 2, 40, rect.height - 4);
            string tagText;
            Color tagColor;
            if (entry.IsBuiltIn)
            {
                tagText = "内置";
                tagColor = new Color(0.4f, 0.7f, 1f);
            }
            else if (entry.IsCode)
            {
                tagText = "C#";
                tagColor = new Color(0.9f, 0.75f, 0.4f);
            }
            else
            {
                tagText = "SO";
                tagColor = new Color(0.5f, 1f, 0.5f);
            }
            var tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = tagColor }
            };
            EditorGUI.LabelField(tagRect, tagText, tagStyle);

            // 点击选中
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedAction = entry;
                _selectedActionSO = entry.Template != null
                    ? new SerializedObject(entry.Template)
                    : null;
                Event.current.Use();
                Repaint();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Action 模板标签页 — 右侧编辑
        // ═══════════════════════════════════════════════════════════

        private void DrawActionEditor()
        {
            if (_selectedAction == null)
            {
                EditorGUILayout.LabelField("请在左侧选择一个 Action 定义", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                var entry = _selectedAction;

                if (entry.IsCode)
                {
                    DrawCodeActionViewer(entry);
                }
                else
                {
                    DrawTemplateActionEditor(entry);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>C# 定义的只读查看器</summary>
        private void DrawCodeActionViewer(ActionEntryItem entry)
        {
            string sourceLabel = entry.IsBuiltIn ? "内置定义" : "自定义 C#";
            EditorGUILayout.LabelField($"{entry.DisplayName}  [{sourceLabel} - 只读]", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("TypeId", entry.TypeId);
            EditorGUILayout.TextField("DisplayName", entry.DisplayName);
            EditorGUILayout.TextField("Category", entry.Category);
            EditorGUILayout.ColorField("ThemeColor", entry.ThemeColor);
            EditorGUILayout.TextField("Duration", entry.Duration);

            if (entry.Definition != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("端口", EditorStyles.boldLabel);
                foreach (var port in entry.Definition.Ports)
                {
                    EditorGUILayout.LabelField(
                        $"  {(port.Direction == PortDirection.In ? "→" : "←")} {port.Id}",
                        port.DisplayName);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("属性", EditorStyles.boldLabel);
                foreach (var prop in entry.Definition.Properties)
                {
                    string defaultStr = prop.DefaultValue != null ? $" = {prop.DefaultValue}" : "";
                    EditorGUILayout.LabelField(
                        $"  {prop.DisplayName} ({prop.Type})",
                        $"key={prop.Key}{defaultStr}");
                }

                if (entry.Definition.SceneRequirements.Length > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("场景需求", EditorStyles.boldLabel);
                    foreach (var req in entry.Definition.SceneRequirements)
                    {
                        var reqStr = req.Required ? "必需" : "可选";
                        EditorGUILayout.LabelField(
                            $"  {req.DisplayName} [{req.MarkerTypeId}]",
                            $"key={req.BindingKey} ({reqStr})");
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            string helpMsg = entry.IsBuiltIn
                ? "此为框架内置定义，不可修改。"
                : "此定义来自 C# 代码，在配置窗口中只读。如需修改请编辑对应的 C# 源文件。";
            EditorGUILayout.HelpBox(helpMsg, MessageType.Info);
        }

        /// <summary>SO 模板的可编辑面板</summary>
        private void DrawTemplateActionEditor(ActionEntryItem entry)
        {
            if (entry.Template == null || _selectedActionSO == null) return;

            EditorGUILayout.LabelField($"{entry.DisplayName}  [SO 模板 - 可编辑]", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedActionSO.Update();

            // 使用 SerializedProperty 绘制，这样 Undo 和脏标记都自动处理
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("TypeId"));
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("DisplayName"));

            // Category 下拉选择（自动收集已有分类）
            DrawCategoryDropdown(_selectedActionSO.FindProperty("Category"));

            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("ThemeColor"));
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("Icon"));
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("Description"));
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("Duration"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("OutputPorts"), true);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("Properties"), true);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_selectedActionSO.FindProperty("SceneRequirements"), true);

            if (_selectedActionSO.ApplyModifiedProperties())
            {
                RefreshActionEntries();
            }

            EditorGUILayout.Space(8);

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("在 Project 中定位"))
                {
                    EditorGUIUtility.PingObject(entry.Template);
                    Selection.activeObject = entry.Template;
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("删除模板", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("确认删除",
                        $"确定要删除模板 '{entry.DisplayName}' ({entry.TypeId}) 吗？\n此操作不可撤销。",
                        "删除", "取消"))
                    {
                        var path = AssetDatabase.GetAssetPath(entry.Template);
                        AssetDatabase.DeleteAsset(path);
                        _selectedAction = null;
                        _selectedActionSO = null;
                        RefreshActionEntries();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // 验证信息
            EditorGUILayout.Space(4);
            DrawActionTemplateValidation(entry.Template);
        }

        /// <summary>Category 下拉选择器（自动收集已有分类）</summary>
        private void DrawCategoryDropdown(SerializedProperty categoryProp)
        {
            var categories = _actionEntries
                .Select(e => e.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(categoryProp);

            if (categories.Count > 0)
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("▼"), FocusType.Keyboard, GUILayout.Width(24)))
                {
                    var menu = new GenericMenu();
                    foreach (var cat in categories)
                    {
                        var captured = cat;
                        menu.AddItem(new GUIContent(cat), categoryProp.stringValue == cat, () =>
                        {
                            categoryProp.serializedObject.Update();
                            categoryProp.stringValue = captured;
                            categoryProp.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    menu.ShowAsContext();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>ActionTemplateSO 验证信息（内联显示）</summary>
        private void DrawActionTemplateValidation(ActionTemplateSO template)
        {
            if (string.IsNullOrWhiteSpace(template.TypeId))
                EditorGUILayout.HelpBox("TypeId 不能为空", MessageType.Error);
            else if (!template.TypeId.Contains('.'))
                EditorGUILayout.HelpBox("建议使用 'Category.Name' 格式", MessageType.Warning);

            if (string.IsNullOrWhiteSpace(template.DisplayName))
                EditorGUILayout.HelpBox("DisplayName 不能为空", MessageType.Error);

            // 检查重复
            var duplicates = _actionEntries.Where(e =>
                e.TypeId == template.TypeId && e.Template != template).ToList();
            if (duplicates.Any())
            {
                var source = duplicates[0].IsCode ? "C# 定义" : "其他 SO 模板";
                EditorGUILayout.HelpBox($"TypeId '{template.TypeId}' 与{source}冲突", MessageType.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  蓝图模板标签页 — 左侧列表
        // ═══════════════════════════════════════════════════════════

        private void DrawBlueprintList()
        {
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                var filtered = _blueprintTemplates
                    .Where(t => string.IsNullOrEmpty(_searchText) ||
                                t.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                t.Category.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                var grouped = filtered
                    .GroupBy(t => string.IsNullOrEmpty(t.Category) ? "未分类" : t.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    EditorGUILayout.LabelField(
                        $"{group.Key} ({group.Count()})",
                        EditorStyles.boldLabel);

                    foreach (var tmpl in group.OrderBy(t => t.DisplayName))
                    {
                        bool isSelected = _selectedBlueprint == tmpl;
                        var bgColor = isSelected ? new Color(0.24f, 0.37f, 0.59f) : Color.clear;

                        var rect = EditorGUILayout.GetControlRect(false, 22);
                        EditorGUI.DrawRect(rect, bgColor);

                        string displayName = string.IsNullOrEmpty(tmpl.DisplayName) ? tmpl.name : tmpl.DisplayName;
                        var style = new GUIStyle(EditorStyles.label)
                        {
                            normal = { textColor = isSelected ? Color.white : GUI.skin.label.normal.textColor }
                        };
                        EditorGUI.LabelField(new Rect(rect.x + 4, rect.y, rect.width - 50, rect.height),
                            displayName, style);

                        // 节点数标签
                        var countStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleRight,
                            normal = { textColor = Color.gray }
                        };
                        EditorGUI.LabelField(new Rect(rect.xMax - 45, rect.y, 40, rect.height),
                            $"{tmpl.NodeCount}节点", countStyle);

                        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                        {
                            _selectedBlueprint = tmpl;
                            Event.current.Use();
                            Repaint();
                        }
                    }

                    EditorGUILayout.Space(4);
                }

                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无蓝图模板", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.LabelField("在蓝图编辑器中右键子蓝图", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.LabelField("选择「保存为模板」创建", EditorStyles.centeredGreyMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════
        //  蓝图模板标签页 — 右侧编辑
        // ═══════════════════════════════════════════════════════════

        private void DrawBlueprintEditor()
        {
            if (_selectedBlueprint == null)
            {
                EditorGUILayout.LabelField("请在左侧选择一个蓝图模板", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                var tmpl = _selectedBlueprint;
                string displayName = string.IsNullOrEmpty(tmpl.DisplayName) ? tmpl.name : tmpl.DisplayName;
                EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // 可编辑元数据
                var so = new SerializedObject(tmpl);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("DisplayName"));

                // Category 下拉
                var catProp = so.FindProperty("Category");
                var bpCategories = _blueprintTemplates
                    .Select(t => t.Category)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct().OrderBy(c => c).ToList();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(catProp);
                if (bpCategories.Count > 0 &&
                    EditorGUILayout.DropdownButton(new GUIContent("▼"), FocusType.Keyboard, GUILayout.Width(24)))
                {
                    var menu = new GenericMenu();
                    foreach (var cat in bpCategories)
                    {
                        var captured = cat;
                        menu.AddItem(new GUIContent(cat), catProp.stringValue == cat, () =>
                        {
                            catProp.serializedObject.Update();
                            catProp.stringValue = captured;
                            catProp.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(so.FindProperty("Description"));
                EditorGUILayout.PropertyField(so.FindProperty("Thumbnail"));
                so.ApplyModifiedProperties();

                // 统计信息（只读）
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("统计信息", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("节点数", tmpl.NodeCount.ToString());
                    EditorGUILayout.LabelField("Action 类型", string.IsNullOrEmpty(tmpl.ActionTypesSummary)
                        ? "(无)" : tmpl.ActionTypesSummary);
                    EditorGUILayout.LabelField("创建日期", string.IsNullOrEmpty(tmpl.CreatedDate)
                        ? "(未知)" : tmpl.CreatedDate);

                    if (tmpl.HasValidGraph)
                        EditorGUILayout.LabelField("图数据", $"{tmpl.GraphJson.Length} 字符");
                    else
                        EditorGUILayout.HelpBox("GraphJson 为空，模板不可用", MessageType.Warning);
                }

                // 绑定需求
                if (tmpl.BindingRequirements.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("绑定需求", EditorStyles.boldLabel);
                        foreach (var req in tmpl.BindingRequirements)
                        {
                            EditorGUILayout.LabelField(
                                $"  {req.Description}",
                                $"[{req.MarkerTypeId}] key={req.BindingKey}");
                        }
                    }
                }

                // 操作按钮
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("在 Project 中定位"))
                    {
                        EditorGUIUtility.PingObject(tmpl);
                        Selection.activeObject = tmpl;
                    }

                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("删除模板", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除蓝图模板 '{displayName}' 吗？\n此操作不可撤销。",
                            "删除", "取消"))
                        {
                            var path = AssetDatabase.GetAssetPath(tmpl);
                            AssetDatabase.DeleteAsset(path);
                            _selectedBlueprint = null;
                            RefreshBlueprintTemplates();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════
        //  Gizmo 样式标签页
        // ═══════════════════════════════════════════════════════════

        private void DrawGizmoStyleLeft()
        {
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                EditorGUILayout.LabelField("Gizmo 样式配置", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                if (GizmoStyleConstants.HasOverride)
                {
                    EditorGUILayout.HelpBox("已加载 GizmoStyleSO，修改将实时反映到 Scene View。", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("未找到 GizmoStyleSO 资产。\n使用 C# 默认值。\n点击下方按钮创建。", MessageType.Warning);

                    if (GUILayout.Button("创建 GizmoStyleSO", GUILayout.Height(28)))
                        CreateGizmoStyleAsset();
                }

                EditorGUILayout.Space(8);

                // 分类导航
                EditorGUILayout.LabelField("配置分类", EditorStyles.boldLabel);
                string[] sections = { "图层颜色", "填充透明度", "脉冲动画", "拾取参数", "标签显示",
                    "Point 标记", "Area 标记", "Entity 标记", "高亮效果", "选中效果" };
                foreach (var s in sections)
                {
                    EditorGUILayout.LabelField($"  · {s}", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawGizmoStyleEditor()
        {
            if (_gizmoStyleSO == null)
            {
                EditorGUILayout.LabelField("未找到 GizmoStyleSO", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("项目中没有 GizmoStyleSO 资产，所有 Gizmo 使用 C# 默认值。\n在左侧点击「创建」按钮，或通过 Create 菜单创建。", MessageType.Info);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                _gizmoStyleSO.Update();

                EditorGUILayout.LabelField("Gizmo 样式配置  [可编辑]", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // 逐个 Header 绘制（保留 SO 中的 Header 分组）
                var iterator = _gizmoStyleSO.GetIterator();
                iterator.NextVisible(true); // 跳过 m_Script
                while (iterator.NextVisible(false))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                if (_gizmoStyleSO.ApplyModifiedProperties())
                {
                    // SO 修改后刷新缓存，Scene View 下一帧自动使用新值
                    GizmoStyleConstants.InvalidateCache();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("在 Project 中定位"))
                    {
                        var so = _gizmoStyleSO.targetObject;
                        EditorGUIUtility.PingObject(so);
                        Selection.activeObject = so;
                    }

                    if (GUILayout.Button("重置为默认值", GUILayout.Width(100)))
                    {
                        if (EditorUtility.DisplayDialog("重置确认",
                            "确定要将所有 Gizmo 样式重置为 C# 默认值吗？", "重置", "取消"))
                        {
                            ResetGizmoStyleToDefaults();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateGizmoStyleAsset()
        {
            var dir = DefaultTemplateDir;
            EnsureDirectory(dir);

            var asset = ScriptableObject.CreateInstance<GizmoStyleSO>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/GizmoStyle.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            GizmoStyleConstants.InvalidateCache();
            RefreshGizmoStyle();

            SBLog.Info(SBLogTags.Template, $"已创建 GizmoStyleSO: {path}");
            Repaint();
        }

        private void ResetGizmoStyleToDefaults()
        {
            if (_gizmoStyleSO?.targetObject is GizmoStyleSO style)
            {
                Undo.RecordObject(style, "重置 GizmoStyle");
                // 通过创建临时默认实例来复制默认值
                var defaults = ScriptableObject.CreateInstance<GizmoStyleSO>();
                EditorUtility.CopySerialized(defaults, style);
                ScriptableObject.DestroyImmediate(defaults);
                EditorUtility.SetDirty(style);
                GizmoStyleConstants.InvalidateCache();
                SceneView.RepaintAll();
                _gizmoStyleSO = new SerializedObject(style);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  标记预设标签页
        // ═══════════════════════════════════════════════════════════

        private void DrawMarkerPresetList()
        {
            // 新建按钮
            if (GUILayout.Button("+ 新建标记预设", GUILayout.Height(24)))
                CreateNewMarkerPreset();

            EditorGUILayout.Space(4);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                var filtered = _markerPresets
                    .Where(p => string.IsNullOrEmpty(_searchText) ||
                                p.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                p.PresetId.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                p.BaseMarkerTypeId.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                var grouped = filtered
                    .GroupBy(p => string.IsNullOrEmpty(p.Category) ? "未分类" : p.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    EditorGUILayout.LabelField(
                        $"{group.Key} ({group.Count()})",
                        EditorStyles.boldLabel);

                    foreach (var preset in group.OrderBy(p => p.DisplayName))
                    {
                        DrawPresetListItem(preset);
                    }

                    EditorGUILayout.Space(2);
                }

                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无标记预设", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("标记预设用于定义已有标记类型的语义变体。\n例如: 精英刷怪点 = PointMarker + 红色 + Tag=\"Combat.Elite\"", MessageType.Info);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetListItem(MarkerPresetSO preset)
        {
            bool isSelected = _selectedPreset == preset;
            var bgColor = isSelected ? new Color(0.24f, 0.37f, 0.59f) : Color.clear;

            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, bgColor);

            // 左侧颜色条（使用自定义颜色或默认灰色）
            var barColor = preset.UseGizmoColor ? preset.GizmoColor : Color.gray;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), barColor);

            // 名称
            string displayName = string.IsNullOrEmpty(preset.DisplayName) ? preset.name : preset.DisplayName;
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = isSelected ? Color.white : GUI.skin.label.normal.textColor }
            };
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, rect.width - 55, rect.height),
                displayName, style);

            // 类型标签
            var typeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            };
            EditorGUI.LabelField(new Rect(rect.xMax - 50, rect.y, 45, rect.height),
                preset.BaseMarkerTypeId, typeStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedPreset = preset;
                _selectedPresetSO = new SerializedObject(preset);
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawMarkerPresetEditor()
        {
            if (_selectedPreset == null || _selectedPresetSO == null)
            {
                EditorGUILayout.LabelField("请在左侧选择一个标记预设", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                string displayName = string.IsNullOrEmpty(_selectedPreset.DisplayName)
                    ? _selectedPreset.name : _selectedPreset.DisplayName;
                EditorGUILayout.LabelField($"{displayName}  [标记预设 - 可编辑]", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                _selectedPresetSO.Update();

                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("PresetId"));
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DisplayName"));

                // Category 下拉
                var catProp = _selectedPresetSO.FindProperty("Category");
                var presetCategories = _markerPresets
                    .Select(p => p.Category)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct().OrderBy(c => c).ToList();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(catProp);
                if (presetCategories.Count > 0 &&
                    EditorGUILayout.DropdownButton(new GUIContent("▼"), FocusType.Keyboard, GUILayout.Width(24)))
                {
                    var menu = new GenericMenu();
                    foreach (var cat in presetCategories)
                    {
                        var captured = cat;
                        menu.AddItem(new GUIContent(cat), catProp.stringValue == cat, () =>
                        {
                            catProp.serializedObject.Update();
                            catProp.stringValue = captured;
                            catProp.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("Description"));

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("BaseMarkerTypeId"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("默认值", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DefaultTag"));
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("NamePrefix"));
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("UseGizmoColor"));
                if (_selectedPreset.UseGizmoColor)
                    EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("GizmoColor"));

                // 按类型条件显示特有属性
                if (_selectedPreset.BaseMarkerTypeId == "Area")
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Area 特有", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DefaultAreaShape"));
                    EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DefaultBoxSize"));
                    EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DefaultHeight"));
                }

                if (_selectedPreset.BaseMarkerTypeId == "Entity")
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Entity 特有", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("DefaultPrefab"));
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_selectedPresetSO.FindProperty("MatchTags"), true);

                if (_selectedPresetSO.ApplyModifiedProperties())
                {
                    MarkerPresetRegistry.Invalidate();
                    RefreshMarkerPresets();
                }

                // 验证
                EditorGUILayout.Space(4);
                if (string.IsNullOrWhiteSpace(_selectedPreset.PresetId))
                    EditorGUILayout.HelpBox("PresetId 不能为空", MessageType.Error);
                if (string.IsNullOrWhiteSpace(_selectedPreset.DisplayName))
                    EditorGUILayout.HelpBox("DisplayName 不能为空", MessageType.Error);

                // 引用关系：哪些 Action 引用了此预设
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("引用此预设的 Action", EditorStyles.boldLabel);
                var referencingActions = _actionEntries
                    .Where(e => IsActionReferencingPreset(e, _selectedPreset.PresetId))
                    .ToList();

                if (referencingActions.Count > 0)
                {
                    foreach (var entry in referencingActions)
                    {
                        EditorGUILayout.LabelField($"  • {entry.DisplayName} ({entry.TypeId})",
                            EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("  暂无 Action 引用此预设", EditorStyles.miniLabel);
                }

                // 操作按钮
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("在 Project 中定位"))
                    {
                        EditorGUIUtility.PingObject(_selectedPreset);
                        Selection.activeObject = _selectedPreset;
                    }

                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("删除预设", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除预设 '{displayName}' 吗？\n此操作不可撤销。",
                            "删除", "取消"))
                        {
                            var path = AssetDatabase.GetAssetPath(_selectedPreset);
                            AssetDatabase.DeleteAsset(path);
                            _selectedPreset = null;
                            _selectedPresetSO = null;
                            MarkerPresetRegistry.Invalidate();
                            RefreshMarkerPresets();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewMarkerPreset()
        {
            var dir = DefaultTemplateDir + "/MarkerPresets";
            EnsureDirectory(dir);

            var preset = CreateInstance<MarkerPresetSO>();
            preset.PresetId = "New.Preset";
            preset.DisplayName = "新预设";
            preset.BaseMarkerTypeId = "Point";

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewMarkerPreset.asset");
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            MarkerPresetRegistry.Invalidate();
            RefreshMarkerPresets();

            _selectedPreset = preset;
            _selectedPresetSO = new SerializedObject(preset);

            SBLog.Info(SBLogTags.Template, $"已创建新标记预设: {path}");
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════
        //  分类标签页
        // ═══════════════════════════════════════════════════════════

        private void DrawCategoryList()
        {
            // 新建按钮
            if (GUILayout.Button("+ 新建分类", GUILayout.Height(24)))
                CreateNewCategory();

            EditorGUILayout.Space(4);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                var filtered = _categories
                    .Where(c => string.IsNullOrEmpty(_searchText) ||
                                c.CategoryId.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                c.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.CategoryId)
                    .ToList();

                foreach (var cat in filtered)
                {
                    DrawCategoryListItem(cat);
                }

                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无分类定义", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("分类用于管理 Action 节点的分组、排序和默认主题色。\n未配置分类时使用字母排序和默认灰色。", MessageType.Info);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryListItem(CategorySO cat)
        {
            bool isSelected = _selectedCategory == cat;
            var bgColor = isSelected ? new Color(0.24f, 0.37f, 0.59f) : Color.clear;

            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, bgColor);

            // 左侧颜色条
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), cat.ThemeColor);

            // 名称
            string displayName = cat.GetDisplayName();
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = isSelected ? Color.white : GUI.skin.label.normal.textColor }
            };
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, rect.width - 50, rect.height),
                displayName, style);

            // 排序权重
            var orderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            };
            EditorGUI.LabelField(new Rect(rect.xMax - 45, rect.y, 40, rect.height),
                $"#{cat.SortOrder}", orderStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedCategory = cat;
                _selectedCategorySO = new SerializedObject(cat);
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawCategoryEditor()
        {
            if (_selectedCategory == null || _selectedCategorySO == null)
            {
                EditorGUILayout.LabelField("请在左侧选择一个分类", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                string displayName = _selectedCategory.GetDisplayName();
                EditorGUILayout.LabelField($"{displayName}  [分类 - 可编辑]", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                _selectedCategorySO.Update();

                var iterator = _selectedCategorySO.GetIterator();
                iterator.NextVisible(true); // 跳过 m_Script
                while (iterator.NextVisible(false))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                if (_selectedCategorySO.ApplyModifiedProperties())
                {
                    CategoryRegistry.Invalidate();
                    RefreshCategories();
                }

                // 显示此分类下有多少 Action
                EditorGUILayout.Space(4);
                int actionCount = _actionEntries.Count(e =>
                    string.Equals(e.Category, _selectedCategory.CategoryId, StringComparison.OrdinalIgnoreCase));
                EditorGUILayout.HelpBox($"此分类下有 {actionCount} 个 Action 定义。", MessageType.Info);

                // 操作按钮
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("在 Project 中定位"))
                    {
                        EditorGUIUtility.PingObject(_selectedCategory);
                        Selection.activeObject = _selectedCategory;
                    }

                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("删除分类", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除分类 '{displayName}' 吗？\n" +
                            "已有 Action 的 Category 字符串不会被修改，只是失去排序和主题色继承。",
                            "删除", "取消"))
                        {
                            var path = AssetDatabase.GetAssetPath(_selectedCategory);
                            AssetDatabase.DeleteAsset(path);
                            _selectedCategory = null;
                            _selectedCategorySO = null;
                            CategoryRegistry.Invalidate();
                            RefreshCategories();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewCategory()
        {
            var dir = DefaultTemplateDir + "/Categories";
            EnsureDirectory(dir);

            var cat = CreateInstance<CategorySO>();
            cat.CategoryId = "NewCategory";
            cat.DisplayName = "新分类";
            cat.SortOrder = 100;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewCategory.asset");
            AssetDatabase.CreateAsset(cat, path);
            AssetDatabase.SaveAssets();

            CategoryRegistry.Invalidate();
            RefreshCategories();

            _selectedCategory = cat;
            _selectedCategorySO = new SerializedObject(cat);

            SBLog.Info(SBLogTags.Template, $"已创建新分类: {path}");
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════
        //  验证规则标签页
        // ═══════════════════════════════════════════════════════════

        private void DrawValidationRuleList()
        {
            // 新建按钮
            if (GUILayout.Button("+ 新建验证规则", GUILayout.Height(24)))
                CreateNewValidationRule();

            EditorGUILayout.Space(4);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            {
                var filtered = _validationRules
                    .Where(r => string.IsNullOrEmpty(_searchText) ||
                                r.RuleId.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Description.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                // 按类型分组
                var grouped = filtered
                    .GroupBy(r => r.Type)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    string typeName = group.Key switch
                    {
                        ValidationType.PropertyRequired => "属性必填",
                        ValidationType.BindingRequired => "绑定必需",
                        ValidationType.MinNodesInSubGraph => "子蓝图最少节点",
                        _ => group.Key.ToString()
                    };
                    EditorGUILayout.LabelField($"{typeName} ({group.Count()})", EditorStyles.boldLabel);

                    foreach (var rule in group.OrderBy(r => r.RuleId))
                    {
                        DrawValidationRuleListItem(rule);
                    }

                    EditorGUILayout.Space(2);
                }

                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无验证规则", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("验证规则在导出时自动执行，补充 C# 内置的结构验证。\n" +
                        "支持的规则类型：属性必填、绑定必需、子蓝图最少节点。", MessageType.Info);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawValidationRuleListItem(ValidationRuleSO rule)
        {
            bool isSelected = _selectedRule == rule;
            var bgColor = isSelected ? new Color(0.24f, 0.37f, 0.59f) : Color.clear;

            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, bgColor);

            // 左侧状态指示（启用=绿色，禁用=灰色）
            var statusColor = rule.Enabled ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), statusColor);

            // 名称
            string displayName = string.IsNullOrEmpty(rule.RuleId) ? rule.name : rule.RuleId;
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = isSelected ? Color.white : (rule.Enabled ? GUI.skin.label.normal.textColor : Color.gray) }
            };
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, rect.width - 50, rect.height),
                displayName, style);

            // 严重级别标签
            var sevStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = rule.Severity switch
                {
                    ValidationSeverity.Error => new Color(0.9f, 0.3f, 0.3f),
                    ValidationSeverity.Warning => new Color(0.9f, 0.7f, 0.2f),
                    _ => Color.gray
                }}
            };
            EditorGUI.LabelField(new Rect(rect.xMax - 50, rect.y, 45, rect.height),
                rule.Severity.ToString(), sevStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedRule = rule;
                _selectedRuleSO = new SerializedObject(rule);
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawValidationRuleEditor()
        {
            if (_selectedRule == null || _selectedRuleSO == null)
            {
                EditorGUILayout.LabelField("请在左侧选择一个验证规则", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            {
                string displayName = string.IsNullOrEmpty(_selectedRule.RuleId) ? _selectedRule.name : _selectedRule.RuleId;
                EditorGUILayout.LabelField($"{displayName}  [验证规则 - 可编辑]", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                _selectedRuleSO.Update();

                EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("RuleId"));
                EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("Description"));
                EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("Severity"));
                EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("Enabled"));

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("Type"));

                // 按类型条件显示参数
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);

                switch (_selectedRule.Type)
                {
                    case ValidationType.PropertyRequired:
                        EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("TargetActionTypeId"));
                        EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("TargetPropertyKey"));
                        break;
                    case ValidationType.BindingRequired:
                        EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("TargetActionTypeId"));
                        EditorGUILayout.HelpBox("将检查目标 Action 的所有 SceneRequirement 是否都已绑定。", MessageType.Info);
                        break;
                    case ValidationType.MinNodesInSubGraph:
                        EditorGUILayout.PropertyField(_selectedRuleSO.FindProperty("MinNodeCount"));
                        EditorGUILayout.HelpBox("将检查每个子蓝图中的非边界节点数是否达到最少要求。", MessageType.Info);
                        break;
                }

                if (_selectedRuleSO.ApplyModifiedProperties())
                {
                    ValidationRuleRegistry.Invalidate();
                    RefreshValidationRules();
                }

                // 验证
                EditorGUILayout.Space(4);
                if (string.IsNullOrWhiteSpace(_selectedRule.RuleId))
                    EditorGUILayout.HelpBox("RuleId 不能为空", MessageType.Error);

                if (_selectedRule.Type == ValidationType.PropertyRequired)
                {
                    if (string.IsNullOrWhiteSpace(_selectedRule.TargetActionTypeId))
                        EditorGUILayout.HelpBox("TargetActionTypeId 不能为空", MessageType.Error);
                    if (string.IsNullOrWhiteSpace(_selectedRule.TargetPropertyKey))
                        EditorGUILayout.HelpBox("TargetPropertyKey 不能为空", MessageType.Error);
                }
                else if (_selectedRule.Type == ValidationType.BindingRequired)
                {
                    if (string.IsNullOrWhiteSpace(_selectedRule.TargetActionTypeId))
                        EditorGUILayout.HelpBox("TargetActionTypeId 不能为空", MessageType.Error);
                }

                // 操作按钮
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("在 Project 中定位"))
                    {
                        EditorGUIUtility.PingObject(_selectedRule);
                        Selection.activeObject = _selectedRule;
                    }

                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("删除规则", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除验证规则 '{displayName}' 吗？\n此操作不可撤销。",
                            "删除", "取消"))
                        {
                            var path = AssetDatabase.GetAssetPath(_selectedRule);
                            AssetDatabase.DeleteAsset(path);
                            _selectedRule = null;
                            _selectedRuleSO = null;
                            ValidationRuleRegistry.Invalidate();
                            RefreshValidationRules();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewValidationRule()
        {
            var dir = DefaultTemplateDir + "/ValidationRules";
            EnsureDirectory(dir);

            var rule = CreateInstance<ValidationRuleSO>();
            rule.RuleId = "New.Rule";
            rule.Description = "新验证规则";
            rule.Severity = ValidationSeverity.Warning;
            rule.Enabled = true;
            rule.Type = ValidationType.PropertyRequired;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewValidationRule.asset");
            AssetDatabase.CreateAsset(rule, path);
            AssetDatabase.SaveAssets();

            ValidationRuleRegistry.Invalidate();
            RefreshValidationRules();

            _selectedRule = rule;
            _selectedRuleSO = new SerializedObject(rule);

            SBLog.Info(SBLogTags.Template, $"已创建新验证规则: {path}");
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════
        //  数据刷新
        // ═══════════════════════════════════════════════════════════

        private void RefreshActionEntries()
        {
            _actionEntries.Clear();

            // C# 定义
            var csharpRegistry = new ActionRegistry();
            csharpRegistry.AutoDiscover();
            foreach (var def in csharpRegistry.GetAll())
            {
                _actionEntries.Add(new ActionEntryItem
                {
                    TypeId = def.TypeId,
                    DisplayName = def.DisplayName,
                    Category = def.Category,
                    ThemeColor = new Color(def.ThemeColor.R, def.ThemeColor.G, def.ThemeColor.B, def.ThemeColor.A),
                    Duration = def.Duration.ToString(),
                    IsCode = true,
                    IsBuiltIn = BuiltInCategories.Contains(def.Category),
                    Definition = def,
                    Template = null
                });
            }

            // SO 模板
            var guids = AssetDatabase.FindAssets("t:ActionTemplateSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<ActionTemplateSO>(path);
                if (template == null) continue;

                _actionEntries.Add(new ActionEntryItem
                {
                    TypeId = template.TypeId,
                    DisplayName = string.IsNullOrEmpty(template.DisplayName) ? template.name : template.DisplayName,
                    Category = template.Category,
                    ThemeColor = template.ThemeColor,
                    Duration = template.Duration.ToString(),
                    IsCode = false,
                    Definition = null,
                    Template = template
                });
            }
        }

        private void RefreshBlueprintTemplates()
        {
            _blueprintTemplates = BlueprintTemplateUtils.FindAllTemplates();
        }

        private void RefreshGizmoStyle()
        {
            GizmoStyleConstants.InvalidateCache();
            var so = GizmoStyleConstants.Override;
            _gizmoStyleSO = so != null ? new SerializedObject(so) : null;
        }

        private void RefreshMarkerPresets()
        {
            MarkerPresetRegistry.Invalidate();
            _markerPresets = MarkerPresetRegistry.GetAll().ToList();
        }

        private void RefreshCategories()
        {
            CategoryRegistry.Invalidate();
            _categories = CategoryRegistry.GetAll().ToList();
        }

        private void RefreshValidationRules()
        {
            ValidationRuleRegistry.Invalidate();
            _validationRules = ValidationRuleRegistry.GetAll().ToList();
        }

        // ═══════════════════════════════════════════════════════════
        //  创建新模板
        // ═══════════════════════════════════════════════════════════

        private void CreateNewActionTemplate()
        {
            // 确保目录存在
            var dir = DefaultTemplateDir + "/Actions";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                EnsureDirectory(dir);
            }

            var template = CreateInstance<ActionTemplateSO>();
            template.TypeId = "NewCategory.NewAction";
            template.DisplayName = "新 Action";
            template.Category = "NewCategory";

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewActionTemplate.asset");
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();

            RefreshActionEntries();

            // 选中新创建的条目
            _selectedAction = _actionEntries.FirstOrDefault(e => e.Template == template);
            if (_selectedAction != null)
                _selectedActionSO = new SerializedObject(template);

            SBLog.Info(SBLogTags.Template, $"已创建新 Action 模板: {path}");
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        private bool MatchSearch(ActionEntryItem entry)
        {
            if (string.IsNullOrEmpty(_searchText)) return true;
            return entry.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.TypeId.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Category.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsActionReferencingPreset(ActionEntryItem entry, string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId)) return false;

            if (entry.Definition != null)
            {
                return entry.Definition.SceneRequirements.Any(r =>
                    !string.IsNullOrEmpty(r.PresetId)
                    && r.PresetId.Equals(presetId, StringComparison.OrdinalIgnoreCase));
            }

            if (entry.Template != null)
            {
                return entry.Template.SceneRequirements.Any(r =>
                    r.PresetRef != null
                    && !string.IsNullOrEmpty(r.PresetRef.PresetId)
                    && r.PresetRef.PresetId.Equals(presetId, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private static void EnsureDirectory(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  数据模型
        // ═══════════════════════════════════════════════════════════

        private class ActionEntryItem
        {
            public string TypeId = "";
            public string DisplayName = "";
            public string Category = "";
            public Color ThemeColor = Color.gray;
            public string Duration = "";
            public bool IsCode;
            public bool IsBuiltIn;
            public ActionDefinition? Definition;
            public ActionTemplateSO? Template;
        }
    }
}
