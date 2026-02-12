#nullable enable
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Tests.Scripts
{
    /// <summary>
    /// SceneBlueprint测试系统配置 - 保存测试相关的设置和白名单
    /// </summary>
    [CreateAssetMenu(fileName = "SceneBlueprintTestConfig", menuName = "SceneBlueprint/Test Configuration")]
    public class TestConfiguration : ScriptableObject
    {
        [Header("程序集白名单")]
        [Tooltip("只有在白名单中的程序集会在Test Runner中显示")]
        public List<string> allowedAssemblies = new List<string>
        {
            "SceneBlueprint.Tests"
        };

        [Header("测试执行设置")]
        [Tooltip("是否启用程序集过滤")]
        public bool enableAssemblyFiltering = true;
        
        [Tooltip("测试失败时是否自动生成详细报告")]
        public bool autoGenerateReportOnFailure = true;
        
        [Tooltip("是否在控制台显示详细的测试日志")]
        public bool verboseLogging = true;

        [Header("报告设置")]
        [Tooltip("报告中包含的最大堆栈跟踪行数")]
        [Range(5, 50)]
        public int maxStackTraceLines = 10;
        
        [Tooltip("是否在报告中包含成功测试的列表")]
        public bool includeSuccessfulTests = true;

        [Header("Unity Test Runner集成")]
        [Tooltip("是否隐藏非白名单程序集")]
        public bool hideNonWhitelistedAssemblies = true;
        
        [Tooltip("启动时是否自动打开Test Runner")]
        public bool autoOpenTestRunner = false;

        /// <summary>检查程序集是否在白名单中</summary>
        public bool IsAssemblyAllowed(string assemblyName)
        {
            if (!enableAssemblyFiltering)
                return true;

            foreach (var allowed in allowedAssemblies)
            {
                if (assemblyName.Contains(allowed))
                    return true;
            }
            return false;
        }

        /// <summary>获取默认配置实例</summary>
        public static TestConfiguration GetDefault()
        {
            TestConfiguration config = null;

#if UNITY_EDITOR
            // 首先尝试从项目根目录加载
            var rootConfigPath = "Assets/Extensions/SceneBlueprint/Tests/SceneBlueprintTestConfig.asset";
            config = UnityEditor.AssetDatabase.LoadAssetAtPath<TestConfiguration>(rootConfigPath);
            
            if (config != null)
            {
                Debug.Log($"[SceneBlueprint] 已加载测试配置: {rootConfigPath}");
                return config;
            }

            // 然后尝试从Resources目录加载
            config = Resources.Load<TestConfiguration>("SceneBlueprintTestConfig");
            if (config != null)
            {
                Debug.Log("[SceneBlueprint] 从Resources目录加载测试配置");
                return config;
            }

            // 查找项目中任何名为SceneBlueprintTestConfig的配置文件
            var guids = UnityEditor.AssetDatabase.FindAssets("SceneBlueprintTestConfig t:TestConfiguration");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<TestConfiguration>(path);
                if (config != null)
                {
                    Debug.Log($"[SceneBlueprint] 在项目中找到测试配置: {path}");
                    return config;
                }
            }
#endif

            // 如果都没找到，创建默认配置
            config = CreateInstance<TestConfiguration>();
            config.allowedAssemblies = new List<string> { "SceneBlueprint.Tests" };
            config.enableAssemblyFiltering = true;
            config.autoGenerateReportOnFailure = true;
            config.verboseLogging = true;
            config.maxStackTraceLines = 10;
            config.includeSuccessfulTests = true;
            config.hideNonWhitelistedAssemblies = true;
            config.autoOpenTestRunner = false;
            
            Debug.LogWarning("[SceneBlueprint] 未找到测试配置文件，使用默认设置。已创建的配置文件请确保命名为'SceneBlueprintTestConfig'");
            return config;
        }

        /// <summary>保存配置到Resources目录</summary>
        public void SaveToResources()
        {
#if UNITY_EDITOR
            var resourcesPath = "Assets/Extensions/SceneBlueprint/Tests/Resources";
            if (!System.IO.Directory.Exists(resourcesPath))
            {
                System.IO.Directory.CreateDirectory(resourcesPath);
                UnityEditor.AssetDatabase.Refresh();
            }
            
            var assetPath = $"{resourcesPath}/SceneBlueprintTestConfig.asset";
            UnityEditor.AssetDatabase.CreateAsset(this, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            
            Debug.Log($"[SceneBlueprint] 测试配置已保存到: {assetPath}");
#endif
        }

        void OnValidate()
        {
            // 确保至少有一个允许的程序集
            if (allowedAssemblies.Count == 0)
            {
                allowedAssemblies.Add("SceneBlueprint.Tests");
            }
            
            // 限制堆栈跟踪行数
            maxStackTraceLines = Mathf.Clamp(maxStackTraceLines, 5, 50);
        }
    }
}
#endif
