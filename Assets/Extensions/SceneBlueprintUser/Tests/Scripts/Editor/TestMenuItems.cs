#nullable enable
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Tests.Scripts.Editor
{
    /// <summary>
    /// SceneBlueprint 精简测试菜单 - 只提供3个核心功能
    /// <para>
    /// 🚀 运行测试(自动生成报告) | ⚙️ 测试配置 | ❓ 帮助指南
    /// </para>
    /// </summary>
    public static class TestMenuItems
    {
        private const string MenuRoot = "SceneBlueprint/Tests/";
        private const int MenuPriority = 1000;

        // ═══ 核心功能菜单（共3项）═══

        [MenuItem(MenuRoot + "🚀 运行测试", false, MenuPriority)]
        public static void RunTests()
        {
            Debug.Log("\n=== 🚀 SceneBlueprint 测试执行 ===");
            
            try
            {
                // 先分析当前项目的测试情况
                SceneBlueprintTestRunner.AnalyzeSceneBlueprintTests();
                
                // 显示运行选项
                int option = EditorUtility.DisplayDialogComplex(
                    "运行 SceneBlueprint 测试",
                    "选择测试运行方式：\n\n" +
                    "🎯 自动运行：直接运行SceneBlueprint.Tests程序集\n" +
                    "📖 手动运行：打开Test Runner并提供操作指导\n\n" +
                    "推荐使用自动运行，更快捷准确！",
                    "🎯 自动运行",      // 0
                    "📖 手动运行",      // 1  
                    "取消"             // 2
                );

                switch (option)
                {
                    case 0: // 自动运行
                        SceneBlueprintTestRunner.RunSceneBlueprintTests();
                        break;
                    case 1: // 手动运行
                        SceneBlueprintTestRunner.OpenTestRunnerWithGuidance();
                        break;
                    case 2: // 取消
                        Debug.Log("已取消测试运行");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 运行测试时出现异常: {ex.Message}");
                EditorUtility.DisplayDialog("测试执行失败", 
                    $"运行测试时出现异常:\n{ex.Message}\n\n请检查Console获取详细信息。", 
                    "确定");
            }
        }

        [MenuItem(MenuRoot + "⚙️ 测试配置", false, MenuPriority + 1)]
        public static void OpenTestConfiguration()
        {
            Debug.Log("\n=== ⚙️ SceneBlueprint 测试配置 ===");
            
            var config = TestConfiguration.GetDefault();
            
            Debug.Log($"当前配置状态:");
            Debug.Log($"   🎯 程序集过滤: {(config.enableAssemblyFiltering ? "启用" : "禁用")}");
            Debug.Log($"   📋 白名单程序集: {string.Join(", ", config.allowedAssemblies)}");
            Debug.Log($"   🔍 详细日志: {(config.verboseLogging ? "启用" : "禁用")}");
            Debug.Log($"   📄 自动生成报告: {(config.autoGenerateReportOnFailure ? "启用" : "禁用")}");
            Debug.Log("=======================================\n");
            
            string message = $"⚙️ 当前测试配置\n\n" +
                           $"🎯 程序集过滤: {(config.enableAssemblyFiltering ? "✅ 启用" : "❌ 禁用")}\n" +
                           $"📋 白名单程序集数: {config.allowedAssemblies.Count}\n" +
                           $"🔍 详细日志: {(config.verboseLogging ? "✅ 启用" : "❌ 禁用")}\n" +
                           $"📄 失败时自动报告: {(config.autoGenerateReportOnFailure ? "✅ 启用" : "❌ 禁用")}\n\n" +
                           $"💡 配置文件位置:\nAssets/Extensions/SceneBlueprintUser/Tests/Resources/";
            
            int option = EditorUtility.DisplayDialogComplex(
                "测试配置",
                message,
                "打开配置文件",     // 0
                "重置为默认",       // 1  
                "确定"             // 2
            );

            switch (option)
            {
                case 0: // 打开配置文件
                    var configAsset = Resources.Load<TestConfiguration>("SceneBlueprintTestConfig");
                    if (configAsset != null)
                    {
                        Selection.activeObject = configAsset;
                        EditorGUIUtility.PingObject(configAsset);
                        Debug.Log("📝 已选中测试配置文件，可在Inspector中修改设置");
                    }
                    else
                    {
                        Debug.Log("💡 配置文件不存在，将使用默认设置");
                        EditorUtility.DisplayDialog("配置文件不存在", 
                            "测试配置文件不存在，系统将使用默认设置。\n\n" +
                            "如需自定义配置，请在Project窗口中:\n" +
                            "右键 → Create → SceneBlueprint → Test Configuration", 
                            "确定");
                    }
                    break;
                case 1: // 重置为默认
                    var defaultConfig = TestConfiguration.CreateInstance<TestConfiguration>();
                    defaultConfig.SaveToResources();
                    Debug.Log("🔄 测试配置已重置为默认设置");
                    break;
                case 2: // 确定
                    Debug.Log("💡 如需修改配置，可使用菜单中的'打开配置文件'选项");
                    break;
            }
        }

        [MenuItem(MenuRoot + "❓ 帮助指南", false, MenuPriority + 2)]
        public static void ShowHelpGuide()
        {
            Debug.Log("\n=== ❓ SceneBlueprint 测试帮助指南 ===");
            Debug.Log("");
            Debug.Log("🚀 核心功能说明:");
            Debug.Log("   • 运行测试 - 智能运行SceneBlueprint专用测试，自动生成详细报告");
            Debug.Log("   • 测试配置 - 管理白名单程序集和测试设置");
            Debug.Log("   • 帮助指南 - 查看使用说明和常见问题");
            Debug.Log("");
            Debug.Log("🎯 白名单模式工作原理:");
            Debug.Log("   • 系统会自动识别SceneBlueprint.Tests程序集");
            Debug.Log("   • '自动运行'模式：直接执行白名单测试，忽略其他程序集");
            Debug.Log("   • '手动运行'模式：打开Test Runner并提供操作指导");
            Debug.Log("   • Unity Test Runner UI无法完全隐藏其他程序集（Unity限制）");
            Debug.Log("");
            Debug.Log("📂 测试目录结构:");
            Debug.Log("   📁 Tests/Unit/Core/     - 单元测试");
            Debug.Log("   📁 Tests/Integration/   - 集成测试");
            Debug.Log("   📁 Tests/E2E/          - 端到端测试");
            Debug.Log("");
            Debug.Log("🛠️ 可用工具类:");
            Debug.Log("   • TestDataBuilder - 快速创建测试数据");
            Debug.Log("   • AssertionExtensions - 增强的断言方法");
            Debug.Log("");
            Debug.Log("💡 常见问题:");
            Debug.Log("   Q: 为什么Test Runner还显示其他测试？ A: Unity技术限制，但不影响实际执行");
            Debug.Log("   Q: 推荐哪种运行方式？ A: 自动运行更准确，手动运行需注意只选择SceneBlueprint.Tests");
            Debug.Log("   Q: 测试失败如何分析？ A: 使用'生成详细报告'获取完整信息");
            Debug.Log("   Q: 配置文件在哪里？ A: Assets/Extensions/SceneBlueprintUser/Tests/SceneBlueprintTestConfig.asset");
            Debug.Log("==========================================\n");
            
            string message = "❓ SceneBlueprint 测试帮助\n\n" +
                           "🚀 3个核心功能:\n" +
                           "• 运行测试 - 智能执行SceneBlueprint测试，自动生成报告\n" +
                           "• 测试配置 - 白名单和设置管理\n" +
                           "• 帮助指南 - 使用说明\n\n" +
                           "🎯 白名单模式说明:\n" +
                           "• 自动运行：推荐方式，直接执行SceneBlueprint测试+自动报告\n" +
                           "• 手动运行：打开Test Runner + 操作指导\n" +
                           "• Unity UI限制：无法完全隐藏其他程序集显示\n\n" +
                           "📚 学习资源:\n" +
                           "• PropertyBagTests_Example.cs (最佳实践)\n" +
                           "• TestDataBuilder + AssertionExtensions (工具类)\n\n" +
                           "💡 推荐工作流:\n" +
                           "1. 运行测试(自动模式) → 2. 测试完成自动生成报告 → 3. 复制报告给AI分析";
            
            EditorUtility.DisplayDialog("SceneBlueprint 测试帮助", message, "确定");
        }
    }
}
#endif
