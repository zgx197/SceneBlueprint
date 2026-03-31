import { describe, expect, it } from "vitest";
import { createGraphWorkspaceIssueFeedbackSummary } from "./graphWorkspaceIssueFeedback";
import type { GraphWorkspaceIssue } from "../runtime/graphWorkspaceExport";

function createIssue(overrides: Partial<GraphWorkspaceIssue> = {}): GraphWorkspaceIssue {
  return {
    code: "graph-issue",
    severity: "warning",
    blocking: false,
    message: "warning message",
    location: { entityKind: "graph" },
    ...overrides,
  };
}

describe("graphWorkspaceIssueFeedback", () => {
  it("returns healthy summary when no issue exists", () => {
    const summary = createGraphWorkspaceIssueFeedbackSummary({
      valid: true,
      issues: [],
      blockingIssues: [],
      warningCount: 0,
      errorCount: 0,
    });

    expect(summary.tone).toBe("healthy");
    expect(summary.statusLabel).toBe("通过");
    expect(summary.topIssues).toEqual([]);
    expect(summary.detail).toContain("未发现");
  });

  it("prioritizes blocking issues for error summary", () => {
    const blockingIssue = createIssue({
      code: "graph-blocking",
      severity: "error",
      blocking: true,
      message: "blocking message",
      location: { entityKind: "marker", entityId: "marker-1" },
    });
    const warningIssue = createIssue({
      code: "graph-warning",
      message: "warning message 2",
    });

    const summary = createGraphWorkspaceIssueFeedbackSummary({
      valid: false,
      issues: [warningIssue, blockingIssue],
      blockingIssues: [blockingIssue],
      warningCount: 1,
      errorCount: 1,
    });

    expect(summary.tone).toBe("error");
    expect(summary.statusLabel).toBe("阻塞 1");
    expect(summary.topIssues).toEqual([blockingIssue]);
    expect(summary.detail).toContain("blocking issue");
  });

  it("keeps warning-only summary exportable", () => {
    const warningIssue = createIssue({
      code: "graph-warning-only",
      message: "only warning",
    });

    const summary = createGraphWorkspaceIssueFeedbackSummary({
      valid: true,
      issues: [warningIssue],
      blockingIssues: [],
      warningCount: 1,
      errorCount: 0,
    });

    expect(summary.tone).toBe("warning");
    expect(summary.valid).toBe(true);
    expect(summary.statusLabel).toBe("警告 1");
    expect(summary.topIssues).toEqual([warningIssue]);
  });
});
