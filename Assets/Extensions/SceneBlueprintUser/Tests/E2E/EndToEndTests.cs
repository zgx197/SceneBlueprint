#nullable enable
using System.Linq;
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.E2E
{
    /// <summary>
    /// 端到端集成测试——模拟完整的使用流程，验证各组件协作正确。
    /// <para>
    /// 测试范围：
    /// - 完整流程：AutoDiscover → 创建 NodeData → 修改属性 → JSON 序列化往返
    /// - 所有已注册类型的 NodeData 创建正确性
    /// - VisibleWhen 与实际 Spawn 数据的配合
    /// - Registry 分类正确性
    /// </para>
    /// </summary>
    public class EndToEndTests
    {
        /// <summary>
        /// 完整流程测试：初始化 Registry → 创建 Spawn NodeData → 修改属性 → JSON 序列化往返。
        /// <para>模拟编辑器中的实际使用场景。</para>
        /// </summary>
        [Test]
        public void FullFlow_CreateNodeData_SetProperties_SerializeRoundTrip()
        {
            // 1. 初始化 Registry
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            // 2. 从 Definition 创建 ActionNodeData
            var spawnDef = registry.Get("Spawn.Wave");
            var spawnData = ActionNodeData.CreateFromDefinition(spawnDef);

            // 3. 修改属性
            spawnData.Properties.Set("monstersPerWave", 8);
            spawnData.Properties.Set("monsterTemplate", "elite_group_01");
            spawnData.Properties.Set("waveCount", 5);
            spawnData.Properties.Set("waveInterval", 3.5f);

            // 4. 序列化
            string json = PropertyBagSerializer.ToJson(spawnData.Properties);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Length > 0);

            // 5. 反序列化
            var restored = PropertyBagSerializer.FromJson(json);

            // 6. 断言一致性
            Assert.AreEqual(8, restored.Get<int>("monstersPerWave"));
            Assert.AreEqual("elite_group_01", restored.Get<string>("template"));
            Assert.AreEqual("Interval", restored.Get<string>("tempoType"));
            Assert.AreEqual(3.5f, restored.Get<float>("interval"), 0.001f);
        }

        /// <summary>
        /// 验证所有已注册类型的 NodeData 创建正确性——所有有默认值的属性都应被填充。
        /// </summary>
        [Test]
        public void FullFlow_MultipleNodeTypes_AllCreateCorrectly()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            // 为每种已注册类型创建 NodeData 并验证
            foreach (var def in registry.GetAll())
            {
                var data = ActionNodeData.CreateFromDefinition(def);

                Assert.AreEqual(def.TypeId, data.ActionTypeId,
                    $"ActionNodeData TypeId 与 Definition 不一致: {def.TypeId}");

                // 所有有默认值的属性都应该被设置
                foreach (var prop in def.Properties)
                {
                    if (prop.DefaultValue != null)
                    {
                        Assert.IsTrue(data.Properties.Has(prop.Key),
                            $"[{def.TypeId}] 属性 '{prop.Key}' 默认值未设置");
                    }
                }
            }
        }

        /// <summary>
        /// 完整流程：验证 VisibleWhen 与实际节点数据的配合（使用 Behavior.Assign 测试条件可见）。
        /// <para>
        /// 测试点：
        /// - behaviorType == Patrol 时 patrolRoute 可见
        /// - behaviorType == Guard 时 guardRadius 可见
        /// </para>
        /// </summary>
        [Test]
        public void FullFlow_VisibleWhen_WorksWithBehaviorAssign()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            var behaviorDef = registry.Get("Behavior.Assign");
            var data = ActionNodeData.CreateFromDefinition(behaviorDef);

            // 默认 behaviorType 是 "Idle"
            data.Properties.Set("behaviorType", "Patrol");

            // patrolRoute 的 VisibleWhen = "behaviorType == Patrol"
            var routeProp = behaviorDef.Properties.FirstOrDefault(p => p.Key == "patrolRoute");
            if (routeProp != null)
            {
                Assert.IsTrue(VisibleWhenEvaluator.Evaluate(routeProp.VisibleWhen, data.Properties),
                    "behaviorType == Patrol 时，patrolRoute 应该可见");
            }

            // 改为 Guard
            data.Properties.Set("behaviorType", "Guard");
            var radiusProp = behaviorDef.Properties.FirstOrDefault(p => p.Key == "guardRadius");
            if (radiusProp != null)
            {
                Assert.IsTrue(VisibleWhenEvaluator.Evaluate(radiusProp.VisibleWhen, data.Properties),
                    "behaviorType == Guard 时，guardRadius 应该可见");
            }
        }

        /// <summary>验证 Registry 分类正确：Flow 至少 5 个，Spawn 至少 2 个</summary>
        [Test]
        public void FullFlow_Registry_CategoriesAreCorrect()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            var categories = registry.GetCategories();
            CollectionAssert.Contains((System.Collections.ICollection)categories, "Flow");
            CollectionAssert.Contains((System.Collections.ICollection)categories, "Spawn");

            // Flow 至少 5 个，Spawn 至少 2 个
            Assert.GreaterOrEqual(registry.GetByCategory("Flow").Count, 5);
            Assert.GreaterOrEqual(registry.GetByCategory("Spawn").Count, 2);
        }
    }
}
