#nullable enable
using System;
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Blackboard
{
    /// <summary>
    /// 黑板写入节点——向声明变量写入一个新值。
    /// <para>
    /// Phase 2：variableName 属性暂用字符串输入；Phase 3 升级为变量面板下拉选择。
    /// 运行时根据变量声明的 Scope 自动路由到 Local（frame.Blackboard）或 Global（GlobalBlackboard）。
    /// </para>
    /// </summary>
    [ActionType("Blackboard.Set")]
    public class BlackboardSetDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId      = "Blackboard.Set",
            DisplayName = "设置变量",
            Category    = "Blackboard",
            Description = "向声明变量写入新值（Local 写入蓝图实例，Global 跨实例共享）",
            ThemeColor  = new Color4(0.3f, 0.6f, 0.9f),
            Duration    = ActionDuration.Instant,

            Ports = new[]
            {
                Port.In("in",  "输入"),
                Port.Out("out", "输出"),
            },

            Properties = new[]
            {
                Prop.VariableSelector("variableIndex", "变量", defaultValue: -1, order: 0),
                Prop.String("value",        "值",    defaultValue: "", order: 1),
            },

            SceneRequirements = Array.Empty<MarkerRequirement>()
        };
    }
}
