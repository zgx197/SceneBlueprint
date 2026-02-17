#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// Spawn.Execute - 执行刷怪动作
    /// <para>
    /// 节点类型：Executor（执行动作）
    /// 特点：有 Flow 端口 + Data 输入端口 + Data 输出端口
    /// 用途：接收位置和怪物配置，执行实际的刷怪逻辑
    /// </para>
    /// </summary>
    [ActionType("Spawn.Execute")]
    public class SpawnExecuteDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId = "Spawn.Execute",
            DisplayName = "执行刷怪",
            Category = "Spawn",
            Description = "根据输入的位置和怪物配置执行刷怪",
            ThemeColor = new Color4(0.2f, 0.7f, 0.3f), // 深绿色 - 与 Spawn 分类统一
            Duration = ActionDuration.Duration, // 执行型节点通常是 Duration

            Ports = new[]
            {
                // === Flow 端口 ===
                Port.In("in", "激活"),
                Port.Out("out", "完成"),

                // === Data 输入端口（必需）===
                // 新 API：使用泛型版本，代码更简洁
                Port.DataIn<Vector3ArrayType>("positions", "位置列表",
                    required: true,
                    description: "刷怪位置列表，通常来自 Location 节点"),

                Port.DataIn<MonsterConfigArrayType>("monsters", "怪物配置",
                    required: true,
                    description: "怪物配置列表，通常来自 Monster.Pool 节点"),

                // === Data 输出端口 ===
                Port.DataOut<EntityRefArrayType>("spawnedEntities", "已刷出实体",
                    description: "刷出的实体引用列表，可传递给 Condition 节点监听")
            },

            Properties = new[]
            {
                // 刷怪模式
                Prop.Enum("spawnMode", "刷怪模式",
                    new[] { "OnePerPosition", "RandomDistribute", "AllAtEach" },
                    defaultValue: "OnePerPosition", order: 0),

                // 刷怪间隔（秒）
                Prop.Float("spawnInterval", "刷怪间隔", defaultValue: 0.1f, min: 0f, max: 10f, order: 1),

                // 最大存活数（0 表示不限制）
                Prop.Int("maxAlive", "最大存活数", defaultValue: 0, min: 0, max: 100, order: 2),

                // 是否立即激活怪物
                Prop.Bool("activateImmediately", "立即激活", defaultValue: true, order: 3)
            }
        };
    }
}
