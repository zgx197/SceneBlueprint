#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests
{
    /// <summary>
    /// Step 6: VisibleWhen 条件评估器测试——验证所有支持的操作符和边界情况。
    /// <para>
    /// 测试范围：
    /// - 字符串相等/不等（== / !=）
    /// - 数值比较（&gt; / &lt; / ==）
    /// - OR 逻辑（||）——任一满足
    /// - AND 逻辑（&amp;&amp;）——全部满足
    /// - 空表达式返回 true
    /// - 缺失键的处理
    /// </para>
    /// </summary>
    public class VisibleWhenTests
    {
        /// <summary>字符串相等：属性值 == 期望值时返回 true</summary>
        [Test]
        public void EqualEnum_True()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Interval");

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("mode == Interval", bag));
        }

        /// <summary>字符串相等：属性值 != 期望值时返回 false</summary>
        [Test]
        public void EqualEnum_False()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Burst");

            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("mode == Interval", bag));
        }

        [Test]
        public void NotEqual_True()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Burst");

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("mode != Interval", bag));
        }

        [Test]
        public void NotEqual_False()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Interval");

            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("mode != Interval", bag));
        }

        [Test]
        public void NumericGreaterThan()
        {
            var bag = new PropertyBag();
            bag.Set("waves", 3);

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("waves > 1", bag));
            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("waves > 5", bag));
        }

        [Test]
        public void NumericLessThan()
        {
            var bag = new PropertyBag();
            bag.Set("waves", 3);

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("waves < 5", bag));
            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("waves < 1", bag));
        }

        [Test]
        public void NumericEqual()
        {
            var bag = new PropertyBag();
            bag.Set("count", 5);

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("count == 5", bag));
            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("count == 3", bag));
        }

        /// <summary>OR 逻辑：第一个条件满足时返回 true</summary>
        [Test]
        public void Or_FirstTrue()
        {
            var bag = new PropertyBag();
            bag.Set("action", "LookAt");

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("action == LookAt || action == Follow", bag));
        }

        [Test]
        public void Or_SecondTrue()
        {
            var bag = new PropertyBag();
            bag.Set("action", "Follow");

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("action == LookAt || action == Follow", bag));
        }

        [Test]
        public void Or_NoneTrue()
        {
            var bag = new PropertyBag();
            bag.Set("action", "ZoomIn");

            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("action == LookAt || action == Follow", bag));
        }

        /// <summary>AND 逻辑：两个条件都满足时返回 true</summary>
        [Test]
        public void And_BothTrue()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Interval");
            bag.Set("waves", 3);

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("mode == Interval && waves > 1", bag));
        }

        /// <summary>AND 逻辑：其中一个条件不满足时返回 false</summary>
        [Test]
        public void And_OneFalse()
        {
            var bag = new PropertyBag();
            bag.Set("mode", "Burst");
            bag.Set("waves", 3);

            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("mode == Interval && waves > 1", bag));
        }

        /// <summary>空表达式（null）应返回 true（始终可见）</summary>
        [Test]
        public void NullExpression_ReturnsTrue()
        {
            var bag = new PropertyBag();

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate(null, bag));
        }

        /// <summary>空字符串和空白表达式应返回 true（始终可见）</summary>
        [Test]
        public void EmptyExpression_ReturnsTrue()
        {
            var bag = new PropertyBag();

            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("", bag));
            Assert.IsTrue(VisibleWhenEvaluator.Evaluate("   ", bag));
        }

        /// <summary>缺失键的相等比较应返回 false（实际值为 null）</summary>
        [Test]
        public void MissingKey_EqualComparison_ReturnsFalse()
        {
            var bag = new PropertyBag();

            Assert.IsFalse(VisibleWhenEvaluator.Evaluate("missing == something", bag));
        }
    }
}
