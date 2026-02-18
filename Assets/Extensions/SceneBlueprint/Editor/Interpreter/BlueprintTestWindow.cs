#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Systems;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Interpreter
{
    /// <summary>
    /// 蓝图运行时测试窗口——在编辑器中一键加载并执行导出的蓝图 JSON，验证运行时解释器。
    /// <para>
    /// 使用方式：
    /// 1. 菜单 SceneBlueprint → 运行时测试
    /// 2. 拖入导出的 JSON 文件（TextAsset）
    /// 3. 点击 [加载并执行] → 在 Console 中查看执行日志
    /// 4. 或点击 [逐帧执行] → 手动 Tick 观察每帧状态
    /// </para>
    /// </summary>
    public class BlueprintTestWindow : EditorWindow
    {
        [MenuItem("SceneBlueprint/运行时测试", priority = 2000)]
        private static void Open()
        {
            var window = GetWindow<BlueprintTestWindow>("蓝图运行时测试");
            window.minSize = new Vector2(400, 300);
        }

        // ── 序列化字段（Inspector 持久化）──
        [SerializeField] private TextAsset? _jsonAsset;

        // ── 运行时状态 ──
        private BlueprintRunner? _runner;
        private string _statusText = "未加载";
        private Vector2 _scrollPos;

        /// <summary>运行时配置（从全局设置读取）</summary>
        private BlueprintRuntimeSettings Settings => BlueprintRuntimeSettings.Instance;

        // ══════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("蓝图运行时解释器测试", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            // ── 输入区 ──
            _jsonAsset = (TextAsset?)EditorGUILayout.ObjectField(
                "蓝图 JSON", _jsonAsset, typeof(TextAsset), false);

            EditorGUILayout.Space(8);

            // ── 操作按钮 ──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _jsonAsset != null;

                if (GUILayout.Button("加载并执行", GUILayout.Height(30)))
                {
                    LoadAndRunAll();
                }

                if (GUILayout.Button("加载（不执行）", GUILayout.Height(30)))
                {
                    LoadOnly();
                }

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _runner?.Frame != null && !_runner.IsCompleted;

                if (GUILayout.Button("单步 Tick", GUILayout.Height(24)))
                {
                    StepTick();
                }

                if (GUILayout.Button("执行 10 Ticks", GUILayout.Height(24)))
                {
                    StepTicks(Settings.BatchTickCount);
                }

                GUI.enabled = _runner != null;

                if (GUILayout.Button("重置", GUILayout.Height(24)))
                {
                    Reset();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);

            // ── 状态显示 ──
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_statusText, MessageType.Info);

            // ── Action 状态表 ──
            if (_runner?.Frame != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Action 状态", EditorStyles.boldLabel);
                DrawActionStates();
            }
        }

        // ══════════════════════════════════════════
        //  Action 状态表绘制
        // ══════════════════════════════════════════

        private void DrawActionStates()
        {
            var frame = _runner!.Frame!;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

            // 表头
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Idx", GUILayout.Width(30));
                GUILayout.Label("TypeId", GUILayout.Width(150));
                GUILayout.Label("Phase", GUILayout.Width(100));
                GUILayout.Label("Ticks", GUILayout.Width(50));
            }

            // 表体
            for (int i = 0; i < frame.ActionCount; i++)
            {
                var typeId = frame.GetTypeId(i);
                ref var state = ref frame.States[i];
                var phaseColor = GetPhaseColor(state.Phase);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i.ToString(), GUILayout.Width(30));
                    GUILayout.Label(typeId, GUILayout.Width(150));

                    var prevColor = GUI.contentColor;
                    GUI.contentColor = phaseColor;
                    GUILayout.Label(state.Phase.ToString(), GUILayout.Width(100));
                    GUI.contentColor = prevColor;

                    GUILayout.Label(state.TicksInPhase.ToString(), GUILayout.Width(50));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static Color GetPhaseColor(ActionPhase phase) => phase switch
        {
            ActionPhase.Idle => Color.gray,
            ActionPhase.WaitingTrigger => new Color(1f, 0.8f, 0.2f),
            ActionPhase.Running => new Color(0.3f, 0.8f, 1f),
            ActionPhase.Completed => new Color(0.3f, 0.9f, 0.3f),
            ActionPhase.Failed => new Color(1f, 0.3f, 0.3f),
            _ => Color.white
        };

        // ══════════════════════════════════════════
        //  操作实现
        // ══════════════════════════════════════════

        private BlueprintRunner CreateRunner()
        {
            var runner = new BlueprintRunner
            {
                Log = msg => UnityEngine.Debug.Log(msg),
                LogWarning = msg => UnityEngine.Debug.LogWarning(msg),
                LogError = msg => UnityEngine.Debug.LogError(msg)
            };

            // 注册所有基础 System
            runner.RegisterSystems(
                new TransitionSystem(),
                new FlowSystem(),
                new SpawnPresetSystem(),
                new SpawnWaveSystem(),
                new TriggerEnterAreaSystem()
            );

            return runner;
        }

        private void LoadAndRunAll()
        {
            if (_jsonAsset == null) return;

            _runner = CreateRunner();
            _runner.Load(_jsonAsset.text);

            if (_runner.Frame == null)
            {
                _statusText = "加载失败，请查看 Console";
                return;
            }

            UnityEngine.Debug.Log("══════════════════════════════════════════");
            UnityEngine.Debug.Log("  蓝图运行时测试 - 开始执行");
            UnityEngine.Debug.Log("══════════════════════════════════════════");

            var ticks = _runner.RunUntilComplete(maxTicks: Settings.MaxTicksLimit);

            _statusText = _runner.IsCompleted
                ? $"执行完毕！共 {ticks} Tick，{_runner.Frame.ActionCount} 个 Action"
                : $"达到最大 Tick 限制 ({Settings.MaxTicksLimit})，尚未完成";

            UnityEngine.Debug.Log("══════════════════════════════════════════");
            UnityEngine.Debug.Log($"  蓝图运行时测试 - {_statusText}");
            UnityEngine.Debug.Log("══════════════════════════════════════════");

            Repaint();
        }

        private void LoadOnly()
        {
            if (_jsonAsset == null) return;

            _runner = CreateRunner();
            _runner.Load(_jsonAsset.text);

            if (_runner.Frame == null)
            {
                _statusText = "加载失败，请查看 Console";
                return;
            }

            _statusText = $"已加载: {_runner.Frame.BlueprintName} " +
                          $"({_runner.Frame.ActionCount} Actions, " +
                          $"{_runner.Frame.Transitions.Length} Transitions)";

            Repaint();
        }

        private void StepTick()
        {
            if (_runner?.Frame == null || _runner.IsCompleted) return;

            UnityEngine.Debug.Log($"────── Tick {_runner.TickCount + 1} ──────");
            _runner.Tick();

            _statusText = _runner.IsCompleted
                ? $"执行完毕！Tick={_runner.TickCount}"
                : $"Tick {_runner.TickCount}，活跃 Action: {CountActiveActions()}";

            Repaint();
        }

        private void StepTicks(int count)
        {
            for (int i = 0; i < count && !(_runner?.IsCompleted ?? true); i++)
            {
                StepTick();
            }
        }

        private void Reset()
        {
            _runner?.Shutdown();
            _runner = null;
            _statusText = "已重置";
            Repaint();
        }

        private int CountActiveActions()
        {
            if (_runner?.Frame == null) return 0;
            int count = 0;
            for (int i = 0; i < _runner.Frame.States.Length; i++)
            {
                if (_runner.Frame.States[i].Phase == ActionPhase.Running ||
                    _runner.Frame.States[i].Phase == ActionPhase.WaitingTrigger)
                    count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            _runner?.Shutdown();
        }
    }
}
