#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SceneBlueprint.Runtime.Test;

namespace SceneBlueprint.Editor.Test
{
    /// <summary>
    /// 一键构建蓝图运行时测试场景。
    /// <para>
    /// 菜单路径：SceneBlueprint / 创建运行时测试场景
    /// </para>
    /// </summary>
    public static class RuntimeTestSceneBuilder
    {
        private const string SceneSavePath = "Assets/Extensions/SceneBlueprint/Runtime/Test/BlueprintRuntimeTest.unity";

        [MenuItem("SceneBlueprint/创建运行时测试场景", false, 200)]
        public static void BuildScene()
        {
            // 询问保存当前场景
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 创建新空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. 环境 ──
            CreateEnvironment();

            // ── 2. 灯光 ──
            CreateLighting();

            // ── 3. 玩家 ──
            CreatePlayer();

            // ── 4. 管理器 ──
            CreateManager();

            // 保存场景
            var dir = System.IO.Path.GetDirectoryName(SceneSavePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            EditorSceneManager.SaveScene(scene, SceneSavePath);
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 测试场景已创建: {SceneSavePath}");
            EditorUtility.DisplayDialog("完成", $"运行时测试场景已创建并保存到:\n{SceneSavePath}", "确定");
        }

        // ─────────────────────────────────────────
        //  环境
        // ─────────────────────────────────────────

        private static void CreateEnvironment()
        {
            // Flat Plane 地面
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10, 1, 10); // 100x100 米

            // 地面材质：浅灰色
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard")!);
                mat.color = new Color(0.75f, 0.78f, 0.75f);
                renderer.sharedMaterial = mat;

                // 保存材质到临时路径
                var matPath = "Assets/Extensions/SceneBlueprint/Runtime/Test/GroundMaterial.mat";
                var matDir = System.IO.Path.GetDirectoryName(matPath);
                if (!string.IsNullOrEmpty(matDir) && !System.IO.Directory.Exists(matDir))
                    System.IO.Directory.CreateDirectory(matDir);
                AssetDatabase.CreateAsset(mat, matPath);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }

            // 四周添加小高度标识线（方便感知边界）
            CreateBorderMarker("Border_N", new Vector3(0, 0.05f, 50), new Vector3(100, 0.1f, 0.3f));
            CreateBorderMarker("Border_S", new Vector3(0, 0.05f, -50), new Vector3(100, 0.1f, 0.3f));
            CreateBorderMarker("Border_E", new Vector3(50, 0.05f, 0), new Vector3(0.3f, 0.1f, 100));
            CreateBorderMarker("Border_W", new Vector3(-50, 0.05f, 0), new Vector3(0.3f, 0.1f, 100));
        }

        private static void CreateBorderMarker(string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard")!);
                mat.color = new Color(0.4f, 0.45f, 0.4f);
                renderer.sharedMaterial = mat;
            }
        }

        // ─────────────────────────────────────────
        //  灯光
        // ─────────────────────────────────────────

        private static void CreateLighting()
        {
            // 方向光
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 环境光设置
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
        }

        // ─────────────────────────────────────────
        //  玩家
        // ─────────────────────────────────────────

        private static void CreatePlayer()
        {
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 1, 0);

            // 蓝色材质
            var renderer = playerGo.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard")!);
                mat.color = new Color(0.2f, 0.5f, 1f);
                renderer.sharedMaterial = mat;
            }

            // 移除默认 Collider（CharacterController 自带）
            var capsuleCollider = playerGo.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null) Object.DestroyImmediate(capsuleCollider);

            // 添加 CharacterController
            var cc = playerGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0, 0);
            cc.height = 2f;
            cc.radius = 0.5f;

            // 添加玩家控制器脚本
            playerGo.AddComponent<SimplePlayerController>();
        }

        // ─────────────────────────────────────────
        //  管理器
        // ─────────────────────────────────────────

        private static void CreateManager()
        {
            var managerGo = new GameObject("[BlueprintRuntimeManager]");

            // 添加管理器组件
            var mgr = managerGo.AddComponent<BlueprintRuntimeManager>();

            // 添加怪物生成器
            var spawner = managerGo.AddComponent<MonsterSpawner>();

            // 通过 SerializedObject 设置引用
            var so = new SerializedObject(mgr);
            var spawnerProp = so.FindProperty("_monsterSpawner");
            if (spawnerProp != null)
            {
                spawnerProp.objectReferenceValue = spawner;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 尝试自动查找蓝图 JSON
            var guids = AssetDatabase.FindAssets("预设怪物测试蓝图 t:TextAsset",
                new[] { "Assets/GameAssets/SceneBlueprint" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var jsonProp = so.FindProperty("_blueprintJson");
                if (jsonProp != null && asset != null)
                {
                    jsonProp.objectReferenceValue = asset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 自动绑定蓝图: {path}");
                }
            }
        }
    }
}
