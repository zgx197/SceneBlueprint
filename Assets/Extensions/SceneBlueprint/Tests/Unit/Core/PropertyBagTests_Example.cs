#nullable enable
using System;
using NUnit.Framework;
using SceneBlueprint.Core;
using SceneBlueprint.Tests.Utils;

namespace SceneBlueprint.Tests.Unit.Core
{
    /// <summary>
    /// PropertyBag 示例测试——展示测试编写的最佳实践。
    /// <para>
    /// 这个文件展示了如何使用新的测试框架编写高质量的测试：
    /// - 使用 TestDataBuilder 创建测试数据
    /// - 使用 AssertionExtensions 进行更表达性的断言
    /// - 遵循 AAA 模式和命名约定
    /// - 覆盖边界条件和异常情况
    /// </para>
    /// </summary>
    public class PropertyBagTests_Example
    {
        // ─── 基础功能测试 ───

        /// <summary>验证 PropertyBag 基本的设置和获取功能</summary>
        [Test]
        public void Set_ValidKeyValue_CanRetrieveValue()
        {
            // Arrange - 使用工具类创建测试数据
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act
            bag.Set("testKey", "testValue");
            
            // Assert - 使用扩展断言方法
            bag.ShouldContainKey("testKey");
            bag.ShouldContain("testKey", "testValue");
        }

        /// <summary>验证 PropertyBag 支持多种数据类型</summary>
        [Test]
        public void Set_MultipleTypes_AllTypesStoredCorrectly()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act - 设置各种类型的数据
            bag.Set("stringValue", "hello");
            bag.Set("intValue", 42);
            bag.Set("floatValue", 3.14f);
            bag.Set("boolValue", true);
            
            // Assert - 验证所有类型都正确存储
            bag.ShouldHaveCount(4);
            bag.ShouldContain("stringValue", "hello");
            bag.ShouldContain("intValue", 42);
            bag.ShouldContain("floatValue", 3.14f);
            bag.ShouldContain("boolValue", true);
        }

        /// <summary>验证覆盖已存在的键时行为正确</summary>
        [Test]
        public void Set_ExistingKey_OverwritesValue()
        {
            // Arrange
            var bag = TestDataBuilder.CreatePropertyBag(("key", "oldValue"));
            
            // Act
            bag.Set("key", "newValue");
            
            // Assert
            bag.ShouldHaveCount(1, "覆盖操作不应改变键的数量");
            bag.ShouldContain("key", "newValue", "应该包含新值");
        }

        // ─── 边界条件测试 ───

