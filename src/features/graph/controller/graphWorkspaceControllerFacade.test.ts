import { describe, expect, it, vi } from "vitest";
import {
  createGraphWorkspaceControllerFacade,
} from "./graphWorkspaceControllerFacade";
import type { GraphWorkspaceControllerOrchestrator } from "./graphWorkspaceControllerOrchestrator";
import { createGraphDocument, createGraphPoint } from "../document/graphDocument";
import { createInitialGraphViewState } from "../state/graphViewState";
import { createGraphDefinitionRegistry } from "../definitions/graphDefinitions";
import { defaultGraphNodeDefinitions } from "../definitions/defaultGraphNodeDefinitions";
import { createDefaultGraphProfile } from "../profile/graphProfile";
import { createGraphRuntimeBridgeContract } from "../runtime/graphWorkspaceBridge";

function createOrchestrator(): GraphWorkspaceControllerOrchestrator {
  return {
    executeCommand: vi.fn(),
    undo: vi.fn(),
    redo: vi.fn(),
    replaceRuntimeState: vi.fn(),
    setSelection: vi.fn(),
    patchViewport: vi.fn(),
    patchInteraction: vi.fn(),
    centerViewportOnPoint: vi.fn(),
    setConnectionPreview: vi.fn(),
    saveDraft: vi.fn(),
    loadDraft: vi.fn(() => true),
    hydrateWorkspaceFileFromHost: vi.fn(async () => undefined),
    saveWorkspaceFile: vi.fn(async () => true),
    exportRuntimeContractFile: vi.fn(async () => true),
    loadWorkspaceFile: vi.fn(async () => true),
    patchNodePayload: vi.fn(),
    patchEdgePayload: vi.fn(),
    patchGroup: vi.fn(),
    patchComment: vi.fn(),
    patchSubgraph: vi.fn(),
    createGroupFromSelection: vi.fn(() => true),
    createSubgraphFromSelection: vi.fn(() => true),
    createCommentAtViewportCenter: vi.fn(() => true),
    copySelection: vi.fn(() => true),
    pasteClipboard: vi.fn(() => true),
    autoLayoutSelectionOrAll: vi.fn(async () => true),
    selectAllNodes: vi.fn(),
    resetToBootstrap: vi.fn(),
    deleteSelection: vi.fn(),
    disconnectNodeEdges: vi.fn(),
    disconnectPortEdges: vi.fn(),
  };
}

function createFacadeHarness() {
  const document = createGraphDocument({ id: "graph-facade" });
  const viewState = createInitialGraphViewState();
  const definitions = createGraphDefinitionRegistry(defaultGraphNodeDefinitions);
  const profile = createDefaultGraphProfile();
  const orchestrator = createOrchestrator();
  const layoutService = { id: "layout-service" } as const;
  const nodeSearchService = { id: "search-service" } as const;
  const shortcutBindingService = { id: "shortcut-service" } as const;

  const controller = createGraphWorkspaceControllerFacade({
    document,
    viewState,
    definitions,
    profile,
    graphFrame: { id: "frame" } as never,
    commandSnapshot: { id: "history" } as never,
    selectionTarget: { kind: "canvas" } as never,
    analysis: { id: "analysis" } as never,
    exportPreflight: { id: "preflight" } as never,
    bridgeContract: createGraphRuntimeBridgeContract(document, definitions),
    persistenceSnapshot: {
      hasSavedSnapshot: true,
      storageKey: "draft-key",
      savedAt: "2026-03-31T00:00:00.000Z",
    },
    workspaceFileSnapshot: {
      path: "graph.json",
      exists: true,
      backend: "tauri",
      writtenAt: "2026-03-31T01:00:00.000Z",
    },
    runtimeContractFileSnapshot: {
      path: "graph.runtime.json",
      exists: true,
      backend: "tauri",
      exportedAt: "2026-03-31T02:00:00.000Z",
    },
    clipboardSummary: {
      nodeCount: 2,
      edgeCount: 1,
      copiedAt: "2026-03-31T03:00:00.000Z",
    },
    layoutService: layoutService as never,
    nodeSearchService: nodeSearchService as never,
    shortcutBindingService: shortcutBindingService as never,
    orchestrator,
  });

  return {
    controller,
    document,
    viewState,
    definitions,
    profile,
    orchestrator,
    layoutService,
    nodeSearchService,
    shortcutBindingService,
  };
}

