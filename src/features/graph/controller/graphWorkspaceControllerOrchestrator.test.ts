import { describe, expect, it, vi } from "vitest";
import type { GraphWorkspaceKernel } from "../runtime/graphWorkspaceKernel";
import type { GraphWorkspaceStorage, GraphWorkspaceStorageSnapshot } from "../storage/graphWorkspaceStorage";
import { createInitialGraphViewState } from "../state/graphViewState";
import {
  createGraphDocument,
  createGraphEdge,
  createGraphPoint,
} from "../document/graphDocument";
import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphWorkspaceExportResult } from "../runtime/graphWorkspaceExport";
import { GRAPH_RUNTIME_CONTRACT_SCHEMA } from "../runtime/graphWorkspaceExport";
import { createGraphSubgraphRuntime } from "../runtime/graphSubgraphRuntime";
import { createGraphWorkspaceControllerOrchestrator } from "./graphWorkspaceControllerOrchestrator";
import { instantiateTestNode } from "../testing/graphTestUtils";

function createState(id: string): GraphWorkspaceRuntimeState {
  return {
    document: createGraphDocument({ id }),
    viewState: createInitialGraphViewState(),
  };
}

function createActionState(): GraphWorkspaceRuntimeState {
  const startNode = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
  const markerNode = instantiateTestNode("scene.spawn-marker", "node-marker", { x: 160, y: 40 });
  return {
    document: createGraphDocument({
      id: "graph-action-state",
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
      comments: [{
        id: "comment-1",
        text: "Existing",
        position: { x: 0, y: 0 },
        size: { width: 100, height: 80 },
        tone: "info",
      }],
      subgraphs: [{ id: "subgraph-1", title: "Existing", nodeIds: [startNode.id] }],
    }),
    viewState: createInitialGraphViewState({
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
  };
}

const emptySubgraphAnalysis = createGraphSubgraphRuntime().analyze(createGraphDocument({
  id: "graph-orchestrator-analysis",
}));

function createExportResult(overrides: Partial<GraphWorkspaceExportResult> = {}): GraphWorkspaceExportResult {
  return {
    ok: true,
    analysis: {
      topologyPolicy: "dag",
      hasCycle: false,
      topologicalOrder: [],
      rootNodeIds: [],
      leafNodeIds: [],
      connectedComponents: [],
      subgraphAnalysis: emptySubgraphAnalysis,
    },
    validation: {
      valid: true,
      issues: [],
      blockingIssues: [],
      warningCount: 0,
      errorCount: 0,
    },
    issues: [],
    runtimeContract: null,
    artifact: {
      format: "json",
      schema: GRAPH_RUNTIME_CONTRACT_SCHEMA,
      suggestedFileName: "graph.runtime.json",
      content: "{\"ok\":true}",
    },
    ...overrides,
  };
}

function createHarness(initialState: GraphWorkspaceRuntimeState = createState("graph-current")) {
  let currentState = initialState;
  let currentSnapshot: GraphWorkspaceStorageSnapshot = {
    hasSavedSnapshot: true,
    storageKey: "draft-storage",
    savedAt: "2026-03-31T00:10:00.000Z",
  };
  const clipboardRef = { current: null as import("../runtime/graphClipboard").GraphClipboardSnapshot | null };
  const pasteSequenceRef = { current: 0 };
  const log = vi.fn<(level: "debug" | "info" | "warn" | "error", scope: string, message: string) => void>();
  const syncStateFromRuntime = vi.fn<(state: GraphWorkspaceRuntimeState, options?: { bumpHistory?: boolean }) => void>();
  const setPersistenceSnapshot = vi.fn<(snapshot: GraphWorkspaceStorageSnapshot) => void>();
  const setClipboardSummary = vi.fn<(summary: { nodeCount: number; edgeCount: number; copiedAt: string } | null) => void>();
  const setWorkspaceFileSnapshot = vi.fn<(snapshot: { path: string; exists: boolean; backend: string; readAt?: string; writtenAt?: string } | null) => void>();
  const setRuntimeContractFileSnapshot = vi.fn<(snapshot: { path: string; exists: boolean; backend: string; exportedAt?: string } | null) => void>();

  const layoutService = {
    buildApplyLayoutCommand: vi.fn(async () => ({
      providerId: "dagre",
      command: {
        type: "graph.apply-node-layout" as const,
        entries: [{ nodeId: "node-start", position: { x: 24, y: 12 } }],
      },
    })),
  };

  const kernel = {
    getState: vi.fn(() => currentState),
    replaceState: vi.fn((state: GraphWorkspaceRuntimeState) => {
      currentState = state;
    }),
    compileForExport: vi.fn(() => createExportResult()),
    execute: vi.fn((command) => {
      currentState = {
        ...currentState,
        document: {
          ...currentState.document,
          metadata: {
            lastCommand: command.type,
          },
        },
      };
      return currentState;
    }),
    undo: vi.fn(() => currentState),
    redo: vi.fn(() => currentState),
    setSelection: vi.fn((selection) => {
      currentState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          selection: {
            ...currentState.viewState.selection,
            ...selection,
          },
        },
      };
      return currentState;
    }),
    patchViewport: vi.fn((patch) => {
      currentState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          viewport: {
            ...currentState.viewState.viewport,
            ...patch,
          },
        },
      };
      return currentState;
    }),
    patchInteraction: vi.fn((patch) => {
      currentState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          interaction: {
            ...currentState.viewState.interaction,
            ...patch,
          },
        },
      };
      return currentState;
    }),
    setConnectionPreview: vi.fn((state) => {
      currentState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          connectionPreview: state,
        },
      };
      return currentState;
    }),
    deleteSelection: vi.fn(() => currentState),
    disconnectNodeEdges: vi.fn(() => currentState),
    disconnectPortEdges: vi.fn(() => currentState),
    selectAllNodes: vi.fn(() => {
      currentState = {
        ...currentState,
        viewState: {
          ...currentState.viewState,
          selection: {
            ...currentState.viewState.selection,
            selectedNodeIds: ["node-a", "node-b"],
          },
        },
      };
      return currentState;
    }),
    layoutService,
  } as unknown as GraphWorkspaceKernel;

  let storedDraft: { savedAt: string; runtimeState: GraphWorkspaceRuntimeState } | null = null;
  const storage = {
    getSnapshot: vi.fn(() => currentSnapshot),
    load: vi.fn(() => storedDraft),
    save: vi.fn(() => {
      currentSnapshot = {
        hasSavedSnapshot: true,
        storageKey: "draft-storage",
        savedAt: "2026-03-31T00:10:00.000Z",
      };
      return currentSnapshot;
    }),
    clear: vi.fn(() => {
      currentSnapshot = {
        hasSavedSnapshot: false,
        storageKey: "draft-storage",
      };
      return currentSnapshot;
    }),
  } as unknown as GraphWorkspaceStorage;

  const readWorkspaceGraphFileInfo = vi.fn(async () => ({
    path: "graph.json",
    exists: true,
    backend: "tauri",
  }));
  const readWorkspaceGraphFile = vi.fn(async () => ({
    path: "graph.json",
    exists: true,
    backend: "tauri",
    readAt: "2026-03-31T01:00:00.000Z",
    content: "serialized-graph",
  }));
  const writeWorkspaceGraphFile = vi.fn(async ({ content }: { content: string }) => ({
    path: "graph.json",
    exists: true,
    backend: "tauri",
    writtenAt: "2026-03-31T02:00:00.000Z",
    content,
  }));
  const writeWorkspaceRuntimeContractFile = vi.fn(async ({ content }: { content: string }) => ({
    path: "graph.runtime.json",
    exists: true,
    backend: "tauri",
    writtenAt: "2026-03-31T03:00:00.000Z",
    content,
  }));
  const serializeGraphDocumentFile = vi.fn(() => "serialized-document");
  const deserializeGraphDocumentFile = vi.fn(() => createState("graph-from-host"));
  let canApplyHostIo = true;

  const orchestrator = createGraphWorkspaceControllerOrchestrator({
    kernel,
    storage,
    bootstrapRuntimeState: createState("graph-bootstrap"),
    clipboardRef,
    pasteSequenceRef,
    cloneRuntimeState: (state) => JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState,
    serializeGraphDocumentFile,
    deserializeGraphDocumentFile,
    readWorkspaceGraphFileInfo,
    readWorkspaceGraphFile,
    writeWorkspaceGraphFile: async ({ content }) => {
      const result = await writeWorkspaceGraphFile({ content });
      return {
        path: result.path,
        exists: result.exists,
        backend: result.backend,
        writtenAt: result.writtenAt,
      };
    },
    writeWorkspaceRuntimeContractFile: async ({ content }) => {
      const result = await writeWorkspaceRuntimeContractFile({ content });
      return {
        path: result.path,
        exists: result.exists,
        backend: result.backend,
        writtenAt: result.writtenAt,
      };
    },
    syncStateFromRuntime,
    setPersistenceSnapshot,
    setClipboardSummary,
    setWorkspaceFileSnapshot,
    setRuntimeContractFileSnapshot,
    log,
    canApplyHostIoState: () => canApplyHostIo,
  });

  return {
    kernel,
    storage,
    layoutService,
    clipboardRef,
    pasteSequenceRef,
    orchestrator,
    log,
    syncStateFromRuntime,
    setPersistenceSnapshot,
    setClipboardSummary,
    setWorkspaceFileSnapshot,
    setRuntimeContractFileSnapshot,
    readWorkspaceGraphFileInfo,
    readWorkspaceGraphFile,
    writeWorkspaceGraphFile,
    writeWorkspaceRuntimeContractFile,
    serializeGraphDocumentFile,
    deserializeGraphDocumentFile,
    setStoredDraft(value: { savedAt: string; runtimeState: GraphWorkspaceRuntimeState } | null) {
      storedDraft = value;
    },
    setCanApplyHostIo(value: boolean) {
      canApplyHostIo = value;
    },
  };
}

