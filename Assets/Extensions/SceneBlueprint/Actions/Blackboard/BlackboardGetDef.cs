#nullable enable
using System;
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Blackboard
{
    /// <summary>
    /// 黑板读取节点——从声明变量读取当前值，并写入内部缓存供下游节点使用。
    /// <para>
    /// Phase 2：variableName 属性暂用字符串输入；Phase 3 升级为变量面板下拉 + 强类型数据端口。
    /// 运行时读取变量后，将值存入 frame.Blackboard 内部缓存（key = {nodeId}.{variableName}），
    /// 下游 Flow.Filter 可通过变量名直接读取声明变量，无需依赖缓存。
    /// </para>
    /// </summary>
    [ActionType("Blackboard.Get")]
    public class BlackboardGetDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId      = "Blackboard.Get",
            DisplayName = "读取变量",
            Category    = "Blackboard",
            Description = "从声明变量读取当前值，传递控制流至下游",
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
            },

            SceneRequirements = Array.Empty<MarkerRequirement>()
        };
    }
}
