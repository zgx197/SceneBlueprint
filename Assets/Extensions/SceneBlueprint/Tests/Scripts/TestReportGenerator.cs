#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace SceneBlueprint.Tests.Scripts
{
    /// <summary>
    /// 测试报告生成器 - 收集Unity Test Runner的真实测试结果并生成详细报告
    /// </summary>
    public static class TestReportGenerator
    {
        /// <summary>测试执行结果数据</summary>
        public class TestExecutionReport
        {
            public DateTime ExecutionTime { get; set; } = DateTime.Now;
            public List<TestCaseResult> TestCases { get; set; } = new List<TestCaseResult>();
            public TestSummary Summary { get; set; } = new TestSummary();
        }

        /// <summary>单个测试用例结果</summary>
        public class TestCaseResult
        {
            public string TestName { get; set; } = "";
            public string ClassName { get; set; } = "";
            public string Namespace { get; set; } = "";
            public TestStatus Status { get; set; }
            public double Duration { get; set; }
            public string? ErrorMessage { get; set; }
            public string? StackTrace { get; set; }
            public string? Output { get; set; }
        }

        /// <summary>测试总结</summary>
        public class TestSummary
        {
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public double TotalDuration { get; set; }
            public double SuccessRate => TotalTests > 0 ? (PassedTests * 100.0 / TotalTests) : 0;
        }

        private static TestExecutionReport? _lastReport;
        private static TestResultCollector? _currentCollector;

        /// <summary>测试结果收集器</summary>
        private class TestResultCollector : ICallbacks
        {
            public TestExecutionReport Report { get; } = new TestExecutionReport();
            public bool IsComplete { get; private set; }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Report.Summary.TotalTests = CountTests(testsToRun);
                Debug.Log($"🧪 开始执行测试报告收集，总测试数: {Report.Summary.TotalTests}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Report.Summary.TotalDuration = result.Duration;
                IsComplete = true;
                
                Debug.Log($"📊 测试报告收集完成");
                Debug.Log($"   ✅ 通过: {Report.Summary.PassedTests}");
                Debug.Log($"   ❌ 失败: {Report.Summary.FailedTests}");
                Debug.Log($"   ⏭️ 跳过: {Report.Summary.SkippedTests}");
                Debug.Log($"   ⏱️ 耗时: {result.Duration:F2}秒");
            }

            public void TestStarted(ITestAdaptor test)
            {
                // 可选：记录测试开始
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                var testCase = new TestCaseResult
                {
                    TestName = result.Test.Name,
                    ClassName = ExtractClassName(result.Test.FullName),
                    Namespace = ExtractNamespace(result.Test.FullName),
                    Status = result.TestStatus,
                    Duration = result.Duration,
                    ErrorMessage = result.Message,
                    StackTrace = result.StackTrace,
                    Output = result.Output
                };

                Report.TestCases.Add(testCase);

                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        Report.Summary.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        Report.Summary.FailedTests++;
                        Debug.LogError($"❌ 测试失败: {result.Test.Name}");
                        if (!string.IsNullOrEmpty(result.Message))
                        {
                            Debug.LogError($"   错误: {result.Message}");
                        }
                        break;
                    case TestStatus.Skipped:
                        Report.Summary.SkippedTests++;
                        break;
                }
            }

            private int CountTests(ITestAdaptor test)
            {
                if (!test.HasChildren)
                    return test.IsSuite ? 0 : 1;
                
                return test.Children.Sum(child => CountTests(child));
            }

            private string ExtractClassName(string fullName)
            {
                var parts = fullName.Split('.');
                return parts.Length >= 2 ? parts[parts.Length - 2] : fullName;
            }

            private string ExtractNamespace(string fullName)
            {
                var lastDotIndex = fullName.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    var secondLastDotIndex = fullName.LastIndexOf('.', lastDotIndex - 1);
                    if (secondLastDotIndex > 0)
                    {
                        return fullName.Substring(0, secondLastDotIndex);
                    }
                }
                return "";
            }
        }

        /// <summary>执行测试并生成详细报告</summary>
        public static TestExecutionReport ExecuteTestsAndGenerateReport(TestMode testMode = TestMode.EditMode, string? filter = null)
        {
            Debug.Log("🚀 启动SceneBlueprint测试执行和报告生成...");
            Debug.Log("🎯 只运行SceneBlueprint相关的测试，忽略其他Unity包测试");

            _currentCollector = new TestResultCollector();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            
            var runOptions = new ExecutionSettings();
            
            // 创建SceneBlueprint专用过滤器
            var sceneBlueprintFilter = new Filter 
            { 
                testMode = testMode,
                assemblyNames = new[] { "SceneBlueprint.Tests" }, // 只运行SceneBlueprint测试程序集
                categoryNames = new string[0] // 不按分类过滤
            };
            
            if (!string.IsNullOrEmpty(filter))
            {
                // 如果有额外的过滤器，组合使用
                sceneBlueprintFilter.testNames = new[] { filter };
            }
            
            runOptions.filters = new[] { sceneBlueprintFilter };

            testRunnerApi.RegisterCallbacks(_currentCollector);
            testRunnerApi.Execute(runOptions);

            // 等待测试完成
            var timeout = 0;
            while (!_currentCollector.IsComplete && timeout < 600) // 60秒超时
            {
                System.Threading.Thread.Sleep(100);
                timeout++;
                
                if (timeout % 50 == 0) // 每5秒输出一次进度
                {
                    Debug.Log($"⏳ 测试执行中... ({timeout / 10}秒)");
                }
            }

            testRunnerApi.UnregisterCallbacks(_currentCollector);

            if (timeout >= 600)
            {
                Debug.LogWarning("⚠️ 测试执行超时，可能部分结果不完整");
            }

            _lastReport = _currentCollector.Report;
            return _lastReport;
        }

        /// <summary>生成适合AI分析的文本报告</summary>
        public static string GenerateAIFriendlyReport(TestExecutionReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("SCENEBLUEPRINT 测试执行报告");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine($"执行时间: {report.ExecutionTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 测试总结
            sb.AppendLine("📊 测试总结");
            sb.AppendLine("-".PadRight(40, '-'));
            sb.AppendLine($"总测试数: {report.Summary.TotalTests}");
            sb.AppendLine($"通过测试: {report.Summary.PassedTests} ✅");
            sb.AppendLine($"失败测试: {report.Summary.FailedTests} ❌");
            sb.AppendLine($"跳过测试: {report.Summary.SkippedTests} ⏭️");
            sb.AppendLine($"成功率: {report.Summary.SuccessRate:F1}%");
            sb.AppendLine($"总耗时: {report.Summary.TotalDuration:F2}秒");
            sb.AppendLine();

            // 失败测试详情
            var failedTests = report.TestCases.Where(t => t.Status == TestStatus.Failed).ToList();
            if (failedTests.Any())
            {
                sb.AppendLine("❌ 失败测试详情");
                sb.AppendLine("-".PadRight(40, '-'));
                
                for (int i = 0; i < failedTests.Count; i++)
                {
                    var test = failedTests[i];
                    sb.AppendLine($"\n[{i + 1}] {test.TestName}");
                    sb.AppendLine($"    类名: {test.ClassName}");
                    sb.AppendLine($"    命名空间: {test.Namespace}");
                    sb.AppendLine($"    耗时: {test.Duration:F3}秒");
                    
                    if (!string.IsNullOrEmpty(test.ErrorMessage))
                    {
                        sb.AppendLine($"    错误信息:");
                        sb.AppendLine($"    {test.ErrorMessage}");
                    }
                    
                    if (!string.IsNullOrEmpty(test.StackTrace))
                    {
                        sb.AppendLine($"    堆栈跟踪:");
                        var stackLines = test.StackTrace.Split('\n');
                        foreach (var line in stackLines.Take(5)) // 只显示前5行堆栈
                        {
                            sb.AppendLine($"    {line.Trim()}");
                        }
                        if (stackLines.Length > 5)
                        {
                            sb.AppendLine($"    ... (省略剩余 {stackLines.Length - 5} 行)");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(test.Output))
                    {
                        sb.AppendLine($"    测试输出:");
                        sb.AppendLine($"    {test.Output}");
                    }
                }
            }

            // 成功测试列表
            var passedTests = report.TestCases.Where(t => t.Status == TestStatus.Passed).ToList();
            if (passedTests.Any())
            {
                sb.AppendLine("\n✅ 成功测试列表");
                sb.AppendLine("-".PadRight(40, '-'));
                
                foreach (var test in passedTests)
                {
                    sb.AppendLine($"• {test.TestName} ({test.Duration:F3}s)");
                }
            }

            // 跳过测试列表
            var skippedTests = report.TestCases.Where(t => t.Status == TestStatus.Skipped).ToList();
            if (skippedTests.Any())
            {
                sb.AppendLine("\n⏭️ 跳过测试列表");
                sb.AppendLine("-".PadRight(40, '-'));
                
                foreach (var test in skippedTests)
                {
                    sb.AppendLine($"• {test.TestName}");
                }
            }

            // 测试类统计
            sb.AppendLine("\n📁 测试类统计");
            sb.AppendLine("-".PadRight(40, '-'));
            
            var classSummary = report.TestCases
                .GroupBy(t => t.ClassName)
                .Select(g => new
                {
                    ClassName = g.Key,
                    Total = g.Count(),
                    Passed = g.Count(t => t.Status == TestStatus.Passed),
                    Failed = g.Count(t => t.Status == TestStatus.Failed),
                    Skipped = g.Count(t => t.Status == TestStatus.Skipped)
                })
                .OrderBy(x => x.ClassName);

            foreach (var cls in classSummary)
            {
                sb.AppendLine($"• {cls.ClassName}: {cls.Total}个测试 " +
                             $"(✅{cls.Passed} ❌{cls.Failed} ⏭️{cls.Skipped})");
            }

            sb.AppendLine();
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("报告结束 - 可复制此内容交给AI进行分析");
            sb.AppendLine("=".PadRight(80, '='));

            return sb.ToString();
        }

        /// <summary>获取上一次的测试报告</summary>
        public static TestExecutionReport? GetLastReport()
        {
            return _lastReport;
        }

        /// <summary>将报告保存到文件</summary>
        public static void SaveReportToFile(TestExecutionReport report, string fileName)
        {
            var reportContent = GenerateAIFriendlyReport(report);
            var path = EditorUtility.SaveFilePanel("保存测试报告", "", fileName, "txt");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, reportContent, Encoding.UTF8);
                Debug.Log($"📄 测试报告已保存到: {path}");
                EditorUtility.DisplayDialog("报告已保存", 
                    $"测试报告已保存到:\n{path}\n\n可以复制文件内容交给AI进行分析。", 
                    "确定");
            }
        }

        /// <summary>将报告复制到剪贴板</summary>
        public static void CopyReportToClipboard(TestExecutionReport report)
        {
            var reportContent = GenerateAIFriendlyReport(report);
            EditorGUIUtility.systemCopyBuffer = reportContent;
            
            Debug.Log("📋 测试报告已复制到剪贴板");
            EditorUtility.DisplayDialog("报告已复制", 
                "完整的测试报告已复制到剪贴板！\n\n" +
                "你现在可以直接粘贴到AI聊天窗口进行分析。\n\n" +
                "报告包含:\n" +
                "• 测试总结统计\n" +
                "• 详细的失败信息\n" +
                "• 堆栈跟踪信息\n" +
                "• 测试类分布统计", 
                "确定");
        }
    }
}
#endif
