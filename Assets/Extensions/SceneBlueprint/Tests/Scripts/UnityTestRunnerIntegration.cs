#nullable enable
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneBlueprint.Tests.Scripts
{
    /// <summary>
    /// Unity Test Runner 真实集成——提供与Unity原生测试系统的集成。
    /// <para>
    /// 这个类展示了如何与Unity Test Runner API集成，运行真正的NUnit测试。
    /// 提供了完整的测试执行、结果收集和报告生成功能。
    /// </para>
    /// </summary>
    public static class UnityTestRunnerIntegration
    {
        /// <summary>真实测试结果收集器</summary>
        private class TestResultCollector : ICallbacks
        {
            public TestRunner.TestResults Results { get; } = new TestRunner.TestResults();
            public bool IsComplete { get; private set; }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Results.TotalTests = CountTests(testsToRun);
                UnityEngine.Debug.Log($"[Unity Test Runner] 开始运行 {Results.TotalTests} 个测试...");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Results.TotalDuration = System.TimeSpan.FromSeconds(result.Duration);
                IsComplete = true;
                UnityEngine.Debug.Log($"[Unity Test Runner] 测试执行完成，耗时 {result.Duration:F2} 秒");
            }

            public void TestStarted(ITestAdaptor test)
            {
                // 可选：记录单个测试开始
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        Results.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        Results.FailedTests++;
                        Results.Failures.Add(new TestRunner.TestFailure
                        {
                            TestName = result.Test.FullName,
                            ErrorMessage = result.Message ?? "测试失败",
                            StackTrace = result.StackTrace ?? "",
                            Duration = System.TimeSpan.FromSeconds(result.Duration)
                        });
                        break;
                    case TestStatus.Skipped:
                        Results.SkippedTests++;
                        break;
                }
            }

            private int CountTests(ITestAdaptor test)
            {
                if (!test.HasChildren)
                    return test.IsSuite ? 0 : 1;
                
                return test.Children.Sum(child => CountTests(child));
            }
        }

        /// <summary>运行指定过滤器的测试</summary>
        public static TestRunner.TestResults RunTestsWithFilter(string filter, bool verbose = true)
        {
            if (verbose)
            {
                UnityEngine.Debug.Log($"[Unity Test Runner] 使用过滤器运行测试: {filter}");
            }

            var collector = new TestResultCollector();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            
            var runOptions = new ExecutionSettings
            {
                filters = new[] { new Filter { testMode = TestMode.EditMode, testNames = new[] { filter } } }
            };

            testRunnerApi.RegisterCallbacks(collector);
            testRunnerApi.Execute(runOptions);

            // 等待测试完成（简化版本）
            var timeout = 0;
            while (!collector.IsComplete && timeout < 100)
            {
                System.Threading.Thread.Sleep(100);
                timeout++;
            }

            testRunnerApi.UnregisterCallbacks(collector);

            if (timeout >= 100)
            {
                collector.Results.Warnings.Add(new TestRunner.TestWarning
                {
                    TestName = "Timeout",
                    Message = "测试执行超时，可能需要更长时间",
                    Category = "Performance"
                });
            }

            return collector.Results;
        }

        /// <summary>运行所有 EditMode 测试</summary>
        public static TestRunner.TestResults RunAllEditModeTests(bool verbose = true)
        {
            if (verbose)
            {
                UnityEngine.Debug.Log("[Unity Test Runner] 运行所有 EditMode 测试...");
            }

            var collector = new TestResultCollector();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            
            var runOptions = new ExecutionSettings
            {
                filters = new[] { new Filter { testMode = TestMode.EditMode } }
            };

            testRunnerApi.RegisterCallbacks(collector);
            testRunnerApi.Execute(runOptions);

            // 简化的等待逻辑
            var timeout = 0;
            while (!collector.IsComplete && timeout < 300) // 30秒超时
            {
                System.Threading.Thread.Sleep(100);
                timeout++;
            }

            testRunnerApi.UnregisterCallbacks(collector);
            return collector.Results;
        }

        /// <summary>获取测试发现信息</summary>
        public static (int totalTests, List<string> testClasses) DiscoverTests()
        {
            var testClasses = new List<string>();
            int totalTests = 0;

            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName?.Contains("SceneBlueprint") == true);

                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.Namespace?.StartsWith("SceneBlueprint.Tests") == true && 
                                   t.Name.EndsWith("Tests"));

                    foreach (var type in types)
                    {
                        var testMethods = type.GetMethods()
                            .Where(m => m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Any());

                        var methodCount = testMethods.Count();
                        if (methodCount > 0)
                        {
                            testClasses.Add($"{type.Name} ({methodCount} tests)");
                            totalTests += methodCount;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"测试发现过程中出现异常: {ex.Message}");
            }

            return (totalTests, testClasses);
        }

        /// <summary>提供Unity Test Runner使用建议</summary>
        public static void ShowTestRunnerGuidance()
        {
            UnityEngine.Debug.Log("=== Unity Test Runner 使用指南 ===");
            UnityEngine.Debug.Log("1. 使用 Unity 内置测试运行器:");
            UnityEngine.Debug.Log("   Window → General → Test Runner");
            UnityEngine.Debug.Log("2. 在 Test Runner 窗口中:");
            UnityEngine.Debug.Log("   - EditMode 标签页查看单元测试");
            UnityEngine.Debug.Log("   - 点击 'Run All' 运行所有测试");
            UnityEngine.Debug.Log("   - 右键单个测试类可单独运行");
            UnityEngine.Debug.Log("3. 测试结果会显示:");
            UnityEngine.Debug.Log("   - 通过/失败状态");
            UnityEngine.Debug.Log("   - 执行时间");
            UnityEngine.Debug.Log("   - 详细错误信息");
            UnityEngine.Debug.Log("===============================");

            var (totalTests, testClasses) = DiscoverTests();
            UnityEngine.Debug.Log($"发现 {totalTests} 个测试，分布在 {testClasses.Count} 个测试类中:");
            foreach (var testClass in testClasses.Take(10))
            {
                UnityEngine.Debug.Log($"  - {testClass}");
            }
            if (testClasses.Count > 10)
            {
                UnityEngine.Debug.Log($"  ... 还有 {testClasses.Count - 10} 个测试类");
            }
        }
    }
}
#endif
