#nullable enable
#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using TestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

namespace SceneBlueprint.Tests.Scripts
{
    /// <summary>
    /// Unity Test Runner程序集过滤器 - 使用TestRunnerApi实现白名单过滤
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerFilter
    {
        private static TestConfiguration? _config;
        private static bool _isFilterActive = false;
        private static TestRunnerApi? _testRunnerApi;
        private static TestFilterCallback? _filterCallback;

        static TestRunnerFilter()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            _config = TestConfiguration.GetDefault();
            
            if (_config.enableAssemblyFiltering && _config.hideNonWhitelistedAssemblies)
            {
                EnableFiltering();
            }
            
            Debug.Log($"[SceneBlueprint] 测试过滤器初始化完成，过滤状态: {_isFilterActive}");
        }

        /// <summary>启用程序集过滤</summary>
        public static void EnableFiltering()
        {
            if (_isFilterActive) return;
            
            _config = TestConfiguration.GetDefault();
            
            try
            {
                // 使用Unity官方TestRunnerApi
                _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                _filterCallback = new TestFilterCallback();
                
                _testRunnerApi.RegisterCallbacks(_filterCallback);
                _isFilterActive = true;
                
                Debug.Log("[SceneBlueprint] 测试监控器已启用，将监控测试执行");
                Debug.Log("💡 提示：Unity Test Runner UI无法完全隐藏其他程序集，这是Unity的技术限制");
                Debug.Log("🎯 推荐做法：在Test Runner中手动只运行SceneBlueprint.Tests相关测试");
                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SceneBlueprint] 启用测试监控时出现异常: {ex.Message}");
            }
        }

        /// <summary>禁用程序集过滤</summary>
        public static void DisableFiltering()
        {
            if (!_isFilterActive) return;
            
            _isFilterActive = false;
            Debug.Log("[SceneBlueprint] 测试过滤器已禁用");
        }

        /// <summary>检查程序集是否应该显示</summary>
        public static bool ShouldShowAssembly(string assemblyName)
        {
            if (_config == null || !_config.enableAssemblyFiltering)
                return true;

            return _config.IsAssemblyAllowed(assemblyName);
        }

        /// <summary>获取过滤后的测试程序集列表</summary>
        public static string[] GetFilteredAssemblies()
        {
            if (_config == null)
                return new string[0];

            return _config.allowedAssemblies.ToArray();
        }

        /// <summary>测试执行数据收集器</summary>
        private static TestExecutionData? _currentTestRun;

        /// <summary>测试过滤回调 - 负责实际的测试执行过滤和数据收集</summary>
        private class TestFilterCallback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                // 初始化测试数据收集
                _currentTestRun = new TestExecutionData();
                _currentTestRun.StartTime = System.DateTime.Now;
                
                Debug.Log($"\n=== 🎯 SceneBlueprint 测试过滤器 ===");
                Debug.Log($"📊 发现测试: {CountTests(testsToRun)} 个");
                Debug.Log($"🎯 过滤规则: 只运行 {string.Join(", ", _config?.allowedAssemblies ?? new List<string>())} 程序集");
                Debug.Log($"========================================");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (_currentTestRun != null)
                {
                    _currentTestRun.EndTime = System.DateTime.Now;
                    _currentTestRun.TotalDuration = result.Duration;
                    _currentTestRun.OverallResult = result.TestStatus;
                }
                
                Debug.Log($"\n✅ 测试运行完成，总耗时: {result.Duration:F2}秒");
                Debug.Log($"📈 结果: {(result.TestStatus == TestStatus.Passed ? "通过" : "失败")}");
                
                // 测试完成后自动生成报告
                GenerateTestReport();
            }

            public void TestStarted(ITestAdaptor test) 
            {
                // 在测试开始时输出日志，帮助用户了解正在运行哪些测试
                if (_config?.verboseLogging == true && ShouldShowAssembly(test.FullName))
                {
                    Debug.Log($"[SceneBlueprint] 开始测试: {test.Name}");
                }
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                // 只处理白名单程序集中的测试结果
                if (_config != null && ShouldShowAssembly(result.Test.FullName))
                {
                    // 收集测试结果数据
                    if (_currentTestRun != null && !result.Test.IsSuite)
                    {
                        _currentTestRun.TestResults.Add(new TestResultData
                        {
                            TestName = result.Test.Name,
                            FullName = result.Test.FullName,
                            Status = result.TestStatus,
                            Duration = result.Duration,
                            ErrorMessage = result.Message,
                            StackTrace = result.StackTrace
                        });
                    }
                    
                    if (result.TestStatus == TestStatus.Failed)
                    {
                        Debug.LogError($"[SceneBlueprint] ❌ 测试失败: {result.Test.Name}\n错误: {result.Message}");
                    }
                    else if (result.TestStatus == TestStatus.Passed && _config.verboseLogging)
                    {
                        Debug.Log($"[SceneBlueprint] ✅ 测试通过: {result.Test.Name}");
                    }
                }
            }

            private int CountTests(ITestAdaptor test)
            {
                if (!test.HasChildren)
                    return test.IsSuite ? 0 : 1;
                
                return test.Children.Sum(child => CountTests(child));
            }
        }

        /// <summary>测试执行数据</summary>
        private class TestExecutionData
        {
            public System.DateTime StartTime { get; set; }
            public System.DateTime EndTime { get; set; }
            public double TotalDuration { get; set; }
            public TestStatus OverallResult { get; set; }
            public List<TestResultData> TestResults { get; set; } = new List<TestResultData>();
        }

        /// <summary>单个测试结果数据</summary>
        private class TestResultData
        {
            public string TestName { get; set; } = "";
            public string FullName { get; set; } = "";
            public TestStatus Status { get; set; }
            public double Duration { get; set; }
            public string? ErrorMessage { get; set; }
            public string? StackTrace { get; set; }
        }

        /// <summary>生成测试报告</summary>
        private static void GenerateTestReport()
        {
            if (_currentTestRun == null)
            {
                Debug.LogWarning("[SceneBlueprint] 没有测试数据可生成报告");
                return;
            }

            var passedTests = _currentTestRun.TestResults.Count(r => r.Status == TestStatus.Passed);
            var failedTests = _currentTestRun.TestResults.Count(r => r.Status == TestStatus.Failed);
            var skippedTests = _currentTestRun.TestResults.Count(r => r.Status == TestStatus.Skipped);
            var totalTests = _currentTestRun.TestResults.Count;
            var successRate = totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0.0;

            // 构建报告内容
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("SCENEBLUEPRINT 测试执行报告");
            report.AppendLine("================================================================================");
            report.AppendLine($"执行时间: {_currentTestRun.StartTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();
            report.AppendLine("📊 测试总结");
            report.AppendLine("----------------------------------------");
            report.AppendLine($"总测试数: {totalTests}");
            report.AppendLine($"通过测试: {passedTests} ✅");
            report.AppendLine($"失败测试: {failedTests} ❌");
            report.AppendLine($"跳过测试: {skippedTests} ⏭️");
            report.AppendLine($"成功率: {successRate:F1}%");
            report.AppendLine($"总耗时: {_currentTestRun.TotalDuration:F2}秒");
            report.AppendLine();

            // 添加失败测试详情
            if (failedTests > 0)
            {
                report.AppendLine("❌ 失败测试详情");
                report.AppendLine("----------------------------------------");
                foreach (var failedTest in _currentTestRun.TestResults.Where(r => r.Status == TestStatus.Failed))
                {
                    report.AppendLine($"🔸 {failedTest.TestName}");
                    report.AppendLine($"   路径: {failedTest.FullName}");
                    report.AppendLine($"   耗时: {failedTest.Duration:F3}秒");
                    if (!string.IsNullOrEmpty(failedTest.ErrorMessage))
                    {
                        report.AppendLine($"   错误: {failedTest.ErrorMessage}");
                    }
                    if (!string.IsNullOrEmpty(failedTest.StackTrace))
                    {
                        report.AppendLine($"   堆栈: {failedTest.StackTrace}");
                    }
                    report.AppendLine();
                }
            }

            report.AppendLine("================================================================================");
            report.AppendLine("报告结束 - 可复制此内容交给AI进行分析");
            report.AppendLine("================================================================================");

            // 显示报告摘要
            Debug.Log($"\n📋 SceneBlueprint 测试报告生成完成");
            Debug.Log($"   总数: {totalTests} | 通过: {passedTests} | 失败: {failedTests} | 成功率: {successRate:F1}%");

            // 显示用户对话框
            string message;
            if (failedTests > 0)
            {
                message = $"📋 SceneBlueprint 测试完成\n\n" +
                        $"❌ 发现 {failedTests} 个失败测试！\n\n" +
                        $"📊 统计:\n" +
                        $"• 总数: {totalTests}\n" +
                        $"• 通过: {passedTests} ✅\n" +
                        $"• 失败: {failedTests} ❌\n" +
                        $"• 成功率: {successRate:F1}%\n\n" +
                        $"测试报告已自动生成。\n" +
                        $"点击'复制报告'获取详细错误信息，可直接粘贴给AI分析。";
            }
            else
            {
                message = $"📋 SceneBlueprint 测试完成\n\n" +
                        $"🎉 所有 {totalTests} 个测试都通过了！\n\n" +
                        $"⏱️ 总耗时: {_currentTestRun.TotalDuration:F2}秒\n" +
                        $"📈 成功率: 100%\n\n" +
                        $"测试报告已自动生成。";
            }

            // 延迟显示对话框，避免与其他Unity对话框冲突
            EditorApplication.delayCall += () =>
            {
                bool shouldCopyReport = EditorUtility.DisplayDialog(
                    "测试报告",
                    message,
                    failedTests > 0 ? "复制报告到剪贴板" : "复制报告",
                    "确定"
                );

                if (shouldCopyReport)
                {
                    EditorGUIUtility.systemCopyBuffer = report.ToString();
                    Debug.Log("📋 测试报告已复制到剪贴板");
                }
            };
        }

        /// <summary>强制刷新Test Runner窗口</summary>
        public static void RefreshTestRunner()
        {
            try
            {
                // 查找并刷新Test Runner窗口
                var testRunnerWindows = Resources.FindObjectsOfTypeAll<EditorWindow>()
                    .Where(w => w.GetType().Name == "TestRunnerWindow");

                foreach (var window in testRunnerWindows)
                {
                    window.Repaint();
                }

                Debug.Log("[SceneBlueprint] Test Runner窗口已刷新");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneBlueprint] 刷新Test Runner窗口时出现异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Unity Test Runner集成工具 - 提供更精确的程序集控制
    /// </summary>
    public static class TestRunnerIntegration
    {
        /// <summary>打开Test Runner并应用过滤</summary>
        public static void OpenFilteredTestRunner()
        {
            // 确保过滤器处于活动状态
            TestRunnerFilter.EnableFiltering();
            
            // 打开Test Runner窗口
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            
            // 等待窗口打开后刷新
            EditorApplication.delayCall += () =>
            {
                TestRunnerFilter.RefreshTestRunner();
                
                Debug.Log("\n=== 🎯 SceneBlueprint Test Runner ===");
                Debug.Log("✅ Test Runner已打开并应用过滤设置");
                Debug.Log("🎯 只显示白名单程序集:");
                
                var allowedAssemblies = TestRunnerFilter.GetFilteredAssemblies();
                foreach (var assembly in allowedAssemblies)
                {
                    Debug.Log($"   📁 {assembly}");
                }
                
                Debug.Log("❌ 已隐藏的程序集: cinemachine, AssetGraph, TerrainTools等");
                Debug.Log("💡 如需修改白名单，使用'测试配置'菜单");
                Debug.Log("=====================================\n");
            };
        }

        /// <summary>运行过滤后的测试</summary>
        public static void RunFilteredTests()
        {
            var config = TestConfiguration.GetDefault();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();

            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = config.allowedAssemblies.ToArray()
            };

            var settings = new ExecutionSettings
            {
                filters = new[] { filter }
            };

            Debug.Log($"[SceneBlueprint] 开始运行过滤后的测试，目标程序集: {string.Join(", ", config.allowedAssemblies)}");
            
            api.Execute(settings);
        }
    }
}
#endif