describe("graphWorkspaceControllerOrchestrator", () => {
  it("executes history commands with sync and info log", () => {
    const harness = createHarness();

    harness.orchestrator.executeCommand(
      {
        type: "graph.patch-node-payload",
        nodeId: "node-a",
        payloadPatch: { foo: "bar" },
      },
      "执行测试命令",
    );

    expect(harness.kernel.execute).toHaveBeenCalledTimes(1);
    expect(harness.syncStateFromRuntime).toHaveBeenCalledWith(expect.any(Object), { bumpHistory: true });
    expect(harness.log).toHaveBeenCalledWith("info", "graph", "执行测试命令");
  });

  it("keeps selection and viewport mutations outside history bumps", () => {
    const harness = createHarness();

    harness.orchestrator.setSelection({ selectedNodeIds: ["node-a"] });
    harness.orchestrator.patchViewport({ zoom: 1.5 });
    harness.orchestrator.patchInteraction({ hoveredNodeId: "node-a" });
    harness.orchestrator.setConnectionPreview({ active: true, fromNodeId: "node-a" });
    harness.orchestrator.centerViewportOnPoint(createGraphPoint(20, 10), { width: 100, height: 50 });

    expect(harness.syncStateFromRuntime).toHaveBeenCalledTimes(5);
    expect(harness.syncStateFromRuntime.mock.calls.every(([, options]) => options?.bumpHistory === undefined)).toBe(true);
    expect(harness.kernel.patchViewport).toHaveBeenLastCalledWith({
      panX: 20,
      panY: 10,
    });
  });

  it("loads draft through unified replace pipeline and warns when missing", () => {
    const harness = createHarness();

    expect(harness.orchestrator.loadDraft()).toBe(false);
    expect(harness.log).toHaveBeenCalledWith("warn", "graph", "当前没有可恢复的 Graph Workspace 草稿。");

    const draftState = createState("graph-draft");
    harness.setStoredDraft({
      savedAt: "2026-03-31T00:20:00.000Z",
      runtimeState: draftState,
    });

    expect(harness.orchestrator.loadDraft()).toBe(true);
    expect(harness.kernel.replaceState).toHaveBeenLastCalledWith(draftState, { resetHistory: true });
    expect(harness.syncStateFromRuntime).toHaveBeenLastCalledWith(draftState, { bumpHistory: true });
    expect(harness.setPersistenceSnapshot).toHaveBeenLastCalledWith({
      hasSavedSnapshot: true,
      storageKey: "draft-storage",
      savedAt: "2026-03-31T00:10:00.000Z",
    });
    expect(harness.log).toHaveBeenLastCalledWith("info", "graph", "Graph Workspace 草稿已恢复：2026-03-31T00:20:00.000Z");
  });

  it("orchestrates host hydrate, save, load and export flows", async () => {
    const harness = createHarness(createActionState());

    await harness.orchestrator.hydrateWorkspaceFileFromHost(true);
    expect(harness.readWorkspaceGraphFileInfo).toHaveBeenCalledTimes(1);
    expect(harness.setWorkspaceFileSnapshot).toHaveBeenCalledWith({
      path: "graph.json",
      exists: true,
      backend: "tauri",
      readAt: "2026-03-31T01:00:00.000Z",
    });
    expect(harness.kernel.replaceState).toHaveBeenCalledWith(createState("graph-from-host"), { resetHistory: true });

    await expect(harness.orchestrator.saveWorkspaceFile()).resolves.toBe(true);
    expect(harness.serializeGraphDocumentFile).toHaveBeenCalledTimes(1);
    expect(harness.writeWorkspaceGraphFile).toHaveBeenCalledWith({ content: "serialized-document" });

    await expect(harness.orchestrator.loadWorkspaceFile()).resolves.toBe(true);
    expect(harness.readWorkspaceGraphFile).toHaveBeenCalled();

    await expect(harness.orchestrator.exportRuntimeContractFile()).resolves.toBe(true);
    expect(harness.writeWorkspaceRuntimeContractFile).toHaveBeenCalledWith({ content: "{\"ok\":true}" });
    expect(harness.setRuntimeContractFileSnapshot).toHaveBeenCalledWith({
      path: "graph.runtime.json",
      exists: true,
      backend: "tauri",
      exportedAt: "2026-03-31T03:00:00.000Z",
    });
  });

  it("skips host state application when host I/O state is disabled", async () => {
    const harness = createHarness();
    harness.setCanApplyHostIo(false);

    await harness.orchestrator.hydrateWorkspaceFileFromHost(false);
    await expect(harness.orchestrator.saveWorkspaceFile()).resolves.toBe(false);
    await expect(harness.orchestrator.loadWorkspaceFile()).resolves.toBe(false);
    await expect(harness.orchestrator.exportRuntimeContractFile()).resolves.toBe(false);

    expect(harness.setWorkspaceFileSnapshot).not.toHaveBeenCalled();
    expect(harness.setRuntimeContractFileSnapshot).not.toHaveBeenCalled();
  });

  it("orchestrates create group/subgraph/comment actions", () => {
    const harness = createHarness(createActionState());

    expect(harness.orchestrator.createGroupFromSelection()).toBe(true);
    expect(harness.kernel.execute).toHaveBeenCalledWith(expect.objectContaining({
      type: "graph.add-group",
      group: expect.objectContaining({ id: "group-2" }),
    }));

    expect(harness.orchestrator.createSubgraphFromSelection()).toBe(true);
    expect(harness.kernel.execute).toHaveBeenCalledWith(expect.objectContaining({
      type: "graph.add-subgraph",
      subgraph: expect.objectContaining({ id: "subgraph-2", entryNodeId: "node-marker" }),
    }));

    expect(harness.orchestrator.createCommentAtViewportCenter({ width: 500, height: 300 })).toBe(true);
    expect(harness.kernel.execute).toHaveBeenCalledWith(expect.objectContaining({
      type: "graph.add-comment",
      comment: expect.objectContaining({ id: "comment-2" }),
    }));
  });

  it("orchestrates copy and paste clipboard flows", () => {
    const harness = createHarness(createActionState());

    expect(harness.orchestrator.copySelection()).toBe(true);
    expect(harness.clipboardRef.current).not.toBeNull();
    expect(harness.pasteSequenceRef.current).toBe(0);
    expect(harness.setClipboardSummary).toHaveBeenCalledWith(expect.objectContaining({
      nodeCount: 2,
      edgeCount: 1,
    }));

    expect(harness.orchestrator.pasteClipboard()).toBe(true);
    expect(harness.pasteSequenceRef.current).toBe(1);
    expect(harness.kernel.execute).toHaveBeenLastCalledWith(expect.objectContaining({
      type: "graph.paste-clipboard",
      offset: { x: 56, y: 56 },
    }));

    const emptyHarness = createHarness(createState("graph-empty"));
    expect(emptyHarness.orchestrator.copySelection()).toBe(false);
    expect(emptyHarness.orchestrator.pasteClipboard()).toBe(false);
  });

  it("orchestrates auto layout and shared reset/select flows", async () => {
    const harness = createHarness(createActionState());

    await expect(harness.orchestrator.autoLayoutSelectionOrAll()).resolves.toBe(true);
    expect(harness.layoutService.buildApplyLayoutCommand).toHaveBeenCalledTimes(1);
    expect(harness.kernel.execute).toHaveBeenLastCalledWith({
      type: "graph.apply-node-layout",
      entries: [{ nodeId: "node-start", position: { x: 24, y: 12 } }],
    });

    harness.orchestrator.selectAllNodes();
    expect(harness.kernel.selectAllNodes).toHaveBeenCalledTimes(1);
    expect(harness.log).toHaveBeenCalledWith("info", "graph", "已全选 Graph 节点：2 个。");

    harness.orchestrator.resetToBootstrap();
    expect(harness.storage.clear).toHaveBeenCalledTimes(1);
    expect(harness.setPersistenceSnapshot).toHaveBeenLastCalledWith({
      hasSavedSnapshot: false,
      storageKey: "draft-storage",
    });
  });
});
