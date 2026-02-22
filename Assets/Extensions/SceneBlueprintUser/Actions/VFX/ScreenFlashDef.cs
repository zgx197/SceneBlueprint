#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.VFX
{
    /// <summary>
    /// 屏幕闪光节点——全屏颜色闪烁效果。
    /// <para>
    /// 职责：视觉表现，用于受击反馈、危险提示等。
    /// </para>
    /// </summary>
    [ActionType("VFX.ScreenFlash")]
    public class ScreenFlashDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "VFX.ScreenFlash",
            DisplayName = "屏幕闪光",
            Category = "VFX",
            Description = "全屏颜色闪烁效果",
            ThemeColor = new Color4(0.8f, 0.4f, 0.9f), // 粉紫色 - 视觉效果
            Duration = ActionDuration.Duration, // 持续型 - 闪光有时长

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },

            Properties = new[]
            {
                Prop.Enum("flashColor", "颜色",
                    new[] { "Red", "White", "Yellow", "Blue" },
                    defaultValue: "Red", order: 0),

                Prop.Float("duration", "时长(秒)", defaultValue: 0.3f, min: 0.1f, max: 3f, order: 1),

                Prop.Float("intensity", "强度", defaultValue: 0.5f, min: 0.1f, max: 1f, order: 2)
            },

            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
}
