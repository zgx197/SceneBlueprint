#nullable enable
using NodeGraph.Abstraction;
using NodeGraph.Core;
using NodeGraph.View;
using SceneBlueprint.Core;

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
        /// 获取 ActionRegistry 实例（用于外部查询已注册的 Action 类型）。
        /// 每次调用都会重新发现，适合在编辑器初始化时使用。
        /// </summary>
        public static ActionRegistry CreateActionRegistry()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();
            return registry;
        }
    }
}
