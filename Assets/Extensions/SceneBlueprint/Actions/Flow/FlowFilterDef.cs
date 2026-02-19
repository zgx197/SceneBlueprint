#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 条件过滤节点——从 Blackboard 读取上游数据，做条件判断，决定走 pass 或 reject 端口。
    /// <para>
    /// 典型用法：接在事件端口（如 Spawn.Wave 的 onWaveStart）后面，
    /// 根据上游写入 Blackboard 的变量值过滤事件。
    /// </para>
    /// <para>
    /// 来源节点自动推断：运行时从 TransitionSystem 写入的 _activatedBy.{myId} 获取
    /// 上游节点 ID，拼接 Blackboard key = "{sourceId}.{key}" 读取数据。
    /// </para>
    /// <para>
    /// 复杂条件通过蓝图拓扑组合：
    /// - AND = 串联多个 Filter（Filter1.pass → Filter2.in）
    /// - OR  = 并联多个 Filter（Filter1.pass + Filter2.pass → 同一下游）
    /// - NOT = 使用 reject 端口
    /// </para>
    /// <para>节点拓扑：
    ///              ┌─ pass →   [条件满足时的下游]
    /// [上游] ─in→ [Filter]
    ///              └─ reject → [条件不满足时的下游（可选）]
    /// </para>
    /// </summary>
    [ActionType("Flow.Filter")]
    public class FlowFilterDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.Filter",
            DisplayName = "条件过滤",
            Category = "Flow",
            Description = "根据 Blackboard 变量值过滤事件，条件满足走 pass，否则走 reject",
            ThemeColor = new Color4(0.9f, 0.7f, 0.2f), // 黄色——决策类节点
            Duration = ActionDuration.Instant, // 瞬时型——立即判断并路由

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("pass", "满足"),    // 条件满足时触发
                Port.Out("reject", "不满足"), // 条件不满足时触发（可选）
            },

            Properties = new[]
            {
                Prop.String("key", "变量名", defaultValue: "", order: 0),
                Prop.Enum("op", "操作符",
                    new[] { "==", "!=", ">", "<", ">=", "<=" },
                    defaultValue: "==", order: 1),
                Prop.String("value", "目标值", defaultValue: "", order: 2),
            },

            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
}
