#nullable enable
using NodeGraph.Abstraction;
using NodeGraph.Core;
using NodeGraph.View;
using NodeGraph.Math;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Analysis.Rules;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 蓝图配置工厂。
    /// 创建并配置 BlueprintProfile，将 SceneBlueprint 的 Action 体系
    /// 完整注册到 NodeGraph 的节点类型系统中。
    /// </summary>
    public static class SceneBlueprintProfile
    {
        /// <summary>
        /// 创建 SceneBlueprint 专用的 BlueprintProfile。
        /// </summary>
        /// <param name="textMeasurer">文字测量器（由引擎层提供）</param>
        /// <param name="nodeTypeRegistry">
        /// 节点类型注册表（通常传入 GraphSettings.NodeTypes，
        /// 确保 Graph 和 Profile 共享同一份注册表）
        /// </param>
        /// <returns>配置完成的 BlueprintProfile</returns>
        public static BlueprintProfile Create(ITextMeasurer textMeasurer, NodeTypeRegistry nodeTypeRegistry)
        {
            // 1. 创建并填充 ActionRegistry（自动发现所有 [ActionType] 标注的 Provider）
            var actionRegistry = new ActionRegistry();
            actionRegistry.AutoDiscover();

            // 1b. 从 ActionTemplateSO 加载策划配置的模板（补充，不覆盖 C#）
            RegisterTemplates(actionRegistry);

            // 1c. ThemeColor 继承：Action 未指定主题色时从 CategorySO 继承
            ApplyCategoryThemeColors(actionRegistry);

            // 2. 将所有 ActionDefinition 桥接注册到传入的 NodeTypeRegistry
            ActionNodeTypeAdapter.RegisterAll(actionRegistry, nodeTypeRegistry);

            // 3. 创建通用内容渲染器
            var contentRenderer = new ActionContentRenderer(actionRegistry);

            // 4. 构建 BlueprintProfile
            var profile = new BlueprintProfile
            {
                Name = "SceneBlueprint",
                FrameBuilder = new DefaultFrameBuilder(textMeasurer),
                Theme = NodeVisualTheme.Dark,
                Topology = GraphTopologyPolicy.DAG,
                NodeTypes = nodeTypeRegistry,
                Features = BlueprintFeatureFlags.MiniMap | BlueprintFeatureFlags.Search
            };

            // 5. 为每种注册的 Action 类型绑定同一个 ContentRenderer
            foreach (var actionDef in actionRegistry.GetAll())
            {
                profile.ContentRenderers[actionDef.TypeId] = contentRenderer;
            }

            return profile;
        }

        /// <summary>
        /// 创建配置完成的 BlueprintAnalyzer（T4 Analyze Phase 入口）。
        /// 按推荐顺序注册内置规则：SB003 → SB001 → SB002 → SB004 → SB005。
        /// </summary>
        public static BlueprintAnalyzer CreateAnalyzer(INodeTypeProvider typeProvider, ActionRegistry actionRegistry)
        {
            return new BlueprintAnalyzer(typeProvider, actionRegistry)
                .AddRule(new MultipleStartRule())              // SB003：快速失败
                .AddRule(new ReachabilityRule())               // SB001：计算可达集合
                .AddRule(new RequiredPortRule())               // SB002：依赖可达集合
                .AddRule(new DeadOutputRule())                 // SB004
                .AddRule(new IsolatedNodeRule());              // SB005
        }

        /// <summary>
        /// 获取 ActionRegistry 实例（用于外部查询已注册的 Action 类型）。
        /// 每次调用都会重新发现，适合在编辑器初始化时使用。
        /// </summary>
        public static ActionRegistry CreateActionRegistry()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();
            RegisterTemplates(registry);
            return registry;
        }

        /// <summary>
        /// 扫描项目中所有 ActionTemplateSO 资产，转换为 ActionDefinition 并注册。
        /// C# 已注册的 TypeId 不会被 SO 覆盖。
        /// </summary>
        private static void RegisterTemplates(ActionRegistry registry)
        {
            var guids = AssetDatabase.FindAssets("t:ActionTemplateSO");
            int registered = 0;
            int skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<ActionTemplateSO>(path);
                if (template == null || string.IsNullOrEmpty(template.TypeId)) continue;

                // C# 已注册的 TypeId 不被 SO 覆盖
                if (registry.TryGet(template.TypeId, out _))
                {
                    SBLog.Warn(SBLogTags.Registry,
                        $"ActionTemplateSO '{template.name}' 的 TypeId '{template.TypeId}' " +
                        $"与 C# 定义冲突，已跳过 ({path})");
                    skipped++;
                    continue;
                }

                try
                {
                    var def = ActionTemplateConverter.Convert(template);
                    registry.Register(def);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    SBLog.Error(SBLogTags.Registry,
                        $"ActionTemplateSO '{template.name}' 转换失败: {ex.Message}");
                }
            }

            if (registered > 0 || skipped > 0)
            {
                SBLog.Info(SBLogTags.Registry,
                    $"ActionTemplateSO 加载完成：注册 {registered} 个，跳过 {skipped} 个");
            }
        }

        /// <summary>
        /// 遍历所有已注册的 ActionDefinition，如果 ThemeColor 是默认灰色且存在匹配的 CategorySO，
        /// 则继承 CategorySO.ThemeColor。
        /// </summary>
        private static void ApplyCategoryThemeColors(ActionRegistry registry)
        {
            int inherited = 0;
            foreach (var def in registry.GetAll())
            {
                // 只处理使用默认灰色的 Action
                if (!IsDefaultGray(def.ThemeColor)) continue;

                var catColor = CategoryRegistry.GetThemeColor(def.Category);
                if (catColor.HasValue)
                {
                    var c = catColor.Value;
                    def.ThemeColor = new Color4(c.r, c.g, c.b, c.a);
                    inherited++;
                }
            }

            if (inherited > 0)
            {
                SBLog.Debug(SBLogTags.Template,
                    $"ThemeColor 继承：{inherited} 个 Action 从 CategorySO 继承了主题色");
            }
        }

        /// <summary>判断 Color4 是否为默认灰色 (0.5, 0.5, 0.5, 1.0)</summary>
        private static bool IsDefaultGray(Color4 c)
        {
            const float eps = 0.01f;
            return System.Math.Abs(c.R - 0.5f) < eps &&
                   System.Math.Abs(c.G - 0.5f) < eps &&
                   System.Math.Abs(c.B - 0.5f) < eps &&
                   System.Math.Abs(c.A - 1.0f) < eps;
        }
    }
}
