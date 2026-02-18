#nullable enable
using System;

namespace SceneBlueprint.Actions.Spawn
{
    /// <summary>
    /// 单个波次的配置（纯数据，无 Unity 依赖）。
    /// 存储在 Spawn.Wave 节点的 Properties 中，导出为 JSON 数组。
    /// <para>
    /// 设计原则：波次逻辑属于 Blueprint 层，不属于 SceneView 层。
    /// WaveSpawnConfig 只描述怪物池，WaveEntry 描述"每波刷几个、间隔多久、筛选什么怪"。
    /// </para>
    /// </summary>
    [Serializable]
    public struct WaveEntry
    {
        /// <summary>本波刷怪数量（最小 1）</summary>
        public int count;

        /// <summary>距上一波的间隔（Tick 数，首波为 0 表示立即开始）</summary>
        public int intervalTicks;

        /// <summary>怪物筛选标签（留空或 "All" 表示使用全部怪物池）</summary>
        public string monsterFilter;
    }

    /// <summary>JsonUtility 序列化包装器（顶层必须是 class/struct）</summary>
    [Serializable]
    public struct WaveEntryListWrapper
    {
        public WaveEntry[] items;
    }
}
