#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Unit.Core
{
    /// <summary>
    /// Step 3: PropertyBag + ActionNodeData + 序列化测试。
    /// <para>
    /// 测试范围：
    /// - PropertyBag 的 Set/Get、默认值、移除、计数、覆盖、清空
    /// - 数值类型隐式转换（int → float）
    /// - ActionNodeData.CreateFromDefinition 默认值填充
    /// - PropertyBagSerializer JSON 序列化往返（多类型、空值、特殊字符、中文、负数）
    /// </para>
    /// </summary>
    public class PropertyBagTests
    {
        /// <summary>验证 Set/Get 支持所有基本类型（float/int/bool/string）</summary>
        [Test]
        public void SetGet_AllTypes()
        {
            var bag = new PropertyBag();
            bag.Set("f", 3.14f);
            bag.Set("i", 42);
            bag.Set("b", true);
            bag.Set("s", "hello");

            Assert.AreEqual(3.14f, bag.Get<float>("f"), 0.001f);
            Assert.AreEqual(42, bag.Get<int>("i"));
            Assert.AreEqual(true, bag.Get<bool>("b"));
            Assert.AreEqual("hello", bag.Get<string>("s"));
        }

        /// <summary>验证读取不存在的键时返回类型默认值</summary>
        [Test]
        public void GetMissing_ReturnsDefault()
        {
            var bag = new PropertyBag();

            Assert.AreEqual(0f, bag.Get<float>("missing"));
            Assert.AreEqual(0, bag.Get<int>("missing"));
            Assert.AreEqual(false, bag.Get<bool>("missing"));
            Assert.AreEqual(null, bag.Get<string>("missing"));
        }

        /// <summary>验证读取不存在的键时返回用户指定的默认值</summary>
        [Test]
        public void GetMissing_ReturnsProvidedDefault()
        {
            var bag = new PropertyBag();

            Assert.AreEqual("fallback", bag.Get<string>("missing", "fallback"));
            Assert.AreEqual(99, bag.Get<int>("missing", 99));
        }

        [Test]
        public void Has_ReturnsTrueForExisting()
        {
            var bag = new PropertyBag();
            bag.Set("key", 1);

            Assert.IsTrue(bag.Has("key"));
            Assert.IsFalse(bag.Has("other"));
        }

        [Test]
        public void Remove_RemovesKey()
        {
            var bag = new PropertyBag();
            bag.Set("key", 1);

            Assert.IsTrue(bag.Remove("key"));
            Assert.IsFalse(bag.Has("key"));
            Assert.IsFalse(bag.Remove("key")); // 已移除
        }

        [Test]
        public void Count_IsAccurate()
        {
            var bag = new PropertyBag();
            Assert.AreEqual(0, bag.Count);

            bag.Set("a", 1);
            bag.Set("b", 2);
            Assert.AreEqual(2, bag.Count);

            bag.Remove("a");
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void Set_OverwritesExisting()
        {
            var bag = new PropertyBag();
            bag.Set("key", 1);
            bag.Set("key", 2);

            Assert.AreEqual(2, bag.Get<int>("key"));
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var bag = new PropertyBag();
            bag.Set("a", 1);
            bag.Set("b", 2);
            bag.Clear();

            Assert.AreEqual(0, bag.Count);
            Assert.IsFalse(bag.Has("a"));
        }

        /// <summary>验证数值类型隐式转换：int 存入，float 读取应能正确转换</summary>
        [Test]
        public void NumericTypeConversion_IntToFloat()
        {
            var bag = new PropertyBag();
            bag.Set("val", 42);

            // int 存入，float 读取应该可以转换
            Assert.AreEqual(42f, bag.Get<float>("val"), 0.001f);
        }

        // ─── ActionNodeData 测试：验证从定义创建节点数据时默认值的填充 ───

        /// <summary>验证 CreateFromDefinition 会将所有有默认值的属性填充到 PropertyBag</summary>
        [Test]
        public void ActionNodeData_CreateFromDefinition_AppliesDefaults()
        {
            var def = new ActionDefinition
            {
                TypeId = "Test.X",
                Properties = new[]
                {
                    Prop.Int("count", "数量", defaultValue: 5),
                    Prop.Float("speed", "速度", defaultValue: 1.5f),
                    Prop.String("name", "名称", defaultValue: "测试")
                }
            };
            var data = ActionNodeData.CreateFromDefinition(def);

            Assert.AreEqual("Test.X", data.ActionTypeId);
            Assert.AreEqual(5, data.Properties.Get<int>("count"));
            Assert.AreEqual(1.5f, data.Properties.Get<float>("speed"), 0.001f);
            Assert.AreEqual("测试", data.Properties.Get<string>("name"));
        }

        /// <summary>验证 DefaultValue 为 null 的属性不会被填充到 PropertyBag</summary>
        [Test]
        public void ActionNodeData_CreateFromDefinition_SkipsNullDefaults()
        {
            var def = new ActionDefinition
            {
                TypeId = "Test.Y",
                Properties = new[]
                {
                    new PropertyDefinition { Key = "noDefault", Type = PropertyType.String, DefaultValue = null }
                }
            };
            var data = ActionNodeData.CreateFromDefinition(def);

            Assert.IsFalse(data.Properties.Has("noDefault"));
        }

        // ─── PropertyBag JSON 序列化往返测试 ───

        /// <summary>验证所有基本类型的 JSON 序列化/反序列化往返一致性</summary>
        [Test]
        public void JsonRoundTrip_AllTypes()
        {
            var original = new PropertyBag();
            original.Set("name", "elite");
            original.Set("count", 5);
            original.Set("rate", 2.5f);
            original.Set("active", true);

            string json = PropertyBagSerializer.ToJson(original);
            var restored = PropertyBagSerializer.FromJson(json);

            Assert.AreEqual("elite", restored.Get<string>("name"));
            Assert.AreEqual(5, restored.Get<int>("count"));
            Assert.AreEqual(2.5f, restored.Get<float>("rate"), 0.001f);
            Assert.AreEqual(true, restored.Get<bool>("active"));
        }

        /// <summary>验证空 PropertyBag 的 JSON 往返</summary>
        [Test]
        public void JsonRoundTrip_EmptyBag()
        {
            var original = new PropertyBag();
            string json = PropertyBagSerializer.ToJson(original);
            var restored = PropertyBagSerializer.FromJson(json);

            Assert.AreEqual(0, restored.Count);
        }

        /// <summary>验证包含双引号、换行符、制表符等特殊字符的 JSON 往返</summary>
        [Test]
        public void JsonRoundTrip_SpecialCharacters()
        {
            var original = new PropertyBag();
            original.Set("text", "引号\"和换行\n和制表符\t");

            string json = PropertyBagSerializer.ToJson(original);
            var restored = PropertyBagSerializer.FromJson(json);

            Assert.AreEqual("引号\"和换行\n和制表符\t", restored.Get<string>("text"));
        }

        /// <summary>验证中文内容的 JSON 往返</summary>
        [Test]
        public void JsonRoundTrip_ChineseContent()
        {
            var original = new PropertyBag();
            original.Set("模板", "精英怪物组");

            string json = PropertyBagSerializer.ToJson(original);
            var restored = PropertyBagSerializer.FromJson(json);

            Assert.AreEqual("精英怪物组", restored.Get<string>("模板"));
        }

        /// <summary>验证负数（浮点和整数）的 JSON 往返</summary>
        [Test]
        public void JsonRoundTrip_NegativeNumbers()
        {
            var original = new PropertyBag();
            original.Set("offset", -3.5f);
            original.Set("index", -1);

            string json = PropertyBagSerializer.ToJson(original);
            var restored = PropertyBagSerializer.FromJson(json);

            Assert.AreEqual(-3.5f, restored.Get<float>("offset"), 0.001f);
            Assert.AreEqual(-1, restored.Get<int>("index"));
        }

        /// <summary>验证 null/空/空白 JSON 输入返回空 PropertyBag 而不抛异常</summary>
        [Test]
        public void FromJson_NullOrEmpty_ReturnsEmptyBag()
        {
            Assert.AreEqual(0, PropertyBagSerializer.FromJson(null!).Count);
            Assert.AreEqual(0, PropertyBagSerializer.FromJson("").Count);
            Assert.AreEqual(0, PropertyBagSerializer.FromJson("   ").Count);
        }
    }
}
