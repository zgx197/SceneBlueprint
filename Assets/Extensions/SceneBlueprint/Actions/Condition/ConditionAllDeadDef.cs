#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Condition
{
    /// <summary>
    /// Condition.AllDead - 监听所有实体死亡
    /// <para>
    /// 节点类型：Condition（条件监听）
    /// 特点：有 Data 输入端口 + Event 输出端口，无 Flow 端口
    /// 用途：监听指定实体列表，当全部死亡时触发事件
    /// </para>
    /// </summary>
    [ActionType("Condition.AllDead")]
    public class ConditionAllDeadDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Condition.AllDead",
            DisplayName = "全部死亡",
            Category = "Condition",
            Description = "监听实体列表，当全部死亡时触发事件",
            ThemeColor = new Color4(0.9f, 0.7f, 0.2f), // 金黄色 - 条件/触发
            Duration = ActionDuration.Passive, // Condition 节点是 Passive（被动监听）

            Ports = new[]
            {
                // Data 输入：监听的实体列表
                Port.DataIn<EntityRefArrayType>("entities", "监听实体",
                    required: true,
                    description: "要监听的实体引用列表，通常来自 Spawn.Execute 节点"),

                // Event 输出：全部死亡时触发
                Port.Event("onAllDead", "全部死亡")
            },

            Properties = new[]
            {
                // 是否在监听开始时检查一次（如果已经全死了，立即触发）
                Prop.Bool("checkImmediately", "立即检查", defaultValue: false, order: 0),

                // 超时时间（秒），0 表示不限制
                Prop.Float("timeout", "超时时间", defaultValue: 0f, min: 0f, max: 600f, order: 1)
            }
        };
    }
}
