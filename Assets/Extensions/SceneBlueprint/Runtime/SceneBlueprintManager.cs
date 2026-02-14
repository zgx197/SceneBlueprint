#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 场景蓝图管理器（每个场景唯一）。
    /// 持有一个关卡的 BlueprintAsset（SO）和按子蓝图分组的场景绑定数据。
    /// 
    /// 设计原则：
    /// - 由蓝图编辑器的"同步到场景"功能自动创建和维护
    /// - 策划无需直接操作此组件的 Inspector
    /// - 导出时从此组件读取绑定数据，合并 SO 图数据生成运行时 JSON
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SceneBlueprint/场景蓝图管理器")]
    public class SceneBlueprintManager : MonoBehaviour
    {
        [Tooltip("关卡蓝图资产（一个关卡对应一个 SO）")]
        [SerializeField]
        private BlueprintAsset? _blueprintAsset;

        [Tooltip("按子蓝图分组的场景绑定")]
        [SerializeField]
        private List<SubGraphBindingGroup> _bindingGroups = new List<SubGraphBindingGroup>();

        /// <summary>关卡蓝图资产</summary>
        public BlueprintAsset? BlueprintAsset
        {
            get => _blueprintAsset;
            set => _blueprintAsset = value;
        }

        /// <summary>绑定分组列表</summary>
        public List<SubGraphBindingGroup> BindingGroups => _bindingGroups;

        /// <summary>根据子蓝图框 ID 查找绑定分组</summary>
        public SubGraphBindingGroup? FindBindingGroup(string subGraphFrameId)
        {
            foreach (var group in _bindingGroups)
            {
                if (group.SubGraphFrameId == subGraphFrameId)
                    return group;
            }
            return null;
        }

        /// <summary>根据子蓝图名称查找绑定分组</summary>
        public SubGraphBindingGroup? FindBindingGroupByTitle(string title)
        {
            foreach (var group in _bindingGroups)
            {
                if (group.SubGraphTitle == title)
                    return group;
            }
            return null;
        }

        /// <summary>根据 bindingKey 在所有分组中查找绑定</summary>
        public SceneBindingSlot? FindBinding(string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey))
                return null;

            // C5：优先按 scopedBindingKey 的 scope 定位分组，避免跨子图同名 key 冲突。
            if (TryExtractScopeId(bindingKey, out var scopeId))
            {
                var scopedGroup = FindBindingGroup(scopeId);
                if (scopedGroup != null)
                {
                    var scopedSlot = scopedGroup.FindBinding(bindingKey);
                    if (scopedSlot != null)
                        return scopedSlot;
                }
            }

            // 兼容旧调用：raw key 仅在唯一命中时返回；多命中视为歧义并返回 null。
            string rawBindingKey = ExtractRawBindingKey(bindingKey);
            SceneBindingSlot? matched = null;
            foreach (var group in _bindingGroups)
            {
                var slot = group.FindBinding(rawBindingKey);
                if (slot == null)
                    continue;

                if (matched != null)
                    return null;

                matched = slot;
            }

            return matched;
        }

        private static bool TryExtractScopeId(string scopedOrRawKey, out string scopeId)
        {
            scopeId = "";
            if (string.IsNullOrEmpty(scopedOrRawKey))
                return false;

            int idx = scopedOrRawKey.IndexOf('/');
            if (idx <= 0)
                return false;

            scopeId = scopedOrRawKey.Substring(0, idx);
            return true;
        }

        private static string ExtractRawBindingKey(string scopedOrRawKey)
        {
            if (string.IsNullOrEmpty(scopedOrRawKey))
                return "";

            int idx = scopedOrRawKey.IndexOf('/');
            if (idx < 0 || idx + 1 >= scopedOrRawKey.Length)
                return scopedOrRawKey;

            return scopedOrRawKey[(idx + 1)..];
        }
    }
}
