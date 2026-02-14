#nullable enable
using NUnit.Framework;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Tests.Unit.Runtime
{
    /// <summary>
    /// C5 回归：验证 Runtime 侧 scopedBindingKey 查找策略。
    /// 覆盖 SubGraphBindingGroup 与 SceneBlueprintManager 的 scoped/raw 兼容行为。
    /// </summary>
    public class BindingScopeLookupTests
    {
        [Test]
        public void SubGraphBindingGroup_FindBinding_ExactScopedMatch_Preferred()
        {
            var group = new SubGraphBindingGroup();
            var rawSlot = new SceneBindingSlot { BindingKey = "spawn" };
            var scopedSlot = new SceneBindingSlot { BindingKey = "sg_01/spawn" };
            group.Bindings.Add(rawSlot);
            group.Bindings.Add(scopedSlot);

            var found = group.FindBinding("sg_01/spawn");

            Assert.AreSame(scopedSlot, found);
        }

        [Test]
        public void SubGraphBindingGroup_FindBinding_RawFallback_Works()
        {
            var group = new SubGraphBindingGroup();
            var scopedSlot = new SceneBindingSlot { BindingKey = "sg_01/spawn" };
            group.Bindings.Add(scopedSlot);

            var found = group.FindBinding("spawn");

            Assert.AreSame(scopedSlot, found);
        }

        [Test]
        public void SceneBlueprintManager_FindBinding_ScopedKey_UsesTargetScope()
        {
            var host = new GameObject("scene-blueprint-manager-test");
            try
            {
                var manager = host.AddComponent<SceneBlueprintManager>();

                var groupA = new SubGraphBindingGroup { SubGraphFrameId = "sg_A" };
                var groupB = new SubGraphBindingGroup { SubGraphFrameId = "sg_B" };

                var slotA = new SceneBindingSlot { BindingKey = "sg_A/spawn" };
                var slotB = new SceneBindingSlot { BindingKey = "sg_B/spawn" };
                groupA.Bindings.Add(slotA);
                groupB.Bindings.Add(slotB);

                manager.BindingGroups.Add(groupA);
                manager.BindingGroups.Add(groupB);

                var found = manager.FindBinding("sg_B/spawn");

                Assert.AreSame(slotB, found);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SceneBlueprintManager_FindBinding_RawKeyWithAmbiguousMatches_ReturnsNull()
        {
            var host = new GameObject("scene-blueprint-manager-test");
            try
            {
                var manager = host.AddComponent<SceneBlueprintManager>();

                var groupA = new SubGraphBindingGroup { SubGraphFrameId = "sg_A" };
                var groupB = new SubGraphBindingGroup { SubGraphFrameId = "sg_B" };

                groupA.Bindings.Add(new SceneBindingSlot { BindingKey = "sg_A/spawn" });
                groupB.Bindings.Add(new SceneBindingSlot { BindingKey = "sg_B/spawn" });

                manager.BindingGroups.Add(groupA);
                manager.BindingGroups.Add(groupB);

                var found = manager.FindBinding("spawn");

                Assert.IsNull(found);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SceneBlueprintManager_FindBinding_RawKeyWithSingleMatch_ReturnsSlot()
        {
            var host = new GameObject("scene-blueprint-manager-test");
            try
            {
                var manager = host.AddComponent<SceneBlueprintManager>();

                var groupA = new SubGraphBindingGroup { SubGraphFrameId = "sg_A" };
                var groupB = new SubGraphBindingGroup { SubGraphFrameId = "sg_B" };

                var targetSlot = new SceneBindingSlot { BindingKey = "sg_A/spawn" };
                groupA.Bindings.Add(targetSlot);
                groupB.Bindings.Add(new SceneBindingSlot { BindingKey = "sg_B/other" });

                manager.BindingGroups.Add(groupA);
                manager.BindingGroups.Add(groupB);

                var found = manager.FindBinding("spawn");

                Assert.AreSame(targetSlot, found);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
