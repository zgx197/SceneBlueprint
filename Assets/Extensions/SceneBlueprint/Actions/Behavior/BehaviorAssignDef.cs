#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Behavior
{
    /// <summary>
    /// 赋予行为节点——为刚刷出的怪物指定初始行为。
    /// <para>
    /// 职责：控制怪物的AI行为模式（巡逻、待机、警戒等）。
    /// 通常连接在 Spawn 节点之后。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// [Spawn.Wave] → [Behavior.Assign: Patrol] → [下一步]
    ///                      ↓ (设置巡逻路径)
    ///                 (绑定 patrolRoute)
    /// </code>
    /// </para>
    /// </summary>
    [ActionType("Behavior.Assign")]
    public class BehaviorAssignDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Behavior.Assign",
            DisplayName = "赋予行为",
            Category = "Behavior",
            Description = "为怪物指定初始AI行为",
            ThemeColor = new Color4(0.6f, 0.3f, 0.8f), // 紫色 - 行为控制
            Duration = ActionDuration.Instant, // 瞬时型 - 立即设置行为

            Ports = new[]
            {
                Port.In("in", "输入"),
                Port.Out("out", "输出")
            },

            Properties = new[]
            {
                // 行为类型
                Prop.Enum("behaviorType", "行为类型",
                    new[] { "Idle", "Patrol", "Guard", "Chase", "Flee" },
                    defaultValue: "Idle", order: 0),

                // 巡逻路径（仅 Patrol 模式下可见）
                Prop.SceneBinding("patrolRoute", "巡逻路径", BindingType.Path,
                    visibleWhen: "behaviorType == Patrol", order: 1),

                // 警戒范围（Guard 模式下可见）
                Prop.Float("guardRadius", "警戒半径", defaultValue: 5f, min: 1f, max: 50f,
                    visibleWhen: "behaviorType == Guard", order: 2),

                // 是否循环巡逻
                Prop.Bool("loopPatrol", "循环巡逻", defaultValue: true,
                    visibleWhen: "behaviorType == Patrol", order: 3)
            },

            SceneRequirements = new[]
            {
                // 巡逻路径点（可选，多个）
                new MarkerRequirement("patrolRoute", MarkerTypeIds.Point,
                    displayName: "巡逻路径点",
                    defaultTag: "Behavior.PatrolPoint",
                    allowMultiple: true,
                    minCount: 2,
                    required: false) // 只在 Patrol 模式下需要
            }
        };
    }
}
