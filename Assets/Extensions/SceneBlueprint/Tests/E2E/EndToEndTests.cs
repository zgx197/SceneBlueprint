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
            var spawnDef = registry.Get("Combat.Spawn");
            var spawnData = ActionNodeData.CreateFromDefinition(spawnDef);

            // 3. 修改属性
            spawnData.Properties.Set("monstersPerWave", 8);
            spawnData.Properties.Set("template", "elite_group_01");
            spawnData.Properties.Set("tempoType", "Interval");
            spawnData.Properties.Set("interval", 3.5f);

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
        /// 验证 VisibleWhen 与实际 Spawn 数据的配合：
        /// - tempoType == Interval 时 interval 可见
        /// - tempoType == Instant 时 interval 隐藏、totalWaves 隐藏
        /// - tempoType == Burst 时 totalWaves 可见
        /// </summary>
        [Test]
        public void FullFlow_VisibleWhen_WorksWithActualSpawnData()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            var spawnDef = registry.Get("Combat.Spawn");
            var data = ActionNodeData.CreateFromDefinition(spawnDef);

            // tempoType 默认是 "Interval"
            data.Properties.Set("tempoType", "Interval");

            // interval 属性的 VisibleWhen = "tempoType == Interval"
            var intervalProp = spawnDef.Properties.First(p => p.Key == "interval");
            Assert.IsTrue(VisibleWhenEvaluator.Evaluate(intervalProp.VisibleWhen, data.Properties),
                "tempoType == Interval 时，interval 应该可见");

            // 改为 Instant
            data.Properties.Set("tempoType", "Instant");
            Assert.IsFalse(VisibleWhenEvaluator.Evaluate(intervalProp.VisibleWhen, data.Properties),
                "tempoType == Instant 时，interval 应该隐藏");

            // totalWaves 的 VisibleWhen = "tempoType != Instant"
            var wavesProp = spawnDef.Properties.First(p => p.Key == "totalWaves");
            Assert.IsFalse(VisibleWhenEvaluator.Evaluate(wavesProp.VisibleWhen, data.Properties),
                "tempoType == Instant 时，totalWaves 应该隐藏");

            data.Properties.Set("tempoType", "Burst");
            Assert.IsTrue(VisibleWhenEvaluator.Evaluate(wavesProp.VisibleWhen, data.Properties),
                "tempoType == Burst 时，totalWaves 应该可见");
        }

        /// <summary>验证 Registry 分类正确：Flow 至少 5 个，Combat 至少 2 个</summary>
        [Test]
        public void FullFlow_Registry_CategoriesAreCorrect()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            var categories = registry.GetCategories();
            CollectionAssert.Contains((System.Collections.ICollection)categories, "Flow");
            CollectionAssert.Contains((System.Collections.ICollection)categories, "Combat");

            // Flow 至少 5 个，Combat 至少 2 个
            Assert.GreaterOrEqual(registry.GetByCategory("Flow").Count, 5);
            Assert.GreaterOrEqual(registry.GetByCategory("Combat").Count, 2);
        }
    }
}
