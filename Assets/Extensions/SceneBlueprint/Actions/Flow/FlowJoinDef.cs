#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 汇合节点定义——等待所有连入的输入都完成后才继续执行（AND Join）。
    /// <para>
    /// 用于并行分支的同步点。例如：两波刷怪都完成后才触发 Boss 出场。
    /// 等待数量隐式等于实际连入的线数，无需额外配置。
    /// </para>
    /// <para>节点拓扑：
    /// [行动A] ─┐
    ///           ├─in→ [Join] ─out→ [后续]
    /// [行动B] ─┘
    /// </para>
    /// </summary>
    [ActionType(AT.Flow.Join)]
    public class FlowJoinDef : IActionDefinitionProvider
    {
        /// <summary>
        /// 运行时由编译器自动写入的属性键（不在 Inspector 中显示）。
        /// TransitionSystem 通过此键读取待收集的进边数量。
        /// </summary>
        public static class Props
        {
            public const string InEdgeCount = "inEdgeCount";
        }

        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = AT.Flow.Join,
            DisplayName = "汇合",
            Category = "Flow",
            Description = "等待所有输入完成后再继续",
            ThemeColor = new Color4(0.5f, 0.5f, 0.8f), // 蓝色——表示“同步/等待”
            Duration = ActionDuration.Duration, // 持续型——需要等待所有输入完成
            Ports = new[]
            {
                Port.InMulti("in", "输入"), // 汇合语义：可接收多个前置行动
                Port.Out("out", "输出")
            },
        };
    }
}
