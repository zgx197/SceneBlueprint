#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 条件分支节点定义——根据条件表达式选择不同的执行路径。
    /// <para>
    /// 运行时求值 condition 表达式，为 true 时走 "true" 端口，
    /// 否则走 "false" 端口。是瞬时型行动。
    /// </para>
    /// <para>节点拓扑：
    ///              ┌─ true →  [行动A]
    /// [前置] ─in→ [Branch]
    ///              └─ false → [行动B]
    /// </para>
    /// </summary>
    [ActionType("Flow.Branch")]
    public class FlowBranchDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.Branch",
            DisplayName = "条件分支",
            Category = "Flow",
            Description = "根据条件选择不同的执行路径",
            ThemeColor = new Color4(0.9f, 0.7f, 0.2f), // 黄色——表示“决策”
            Duration = ActionDuration.Instant, // 瞬时型——立即判断并走对应分支
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("true", "True"),   // 条件为 true 时的输出
                Port.Out("false", "False")  // 条件为 false 时的输出
            },
            Properties = new[]
            {
                // 条件表达式，运行时求值。具体语法由运行时决定。
                Prop.String("condition", "条件表达式", defaultValue: "")
            }
        };
    }
}
