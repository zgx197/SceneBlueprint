import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { GraphWorkspaceIssueBanner } from "./GraphWorkspaceIssueBanner";
import type { GraphWorkspaceExportPreflight } from "../runtime/graphWorkspaceKernel";
import { createGraphDocument } from "../document/graphDocument";
import { createGraphSubgraphIndex } from "../runtime/graphSubgraphIndex";
import type { GraphWorkspaceIssue } from "../runtime/graphWorkspaceExport";

function createIssue(overrides: Partial<GraphWorkspaceIssue> = {}): GraphWorkspaceIssue {
  return {
    code: "graph-warning",
    severity: "warning",
    blocking: false,
    message: "warning message",
    location: { entityKind: "graph" },
    ...overrides,
  };
}

function createPreflight(overrides: Partial<GraphWorkspaceExportPreflight> = {}): GraphWorkspaceExportPreflight {
  const normalizedDocument = createGraphDocument({ id: "graph-1" });

  return {
    valid: true,
    issues: [],
    blockingIssues: [],
    warningCount: 0,
    errorCount: 0,
    analysis: {
      topologyPolicy: "dag",
      hasCycle: false,
      topologicalOrder: [],
      rootNodeIds: [],
      leafNodeIds: [],
      connectedComponents: [],
      subgraphAnalysis: {
        normalizedDocument,
        index: createGraphSubgraphIndex(normalizedDocument),
        issues: [],
      },
    },
    ...overrides,
  };
}

describe("GraphWorkspaceIssueBanner", () => {
  it("renders healthy banner state", () => {
    const html = renderToStaticMarkup(<GraphWorkspaceIssueBanner preflight={createPreflight()} />);

    expect(html).toContain("导出预检通过");
    expect(html).toContain("预检 通过");
    expect(html).toContain("总计 0");
  });

  it("renders blocking issues in banner list", () => {
    const blockingIssue = createIssue({
      code: "graph-blocking",
      severity: "error",
      blocking: true,
      message: "blocking message",
      location: { entityKind: "marker", entityId: "marker-1" },
    });
    const html = renderToStaticMarkup(
      <GraphWorkspaceIssueBanner
        preflight={createPreflight({
          valid: false,
          issues: [blockingIssue],
          blockingIssues: [blockingIssue],
          warningCount: 0,
          errorCount: 1,
        })}
      />,
    );

    expect(html).toContain("导出预检失败");
    expect(html).toContain("graph-blocking");
    expect(html).toContain("blocking message");
    expect(html).toContain("阻塞 1");
  });
});
