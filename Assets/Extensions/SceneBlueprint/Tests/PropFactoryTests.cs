#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests
{
    /// <summary>
    /// Step 4: Prop 便捷工厂测试——验证每种属性类型的工厂方法能正确生成 PropertyDefinition。
    /// <para>
    /// 测试范围：
    /// - Float/Int/Bool/String 基本类型工厂
    /// - Enum 泛型版（自动提取选项）和字符串数组版
    /// - AssetRef、SceneBinding、Tag 特殊类型
    /// - VisibleWhen 和 Order 的保留性
    /// </para>
    /// </summary>
    public class PropFactoryTests
    {
        /// <summary>验证 Prop.Float 设置所有字段（键名、显示名、类型、默认值、Min/Max、分组）</summary>
        [Test]
        public void Float_SetsAllFields()
        {
            var p = Prop.Float("interval", "间隔", defaultValue: 2f, min: 0.1f, max: 30f, category: "节奏");

            Assert.AreEqual("interval", p.Key);
            Assert.AreEqual("间隔", p.DisplayName);
            Assert.AreEqual(PropertyType.Float, p.Type);
            Assert.AreEqual(2f, p.DefaultValue);
            Assert.AreEqual(0.1f, p.Min);
            Assert.AreEqual(30f, p.Max);
            Assert.AreEqual("节奏", p.Category);
        }

        /// <summary>验证 Prop.Int 的 min/max 从 int 正确转为 float 存储</summary>
        [Test]
        public void Int_SetsMinMax()
        {
            var p = Prop.Int("count", "数量", defaultValue: 5, min: 1, max: 20);

            Assert.AreEqual(PropertyType.Int, p.Type);
            Assert.AreEqual(5, p.DefaultValue);
            Assert.AreEqual(1f, p.Min);
            Assert.AreEqual(20f, p.Max);
        }

        [Test]
        public void Bool_SetsDefault()
        {
            var p = Prop.Bool("active", "激活", defaultValue: true);

            Assert.AreEqual(PropertyType.Bool, p.Type);
            Assert.AreEqual(true, p.DefaultValue);
        }

        [Test]
        public void String_SetsDefault()
        {
            var p = Prop.String("name", "名称", defaultValue: "hello");

            Assert.AreEqual(PropertyType.String, p.Type);
            Assert.AreEqual("hello", p.DefaultValue);
        }

        /// <summary>验证 Prop.Enum&lt;T&gt; 泛型版能自动从 C# 枚举提取所有选项</summary>
        [Test]
        public void EnumGeneric_ExtractsOptions()
        {
            var p = Prop.Enum<ActionDuration>("duration", "持续类型");

            Assert.AreEqual(PropertyType.Enum, p.Type);
            Assert.IsNotNull(p.EnumOptions);
            Assert.AreEqual(3, p.EnumOptions!.Length);
            CollectionAssert.Contains(p.EnumOptions, "Instant");
            CollectionAssert.Contains(p.EnumOptions, "Duration");
            CollectionAssert.Contains(p.EnumOptions, "Passive");
        }

        /// <summary>验证 Prop.Enum 字符串数组版能正确设置选项和默认值</summary>
        [Test]
        public void EnumStringArray_SetsOptions()
        {
            var p = Prop.Enum("mode", "模式", new[] { "A", "B", "C" }, defaultValue: "B");

            Assert.AreEqual(PropertyType.Enum, p.Type);
            Assert.AreEqual("B", p.DefaultValue);
            Assert.AreEqual(3, p.EnumOptions!.Length);
        }

        /// <summary>验证 Prop.AssetRef 能正确设置资产类型过滤名</summary>
        [Test]
        public void AssetRef_SetsTypeName()
        {
            var p = Prop.AssetRef("template", "模板", assetFilterTypeName: "MonsterGroupTemplate");

            Assert.AreEqual(PropertyType.AssetRef, p.Type);
            Assert.AreEqual("MonsterGroupTemplate", p.AssetFilterTypeName);
            Assert.AreEqual("", p.DefaultValue);
        }

        /// <summary>验证 Prop.SceneBinding 能正确设置绑定类型</summary>
        [Test]
        public void SceneBinding_SetsBindingType()
        {
            var p = Prop.SceneBinding("area", "区域", BindingType.Area);

            Assert.AreEqual(PropertyType.SceneBinding, p.Type);
            Assert.AreEqual(BindingType.Area, p.SceneBindingType);
        }

        [Test]
        public void Tag_SetsType()
        {
            var p = Prop.Tag("tag", "标签");

            Assert.AreEqual(PropertyType.Tag, p.Type);
            Assert.AreEqual("", p.DefaultValue);
        }

        /// <summary>验证 VisibleWhen 表达式能被正确保留</summary>
        [Test]
        public void VisibleWhen_IsPreserved()
        {
            var p = Prop.Float("interval", "间隔", visibleWhen: "mode == Interval");

            Assert.AreEqual("mode == Interval", p.VisibleWhen);
        }

        /// <summary>验证 Order 值能被正确保留</summary>
        [Test]
        public void Order_IsPreserved()
        {
            var p = Prop.Int("count", "数量", order: 5);

            Assert.AreEqual(5, p.Order);
        }
    }
}
