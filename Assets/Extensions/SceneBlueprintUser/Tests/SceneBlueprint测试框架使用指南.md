# SceneBlueprint 测试框架使用指南

> 状态：当前主使用说明  
> doc_status: active  
> last_reviewed: 2026-02-15

## 概述

SceneBlueprint 测试框架基于 Unity Test Framework (NUnit)，专注于 **Editor 内的便捷测试体验**。

核心特性：
- **白名单程序集过滤**：只运行 `SceneBlueprint.Tests` 程序集中的测试
- **自动报告生成**：测试完成后自动弹出报告，可一键复制给 AI 分析
- **精简菜单**：3 个核心菜单项，无冗余功能

---

## 1. 快速开始

### Unity Editor 菜单

```
SceneBlueprint → Tests →
├── 🚀 运行测试          # 自动运行 + 自动生成报告
├── ⚙️ 测试配置          # 白名单程序集管理
└── ❓ 帮助指南          # 使用说明
```

### 运行测试

点击 **SceneBlueprint → Tests → 🚀 运行测试**，选择运行方式：

| 方式 | 说明 | 推荐场景 |
|------|------|----------|
| **自动运行** | 直接执行白名单程序集测试，完成后自动生成报告 | 日常开发（推荐） |
| **手动运行** | 打开 Unity Test Runner + 操作指导 | 需要选择性运行单个测试时 |

### 测试完成后

测试执行完毕后，系统自动：
1. 在 Console 输出测试摘要（总数/通过/失败/成功率）
2. 弹出报告对话框
3. 可选择「复制报告到剪贴板」→ 粘贴给 AI 分析错误原因

---

## 2. 测试目录结构

```
Tests/
├── Unit/Core/              # 单元测试
│   ├── ActionDefinitionTests.cs
│   ├── PropertyBagTests.cs
│   ├── PropertyBagTests_Example.cs  # 最佳实践示例
│   ├── ActionRegistryTests.cs
│   ├── PropFactoryTests.cs
│   └── VisibleWhenTests.cs
├── Integration/            # 集成测试
│   ├── FlowActionTests.cs
│   └── CombatActionTests.cs
├── E2E/                    # 端到端测试
│   └── EndToEndTests.cs
├── Unit/Utils/             # 测试工具类
│   ├── TestDataBuilder.cs
│   └── AssertionExtensions.cs
└── Scripts/                # 测试框架脚本
    ├── TestRunnerFilter.cs         # 白名单过滤 + 数据收集 + 自动报告
    ├── TestConfiguration.cs        # 配置（ScriptableObject）
    ├── SceneBlueprintTestRunner.cs # 测试运行器
    ├── TestReportGenerator.cs      # 报告生成
    └── Editor/TestMenuItems.cs     # 菜单定义
```

### 添加新测试文件

| 测试类型 | 路径 | 命名 |
|----------|------|------|
| 单元测试 | `Tests/Unit/Core/` | `{ClassName}Tests.cs` |
| 集成测试 | `Tests/Integration/` | `{Feature}Tests.cs` |
| E2E 测试 | `Tests/E2E/` | `{Scenario}E2ETests.cs` |

---

## 3. 编写测试

### 测试模板

```csharp
#nullable enable
using NUnit.Framework;
using SceneBlueprint.Core;
using SceneBlueprint.Tests.Utils;

namespace SceneBlueprint.Tests.Unit.Core
{
    public class MyClassTests
    {
        [Test]
        public void Method_Scenario_Expected()
        {
            // Arrange
            var bag = TestDataBuilder.CreateEmptyPropertyBag();

            // Act
            bag.Set("key", "value");

            // Assert
            bag.ShouldContainKey("key");
            bag.ShouldContain("key", "value");
        }
    }
}
```

### TestDataBuilder API

