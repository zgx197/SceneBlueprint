import { describe, expect, it } from "vitest";
import { createGraphDocument } from "../document/graphDocument";
import { createBootstrapGraphWorkspaceRuntimeState } from "../runtime/graphWorkspaceRuntime";
import { createTestDefinitionRegistry, instantiateTestNode } from "../testing/graphTestUtils";
import {
  deserializeGraphDocumentFile,
  GRAPH_DOCUMENT_FILE_SCHEMA,
  parseGraphDocumentFileEnvelope,
  serializeGraphDocumentFile,
} from "./graphDocumentFile";

describe("graphDocumentFile", () => {
  it("round-trips graph document and workspace snapshot", () => {
    const definitions = createTestDefinitionRegistry();
    const runtimeState = createBootstrapGraphWorkspaceRuntimeState(definitions);

    const restored = deserializeGraphDocumentFile(serializeGraphDocumentFile(runtimeState));

    expect(restored.document).toEqual(runtimeState.document);
    expect(restored.viewState.viewport).toEqual(runtimeState.viewState.viewport);
    expect(restored.viewState.selection).toEqual(runtimeState.viewState.selection);
  });

  it("rejects unsupported schema versions", () => {
    expect(() =>
      parseGraphDocumentFileEnvelope(
        JSON.stringify({
          schema: "sceneblueprint.graph-document.v0",
          savedAt: "2026-03-30T00:00:00.000Z",
          graph: { id: "graph" },
          workspace: {},
        }),
      ),
    ).toThrow("当前不支持的 Graph 文件 schema");
  });

  it("normalizes stale workspace selections and invalid structures on deserialize", () => {
    const startNode = instantiateTestNode("flow.start", "node-start", { x: 20, y: 30 });

    const raw = JSON.stringify({
      schema: GRAPH_DOCUMENT_FILE_SCHEMA,
      savedAt: "2026-03-30T00:00:00.000Z",
      graph: {
        ...createGraphDocument({
          id: "legacy-file",
          nodes: [startNode],
        }),
        groups: [
          { id: "group-drop", title: "Drop", nodeIds: ["ghost-node"] },
          { id: "group-keep", title: "Keep", nodeIds: ["ghost-node", "node-start"] },
        ],
        subgraphs: [
          { id: "subgraph-keep", title: "Main", nodeIds: ["ghost-node", "node-start"], entryNodeId: "ghost-node" },
          { id: "subgraph-drop", title: "Drop", nodeIds: ["ghost-node"] },
        ],
      },
      workspace: {
        viewport: { zoom: 0.9, panX: 12, panY: -14 },
        selection: {
          selectedNodeIds: ["ghost-node", "node-start"],
          primarySelectedNodeId: "ghost-node",
        },
      },
    });

    const restored = deserializeGraphDocumentFile(raw);

    expect(restored.document.groups).toEqual([
      expect.objectContaining({
        id: "group-keep",
        nodeIds: ["node-start"],
      }),
    ]);
    expect(restored.document.subgraphs).toEqual([
      expect.objectContaining({
        id: "subgraph-keep",
        entryNodeId: "node-start",
      }),
    ]);
    expect(restored.viewState.selection.selectedNodeIds).toEqual(["node-start"]);
    expect(restored.viewState.selection.primarySelectedNodeId).toBe("node-start");
  });
});
