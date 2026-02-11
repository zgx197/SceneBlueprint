#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Flow
{
    /// <summary>
    /// 蓝图结束节点定义。
    /// <para>
    /// 标记蓝图流程的终点。一张蓝图可以有多个 End 节点（不同分支可以有不同的结束点）。
    /// 它没有任何属性，只有一个输入端口。
    /// </para>
    /// <para>节点拓扑：[前置行动] ─in→ [End]</para>
    /// </summary>
    [ActionType("Flow.End")]
    public class FlowEndDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Flow.End",
            DisplayName = "结束",
            Category = "Flow",
            Description = "蓝图结束节点",
            ThemeColor = new Color4(0.8f, 0.3f, 0.3f), // 红色——表示“结束”
            Duration = ActionDuration.Instant,
            Ports = new[]
            {
                Port.FlowIn("in", "输入") // 唯一的输入端口
            },
            Properties = System.Array.Empty<PropertyDefinition>() // 无属性
        };
    }
}
