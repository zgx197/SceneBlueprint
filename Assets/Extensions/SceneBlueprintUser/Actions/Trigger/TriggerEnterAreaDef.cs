#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Actions.Trigger
{
    /// <summary>
    /// 进入区域条件等待节点——被上游激活后，持续检查玩家是否进入指定区域，满足后完成。
    /// <para>
    /// 统一为"条件等待节点"语义：
    /// - 有 in 端口，通过上游激活（Flow.Start 是唯一起点）
    /// - Duration = Duration，Running 阶段持续检查条件
    /// - 条件满足后 Completed，触发 out 端口的下游节点
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// [Flow.Start] → [Trigger.EnterArea] → [Spawn.Wave] → [Flow.End]
    /// </code>
    /// </para>
    /// </summary>
    [ActionType(AT.Trigger.EnterArea)]
    public class TriggerEnterAreaDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = AT.Trigger.EnterArea,
            DisplayName = "进入区域",
            Category = "Trigger",
            Description = "等待玩家进入指定区域后继续",
            ThemeColor = new Color4(0.9f, 0.6f, 0.2f), // 橙色 - 触发器
            Duration = ActionDuration.Duration, // 持续型：Running 阶段检查条件

            Ports = new[]
            {
                Port.In(ActionPortIds.TriggerEnterArea.In,  "输入"),     // 被上游激活
                Port.Out(ActionPortIds.TriggerEnterArea.Out, "进入时"),  // 条件满足后触发下游
            },

            Properties = new[]
            {
                Prop.SceneBinding(ActionPortIds.TriggerEnterArea.TriggerArea, "触发区域", BindingType.Area, order: 0),
                Prop.Bool(ActionPortIds.TriggerEnterArea.RequireFullyInside, "需要完全进入", defaultValue: false, order: 1),
            },

            SceneRequirements = new[]
            {
                new MarkerRequirement(ActionPortIds.TriggerEnterArea.TriggerArea, MarkerTypeIds.Area,
                    displayName: "触发区域",
                    defaultTag: "Trigger.Enter",
                    required: true)
            }
        };
    }
}
