#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Systems;
using UnityEngine;

namespace SceneBlueprintUser.Systems
{
    /// <summary>
    /// DiGame 专属 System 提供者——向 BlueprintRunner 注册所有游戏特定 System。
    /// <para>
    /// 游戏启动时（BeforeSceneLoad）自动向 BlueprintSystemRegistry 注册，
    /// 框架在 BlueprintRunnerFactory.CreateDefault() 时自动发现并注入所有 System。
    /// </para>
    /// <para>
    /// Handler 注入示例：
    /// <code>
    /// var runner = BlueprintRunnerFactory.CreateDefault();
    /// runner.GetSystem&lt;CameraShakeSystem&gt;()?.ShakeHandler = myCameraShakeHandler;
    /// runner.GetSystem&lt;SpawnWaveSystem&gt;()?.SpawnHandler = mySpawnHandler;
    /// runner.Load(jsonText);
    /// </code>
    /// </para>
    /// </summary>
    public class DiGameSystemProvider : IBlueprintSystemProvider
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
            => BlueprintSystemRegistry.Register(new DiGameSystemProvider());

        public IEnumerable<BlueprintSystemBase> CreateSystems()
        {
            yield return new TransitionSystem();
            yield return new CameraShakeSystem();
            yield return new ShowWarningSystem();
            yield return new SpawnPresetSystem();
            yield return new SpawnWaveSystem();
            yield return new TriggerEnterAreaSystem();
        }
    }
}
