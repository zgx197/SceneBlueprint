import { describe, expect, it } from "vitest";
import { createInitialGraphViewState } from "../state/graphViewState";
import { createBootstrapGraphDocument } from "../runtime/graphWorkspaceRuntime";
import { createTestDefinitionRegistry } from "../testing/graphTestUtils";
import {
  buildSceneMarkerSelectionTarget,
  createGraphInspectorBinding,
} from "./graphInspectorBinding";
import { createGraphRuntimeBridgeContract } from "../runtime/graphWorkspaceBridge";

describe("graphInspectorBinding", () => {
  it("projects selected nodes into inspector-ready content data", () => {
    const definitions = createTestDefinitionRegistry();
    const binding = createGraphInspectorBinding();
    const selection = binding.getSelectionTarget(
      createBootstrapGraphDocument(definitions),
      createInitialGraphViewState({
        selection: {
          selectedNodeIds: ["node-spawn-marker"],
          primarySelectedNodeId: "node-spawn-marker",
        },
      }),
      definitions,
    );

    expect(selection.kind).toBe("graph-node");
    if (selection.kind !== "graph-node") {
      throw new Error("Expected graph-node selection.");
    }

    expect(selection.displayName).toBe("Spawn Marker");
    expect(selection.contentProjection.summaryText).toContain("Marker：marker_spawn_a");
    expect(selection.contentProjection.detailLines).toEqual(
      expect.arrayContaining([expect.objectContaining({ key: "facingMode" })]),
    );
  });

  it("projects selected edges with label and diagnostic metadata", () => {
    const definitions = createTestDefinitionRegistry();
    const binding = createGraphInspectorBinding();
    const selection = binding.getSelectionTarget(
      createBootstrapGraphDocument(definitions),
      createInitialGraphViewState({
        selection: {
          selectedEdgeIds: ["edge-start-to-spawn"],
          primarySelectedEdgeId: "edge-start-to-spawn",
        },
      }),
      definitions,
    );

    expect(selection).toEqual(
      expect.objectContaining({
        kind: "graph-edge",
        sourceNodeTitle: "Start",
        targetNodeTitle: "Spawn Marker",
        label: "进入出生点",
        diagnosticTone: "info",
      }),
    );
  });

  it("projects annotation selections for groups, comments, and subgraphs", () => {
    const definitions = createTestDefinitionRegistry();
    const document = createBootstrapGraphDocument(definitions);
    const binding = createGraphInspectorBinding();

    const groupSelection = binding.getSelectionTarget(
      document,
      createInitialGraphViewState({
        selection: {
          selectedGroupIds: ["group-bootstrap-flow"],
          primarySelectedGroupId: "group-bootstrap-flow",
        },
      }),
      definitions,
    );
    const commentSelection = binding.getSelectionTarget(
      document,
      createInitialGraphViewState({
        selection: {
          selectedCommentIds: ["comment-bootstrap-note"],
          primarySelectedCommentId: "comment-bootstrap-note",
        },
      }),
      definitions,
    );
    const subgraphSelection = binding.getSelectionTarget(
      document,
      createInitialGraphViewState({
        selection: {
          selectedSubgraphIds: ["subgraph-bootstrap-main"],
          primarySelectedSubgraphId: "subgraph-bootstrap-main",
        },
      }),
      definitions,
    );

    expect(groupSelection).toEqual(
      expect.objectContaining({
        kind: "graph-group",
        memberDisplayNames: ["Start", "Spawn Marker"],
      }),
    );
    expect(commentSelection).toEqual(
      expect.objectContaining({
        kind: "graph-comment",
        commentId: "comment-bootstrap-note",
      }),
    );
    expect(subgraphSelection).toEqual(
      expect.objectContaining({
        kind: "graph-subgraph",
        entryNodeDisplayName: "Start",
      }),
    );
  });

  it("builds inspector selection data for scene markers from the formal bridge contract", () => {
    const definitions = createTestDefinitionRegistry();
    const document = createBootstrapGraphDocument(definitions);
    const bridge = createGraphRuntimeBridgeContract(document, definitions);
    const selection = buildSceneMarkerSelectionTarget(document, definitions, bridge, [
      {
        code: "graph-bridge-project-missing",
        severity: "warning",
        blocking: false,
        message: "当前 bridge contract 尚未指定 Project Id。",
        location: {
          entityKind: "project",
          entityId: bridge.project.graphId,
        },
      },
    ], bridge.markers[0].id);

    expect(selection.kind).toBe("scene-marker");
    if (selection.kind !== "scene-marker") {
      throw new Error("Expected scene-marker selection.");
    }

    expect(selection.graphNodeDisplayName).toBe("Spawn Marker");
    expect(selection.marker.nodeId).toBe("node-spawn-marker");
    expect(selection.issueMessages).toEqual(["当前 bridge contract 尚未指定 Project Id。"]);
  });
});
