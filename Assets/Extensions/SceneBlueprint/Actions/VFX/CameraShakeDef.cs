#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.VFX
{
    /// <summary>
    /// 摄像机震动节点——产生屏幕震动效果。
    /// <para>
    /// 职责：视觉表现，通常用于Boss登场、爆炸等场景。
    /// </para>
    /// </summary>
    [ActionType("VFX.CameraShake")]
    public class CameraShakeDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "VFX.CameraShake",
            DisplayName = "摄像机震动",
            Category = "VFX",
            Description = "产生屏幕震动效果",
            ThemeColor = new Color4(0.8f, 0.4f, 0.9f), // 粉紫色 - 视觉效果
            Duration = ActionDuration.Duration, // 持续型 - 震动有时长

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },

            Properties = new[]
            {
                Prop.Float("intensity", "强度", defaultValue: 1f, min: 0.1f, max: 10f, order: 0),

                Prop.Float("duration", "时长(秒)", defaultValue: 0.5f, min: 0.1f, max: 5f, order: 1),

                Prop.Float("frequency", "频率", defaultValue: 20f, min: 1f, max: 100f, order: 2)
            },

            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
}
