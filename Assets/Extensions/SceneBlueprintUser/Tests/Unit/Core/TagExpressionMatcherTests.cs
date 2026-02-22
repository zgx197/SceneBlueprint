#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;

namespace SceneBlueprint.Tests.Unit.Core
{
    /// <summary>
    /// Phase 5: Tag 表达式匹配器测试。
    /// </summary>
    public class TagExpressionMatcherTests
    {
        [Test]
        public void Evaluate_EmptyExpression_ReturnsTrue()
        {
            Assert.IsTrue(TagExpressionMatcher.Evaluate("", "Combat.SpawnPoint"));
            Assert.IsTrue(TagExpressionMatcher.Evaluate("   ", "Combat.SpawnPoint"));
            Assert.IsTrue(TagExpressionMatcher.Evaluate(null, "Combat.SpawnPoint"));
        }

        [Test]
        public void IsExactOrPrefixMatch_ExactAndPrefix_AreTrue()
        {
            Assert.IsTrue(TagExpressionMatcher.IsExactOrPrefixMatch("Combat.SpawnPoint", "Combat.SpawnPoint"));
            Assert.IsTrue(TagExpressionMatcher.IsExactOrPrefixMatch("Combat.SpawnPoint.Elite", "Combat.SpawnPoint"));
            Assert.IsFalse(TagExpressionMatcher.IsExactOrPrefixMatch("Combat.Trigger", "Combat.SpawnPoint"));
        }

        [Test]
        public void IsPatternMatch_WildcardSegment_Works()
        {
            Assert.IsTrue(TagExpressionMatcher.IsPatternMatch("Combat.SpawnPoint.Elite", "Combat.*.Elite"));
            Assert.IsFalse(TagExpressionMatcher.IsPatternMatch("Combat.SpawnPoint.Boss.Elite", "Combat.*.Elite"));
        }

        [Test]
        public void Evaluate_MultiplePatterns_AnyMatchReturnsTrue()
        {
            string expression = "Trigger.Zone; Combat.*.Elite";

            Assert.IsTrue(TagExpressionMatcher.Evaluate(expression, "Combat.SpawnPoint.Elite"));
            Assert.IsTrue(TagExpressionMatcher.Evaluate(expression, "Trigger.Zone.Enter"));
            Assert.IsFalse(TagExpressionMatcher.Evaluate(expression, "Narrative.Dialogue"));
        }

        [Test]
        public void IsPatternMatch_AnyWildcard_MatchesAll()
        {
            Assert.IsTrue(TagExpressionMatcher.IsPatternMatch("Anything.Really", "*"));
        }

        [Test]
        public void Evaluate_NonEmptyExpression_EmptyTagReturnsFalse()
        {
            Assert.IsFalse(TagExpressionMatcher.Evaluate("Combat.*", ""));
            Assert.IsFalse(TagExpressionMatcher.Evaluate("Combat.*", null));
        }
    }
}
