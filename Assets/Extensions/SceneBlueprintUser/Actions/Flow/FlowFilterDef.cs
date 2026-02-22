#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 条件过滤节点——通过数据端口接收上游值，与常量比较，决定走 pass 或 reject 端口。
    /// <para>
    /// 典型用法：
    /// 1. 将 Spawn.Wave.当前波次 DataOut → compareValue DataIn（数据连线）
    /// 2. 将 Spawn.Wave.每波开始 Flow Out → in Flow In（控制流连线）
    /// 3. 设置 op = ">="，constValue = "3"
    /// 结果：第 3 波以后满足条件，走 pass；否则走 reject。
    /// </para>
    /// <para>
    /// 复杂条件通过蓝图拓扑组合：
    /// - AND = 串联多个 Filter（Filter1.pass → Filter2.in）
    /// - OR  = 并联多个 Filter（Filter1.pass + Filter2.pass → 同一下游）
    /// - NOT = 使用 reject 端口
    /// </para>
    /// <para>节点拓扑：
    ///                                     ┌─ pass →   [条件满足时的下游]
    /// [上游] ─in→ [Filter] ←compareValue─
    ///                                     └─ reject → [条件不满足时的下游（可选）]
    /// </para>
    /// </summary>
    [ActionType(AT.Flow.Filter)]
    public class FlowFilterDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = AT.Flow.Filter,
            DisplayName = "条件过滤",
            Category = "Flow",
            Description = "通过数据端口接收上游值，与常量比较，条件满足走 pass，否则走 reject",
            ThemeColor = new Color4(0.9f, 0.7f, 0.2f), // 黄色——决策类节点
            Duration = ActionDuration.Instant, // 瞬时型——立即判断并路由

            Ports = new[]
            {
                Port.In(ActionPortIds.FlowFilter.In, "输入"),
                Port.DataIn(ActionPortIds.FlowFilter.CompareValue, "比较値", DataTypes.Any), // 接收上游数据端口的值
                Port.Out(ActionPortIds.FlowFilter.Pass,   "满足"),    // 条件满足时触发
                Port.Out(ActionPortIds.FlowFilter.Reject, "不满足"),  // 条件不满足时触发（可选）
            },

            Properties = new[]
            {
                Prop.Enum(ActionPortIds.FlowFilter.Op, "操作符",
                    new[] { "==", "!=", ">", "<", ">=", "<=" },
                    defaultValue: "==", order: 0),
                Prop.String(ActionPortIds.FlowFilter.ConstValue, "常量（无连线时 pass）",
                    defaultValue: "0", order: 1),
            },

            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
}
