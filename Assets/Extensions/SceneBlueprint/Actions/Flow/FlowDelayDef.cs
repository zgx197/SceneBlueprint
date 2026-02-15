#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 延迟节点定义——等待指定时间后再继续执行后续流程。
    /// <para>
    /// 这是一个 Duration 类型的行动（有运行状态），
    /// 运行时会等待 duration 秒后才触发输出端口。
    /// </para>
    /// <para>节点拓扑：[前置] ─in→ [Delay: 2秒] ─out→ [后续]</para>
    /// </summary>
    [ActionType("Flow.Delay")]
    public class FlowDelayDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.Delay",
            DisplayName = "延迟",
            Category = "Flow",
            Description = "等待指定时间后继续执行",
            ThemeColor = new Color4(0.6f, 0.6f, 0.6f), // 灰色——表示“等待”
            Duration = ActionDuration.Duration, // 持续型——需要等待时间到期
            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },
            Properties = new[]
            {
                // 延迟时间，单位秒。范围 0~300秒，默认 1秒。
                Prop.Float("duration", "延迟时间(秒)", defaultValue: 1f, min: 0f, max: 300f)
            }
        };
    }
}