        /// <summary>验证获取不存在的键时返回适当的默认值</summary>
        [Test]
        public void Get_NonExistentKey_ReturnsTypeDefault()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act & Assert
            Assert.AreEqual(null, bag.Get<string>("missing"), "字符串类型应返回 null");
            Assert.AreEqual(0, bag.Get<int>("missing"), "整数类型应返回 0");
            Assert.AreEqual(0f, bag.Get<float>("missing"), "浮点数类型应返回 0.0");
            Assert.AreEqual(false, bag.Get<bool>("missing"), "布尔类型应返回 false");
        }

        /// <summary>验证使用用户指定默认值的获取功能</summary>
        [Test]
        public void Get_NonExistentKeyWithDefault_ReturnsProvidedDefault()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act & Assert
            Assert.AreEqual("fallback", bag.Get("missing", "fallback"));
            Assert.AreEqual(99, bag.Get("missing", 99));
            Assert.AreEqual(2.5f, bag.Get("missing", 2.5f), 0.001f);
        }

        /// <summary>验证数值类型的隐式转换</summary>
        [Test]
        public void Get_NumericTypeConversion_WorksCorrectly()
        {
            // Arrange
            var bag = TestDataBuilder.CreatePropertyBag(("intValue", 42));
            
            // Act - 以不同类型读取同一个值
            var asInt = bag.Get<int>("intValue");
            var asFloat = bag.Get<float>("intValue");
            
            // Assert
            Assert.AreEqual(42, asInt);
            Assert.AreEqual(42f, asFloat, 0.001f, "整数应该能够转换为浮点数");
        }

        // ─── 异常情况测试 ───

        /// <summary>验证空键名的处理</summary>
        [Test]
        public void Set_EmptyKey_HandlesProperly()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act & Assert - 根据实际实现决定是否应该抛出异常
            // 这里假设空键是允许的
            Assert.DoesNotThrow(() => bag.Set("", "value"));
            bag.ShouldContain("", "value");
        }

        /// <summary>验证 null 值的处理</summary>
        [Test]
        public void Set_NullValue_HandlesProperly()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act
            bag.Set("nullKey", null!);
            
            // Assert
            bag.ShouldContainKey("nullKey");
            Assert.AreEqual(null, bag.Get<object>("nullKey"));
        }

        // ─── 集合操作测试 ───

        /// <summary>验证移除功能</summary>
        [Test]
        public void Remove_ExistingKey_RemovesAndReturnsTrue()
        {
            // Arrange
            var bag = TestDataBuilder.CreatePropertyBag(("key1", "value1"), ("key2", "value2"));
            
            // Act
            bool result = bag.Remove("key1");
            
            // Assert
            Assert.IsTrue(result, "移除存在的键应返回 true");
            bag.ShouldHaveCount(1, "移除后应该只剩一个键");
            bag.ShouldNotContainKey("key1");
            bag.ShouldContainKey("key2");
        }

        /// <summary>验证移除不存在的键</summary>
        [Test]
        public void Remove_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act
            bool result = bag.Remove("nonExistent");
            
            // Assert
            Assert.IsFalse(result, "移除不存在的键应返回 false");
        }

        /// <summary>验证清空功能</summary>
        [Test]
        public void Clear_NonEmptyBag_RemovesAllItems()
        {
            // Arrange
            var bag = TestDataBuilder.CreateMixedTypePropertyBag();
            Assert.Greater(bag.Count, 0, "测试数据应该非空");
            
            // Act
            bag.Clear();
            
            // Assert
            bag.ShouldBeEmpty();
        }

        // ─── 序列化测试 ───

        /// <summary>验证 JSON 序列化往返的正确性</summary>
        [Test]
        public void JsonSerialization_MixedTypeData_RoundTripCorrect()
        {
            // Arrange
            var originalBag = TestDataBuilder.CreateMixedTypePropertyBag();
            
            // Act & Assert - 使用扩展方法验证序列化
            originalBag.ShouldSerializeCorrectly();
        }

        /// <summary>验证空 PropertyBag 的序列化</summary>
        [Test]
        public void JsonSerialization_EmptyBag_ProducesValidJson()
        {
            // Arrange
            var emptyBag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act
            string json = PropertyBagSerializer.ToJson(emptyBag);
            var restoredBag = PropertyBagSerializer.FromJson(json);
            
            // Assert
            Assert.IsNotNull(json, "空包的 JSON 不应为 null");
            restoredBag.ShouldBeEmpty();
        }

        // ─── 性能测试 ───

        /// <summary>验证大量数据的处理性能</summary>
        [Test]
        public void Performance_LargeDataSet_CompletesWithinTimeLimit()
        {
            // Arrange
            const int itemCount = 1000;
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act - 测试大量数据的插入性能
            Action massInsert = () =>
            {
                for (int i = 0; i < itemCount; i++)
                {
                    bag.Set($"key{i}", $"value{i}");
                }
            };
            
            // Assert - 使用性能断言扩展
            massInsert.ShouldCompleteWithin(1000, "插入 1000 项应在 1 秒内完成");
            bag.ShouldHaveCount(itemCount);
        }

        /// <summary>验证序列化大数据集的性能</summary>
        [Test]
        public void Performance_LargeDataSetSerialization_CompletesWithinTimeLimit()
        {
            // Arrange
            var largeBag = TestDataBuilder.CreateLargePropertyBag(100);
            
            // Act & Assert
            Action serializationTest = () =>
            {
                string json = PropertyBagSerializer.ToJson(largeBag);
                var restored = PropertyBagSerializer.FromJson(json);
                Assert.AreEqual(largeBag.Count, restored.Count);
            };
            
            serializationTest.ShouldCompleteWithin(500, "大数据集序列化应在 0.5 秒内完成");
        }

        // ─── 集成场景测试 ───

        /// <summary>模拟真实使用场景——ActionNodeData 属性存储</summary>
        [Test]
        public void RealWorldScenario_ActionNodeDataProperties_WorksCorrectly()
        {
            // Arrange - 模拟创建一个 Spawn Action 的属性
            var bag = TestDataBuilder.CreateEmptyPropertyBag();
            
            // Act - 模拟编辑器中用户修改属性的操作
            bag.Set("template", "elite_monster_group");
            bag.Set("tempoType", "Interval");
            bag.Set("interval", 2.5f);
            bag.Set("totalWaves", 3);
            bag.Set("monstersPerWave", 8);
            bag.Set("maxAlive", 15);
            bag.Set("spawnArea", "spawn_area_01");
            
            // Assert - 验证所有属性都正确存储
            bag.ShouldHaveCount(7);
            bag.ShouldContain("template", "elite_monster_group");
            bag.ShouldContain("tempoType", "Interval");
            bag.ShouldContain("interval", 2.5f);
            bag.ShouldContain("totalWaves", 3);
            bag.ShouldContain("monstersPerWave", 8);
            bag.ShouldContain("maxAlive", 15);
            bag.ShouldContain("spawnArea", "spawn_area_01");
            
            // 额外验证：确保序列化后仍然正确
            bag.ShouldSerializeCorrectly();
        }
    }
}
