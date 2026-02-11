#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests
{
    /// <summary>
    /// Step 2: ActionRegistry 测试——验证行动注册表的注册、查找、分类、自动发现等功能。
    /// <para>
    /// 测试范围：
    /// - 注册后可通过 TypeId 查找
    /// - 重复 TypeId 抛异常、空 TypeId 抛异常
    /// - Get 未找到抛异常、TryGet 未找到返回 false
    /// - 按 Category 分组查询
    /// - AutoDiscover 自动发现所有 Flow/Combat 行动
    /// - AutoDiscover 幂等性（多次调用不重复注册）
    /// </para>
    /// </summary>
    public class ActionRegistryTests
    {
        /// <summary>注册后可通过 TypeId 查找</summary>
        [Test]
        public void Register_CanRetrieveByTypeId()
        {
            var registry = new ActionRegistry();
            var def = new ActionDefinition { TypeId = "Test.Action", Category = "Test" };
            registry.Register(def);

            Assert.IsTrue(registry.TryGet("Test.Action", out var result));
            Assert.AreEqual("Test.Action", result.TypeId);
        }

        /// <summary>重复 TypeId 应抛出 InvalidOperationException</summary>
        [Test]
        public void Register_DuplicateTypeId_Throws()
        {
            var registry = new ActionRegistry();
            registry.Register(new ActionDefinition { TypeId = "Test.A", Category = "Test" });

            Assert.Throws<System.InvalidOperationException>(() =>
                registry.Register(new ActionDefinition { TypeId = "Test.A", Category = "Test" }));
        }

        /// <summary>空 TypeId 应抛出 ArgumentException</summary>
        [Test]
        public void Register_EmptyTypeId_Throws()
        {
            var registry = new ActionRegistry();

            Assert.Throws<System.ArgumentException>(() =>
                registry.Register(new ActionDefinition { TypeId = "", Category = "Test" }));
        }

        /// <summary>Get 未找到应抛出 KeyNotFoundException</summary>
        [Test]
        public void Get_NotFound_Throws()
        {
            var registry = new ActionRegistry();

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                registry.Get("NonExistent"));
        }

        [Test]
        public void TryGet_NotFound_ReturnsFalse()
        {
            var registry = new ActionRegistry();

            Assert.IsFalse(registry.TryGet("NonExistent", out _));
        }

        [Test]
        public void GetByCategory_ReturnsCorrectGroup()
        {
            var registry = new ActionRegistry();
            registry.Register(new ActionDefinition { TypeId = "A.1", Category = "A" });
            registry.Register(new ActionDefinition { TypeId = "A.2", Category = "A" });
            registry.Register(new ActionDefinition { TypeId = "B.1", Category = "B" });

            var groupA = registry.GetByCategory("A");
            Assert.AreEqual(2, groupA.Count);

            var groupB = registry.GetByCategory("B");
            Assert.AreEqual(1, groupB.Count);
        }

        [Test]
        public void GetByCategory_Empty_ReturnsEmptyList()
        {
            var registry = new ActionRegistry();
            var result = registry.GetByCategory("NonExistent");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetAll_ReturnsAllRegistered()
        {
            var registry = new ActionRegistry();
            registry.Register(new ActionDefinition { TypeId = "A.1", Category = "A" });
            registry.Register(new ActionDefinition { TypeId = "B.1", Category = "B" });

            Assert.AreEqual(2, registry.GetAll().Count);
        }

        [Test]
        public void GetCategories_ReturnsAllCategories()
        {
            var registry = new ActionRegistry();
            registry.Register(new ActionDefinition { TypeId = "A.1", Category = "Alpha" });
            registry.Register(new ActionDefinition { TypeId = "B.1", Category = "Beta" });

            var categories = registry.GetCategories();
            Assert.AreEqual(2, categories.Count);
            Assert.Contains("Alpha", (System.Collections.ICollection)categories);
            Assert.Contains("Beta", (System.Collections.ICollection)categories);
        }

        /// <summary>验证 AutoDiscover 能自动发现所有 Flow 和 Combat 域的行动（至少 7 个）</summary>
        [Test]
        public void AutoDiscover_FindsFlowAndCombatActions()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();

            // Flow 域
            Assert.IsTrue(registry.TryGet("Flow.Start", out _), "Flow.Start 未发现");
            Assert.IsTrue(registry.TryGet("Flow.End", out _), "Flow.End 未发现");
            Assert.IsTrue(registry.TryGet("Flow.Delay", out _), "Flow.Delay 未发现");
            Assert.IsTrue(registry.TryGet("Flow.Branch", out _), "Flow.Branch 未发现");
            Assert.IsTrue(registry.TryGet("Flow.Join", out _), "Flow.Join 未发现");

            // Combat 域
            Assert.IsTrue(registry.TryGet("Combat.Spawn", out _), "Combat.Spawn 未发现");
            Assert.IsTrue(registry.TryGet("Combat.PlacePreset", out _), "Combat.PlacePreset 未发现");

            Assert.GreaterOrEqual(registry.GetAll().Count, 7);
        }

        /// <summary>验证 AutoDiscover 是幂等的——多次调用不会重复注册或抛异常</summary>
        [Test]
        public void AutoDiscover_IsIdempotent()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover();
            int countAfterFirst = registry.GetAll().Count;

            // 再调用一次不应该抛异常，也不应该重复注册
            registry.AutoDiscover();
            Assert.AreEqual(countAfterFirst, registry.GetAll().Count);
        }
    }
}
