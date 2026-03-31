import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { StatusBar } from "./StatusBar";

describe("StatusBar", () => {
  it("renders issue state in the status bar", () => {
    const html = renderToStaticMarkup(
      <StatusBar
        entries={[
          {
            id: "log-1",
            timestamp: "2026-03-31T12:00:00.000Z",
            level: "warn",
            scope: "graph",
            message: "发现预检问题",
          },
        ]}
        appInfo={{
          name: "SceneBlueprint",
          version: "0.1.0",
          platform: "win32",
          runtime: "tauri",
        }}
        pingResult={{
          message: "pong",
          timestamp: "2026-03-31T12:00:01.000Z",
        }}
        graphSummary={{
          graphId: "graph-status",
          nodeCount: 3,
          edgeCount: 2,
          zoom: 1.25,
          selectionKind: "graph-node",
          savedAt: "2026-03-31T12:00:02.000Z",
          issueStatusLabel: "阻塞 1",
          issueDetail: "当前存在 1 个 blocking issue。",
        }}
      />,
    );

    expect(html).toContain("Issues");
    expect(html).toContain("阻塞 1");
    expect(html).toContain("graph-status");
    expect(html).toContain("[graph] 发现预检问题");
  });
});