```csharp
// 基础数据
TestDataBuilder.CreateEmptyPropertyBag()
TestDataBuilder.CreatePropertyBag(("key", "value"), ("count", 42))
TestDataBuilder.CreateDiscoveredRegistry()

// 复杂场景
TestDataBuilder.CreateSpawnNodeData()
TestDataBuilder.CreateFullFlowTestData()

// 边界和性能
TestDataBuilder.CreateLargePropertyBag(1000)
TestDataBuilder.CreateBoundaryValuePropertyBag()
```

### AssertionExtensions API

```csharp
// PropertyBag 断言
bag.ShouldContainKey("key")
bag.ShouldContain("key", "expectedValue")
bag.ShouldHaveCount(5)
bag.ShouldBeEmpty()

// ActionDefinition 断言
def.ShouldHaveBasicFields("TypeId", "DisplayName", "Category")
def.ShouldHavePort("in", PortDirection.In)
def.ShouldHaveProperty("template", PropertyType.AssetRef)

// 性能断言
action.ShouldCompleteWithin(1000)

// 序列化断言
bag.ShouldSerializeCorrectly()
```

---

## 4. 白名单过滤机制

### 工作原理

1. `TestConfiguration` (ScriptableObject) 定义白名单程序集列表
2. `TestRunnerFilter` 在 Editor 加载时注册 `ICallbacks` 回调
3. 运行测试时，通过 `Filter.assemblyNames` 只执行白名单程序集
4. 回调在测试完成后自动收集结果并生成报告

### 配置文件

配置文件位于 `Tests/SceneBlueprintTestConfig.asset`，可通过菜单 **⚙️ 测试配置** 管理：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `allowedAssemblies` | 白名单程序集列表 | `["SceneBlueprint.Tests"]` |
| `enableAssemblyFiltering` | 启用过滤 | `true` |
| `verboseLogging` | 详细日志 | `true` |
| `autoGenerateReportOnFailure` | 失败时自动生成报告 | `true` |

---

## 5. 测试报告

### 报告格式

测试完成后自动生成的报告包含：

```
================================================================================
SCENEBLUEPRINT 测试执行报告
================================================================================
执行时间: 2026-02-12 21:57:36

📊 测试总结
----------------------------------------
总测试数 / 通过 / 失败 / 跳过 / 成功率 / 总耗时

❌ 失败测试详情（如有）
----------------------------------------
每个失败测试的：名称、路径、耗时、错误信息、堆栈跟踪

================================================================================
```

### 推荐工作流

```
1. 运行测试（自动模式）
2. 测试完成 → 自动弹出报告
3. 点击「复制报告」→ 粘贴给 AI 分析
```

---

## 6. 命名规范

| 类型 | 格式 | 示例 |
|------|------|------|
| 测试类 | `{ClassName}Tests` | `PropertyBagTests` |
| 测试方法 | `Method_Scenario_Expected` | `Get_NonExistentKey_ReturnsDefault` |
| 行为描述 | `GivenX_WhenY_ThenZ` | `GivenEmptyBag_WhenSet_ThenContains` |

---

## 7. 常见问题

**Q: 测试没有在 Test Runner 中显示？**
- 检查 `[Test]` 属性是否存在
- 确认命名空间和 asmdef 引用正确
- 确认项目编译成功

**Q: 运行测试后报告显示 0 个测试？**
- 检查配置中的 `allowedAssemblies` 是否包含正确的程序集名
- 确认 `TestRunnerFilter` 已正确注册回调

**Q: 如何只运行单个测试？**
- 使用「手动运行」模式打开 Test Runner
- 在 Test Runner 中右键单个测试 → Run

**Q: 测试工具类找不到？**
```csharp
using SceneBlueprint.Tests.Utils;
```

---

## 8. 设计约定

### PropertyBag 默认值语义
- 遵循 C# 默认语义：`default(string)` = `null`
- `bag.Get<string>("missing")` 返回 `null`
- `bag.Get<int>("missing")` 返回 `0`
- 需要非 null 默认值时显式提供：`bag.Get<string>("key", "")`
