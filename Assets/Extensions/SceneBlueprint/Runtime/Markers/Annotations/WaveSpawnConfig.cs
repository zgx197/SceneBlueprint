#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 波次刷怪配置标注 — 挂在 AreaMarker 上，描述该区域能刷出什么怪和波次规则。
    /// <para>
    /// 配合 Blueprint 中的 Spawn.Wave 节点使用：
    /// 节点绑定带有 WaveSpawnConfig 的 AreaMarker，导出时收集区域几何 + 此标注数据。
    /// 运行时 SpawnWaveSystem 按波次在区域内随机位置生成怪物。
    /// </para>
    /// <para>
    /// 设计原则：
    /// - WaveSpawnConfig 属于 SceneView 层（空间标注），不是 Blueprint 层
    /// - "这个区域刷 3 波骷髅兵"是策划在场景中做的配置
    /// - Blueprint 只引用 AreaMarker ID，不存储怪物池
    /// - 导出时合并：AreaMarker 区域几何 + WaveSpawnConfig 标注 → SceneBinding
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Annotations/Wave Spawn Config")]
    public class WaveSpawnConfig : MarkerAnnotation
    {
        public override string AnnotationTypeId => "WaveSpawn";

        [Serializable]
        public struct MonsterEntry
        {
            [Tooltip("怪物模板 ID")]
            public string monsterId;

            [Tooltip("怪物等级")]
            [Range(1, 100)]
            public int level;

            [Tooltip("初始行为")]
            public InitialBehavior behavior;

            [Tooltip("警戒半径（仅 Guard 生效）")]
            [Min(0.5f)]
            public float guardRadius;

            [Tooltip("每波生成数量")]
            [Min(1)]
            public int count;
        }

        [Header("怪物池")]
        [Tooltip("每波要生成的怪物列表")]
        public MonsterEntry[] Monsters = Array.Empty<MonsterEntry>();

        [Header("波次设置")]
        [Tooltip("总波数")]
        [Min(1)]
        public int WaveCount = 1;

        [Tooltip("波次间隔（Tick 数，帧同步友好）")]
        [Min(1)]
        public int WaveIntervalTicks = 60;

        [Tooltip("怪物之间的最小间距")]
        [Min(0.5f)]
        public float MinSpacing = 1.5f;

        public override void CollectExportData(IDictionary<string, object> data)
        {
            data["monsters"] = JsonUtility.ToJson(new MonsterListWrapper { items = Monsters });
            data["waveCount"] = WaveCount;
            data["waveIntervalTicks"] = WaveIntervalTicks;
            data["minSpacing"] = MinSpacing;
        }

        /// <summary>JsonUtility 序列化包装器（顶层必须是 class/struct）</summary>
        [Serializable]
        public struct MonsterListWrapper
        {
            public MonsterEntry[] items;
        }

        // ── Gizmo 装饰 ──

        public override bool HasGizmoDecoration => Monsters.Length > 0;

        public override Color? GetGizmoColorOverride()
        {
            return Monsters.Length > 0
                ? new Color(0.8f, 0.4f, 0.1f) // 橙色，区分于预设怪的红色
                : null;
        }

        public override void DrawGizmoDecoration(bool isSelected)
        {
#if UNITY_EDITOR
            if (Monsters.Length == 0) return;

            int totalPerWave = 0;
            foreach (var m in Monsters) totalPerWave += m.count;

            var labelPos = transform.position + Vector3.up * 2.5f;
            var label = $"Wave x{WaveCount} ({totalPerWave}/波)";
            var labelColor = new Color(1f, 0.6f, 0.2f);

            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
            };
            style.normal.textColor = labelColor;
            UnityEditor.Handles.Label(labelPos, label, style);
#endif
        }
    }
}
