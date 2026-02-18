#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Core.Export;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 波次刷怪系统——处理 Spawn.Wave 节点的运行时执行。
    /// <para>
    /// 持续型系统：在多个 Tick 内运行，按波次在区域内随机位置生成怪物。
    /// 使用 ActionRuntimeState.CustomInt 存储当前波次索引（从 0 开始），
    /// CustomFloat 存储上次刷怪的 Tick 时间戳。
    /// </para>
    /// <para>
    /// 每波生成流程：
    /// 1. 解析区域几何（center + size + rotation）
    /// 2. 解析 WaveSpawnConfig（怪物池 + 波次规则）
    /// 3. 在区域内用拒绝采样生成随机位置（满足最小间距）
    /// 4. 通过 ISpawnHandler 回调创建怪物
    /// 5. 递增波次索引，等待间隔后生成下一波
    /// 6. 全部波次完成 → Completed
    /// </para>
    /// </summary>
    public class SpawnWaveSystem : BlueprintSystemBase
    {
        public override string Name => "SpawnWaveSystem";
        public override int Order => 110;

        public ISpawnHandler? SpawnHandler { get; set; }

        private const int MaxSamplingAttempts = 100;

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices("Spawn.Wave");
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                ProcessWaveAction(frame, idx, ref state);
            }
        }

        private void ProcessWaveAction(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            var bindings = frame.GetSceneBindings(actionIndex);
            if (bindings.Length == 0)
            {
                Debug.LogWarning($"[SpawnWaveSystem] Spawn.Wave (index={actionIndex}) 无 SceneBinding，标记 Completed");
                state.Phase = ActionPhase.Completed;
                return;
            }

            // 解析配置（仅第一个绑定）
            var binding = bindings[0];
            var areaData = ParseAreaPayload(binding.SpatialPayloadJson);
            var waveConfig = ExtractWaveConfig(binding.Annotations);

            if (!waveConfig.HasData)
            {
                Debug.LogWarning($"[SpawnWaveSystem] Spawn.Wave (index={actionIndex}) 无 WaveSpawn 标注，标记 Completed");
                state.Phase = ActionPhase.Completed;
                return;
            }

            int currentWave = state.CustomInt; // 当前波次索引（0-based）
            int lastSpawnTick = (int)state.CustomFloat; // 上次刷怪 Tick

            // 首波立即生成（CustomFloat == 0 表示未开始）
            bool isFirstWave = (currentWave == 0 && state.CustomFloat == 0f);
            bool intervalElapsed = (frame.TickCount - lastSpawnTick) >= waveConfig.WaveIntervalTicks;

            if (isFirstWave || (currentWave < waveConfig.WaveCount && intervalElapsed))
            {
                SpawnOneWave(areaData, waveConfig, currentWave, actionIndex);

                currentWave++;
                state.CustomInt = currentWave;
                state.CustomFloat = frame.TickCount;

                Debug.Log($"[SpawnWaveSystem] 波次 {currentWave}/{waveConfig.WaveCount} 生成完毕 " +
                          $"(Tick={frame.TickCount}, Action index={actionIndex})");

                // 全部波次完成
                if (currentWave >= waveConfig.WaveCount)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[SpawnWaveSystem] ═══ 所有波次完成 (index={actionIndex}) ═══");
                }
            }
        }

        /// <summary>生成一波怪物</summary>
        private void SpawnOneWave(AreaPayload area, WaveConfig config, int waveIndex, int actionIndex)
        {
            // 统计本波总怪物数
            int totalCount = 0;
            foreach (var m in config.Monsters) totalCount += m.count;

            // 在区域内生成随机位置
            var positions = GenerateRandomPositions(area, totalCount, config.MinSpacing);

            Debug.Log($"[SpawnWaveSystem] ── 波次 {waveIndex + 1}: 生成 {totalCount} 个怪物 ──");

            int posIdx = 0;
            int globalIdx = 0;
            foreach (var monster in config.Monsters)
            {
                for (int i = 0; i < monster.count && posIdx < positions.Count; i++)
                {
                    var pos = positions[posIdx++];
                    var rot = new Vector3(0, Random.Range(0f, 360f), 0);

                    Debug.Log($"[SpawnWaveSystem]   [{globalIdx}] 怪物={monster.monsterId}, " +
                              $"等级={monster.level}, 行为={monster.behavior}, " +
                              $"位置=({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");

                    SpawnHandler?.OnSpawn(new SpawnData
                    {
                        Index = globalIdx,
                        MonsterId = monster.monsterId,
                        Level = monster.level,
                        Behavior = monster.behavior,
                        GuardRadius = monster.guardRadius,
                        Position = pos,
                        EulerRotation = rot
                    });

                    globalIdx++;
                }
            }

            SpawnHandler?.OnSpawnBatchComplete(globalIdx);
        }

        /// <summary>在区域内用拒绝采样生成随机位置</summary>
        private List<Vector3> GenerateRandomPositions(AreaPayload area, int count, float minSpacing)
        {
            var result = new List<Vector3>(count);
            var halfSize = area.Size * 0.5f;
            var rotation = Quaternion.Euler(area.Rotation);
            float minSqr = minSpacing * minSpacing;

            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < MaxSamplingAttempts; attempt++)
                {
                    // 在局部空间随机采样
                    var localPos = new Vector3(
                        Random.Range(-halfSize.x, halfSize.x),
                        0,
                        Random.Range(-halfSize.z, halfSize.z)
                    );

                    // 转换到世界空间
                    var worldPos = area.Center + rotation * localPos;

                    // 检查最小间距
                    bool tooClose = false;
                    for (int j = 0; j < result.Count; j++)
                    {
                        if ((result[j] - worldPos).sqrMagnitude < minSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        result.Add(worldPos);
                        placed = true;
                        break;
                    }
                }

                // 采样失败时强制放置（降级）
                if (!placed)
                {
                    var fallback = area.Center + rotation * new Vector3(
                        Random.Range(-halfSize.x, halfSize.x),
                        0,
                        Random.Range(-halfSize.z, halfSize.z)
                    );
                    result.Add(fallback);
                }
            }

            return result;
        }

        // ── 数据解析 ──

        private struct AreaPayload
        {
            public Vector3 Center;
            public Vector3 Rotation;
            public Vector3 Size;
            public string Shape;
        }

        private struct WaveConfig
        {
            public bool HasData;
            public MonsterInfo[] Monsters;
            public int WaveCount;
            public int WaveIntervalTicks;
            public float MinSpacing;
        }

        private struct MonsterInfo
        {
            public string monsterId;
            public int level;
            public string behavior;
            public float guardRadius;
            public int count;
        }

        private static AreaPayload ParseAreaPayload(string? json)
        {
            var data = new AreaPayload { Size = new Vector3(10, 0, 10), Shape = "Box" };
            if (string.IsNullOrEmpty(json)) return data;

            try
            {
                var payload = JsonUtility.FromJson<AreaPayloadJson>(json);
                if (payload != null)
                {
                    data.Center = new Vector3(payload.center.x, payload.center.y, payload.center.z);
                    data.Rotation = new Vector3(payload.rotation.x, payload.rotation.y, payload.rotation.z);
                    data.Size = new Vector3(payload.size.x, payload.size.y, payload.size.z);
                    data.Shape = payload.shape ?? "Box";
                }
            }
            catch { }

            return data;
        }

        private static WaveConfig ExtractWaveConfig(AnnotationDataEntry[]? annotations)
        {
            var config = new WaveConfig { WaveCount = 1, WaveIntervalTicks = 60, MinSpacing = 1.5f };
            if (annotations == null) return config;

            for (int i = 0; i < annotations.Length; i++)
            {
                if (annotations[i].TypeId != "WaveSpawn") continue;

                config.HasData = true;
                var props = annotations[i].Properties;
                for (int p = 0; p < props.Length; p++)
                {
                    switch (props[p].Key)
                    {
                        case "monsters":
                            config.Monsters = ParseMonsterList(props[p].Value);
                            break;
                        case "waveCount":
                            int.TryParse(props[p].Value, out config.WaveCount);
                            break;
                        case "waveIntervalTicks":
                            int.TryParse(props[p].Value, out config.WaveIntervalTicks);
                            break;
                        case "minSpacing":
                            float.TryParse(props[p].Value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out config.MinSpacing);
                            break;
                    }
                }
                break;
            }

            return config;
        }

        private static MonsterInfo[] ParseMonsterList(string? json)
        {
            if (string.IsNullOrEmpty(json)) return System.Array.Empty<MonsterInfo>();

            try
            {
                var wrapper = JsonUtility.FromJson<MonsterListJson>(json);
                if (wrapper?.items == null) return System.Array.Empty<MonsterInfo>();

                var result = new MonsterInfo[wrapper.items.Length];
                for (int i = 0; i < wrapper.items.Length; i++)
                {
                    var src = wrapper.items[i];
                    result[i] = new MonsterInfo
                    {
                        monsterId = src.monsterId ?? "",
                        level = src.level,
                        behavior = src.behavior ?? "Idle",
                        guardRadius = src.guardRadius,
                        count = Mathf.Max(1, src.count)
                    };
                }
                return result;
            }
            catch
            {
                return System.Array.Empty<MonsterInfo>();
            }
        }

        // ── JSON 反序列化辅助 ──

        [System.Serializable]
        private class AreaPayloadJson
        {
            public Vec3Json center = new();
            public Vec3Json rotation = new();
            public Vec3Json size = new();
            public string shape = "Box";
        }

        [System.Serializable]
        private class Vec3Json
        {
            public float x, y, z;
        }

        [System.Serializable]
        private class MonsterListJson
        {
            public MonsterEntryJson[] items = System.Array.Empty<MonsterEntryJson>();
        }

        [System.Serializable]
        private class MonsterEntryJson
        {
            public string monsterId = "";
            public int level = 1;
            public string behavior = "Idle";
            public float guardRadius = 5f;
            public int count = 1;
        }
    }
}
