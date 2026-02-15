#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Trigger
{
    /// <summary>
    /// 玩家进入区域触发节点——当玩家进入指定区域时激活。
    /// <para>
    /// 职责：监听玩家进入事件，作为战斗或场景事件的起始触发器。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// [Trigger.EnterArea] → [Spawn.Wave] → [Behavior.Assign]
    ///                    ↓
    ///                [VFX.CameraZoom]
    /// </code>
    /// </para>
    /// </summary>
    [ActionType("Trigger.EnterArea")]
    public class TriggerEnterAreaDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Trigger.EnterArea",
            DisplayName = "进入区域",
            Category = "Trigger",
            Description = "玩家进入指定区域时触发",
            ThemeColor = new Color4(0.9f, 0.6f, 0.2f), // 橙色 - 触发器
            Duration = ActionDuration.Instant, // 瞬时型 - 触发后立即执行后续

            Ports = new[]
            {
                // Control 输出端口（Multiple），可以同时触发多个后续节点
                // Trigger 节点通常没有 FlowIn，它是事件起点
                new PortDefinition
                {
                    Id = "onEnter",
                    DisplayName = "进入时",
                    Kind = NodeGraph.Core.PortKind.Control,
                    Direction = NodeGraph.Core.PortDirection.Output,
                    Capacity = NodeGraph.Core.PortCapacity.Multiple
                }
            },

            Properties = new[]
            {
                // 触发区域
                Prop.SceneBinding("triggerArea", "触发区域", BindingType.Area, order: 0),

                // 触发次数限制
                Prop.Int("maxTriggerTimes", "触发次数", defaultValue: 1, min: 0, max: 100, order: 1),

                // 是否需要玩家完全进入
                Prop.Bool("requireFullyInside", "需要完全进入", defaultValue: false, order: 2)
            },

            SceneRequirements = new[]
            {
                // 必需一个区域标记
                new MarkerRequirement("triggerArea", MarkerTypeIds.Area,
                    displayName: "触发区域",
                    defaultTag: "Trigger.Enter",
                    required: true)
            }
        };
    }
}
