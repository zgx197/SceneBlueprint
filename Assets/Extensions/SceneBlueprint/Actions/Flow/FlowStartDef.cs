#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 蓝图入口节点定义。
    /// <para>
    /// 每张场景蓝图有且仅有一个 Start 节点，作为整个流程的起点。
    /// 它没有任何属性，只有一个输出端口。
    /// </para>
    /// <para>节点拓扑：[Start] ─out→ [后续行动]</para>
    /// </summary>
    [ActionType("Flow.Start")]
    public class FlowStartDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.Start",
            DisplayName = "开始",
            Category = "Flow",
            Description = "蓝图入口节点，每张蓝图有且仅有一个",
            ThemeColor = new Color4(0.3f, 0.8f, 0.3f), // 绿色——表示“开始”
            Duration = ActionDuration.Instant,
            Ports = new[]
            {
                // Control 输出端口（Multiple），可以同时触发多个后续节点
                new PortDefinition
                {
                    Id = "out",
                    DisplayName = "输出",
                    Kind = NodeGraph.Core.PortKind.Control,
                    Direction = NodeGraph.Core.PortDirection.Output,
                    Capacity = NodeGraph.Core.PortCapacity.Multiple
                }
            },
            Properties = System.Array.Empty<PropertyDefinition>() // 无属性
        };
    }
}
