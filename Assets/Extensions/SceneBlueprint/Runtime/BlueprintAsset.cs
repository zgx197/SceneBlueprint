#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 蓝图资产（ScriptableObject）。
    /// 存储蓝图的图数据（JSON 字符串）和元信息。
    /// 
    /// 设计原则：
    /// - SO 本身只是数据容器，不包含序列化/反序列化逻辑
    /// - 图的序列化/反序列化由 Editor 层的 JsonGraphSerializer 负责
    /// - 场景绑定不存在 SO 中（由 SceneBlueprintManager 的 Slot 持有）
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBlueprint",
        menuName = "SceneBlueprint/蓝图资产",
        order = 100)]
    public class BlueprintAsset : ScriptableObject
    {
        // ── 元信息 ──

        [Tooltip("蓝图唯一 ID（自动生成）")]
        public string BlueprintId = "";

        [Tooltip("蓝图显示名称")]
        public string BlueprintName = "";

        [Tooltip("蓝图描述")]
        [TextArea(2, 5)]
        public string Description = "";

        [Tooltip("数据版本号")]
        public int Version = 1;

        // ── 图数据 ──

        [Tooltip("序列化的图数据（JSON 格式，由编辑器自动管理）")]
        [HideInInspector]
        public string GraphJson = "";

        /// <summary>图数据是否为空</summary>
        public bool IsEmpty => string.IsNullOrEmpty(GraphJson);

        /// <summary>初始化新蓝图（生成唯一 ID）</summary>
        public void InitializeNew(string name = "")
        {
            if (string.IsNullOrEmpty(BlueprintId))
                BlueprintId = System.Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(name))
                BlueprintName = name;
        }
    }
}
