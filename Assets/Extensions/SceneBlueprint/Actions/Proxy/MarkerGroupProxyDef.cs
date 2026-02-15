#nullable enable
using SceneBlueprint.Core;
using NodeGraph.Math;

namespace SceneBlueprint.Actions.Proxy
{
    /// <summary>
    /// 标记组代理节点定义——场景中 MarkerGroup 在图中的表示。
    /// <para>
    /// Proxy 节点不是 Action，没有执行逻辑，只作为数据引用和连线端点。
    /// 它让场景对象可以在图中被可视化，并通过连线与 Action 节点绑定。
    /// </para>
    /// </summary>
    [ActionType(SceneObjectProxyTypes.Group)]
    public class MarkerGroupProxyDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = SceneObjectProxyTypes.Group,
            DisplayName = "标记组",
            Category = "Proxy",
            Description = "场景中的标记组对象（在图中的代理）",
            ThemeColor = new Color4(0.4f, 0.6f, 0.8f), // 淡蓝色 - 区分于普通 Action
            Duration = ActionDuration.Instant, // 代理节点没有持续时间概念
            Ports = System.Array.Empty<PortDefinition>(), // ProxyNode 不需要端口，它只是场景对象的视觉表示
            Properties = System.Array.Empty<PropertyDefinition>(), // 代理节点没有可配置属性
            SceneRequirements = System.Array.Empty<MarkerRequirement>() // 代理节点不需要创建场景对象
        };
    }
}
