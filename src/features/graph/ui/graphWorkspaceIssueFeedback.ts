import type { GraphWorkspaceExportPreflight } from "../runtime/graphWorkspaceKernel";
import type { GraphWorkspaceIssue } from "../runtime/graphWorkspaceExport";

export type GraphWorkspaceIssueFeedbackTone = "healthy" | "warning" | "error";

export interface GraphWorkspaceIssueFeedbackSummary {
  tone: GraphWorkspaceIssueFeedbackTone;
  valid: boolean;
  totalCount: number;
  blockingCount: number;
  warningCount: number;
  errorCount: number;
  statusLabel: string;
  headline: string;
  detail: string;
  topIssues: GraphWorkspaceIssue[];
}

// GraphPanel / Toolbar / StatusBar 统一复用这套 issue 汇总口径，避免各处各算各的。
export function createGraphWorkspaceIssueFeedbackSummary(
  preflight: Pick<
    GraphWorkspaceExportPreflight,
    "valid" | "issues" | "blockingIssues" | "warningCount" | "errorCount"
  >,
  options?: { maxItems?: number },
): GraphWorkspaceIssueFeedbackSummary {
  const totalCount = preflight.issues.length;
  const blockingCount = preflight.blockingIssues.length;
  const warningCount = preflight.warningCount;
  const errorCount = preflight.errorCount;
  const maxItems = options?.maxItems ?? 3;

  if (totalCount === 0) {
    return {
      tone: "healthy",
      valid: true,
      totalCount,
      blockingCount,
      warningCount,
      errorCount,
      statusLabel: "通过",
      headline: "导出预检通过",
      detail: "当前未发现 blocking 或 warning issue，bridge / export 主路径可以继续推进。",
      topIssues: [],
    };
  }

  if (blockingCount > 0) {
    return {
      tone: "error",
      valid: false,
      totalCount,
      blockingCount,
      warningCount,
      errorCount,
      statusLabel: `阻塞 ${blockingCount}`,
      headline: "导出预检失败",
      detail: `当前存在 ${blockingCount} 个 blocking issue，需先修复后才能安全导出 runtime contract。`,
      topIssues: preflight.blockingIssues.slice(0, maxItems),
    };
  }

  return {
    tone: "warning",
    valid: preflight.valid,
    totalCount,
    blockingCount,
    warningCount,
    errorCount,
    statusLabel: `警告 ${warningCount}`,
    headline: "导出预检通过，但仍有提示项",
    detail: `当前存在 ${warningCount} 个 warning，允许继续导出，但建议在进入正式工作流前处理。`,
    topIssues: preflight.issues.slice(0, maxItems),
  };
}

