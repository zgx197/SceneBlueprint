#nullable enable
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Systems;
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 蓝图运行时测试管理器——驱动蓝图执行并协调可视化生成。
    /// <para>
    /// 职责：
    /// 1. 加载蓝图 JSON 数据
    /// 2. 创建 BlueprintRunner 并注册 System
    /// 3. 将 MonsterSpawner 注入 SpawnPresetSystem
    /// 4. 执行蓝图直到完毕
    /// 5. 提供运行时状态展示（UI）
    /// </para>
    /// </summary>
    public class BlueprintRuntimeManager : MonoBehaviour
    {
        [Header("蓝图数据")]
        [Tooltip("拖入导出的蓝图 JSON 文件")]
        [SerializeField] private TextAsset? _blueprintJson;

        [Header("自动执行")]
        [Tooltip("场景启动后自动加载并执行蓝图")]
        [SerializeField] private bool _autoRun = true;

        [Header("组件引用")]
        [SerializeField] private MonsterSpawner? _monsterSpawner;

        // 运行时状态
        private BlueprintRunner? _runner;
        private bool _executed;
        private string _statusText = "等待加载...";

        /// <summary>当前 Runner 实例（外部访问用）</summary>
        public BlueprintRunner? Runner => _runner;

        private void Start()
        {
            if (_autoRun && _blueprintJson != null)
            {
                LoadAndRun();
            }
        }

        /// <summary>加载蓝图并立即执行到结束</summary>
        public void LoadAndRun()
        {
            if (_blueprintJson == null)
            {
                _statusText = "错误：未指定蓝图 JSON 文件";
                Debug.LogError("[BlueprintRuntimeManager] " + _statusText);
                return;
            }

            // 清除之前的怪物
            if (_monsterSpawner != null) _monsterSpawner.ClearAll();

            // 创建 Runner
            _runner = new BlueprintRunner
            {
                Log = msg => Debug.Log(msg),
                LogWarning = msg => Debug.LogWarning(msg),
                LogError = msg => Debug.LogError(msg)
            };

            // 创建并注册 System
            var spawnSystem = new SpawnPresetSystem();
            if (_monsterSpawner != null)
            {
                spawnSystem.SpawnHandler = _monsterSpawner;
            }

            _runner.RegisterSystems(
                new TransitionSystem(),
                new FlowSystem(),
                spawnSystem
            );

            // 加载蓝图
            _statusText = "正在加载...";
            _runner.Load(_blueprintJson.text);

            // 执行蓝图
            _statusText = "正在执行...";
            int ticks = _runner.RunUntilComplete(1000);

            _executed = true;
            _statusText = $"执行完毕 — 共 {ticks} Tick";
            Debug.Log($"[BlueprintRuntimeManager] {_statusText}");
        }

        /// <summary>重新加载并执行</summary>
        public void Reload()
        {
            _runner?.Shutdown();
            _runner = null;
            _executed = false;
            LoadAndRun();
        }

        // ── 简易运行时 UI ──

        private void OnGUI()
        {
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };

            var area = new Rect(10, 10, 320, 100);
            GUI.Box(area, "", boxStyle);

            GUILayout.BeginArea(new Rect(20, 18, 300, 80));

            GUILayout.Label($"<b>蓝图运行时测试</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
            GUILayout.Label($"状态: {_statusText}");

            if (_blueprintJson != null)
            {
                GUILayout.Label($"蓝图: {_blueprintJson.name}");
            }

            GUILayout.EndArea();

            // 控制按钮
            if (GUI.Button(new Rect(10, 115, 100, 30), _executed ? "重新加载" : "加载执行"))
            {
                if (_executed) Reload();
                else LoadAndRun();
            }

            // 操作提示
            var helpRect = new Rect(Screen.width - 240, 10, 230, 70);
            GUI.Box(helpRect, "");
            GUI.Label(new Rect(helpRect.x + 8, helpRect.y + 6, 220, 60),
                "WASD: 移动\n鼠标右键拖拽: 旋转视角\n滚轮: 缩放",
                new GUIStyle(GUI.skin.label) { fontSize = 12 });
        }
    }
}
