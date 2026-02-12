#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SceneBlueprint.Tests.Scripts
{
    /// <summary>
    /// 代码覆盖率分析器——分析测试覆盖率并生成报告。
    /// <para>
    /// 提供基础的代码覆盖率统计功能，包括类级别和方法级别的覆盖率分析。
    /// 支持生成详细的覆盖率报告，帮助识别未测试的代码区域。
    /// </para>
    /// </summary>
    public static class CoverageAnalyzer
    {
        /// <summary>覆盖率报告</summary>
        public class CoverageReport
        {
            public DateTime GeneratedAt { get; set; } = DateTime.Now;
            public string ProjectName { get; set; } = "SceneBlueprint";
            public Dictionary<string, ModuleCoverage> Modules { get; set; } = new Dictionary<string, ModuleCoverage>();
            public OverallCoverage Overall { get; set; } = new OverallCoverage();
        }

        /// <summary>模块覆盖率</summary>
        public class ModuleCoverage
        {
            public string ModuleName { get; set; } = "";
            public int TotalClasses { get; set; }
            public int TestedClasses { get; set; }
            public int TotalMethods { get; set; }
            public int TestedMethods { get; set; }
            public List<string> UntestedClasses { get; set; } = new List<string>();
            public List<string> UntestedMethods { get; set; } = new List<string>();
            public double ClassCoveragePercentage => TotalClasses > 0 ? (double)TestedClasses / TotalClasses * 100 : 100;
            public double MethodCoveragePercentage => TotalMethods > 0 ? (double)TestedMethods / TotalMethods * 100 : 100;
        }

        /// <summary>整体覆盖率</summary>
        public class OverallCoverage
        {
            public int TotalClasses { get; set; }
            public int TestedClasses { get; set; }
            public int TotalMethods { get; set; }
            public int TestedMethods { get; set; }
            public double ClassCoveragePercentage => TotalClasses > 0 ? (double)TestedClasses / TotalClasses * 100 : 100;
            public double MethodCoveragePercentage => TotalMethods > 0 ? (double)TestedMethods / TotalMethods * 100 : 100;
        }

        // ─── 主要分析方法 ───

        /// <summary>分析 SceneBlueprint 项目的代码覆盖率</summary>
        public static CoverageReport AnalyzeCoverage()
        {
            Debug.Log("[CoverageAnalyzer] 开始分析代码覆盖率...");
            
            var report = new CoverageReport();
            
            // 分析 Core 模块
            var coreAssembly = GetCoreAssembly();
            if (coreAssembly != null)
            {
                report.Modules["Core"] = AnalyzeModuleCoverage("Core", coreAssembly);
            }
            
            // 分析 Actions 模块
            var actionsAssembly = GetActionsAssembly();
            if (actionsAssembly != null)
            {
                report.Modules["Actions"] = AnalyzeModuleCoverage("Actions", actionsAssembly);
            }
            
            // 计算整体覆盖率
            CalculateOverallCoverage(report);
            
            Debug.Log($"[CoverageAnalyzer] 分析完成。整体类覆盖率: {report.Overall.ClassCoveragePercentage:F1}%");
            return report;
        }

        /// <summary>分析单个模块的覆盖率</summary>
        private static ModuleCoverage AnalyzeModuleCoverage(string moduleName, Assembly assembly)
        {
            var coverage = new ModuleCoverage { ModuleName = moduleName };
            
            // 获取所有相关类型
            var allTypes = assembly.GetTypes()
                .Where(t => t.Namespace != null && 
                           t.Namespace.StartsWith("SceneBlueprint") && 
                           !t.Namespace.Contains("Tests"))
                .Where(t => t.IsPublic && !t.IsAbstract)
                .ToArray();
                
            coverage.TotalClasses = allTypes.Length;
            
            // 分析每个类的测试覆盖情况
            var testAssembly = GetTestAssembly();
            var testTypes = testAssembly?.GetTypes()
                .Where(t => t.Name.EndsWith("Tests"))
                .ToArray() ?? new Type[0];
            
            foreach (var type in allTypes)
            {
                var hasTests = HasTestsForType(type, testTypes);
                if (hasTests)
                {
                    coverage.TestedClasses++;
                }
                else
                {
                    coverage.UntestedClasses.Add(type.FullName ?? type.Name);
                }
                
                // 分析方法覆盖率
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => !m.IsSpecialName && m.DeclaringType == type) // 排除属性访问器和继承方法
                    .ToArray();
                    
                coverage.TotalMethods += methods.Length;
                
                foreach (var method in methods)
                {
                    var hasMethodTests = HasTestsForMethod(type, method, testTypes);
                    if (hasMethodTests)
                    {
                        coverage.TestedMethods++;
                    }
                    else
                    {
                        coverage.UntestedMethods.Add($"{type.Name}.{method.Name}");
                    }
                }
            }
            
            return coverage;
        }

        /// <summary>检查类型是否有对应的测试</summary>
        private static bool HasTestsForType(Type type, Type[] testTypes)
        {
            var expectedTestName = $"{type.Name}Tests";
            return testTypes.Any(t => t.Name == expectedTestName);
        }

        /// <summary>检查方法是否有对应的测试（简化版检查）</summary>
        private static bool HasTestsForMethod(Type type, MethodInfo method, Type[] testTypes)
        {
            var testType = testTypes.FirstOrDefault(t => t.Name == $"{type.Name}Tests");
            if (testType == null) return false;
            
            // 简单检查：是否有包含方法名的测试方法
            var testMethods = testType.GetMethods()
                .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)
                .ToArray();
                
            return testMethods.Any(tm => tm.Name.Contains(method.Name));
        }

        /// <summary>计算整体覆盖率</summary>
        private static void CalculateOverallCoverage(CoverageReport report)
        {
            foreach (var module in report.Modules.Values)
            {
                report.Overall.TotalClasses += module.TotalClasses;
                report.Overall.TestedClasses += module.TestedClasses;
                report.Overall.TotalMethods += module.TotalMethods;
                report.Overall.TestedMethods += module.TestedMethods;
            }
        }

        // ─── 程序集获取方法 ───

        private static Assembly? GetCoreAssembly()
        {
            try
            {
                return Assembly.LoadFrom("SceneBlueprint.Core");
            }
            catch
            {
                // 在 Unity 中可能需要不同的加载方式
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName?.Contains("SceneBlueprint.Core") == true);
            }
        }

        private static Assembly? GetActionsAssembly()
        {
            try
            {
                return Assembly.LoadFrom("SceneBlueprint.Actions");
            }
            catch
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName?.Contains("SceneBlueprint.Actions") == true);
            }
        }

        private static Assembly? GetTestAssembly()
        {
            try
            {
                return Assembly.LoadFrom("SceneBlueprint.Tests");
            }
            catch
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName?.Contains("SceneBlueprint.Tests") == true);
            }
        }

        // ─── 报告生成方法 ───

        /// <summary>生成 JSON 格式的覆盖率报告</summary>
        public static string GenerateJsonReport(CoverageReport report)
        {
            var reportData = new
            {
                generatedAt = report.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                projectName = report.ProjectName,
                overall = new
                {
                    totalClasses = report.Overall.TotalClasses,
                    testedClasses = report.Overall.TestedClasses,
                    totalMethods = report.Overall.TotalMethods,
                    testedMethods = report.Overall.TestedMethods,
                    classCoverage = Math.Round(report.Overall.ClassCoveragePercentage, 2),
                    methodCoverage = Math.Round(report.Overall.MethodCoveragePercentage, 2)
                },
                modules = report.Modules.Select(kvp => new
                {
                    name = kvp.Key,
                    totalClasses = kvp.Value.TotalClasses,
                    testedClasses = kvp.Value.TestedClasses,
                    totalMethods = kvp.Value.TotalMethods,
                    testedMethods = kvp.Value.TestedMethods,
                    classCoverage = Math.Round(kvp.Value.ClassCoveragePercentage, 2),
                    methodCoverage = Math.Round(kvp.Value.MethodCoveragePercentage, 2),
                    untestedClasses = kvp.Value.UntestedClasses.Take(10).ToArray(), // 限制数量避免过长
                    untestedMethods = kvp.Value.UntestedMethods.Take(20).ToArray()
                }).ToArray()
            };
            
            return JsonUtility.ToJson(reportData, true);
        }

        /// <summary>生成 Markdown 格式的覆盖率报告</summary>
        public static string GenerateMarkdownReport(CoverageReport report)
        {
            var markdown = new System.Text.StringBuilder();
            
            markdown.AppendLine("# SceneBlueprint 代码覆盖率报告");
            markdown.AppendLine($"**生成时间**: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine();
            
            // 整体覆盖率
            markdown.AppendLine("## 整体覆盖率");
            markdown.AppendLine("| 指标 | 数值 | 覆盖率 |");
            markdown.AppendLine("|------|------|--------|");
            markdown.AppendLine($"| 类覆盖 | {report.Overall.TestedClasses}/{report.Overall.TotalClasses} | {report.Overall.ClassCoveragePercentage:F1}% |");
            markdown.AppendLine($"| 方法覆盖 | {report.Overall.TestedMethods}/{report.Overall.TotalMethods} | {report.Overall.MethodCoveragePercentage:F1}% |");
            markdown.AppendLine();
            
            // 各模块详细覆盖率
            markdown.AppendLine("## 模块覆盖率详情");
            foreach (var kvp in report.Modules)
            {
                var module = kvp.Value;
                markdown.AppendLine($"### {kvp.Key} 模块");
                markdown.AppendLine($"- **类覆盖率**: {module.ClassCoveragePercentage:F1}% ({module.TestedClasses}/{module.TotalClasses})");
                markdown.AppendLine($"- **方法覆盖率**: {module.MethodCoveragePercentage:F1}% ({module.TestedMethods}/{module.TotalMethods})");
                
                if (module.UntestedClasses.Any())
                {
                    markdown.AppendLine($"- **未测试类 ({module.UntestedClasses.Count})**:");
                    foreach (var cls in module.UntestedClasses.Take(5))
                    {
                        markdown.AppendLine($"  - `{cls}`");
                    }
                    if (module.UntestedClasses.Count > 5)
                    {
                        markdown.AppendLine($"  - ... 还有 {module.UntestedClasses.Count - 5} 个");
                    }
                }
                markdown.AppendLine();
            }
            
            // 质量评估
            markdown.AppendLine("## 质量评估");
            var overallScore = (report.Overall.ClassCoveragePercentage + report.Overall.MethodCoveragePercentage) / 2;
            string quality = overallScore >= 90 ? "优秀 🎉" : 
                           overallScore >= 80 ? "良好 👍" :
                           overallScore >= 70 ? "一般 ⚠️" : "需改进 ❌";
            
            markdown.AppendLine($"**综合评分**: {overallScore:F1}% - {quality}");
            markdown.AppendLine();
            
            // 改进建议
            markdown.AppendLine("## 改进建议");
            if (report.Overall.ClassCoveragePercentage < 80)
            {
                markdown.AppendLine("- 🎯 优先为未测试的核心类添加单元测试");
            }
            if (report.Overall.MethodCoveragePercentage < 70)
            {
                markdown.AppendLine("- 🔍 增加方法级别的测试覆盖，特别是边界条件测试");
            }
            if (overallScore >= 90)
            {
                markdown.AppendLine("- ✨ 覆盖率优秀，继续保持！考虑添加更多集成测试和 E2E 测试");
            }
            
            return markdown.ToString();
        }

        /// <summary>生成简化的控制台报告</summary>
        public static void PrintConsoleSummary(CoverageReport report)
        {
            Debug.Log(new string('=', 50));
            Debug.Log("[代码覆盖率报告]");
            Debug.Log(new string('=', 50));
            Debug.Log($"整体类覆盖率: {report.Overall.ClassCoveragePercentage:F1}% ({report.Overall.TestedClasses}/{report.Overall.TotalClasses})");
            Debug.Log($"整体方法覆盖率: {report.Overall.MethodCoveragePercentage:F1}% ({report.Overall.TestedMethods}/{report.Overall.TotalMethods})");
            Debug.Log("");
            
            foreach (var kvp in report.Modules)
            {
                Debug.Log($"{kvp.Key}: 类 {kvp.Value.ClassCoveragePercentage:F1}%, 方法 {kvp.Value.MethodCoveragePercentage:F1}%");
            }
            Debug.Log(new string('=', 50));
        }

        // ─── 质量门禁检查 ───

        /// <summary>检查覆盖率是否满足质量门禁要求</summary>
        public static bool CheckCoverageQualityGate(CoverageReport report)
        {
            const double MinOverallCoverage = 80.0;
            const double MinCoreCoverage = 90.0;
            const double MinActionsCoverage = 85.0;
            
            bool overallPass = report.Overall.ClassCoveragePercentage >= MinOverallCoverage;
            bool corePass = !report.Modules.ContainsKey("Core") || 
                           report.Modules["Core"].ClassCoveragePercentage >= MinCoreCoverage;
            bool actionsPass = !report.Modules.ContainsKey("Actions") || 
                              report.Modules["Actions"].ClassCoveragePercentage >= MinActionsCoverage;
            
            Debug.Log("\n[覆盖率质量门禁检查]");
            Debug.Log($"  {(overallPass ? "✅" : "❌")} 整体覆盖率 {report.Overall.ClassCoveragePercentage:F1}% {(overallPass ? '≥' : '<')} {MinOverallCoverage}%");
            Debug.Log($"  {(corePass ? "✅" : "❌")} Core 模块覆盖率 {(report.Modules.ContainsKey("Core") ? report.Modules["Core"].ClassCoveragePercentage.ToString("F1") : "N/A")}% {(corePass ? '≥' : '<')} {MinCoreCoverage}%");
            Debug.Log($"  {(actionsPass ? "✅" : "❌")} Actions 模块覆盖率 {(report.Modules.ContainsKey("Actions") ? report.Modules["Actions"].ClassCoveragePercentage.ToString("F1") : "N/A")}% {(actionsPass ? '≥' : '<')} {MinActionsCoverage}%");
            
            bool allPass = overallPass && corePass && actionsPass;
            Debug.Log($"\n覆盖率质量门禁: {(allPass ? "✅ 通过" : "❌ 失败")}");
            return allPass;
        }
    }
}
