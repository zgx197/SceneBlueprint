import { describe, expect, it } from "vitest";
import { createGraphComment, createGraphDocument, createGraphPoint } from "../document/graphDocument";
import { createInitialGraphViewState } from "../state/graphViewState";
import {
  normalizeGraphSelectionState,
  normalizeGraphWorkspaceRuntimeState,
} from "./graphRuntimeStateMigration";
import { instantiateTestNode } from "../testing/graphTestUtils";

describe("graphRuntimeStateMigration", () => {
  it("normalizes legacy selection and prunes invalid structure references", () => {
    const startNode = instantiateTestNode("flow.start", "node-start", { x: 32, y: 64 });

    const normalized = normalizeGraphWorkspaceRuntimeState({
      document: {
        id: "legacy-doc",
        nodes: [startNode],
        edges: [],
        groups: [
          { id: "group-keep", title: "Keep", nodeIds: ["ghost-node", "node-start"], padding: 18 },
          { id: "group-drop", title: "Drop", nodeIds: ["ghost-node"] },
        ],
        comments: [
          createGraphComment({
            id: "comment-note",
            text: "note",
            position: createGraphPoint(12, 24),
          }),
        ],
        subgraphs: [
          {
            id: "subgraph-keep",
            title: "Main",
            nodeIds: ["ghost-node", "node-start"],
            entryNodeId: "ghost-node",
          },
          { id: "subgraph-drop", title: "Drop", nodeIds: ["ghost-node"] },
        ],
        metadata: { legacy: true },
      },
      viewState: {
        viewport: { zoom: 1.25, panX: 10, panY: -8 },
        selection: {
          selectedNodeIds: ["ghost-node", "node-start"],
          selectedEdgeIds: [],
          selectedGroupIds: ["group-drop"],
          selectedCommentIds: [],
          selectedSubgraphIds: ["subgraph-drop"],
          primarySelectedNodeId: "ghost-node",
          primarySelectedEdgeId: undefined,
          primarySelectedGroupId: "group-drop",
          primarySelectedCommentId: undefined,
          primarySelectedSubgraphId: "subgraph-drop",
        },
        connectionPreview: {
          active: true,
          fromNodeId: "node-start",
          fromPortId: "node-start:next",
          pointer: { x: 400, y: 220 },
        },
        interaction: {
          draggingNodeIds: ["node-start"],
          hoveredNodeId: "node-start",
          hoveredPortId: "node-start:next",
          hoveredGroupId: "group-keep",
          hoveredCommentId: "comment-note",
          hoveredSubgraphId: "subgraph-keep",
          marqueeSelection: {
            startX: 1,
            startY: 2,
            endX: 3,
            endY: 4,
          },
        },
      },
    });

    expect(normalized.document.groups).toEqual([
      expect.objectContaining({
        id: "group-keep",
        nodeIds: ["node-start"],
      }),
    ]);
    expect(normalized.document.subgraphs).toEqual([
      expect.objectContaining({
        id: "subgraph-keep",
        nodeIds: ["node-start"],
        entryNodeId: "node-start",
      }),
    ]);
    expect(normalized.viewState.selection.selectedNodeIds).toEqual(["node-start"]);
    expect(normalized.viewState.selection.primarySelectedNodeId).toBe("node-start");
    expect(normalized.viewState.connectionPreview).toEqual(createInitialGraphViewState().connectionPreview);
    expect(normalized.viewState.interaction).toEqual(createInitialGraphViewState().interaction);
  });

  it("preserves sanitized transient state when resetTransientState is disabled", () => {
    const startNode = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });

    const normalized = normalizeGraphWorkspaceRuntimeState(
      {
        document: createGraphDocument({
          id: "transient-doc",
          nodes: [startNode],
        }),
        viewState: {
          viewport: { zoom: 0.8, panX: 4, panY: 9 },
          selection: {
            selectedNodeIds: ["node-start"],
            selectedEdgeIds: [],
            selectedGroupIds: [],
            selectedCommentIds: [],
            selectedSubgraphIds: [],
            primarySelectedNodeId: "node-start",
            primarySelectedEdgeId: undefined,
            primarySelectedGroupId: undefined,
            primarySelectedCommentId: undefined,
            primarySelectedSubgraphId: undefined,
          },
          connectionPreview: {
            active: true,
            fromNodeId: "node-start",
            fromPortId: "node-start:next",
            pointer: { x: 20, y: 40 },
          },
          interaction: {
            draggingNodeIds: ["node-start"],
            hoveredNodeId: "node-start",
            hoveredPortId: "node-start:next",
            hoveredGroupId: undefined,
            hoveredCommentId: undefined,
            hoveredSubgraphId: undefined,
            marqueeSelection: {
              startX: 0,
              startY: 0,
              endX: 10,
              endY: 10,
            },
          },
        },
      },
      { resetTransientState: false },
    );

    expect(normalized.viewState.connectionPreview).toEqual({
      active: true,
      fromNodeId: "node-start",
      fromPortId: "node-start:next",
      pointer: { x: 20, y: 40 },
    });
    expect(normalized.viewState.interaction).toEqual({
      draggingNodeIds: ["node-start"],
      hoveredNodeId: "node-start",
      hoveredPortId: "node-start:next",
      hoveredGroupId: undefined,
      hoveredCommentId: undefined,
      hoveredSubgraphId: undefined,
      marqueeSelection: {
        startX: 0,
        startY: 0,
        endX: 10,
        endY: 10,
      },
    });
  });

  it("normalizes malformed selection payloads into stable defaults", () => {
    const normalized = normalizeGraphSelectionState({
      selectedNodeIds: ["node-a", 123, "node-b"],
      selectedEdgeIds: null,
      selectedGroupIds: "group-a",
      selectedCommentIds: ["comment-a"],
      selectedSubgraphIds: ["subgraph-a"],
      primarySelectedNodeId: "missing",
      primarySelectedCommentId: "comment-a",
      primarySelectedSubgraphId: "ghost-subgraph",
    });

    expect(normalized).toEqual({
      selectedNodeIds: ["node-a", "node-b"],
      selectedEdgeIds: [],
      selectedGroupIds: [],
      selectedCommentIds: ["comment-a"],
      selectedSubgraphIds: ["subgraph-a"],
      primarySelectedNodeId: "node-a",
      primarySelectedEdgeId: undefined,
      primarySelectedGroupId: undefined,
      primarySelectedCommentId: "comment-a",
      primarySelectedSubgraphId: "subgraph-a",
    });
  });
});
