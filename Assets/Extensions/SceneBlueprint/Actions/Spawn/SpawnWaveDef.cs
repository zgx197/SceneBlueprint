#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// 波次刷怪节点——按固定间隔刷出多波怪物。
    /// <para>
    /// 职责单一：只负责"波次刷怪"逻辑，不包含行为控制。
    /// 怪物行为由 Behavior.Assign 节点单独控制。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// [Trigger.EnterArea] → [Spawn.Wave] → [Behavior.Assign]
    ///                            ↓ onWaveComplete
    ///                       [VFX.ScreenFlash]
    /// </code>
    /// </para>
    /// </summary>
    [ActionType("Spawn.Wave")]
    public class SpawnWaveDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Spawn.Wave",
            DisplayName = "波次刷怪",
            Category = "Spawn",
            Description = "按固定间隔刷出多波怪物",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色 - 刷怪
            Duration = ActionDuration.Duration, // 持续型 - 多波刷怪需要时间

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "完成"),          // 全部波次完成后
                // 波次完成端口（Multiple），每波完成时可触发多个后续节点
                new PortDefinition
                {
                    Id = "onWaveComplete",
                    DisplayName = "波次完成",
                    Kind = NodeGraph.Core.PortKind.Control,
                    Direction = NodeGraph.Core.PortDirection.Output,
                    Capacity = NodeGraph.Core.PortCapacity.Multiple
                }
            },

            Properties = new[]
            {
                // 怪物模板
                Prop.AssetRef("monsterTemplate", "怪物模板", order: 0),

                // 波数控制
                Prop.Int("waveCount", "波数", defaultValue: 3, min: 1, max: 50, order: 1),

                Prop.Int("monstersPerWave", "每波数量", defaultValue: 5, min: 1, max: 20, order: 2),

                Prop.Float("waveInterval", "波间隔(秒)", defaultValue: 3f, min: 0.1f, max: 60f, order: 3),

                // 场景绑定
                Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area, order: 4)
            },

            SceneRequirements = new[]
            {
                // 必需一个区域标记
                new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
                    displayName: "刷怪区域",
                    defaultTag: "Spawn.Area",
                    required: true)
            }
        };
    }
}
