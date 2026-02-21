#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.VFX
{
    /// <summary>
    /// 屏幕警告文字节点——在屏幕中央显示提示文字，持续一段时间后消失。
    /// <para>
    /// 典型用法：Boss 登场提示、精英波次警告、阶段切换通知等。
    /// 通过 Flow.Filter 控制触发时机，文字内容由策划在属性中配置。
    /// </para>
    /// </summary>
    [ActionType(AT.Vfx.ShowWarning)]
    public class ShowWarningDef : IActionDefinitionProvider
    {
        public static class Props
        {
            public const string Text     = "text";
            public const string Duration = "duration";
            public const string Style    = "style";
            public const string FontSize = "fontSize";
        }

        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = AT.Vfx.ShowWarning,
            DisplayName = "屏幕警告",
            Category = "VFX",
            Description = "在屏幕中央显示警告文字",
            ThemeColor = new Color4(0.9f, 0.3f, 0.3f), // 红色——警告类
            Duration = ActionDuration.Duration, // 持续型——文字显示有时长

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },

            Properties = new[]
            {
                Prop.String(Props.Text,     "显示文字", defaultValue: "警告！", order: 0),
                Prop.Float(Props.Duration,  "时长(秒)", defaultValue: 2f,    min: 0.5f, max: 10f,  order: 1),
                Prop.Enum(Props.Style,      "样式",
                    new[] { "Warning", "Info", "Boss" },
                    defaultValue: "Warning", order: 2),
                Prop.Float(Props.FontSize,  "字号",     defaultValue: 48f,   min: 16f,  max: 128f, order: 3),
            },

            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
}
