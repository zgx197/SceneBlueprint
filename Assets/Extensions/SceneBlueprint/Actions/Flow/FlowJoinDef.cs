#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 汇合节点定义——等待多个输入全部完成后才继续执行。
    /// <para>
    /// 用于并行分支的同步点。例如：两波刷怪都完成后才触发 Boss 出场。
    /// requiredCount 属性控制需要等待几条输入完成。
    /// </para>
    /// <para>节点拓扑：
    /// [行动A] ─┐
    ///           ├─in→ [Join: 2] ─out→ [后续]
    /// [行动B] ─┘
    /// </para>
    /// </summary>
    [ActionType("Flow.Join")]
    public class FlowJoinDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.Join",
            DisplayName = "汇合",
            Category = "Flow",
            Description = "等待所有输入完成后再继续",
            ThemeColor = new Color4(0.5f, 0.5f, 0.8f), // 蓝色——表示“同步/等待”
            Duration = ActionDuration.Duration, // 持续型——需要等待所有输入完成
            Ports = new[]
            {
                Port.In("in", "输入"),  // 多连接——可以接收多个前置行动
                // 汇合后的输出（Multiple），可以同时触发多个后续节点
                new PortDefinition
                {
                    Id = "out",
                    DisplayName = "输出",
                    Kind = NodeGraph.Core.PortKind.Control,
                    Direction = NodeGraph.Core.PortDirection.Output,
                    Capacity = NodeGraph.Core.PortCapacity.Multiple
                }
            },
            Properties = new[]
            {
                // 等待数量：需要多少条输入连线完成后才触发输出
                Prop.Int("requiredCount", "等待数量", defaultValue: 2, min: 1, max: 20,
                    tooltip: "需要多少条输入连线完成后才触发输出")
            }
        };
    }
}
