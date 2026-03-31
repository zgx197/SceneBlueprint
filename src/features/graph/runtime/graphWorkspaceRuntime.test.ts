import { describe, expect, it } from "vitest";
import { createInitialGraphViewState } from "../state/graphViewState";
import { createTestDefinitionRegistry } from "../testing/graphTestUtils";
import {
  createBootstrapGraphWorkspaceRuntimeState,
  createGraphWorkspaceRuntime,
} from "./graphWorkspaceRuntime";

describe("graphWorkspaceRuntime", () => {
  it("deletes the active selection through runtime commands and supports undo", () => {
    const definitions = createTestDefinitionRegistry();
    const runtime = createGraphWorkspaceRuntime({
      initialState: createBootstrapGraphWorkspaceRuntimeState(definitions),
      definitions,
    });

    runtime.deleteSelection();

    expect(runtime.getState().document.nodes.map((node) => node.id)).not.toContain("node-spawn-marker");
    expect(runtime.getState().document.edges).toHaveLength(0);
    expect(runtime.getCommandSnapshot().historyLength).toBe(1);

    runtime.undo();

    expect(runtime.getState().document.nodes.map((node) => node.id)).toContain("node-spawn-marker");
    expect(runtime.getState().document.edges).toHaveLength(2);
  });

  it("clamps viewport zoom and normalizes replaced runtime state", () => {
    const definitions = createTestDefinitionRegistry();
    const runtime = createGraphWorkspaceRuntime({
      initialState: createBootstrapGraphWorkspaceRuntimeState(definitions),
      definitions,
    });

    runtime.patchViewport({ zoom: 999 });
    expect(runtime.getState().viewState.viewport.zoom).toBe(runtime.profile.render.layout.zoomMax);

    runtime.replaceState({
      document: runtime.getState().document,
      viewState: createInitialGraphViewState({
        selection: {
          selectedNodeIds: ["ghost-node"],
          primarySelectedNodeId: "ghost-node",
        },
      }),
    });

    expect(runtime.getState().viewState.selection.selectedNodeIds).toEqual([]);
    expect(runtime.getState().viewState.selection.primarySelectedNodeId).toBeUndefined();
  });
});
