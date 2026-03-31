import { describe, expect, it } from "vitest";
import {
  createGraphDocument,
  createGraphEdge,
} from "../document/graphDocument";
import { createInitialGraphViewState } from "../state/graphViewState";
import {
  buildCreateCommentAtViewportCenterCommand,
  buildCreateGroupFromSelectionCommand,
  buildCreateSubgraphFromSelectionCommand,
  buildGraphCopySelectionResult,
  buildGraphPasteClipboardCommand,
  formatAutoLayoutAppliedMessage,
  getViewportCenterWorldPoint,
  GRAPH_WORKSPACE_PASTE_OFFSET_STEP,
} from "./graphWorkspaceControllerActions";
import {
  createTestRuntimeState,
  instantiateTestNode,
} from "../testing/graphTestUtils";

function createSelectedRuntimeState() {
  const startNode = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
  const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 160, y: 40 });
  const document = createGraphDocument({
    id: "graph-controller-actions",
    nodes: [startNode, markerNode],
    edges: [
      createGraphEdge({
        id: "edge-start-marker",
        sourceNodeId: startNode.id,
        sourcePortId: `${startNode.id}:next`,
        targetNodeId: markerNode.id,
        targetPortId: `${markerNode.id}:in`,
      }),
    ],
    groups: [{ id: "group-1", title: "Existing", nodeIds: [startNode.id] }],
    subgraphs: [{ id: "subgraph-1", title: "Existing", nodeIds: [startNode.id] }],
    comments: [{
      id: "comment-1",
      text: "Existing",
      position: { x: 0, y: 0 },
      size: { width: 100, height: 80 },
      tone: "info",
    }],
  });

  return createTestRuntimeState(
    document,
    createInitialGraphViewState({
      viewport: {
        panX: 100,
        panY: 50,
        zoom: 2,
      },
      selection: {
        selectedNodeIds: [startNode.id, markerNode.id],
        primarySelectedNodeId: markerNode.id,
      },
    }),
  );
}

describe("graphWorkspaceControllerActions", () => {
  it("creates group and subgraph commands from the current selection", () => {
    const state = createSelectedRuntimeState();

    const groupResult = buildCreateGroupFromSelectionCommand(state);
    const subgraphResult = buildCreateSubgraphFromSelectionCommand(state);

    expect(groupResult).toEqual(
      expect.objectContaining({
        ok: true,
        group: expect.objectContaining({
          id: "group-2",
          title: "分组 2",
          nodeIds: ["node-start", "node-marker"],
        }),
        command: expect.objectContaining({ type: "graph.add-group" }),
      }),
    );
    expect(subgraphResult).toEqual(
      expect.objectContaining({
        ok: true,
        subgraph: expect.objectContaining({
          id: "subgraph-2",
          title: "子图 2",
          entryNodeId: "node-marker",
        }),
        command: expect.objectContaining({ type: "graph.add-subgraph" }),
      }),
    );
  });

  it("creates a comment centered in viewport space", () => {
    const state = createSelectedRuntimeState();

    expect(getViewportCenterWorldPoint(state.viewState.viewport, { width: 500, height: 300 })).toEqual({
      x: 75,
      y: 50,
    });

    const result = buildCreateCommentAtViewportCenterCommand(state, { width: 500, height: 300 });
    expect(result).toEqual(
      expect.objectContaining({
        ok: true,
        comment: expect.objectContaining({
          id: "comment-2",
          position: { x: -45, y: -16 },
          size: { width: 240, height: 132 },
        }),
        command: expect.objectContaining({ type: "graph.add-comment" }),
      }),
    );
  });

  it("builds clipboard copy and paste commands", () => {
    const state = createSelectedRuntimeState();

    const copyResult = buildGraphCopySelectionResult(state);
    expect(copyResult).toEqual(
      expect.objectContaining({
        ok: true,
        summary: expect.objectContaining({
          nodeCount: 2,
          edgeCount: 1,
        }),
      }),
    );
    if (!copyResult.ok) {
      throw new Error("expected copyResult to be ok");
    }

    const pasteCommand = buildGraphPasteClipboardCommand(copyResult.snapshot, 3);
    expect(pasteCommand).toEqual({
      type: "graph.paste-clipboard",
      snapshot: copyResult.snapshot,
      offset: {
        x: GRAPH_WORKSPACE_PASTE_OFFSET_STEP * 3,
        y: GRAPH_WORKSPACE_PASTE_OFFSET_STEP * 3,
      },
    });
  });

  it("reports invalid selection and viewport inputs", () => {
    const state = createSelectedRuntimeState();
    const emptySelectionState = {
      ...state,
      viewState: createInitialGraphViewState(),
    };

    expect(buildCreateGroupFromSelectionCommand(emptySelectionState)).toEqual({ ok: false });
    expect(buildCreateSubgraphFromSelectionCommand(emptySelectionState)).toEqual({ ok: false });
    expect(buildGraphCopySelectionResult(emptySelectionState)).toEqual({ ok: false });
    expect(buildCreateCommentAtViewportCenterCommand(state, { width: 0, height: 300 })).toEqual({ ok: false });
  });

  it("formats auto-layout messages for selection and whole graph", () => {
    expect(formatAutoLayoutAppliedMessage("dagre", 2, 5)).toBe("已通过 dagre 对 2 个选中节点执行自动布局。");
    expect(formatAutoLayoutAppliedMessage("elk", 0, 5)).toBe("已通过 elk 对全图 5 个节点执行自动布局。");
  });
});
