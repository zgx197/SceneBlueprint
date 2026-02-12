#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Utils
{
    /// <summary>
    /// 断言扩展方法——提供更丰富、更具表达力的断言功能。
    /// <para>
    /// 扩展 NUnit 的基础断言，为 SceneBlueprint 特定对象提供专门的验证方法。
    /// 包含更清晰的错误消息和边界条件检查。
    /// </para>
    /// </summary>
    public static class AssertionExtensions
    {
        // ─── PropertyBag 专用断言 ───

        /// <summary>断言 PropertyBag 包含指定的键值对</summary>
        public static void ShouldContain(this PropertyBag bag, string key, object expectedValue, string? message = null)
        {
            Assert.IsTrue(bag.Has(key), message ?? $"PropertyBag 应该包含键 '{key}'");
            
            var actualValue = bag.Get<object>(key);
            if (expectedValue is float expectedFloat && actualValue is float actualFloat)
            {
                Assert.AreEqual(expectedFloat, actualFloat, 0.001f, 
                    message ?? $"键 '{key}' 的值不匹配。期望: {expectedValue}，实际: {actualValue}");
            }
            else
            {
                Assert.AreEqual(expectedValue, actualValue, 
                    message ?? $"键 '{key}' 的值不匹配。期望: {expectedValue}，实际: {actualValue}");
            }
        }

        /// <summary>断言 PropertyBag 包含指定的键</summary>
        public static void ShouldContainKey(this PropertyBag bag, string key, string? message = null)
        {
            Assert.IsTrue(bag.Has(key), message ?? $"PropertyBag 应该包含键 '{key}'");
        }

        /// <summary>断言 PropertyBag 不包含指定的键</summary>
        public static void ShouldNotContainKey(this PropertyBag bag, string key, string? message = null)
        {
            Assert.IsFalse(bag.Has(key), message ?? $"PropertyBag 不应该包含键 '{key}'");
        }

        /// <summary>断言 PropertyBag 为空</summary>
        public static void ShouldBeEmpty(this PropertyBag bag, string? message = null)
        {
            Assert.AreEqual(0, bag.Count, message ?? "PropertyBag 应该为空");
        }

        /// <summary>断言 PropertyBag 包含指定数量的项</summary>
        public static void ShouldHaveCount(this PropertyBag bag, int expectedCount, string? message = null)
        {
            Assert.AreEqual(expectedCount, bag.Count, 
                message ?? $"PropertyBag 应该包含 {expectedCount} 个项，实际包含 {bag.Count} 个");
        }

        // ─── ActionDefinition 专用断言 ───

        /// <summary>断言 ActionDefinition 具有指定的基本字段</summary>
        public static void ShouldHaveBasicFields(this ActionDefinition definition, 
            string expectedTypeId, 
            string expectedDisplayName, 
            string expectedCategory,
            string? message = null)
        {
            Assert.AreEqual(expectedTypeId, definition.TypeId, 
                message ?? $"TypeId 不匹配。期望: {expectedTypeId}，实际: {definition.TypeId}");
            Assert.AreEqual(expectedDisplayName, definition.DisplayName, 
                message ?? $"DisplayName 不匹配。期望: {expectedDisplayName}，实际: {definition.DisplayName}");
            Assert.AreEqual(expectedCategory, definition.Category, 
                message ?? $"Category 不匹配。期望: {expectedCategory}，实际: {definition.Category}");
        }

        /// <summary>断言 ActionDefinition 包含指定的端口</summary>
        public static void ShouldHavePort(this ActionDefinition definition, 
            string portId, 
            PortDirection expectedDirection, 
            string? message = null)
        {
            var port = definition.Ports.FirstOrDefault(p => p.Id == portId);
            Assert.IsNotNull(port, message ?? $"ActionDefinition 应该包含端口 '{portId}'");
            Assert.AreEqual(expectedDirection, port!.Direction, 
                message ?? $"端口 '{portId}' 的方向不匹配。期望: {expectedDirection}，实际: {port.Direction}");
        }

        /// <summary>断言 ActionDefinition 包含指定的属性</summary>
        public static void ShouldHaveProperty(this ActionDefinition definition, 
            string propertyKey, 
            PropertyType expectedType, 
            string? message = null)
        {
            var property = definition.Properties.FirstOrDefault(p => p.Key == propertyKey);
            Assert.IsNotNull(property, message ?? $"ActionDefinition 应该包含属性 '{propertyKey}'");
            Assert.AreEqual(expectedType, property!.Type, 
                message ?? $"属性 '{propertyKey}' 的类型不匹配。期望: {expectedType}，实际: {property.Type}");
        }

        /// <summary>断言 ActionDefinition 的端口数量</summary>
        public static void ShouldHavePortCount(this ActionDefinition definition, int expectedCount, string? message = null)
        {
            Assert.AreEqual(expectedCount, definition.Ports.Length, 
                message ?? $"ActionDefinition 应该有 {expectedCount} 个端口，实际有 {definition.Ports.Length} 个");
        }

        /// <summary>断言 ActionDefinition 的属性数量</summary>
        public static void ShouldHavePropertyCount(this ActionDefinition definition, int expectedCount, string? message = null)
        {
            Assert.AreEqual(expectedCount, definition.Properties.Length, 
                message ?? $"ActionDefinition 应该有 {expectedCount} 个属性，实际有 {definition.Properties.Length} 个");
        }

        // ─── ActionRegistry 专用断言 ───

        /// <summary>断言 ActionRegistry 包含指定的行动</summary>
        public static void ShouldContainAction(this ActionRegistry registry, string typeId, string? message = null)
        {
            Assert.IsTrue(registry.TryGet(typeId, out _), 
                message ?? $"ActionRegistry 应该包含行动 '{typeId}'");
        }

        /// <summary>断言 ActionRegistry 不包含指定的行动</summary>
        public static void ShouldNotContainAction(this ActionRegistry registry, string typeId, string? message = null)
        {
            Assert.IsFalse(registry.TryGet(typeId, out _), 
                message ?? $"ActionRegistry 不应该包含行动 '{typeId}'");
        }

        /// <summary>断言 ActionRegistry 指定分类下的行动数量</summary>
        public static void ShouldHaveCategoryCount(this ActionRegistry registry, 
            string category, 
            int expectedCount, 
            string? message = null)
        {
            var actions = registry.GetByCategory(category);
            Assert.AreEqual(expectedCount, actions.Count, 
                message ?? $"分类 '{category}' 应该包含 {expectedCount} 个行动，实际包含 {actions.Count} 个");
        }

        /// <summary>断言 ActionRegistry 的总行动数量</summary>
        public static void ShouldHaveTotalCount(this ActionRegistry registry, int expectedCount, string? message = null)
        {
            var totalCount = registry.GetAll().Count;
            Assert.AreEqual(expectedCount, totalCount, 
                message ?? $"ActionRegistry 应该包含 {expectedCount} 个行动，实际包含 {totalCount} 个");
        }

        // ─── PropertyDefinition 专用断言 ───

        /// <summary>断言 PropertyDefinition 具有指定的基本信息</summary>
        public static void ShouldHaveBasicInfo(this PropertyDefinition property,
            string expectedKey,
            string expectedDisplayName,
            PropertyType expectedType,
            string? message = null)
        {
            Assert.AreEqual(expectedKey, property.Key, 
                message ?? $"属性键不匹配。期望: {expectedKey}，实际: {property.Key}");
            Assert.AreEqual(expectedDisplayName, property.DisplayName, 
                message ?? $"属性显示名不匹配。期望: {expectedDisplayName}，实际: {property.DisplayName}");
            Assert.AreEqual(expectedType, property.Type, 
                message ?? $"属性类型不匹配。期望: {expectedType}，实际: {property.Type}");
        }

        /// <summary>断言 PropertyDefinition 具有指定的默认值</summary>
        public static void ShouldHaveDefaultValue(this PropertyDefinition property, object expectedValue, string? message = null)
        {
            Assert.AreEqual(expectedValue, property.DefaultValue, 
                message ?? $"属性 '{property.Key}' 的默认值不匹配。期望: {expectedValue}，实际: {property.DefaultValue}");
        }

        /// <summary>断言 PropertyDefinition 具有指定的 VisibleWhen 条件</summary>
        public static void ShouldHaveVisibleWhen(this PropertyDefinition property, string expectedCondition, string? message = null)
        {
            Assert.AreEqual(expectedCondition, property.VisibleWhen, 
                message ?? $"属性 '{property.Key}' 的可见条件不匹配。期望: {expectedCondition}，实际: {property.VisibleWhen}");
        }

        // ─── 集合断言扩展 ───

        /// <summary>断言集合包含指定数量的元素</summary>
        public static void ShouldHaveCount<T>(this ICollection<T> collection, int expectedCount, string? message = null)
        {
            Assert.AreEqual(expectedCount, collection.Count, 
                message ?? $"集合应该包含 {expectedCount} 个元素，实际包含 {collection.Count} 个");
        }

        /// <summary>断言集合为空</summary>
        public static void ShouldBeEmpty<T>(this ICollection<T> collection, string? message = null)
        {
            Assert.AreEqual(0, collection.Count, message ?? "集合应该为空");
        }

        /// <summary>断言集合包含指定元素</summary>
        public static void ShouldContain<T>(this ICollection<T> collection, T expectedItem, string? message = null)
        {
            Assert.IsTrue(collection.Contains(expectedItem), 
                message ?? $"集合应该包含元素 {expectedItem}");
        }

        /// <summary>断言集合不包含指定元素</summary>
        public static void ShouldNotContain<T>(this ICollection<T> collection, T unexpectedItem, string? message = null)
        {
            Assert.IsFalse(collection.Contains(unexpectedItem), 
                message ?? $"集合不应该包含元素 {unexpectedItem}");
        }

        // ─── 字符串断言扩展 ───

        /// <summary>断言字符串不为空或空白</summary>
        public static void ShouldNotBeNullOrEmpty(this string? value, string? message = null)
        {
            Assert.IsFalse(string.IsNullOrEmpty(value), message ?? "字符串不应该为 null 或空");
        }

        /// <summary>断言字符串不为空白</summary>
        public static void ShouldNotBeNullOrWhiteSpace(this string? value, string? message = null)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), message ?? "字符串不应该为 null 或空白");
        }

        // ─── 异常断言扩展 ───

        /// <summary>断言操作抛出指定类型的异常并验证消息</summary>
        public static void ShouldThrow<TException>(this Action action, string? expectedMessage = null, string? assertMessage = null)
            where TException : Exception
        {
            var exception = Assert.Throws<TException>(() => action(), 
                assertMessage ?? $"应该抛出 {typeof(TException).Name} 异常");

            if (expectedMessage != null)
            {
                Assert.That(exception.Message, Does.Contain(expectedMessage),
                    $"异常消息应该包含 '{expectedMessage}'，实际消息: '{exception.Message}'");
            }
        }

        /// <summary>断言操作不抛出异常</summary>
        public static void ShouldNotThrow(this Action action, string? message = null)
        {
            Assert.DoesNotThrow(() => action(), message ?? "操作不应该抛出异常");
        }

        // ─── JSON 序列化断言 ───

        /// <summary>断言 JSON 往返序列化后数据一致</summary>
        public static void ShouldSerializeCorrectly(this PropertyBag originalBag, string? message = null)
        {
            // 序列化
            var json = PropertyBagSerializer.ToJson(originalBag);
            Assert.IsNotNull(json, "JSON 序列化结果不应该为 null");
            Assert.IsTrue(json.Length > 0, "JSON 序列化结果不应该为空字符串");

            // 反序列化
            var restoredBag = PropertyBagSerializer.FromJson(json);
            Assert.IsNotNull(restoredBag, "JSON 反序列化结果不应该为 null");

            // 验证数据一致性
            Assert.AreEqual(originalBag.Count, restoredBag.Count, 
                message ?? "序列化后的 PropertyBag 项数不一致");

            // 这里可以添加更详细的字段比较逻辑
        }

        // ─── 性能断言 ───

        /// <summary>断言操作在指定时间内完成</summary>
        public static void ShouldCompleteWithin(this Action action, TimeSpan maxDuration, string? message = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            Assert.LessOrEqual(stopwatch.Elapsed, maxDuration,
                message ?? $"操作应该在 {maxDuration.TotalMilliseconds}ms 内完成，实际耗时 {stopwatch.Elapsed.TotalMilliseconds}ms");
        }

        /// <summary>断言操作在指定毫秒内完成</summary>
        public static void ShouldCompleteWithin(this Action action, int maxMilliseconds, string? message = null)
        {
            action.ShouldCompleteWithin(TimeSpan.FromMilliseconds(maxMilliseconds), message);
        }
    }
}