describe("graphWorkspaceControllerFacade", () => {
  it("exposes snapshots/services and delegates runtime commands", () => {
    const harness = createFacadeHarness();
    const command = {
      type: "graph.patch-node-payload" as const,
      nodeId: "node-a",
      payloadPatch: { foo: "bar" },
    };

    expect(harness.controller.document).toBe(harness.document);
    expect(harness.controller.viewState).toBe(harness.viewState);
    expect(harness.controller.definitions).toBe(harness.definitions);
    expect(harness.controller.profile).toBe(harness.profile);
    expect(harness.controller.layoutService).toBe(harness.layoutService);
    expect(harness.controller.nodeSearchService).toBe(harness.nodeSearchService);
    expect(harness.controller.shortcutBindingService).toBe(harness.shortcutBindingService);

    harness.controller.execute(command);
    harness.controller.undo();
    harness.controller.redo();
    harness.controller.setSelection({ selectedNodeIds: ["node-a"] });
    harness.controller.patchViewport({ zoom: 2 });
    harness.controller.patchInteraction({ hoveredNodeId: "node-a" });
    harness.controller.centerViewportOnPoint(createGraphPoint(10, 20), { width: 400, height: 300 });
    harness.controller.setConnectionPreview({ active: true, fromNodeId: "node-a" });

    expect(harness.orchestrator.executeCommand).toHaveBeenCalledWith(command, "执行 Graph 命令：graph.patch-node-payload");
    expect(harness.orchestrator.undo).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.redo).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.setSelection).toHaveBeenCalledWith({ selectedNodeIds: ["node-a"] });
    expect(harness.orchestrator.patchViewport).toHaveBeenCalledWith({ zoom: 2 });
    expect(harness.orchestrator.patchInteraction).toHaveBeenCalledWith({ hoveredNodeId: "node-a" });
    expect(harness.orchestrator.centerViewportOnPoint).toHaveBeenCalledWith({ x: 10, y: 20 }, { width: 400, height: 300 });
    expect(harness.orchestrator.setConnectionPreview).toHaveBeenCalledWith({ active: true, fromNodeId: "node-a" });
  });

  it("delegates draft, host I/O and action flows", async () => {
    const harness = createFacadeHarness();

    harness.controller.saveDraft();
    expect(harness.controller.loadDraft()).toBe(true);
    await expect(harness.controller.saveWorkspaceFile()).resolves.toBe(true);
    await expect(harness.controller.exportRuntimeContractFile()).resolves.toBe(true);
    await expect(harness.controller.loadWorkspaceFile()).resolves.toBe(true);

    harness.controller.patchNodePayload("node-a", { foo: "bar" });
    harness.controller.patchEdgePayload("edge-a", { baz: 1 });
    harness.controller.patchGroup("group-a", { title: "Group" });
    harness.controller.patchComment("comment-a", { text: "Comment" });
    harness.controller.patchSubgraph("subgraph-a", { title: "Subgraph" });
    expect(harness.controller.createGroupFromSelection()).toBe(true);
    expect(harness.controller.createSubgraphFromSelection()).toBe(true);
    expect(harness.controller.createCommentAtViewportCenter({ width: 800, height: 600 })).toBe(true);
    expect(harness.controller.copySelection()).toBe(true);
    expect(harness.controller.pasteClipboard()).toBe(true);
    await expect(harness.controller.autoLayoutSelectionOrAll()).resolves.toBe(true);
    harness.controller.selectAllNodes();
    harness.controller.resetToBootstrap();
    harness.controller.deleteSelection();
    harness.controller.disconnectNodeEdges(["node-a"]);
    harness.controller.disconnectPortEdges(["port-a"]);

    expect(harness.orchestrator.saveDraft).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.loadDraft).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.saveWorkspaceFile).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.exportRuntimeContractFile).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.loadWorkspaceFile).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.patchNodePayload).toHaveBeenCalledWith("node-a", { foo: "bar" });
    expect(harness.orchestrator.patchEdgePayload).toHaveBeenCalledWith("edge-a", { baz: 1 });
    expect(harness.orchestrator.patchGroup).toHaveBeenCalledWith("group-a", { title: "Group" });
    expect(harness.orchestrator.patchComment).toHaveBeenCalledWith("comment-a", { text: "Comment" });
    expect(harness.orchestrator.patchSubgraph).toHaveBeenCalledWith("subgraph-a", { title: "Subgraph" });
    expect(harness.orchestrator.createGroupFromSelection).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.createSubgraphFromSelection).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.createCommentAtViewportCenter).toHaveBeenCalledWith({ width: 800, height: 600 });
    expect(harness.orchestrator.copySelection).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.pasteClipboard).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.autoLayoutSelectionOrAll).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.selectAllNodes).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.resetToBootstrap).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.deleteSelection).toHaveBeenCalledTimes(1);
    expect(harness.orchestrator.disconnectNodeEdges).toHaveBeenCalledWith(["node-a"]);
    expect(harness.orchestrator.disconnectPortEdges).toHaveBeenCalledWith(["port-a"]);
  });
});
