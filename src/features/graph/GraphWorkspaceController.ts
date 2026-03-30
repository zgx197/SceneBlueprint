import { useEffect, useMemo, useRef, useState } from "react";
import type { GraphDocument, GraphPoint, NodeId, PortId } from "./document/graphDocument";
import { createGraphDefinitionRegistry, type GraphDefinitionRegistry } from "./definitions/graphDefinitions";
import { defaultGraphNodeDefinitions } from "./definitions/defaultGraphNodeDefinitions";
import type {
  GraphCommandBusSnapshot,
  GraphWorkspaceCommand,
  GraphWorkspaceRuntimeState,
} from "./commands/graphCommands";
import type {
  GraphConnectionPreviewState,
  GraphInteractionState,
  GraphSelectionState,
  GraphViewState,
  GraphViewportState,
} from "./state/graphViewState";
import {
  createGraphInspectorBinding,
  type GraphInspectorBinding,
  type WorkspaceSelectionTarget,
} from "./binding/graphInspectorBinding";
import {
  createGraphWorkspaceStorage,
  type GraphWorkspaceStorage,
  type GraphWorkspaceStorageSnapshot,
} from "./storage/graphWorkspaceStorage";
import { useAppLogContext } from "../../shared/logging/LogContext";
import {
  createBootstrapGraphWorkspaceRuntimeState,
  createGraphWorkspaceRuntime,
  type GraphWorkspaceRuntime,
} from "./runtime/graphWorkspaceRuntime";
import { createGraphFrameBuilder, type GraphFrameBuilder } from "./frame/graphFrameBuilder";
import type { GraphFrame } from "./frame/graphFrame";
import { createCanvasTextMeasurer } from "../../host/measurement/textMeasurer";
import {
  readWorkspaceGraphFile,
  readWorkspaceGraphFileInfo,
  writeWorkspaceGraphFile,
} from "../../host/api/commands";
import type { WorkspaceGraphFileInfo } from "../../host/types/host";
import {
  deserializeGraphDocumentFile,
  serializeGraphDocumentFile,
} from "./serialization/graphDocumentFile";
import {
  createGraphClipboardSnapshot,
  getGraphClipboardSummary,
  type GraphClipboardSnapshot,
} from "./runtime/graphClipboard";
import { buildGraphAutoLayoutCommand } from "./runtime/graphAutoLayout";

const GRAPH_WORKSPACE_STORAGE_KEY = "sceneblueprint.graph-workspace.draft";
const GRAPH_PASTE_OFFSET_STEP = 56;

export interface GraphWorkspaceFileSnapshot extends WorkspaceGraphFileInfo {
  readAt?: string;
  writtenAt?: string;
}

export interface GraphClipboardSummary {
  nodeCount: number;
  edgeCount: number;
  copiedAt: string;
}

export interface GraphWorkspaceController {
  readonly document: GraphDocument;
  readonly viewState: GraphViewState;
  readonly definitions: GraphDefinitionRegistry;
  readonly graphFrame: GraphFrame;
  readonly commandSnapshot: GraphCommandBusSnapshot;
  readonly selectionTarget: WorkspaceSelectionTarget;
  readonly persistenceSnapshot: GraphWorkspaceStorageSnapshot;
  readonly workspaceFileSnapshot: GraphWorkspaceFileSnapshot | null;
  readonly clipboardSummary: GraphClipboardSummary | null;

  execute(command: GraphWorkspaceCommand): void;
  undo(): void;
  redo(): void;
  setSelection(selection: GraphSelectionState): void;
  patchViewport(patch: Partial<GraphViewportState>): void;
  patchInteraction(patch: Partial<GraphInteractionState>): void;
  centerViewportOnPoint(point: GraphPoint, viewportSize: { width: number; height: number }): void;
  setConnectionPreview(state: GraphConnectionPreviewState): void;
  saveDraft(): void;
  loadDraft(): boolean;
  saveWorkspaceFile(): Promise<boolean>;
  loadWorkspaceFile(): Promise<boolean>;
  patchNodePayload(nodeId: NodeId, payloadPatch: Record<string, unknown>): void;
  copySelection(): boolean;
  pasteClipboard(): boolean;
  autoLayoutSelectionOrAll(): boolean;
  selectAllNodes(): void;
  resetToBootstrap(): void;
  deleteSelection(): void;
  disconnectNodeEdges(nodeIds: NodeId[]): void;
  disconnectPortEdges(portIds: PortId[]): void;
}

function cloneRuntimeState(state: GraphWorkspaceRuntimeState): GraphWorkspaceRuntimeState {
  return JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState;
}

function getInitialRuntimeState(
  definitions: GraphDefinitionRegistry,
  storage: GraphWorkspaceStorage,
): GraphWorkspaceRuntimeState {
  const stored = storage.load();
  if (stored) {
    return cloneRuntimeState(stored.runtimeState);
  }

  return createBootstrapGraphWorkspaceRuntimeState(definitions);
}

export function useGraphWorkspaceController(): GraphWorkspaceController {
  const { log } = useAppLogContext();
  const definitions = useMemo(() => createGraphDefinitionRegistry(defaultGraphNodeDefinitions), []);
  const storageRef = useRef<GraphWorkspaceStorage>(
    createGraphWorkspaceStorage({
      storageKey: GRAPH_WORKSPACE_STORAGE_KEY,
    }),
  );
  const bindingRef = useRef<GraphInspectorBinding>(createGraphInspectorBinding());
  const bootstrapRuntimeState = useMemo<GraphWorkspaceRuntimeState>(() => {
    return createBootstrapGraphWorkspaceRuntimeState(definitions);
  }, [definitions]);
  const runtimeRef = useRef<GraphWorkspaceRuntime>(
    createGraphWorkspaceRuntime({
      initialState: getInitialRuntimeState(definitions, storageRef.current),
      definitions,
    }),
  );
  const frameBuilderRef = useRef<GraphFrameBuilder>(
    createGraphFrameBuilder({
      connectionPolicy: runtimeRef.current.connectionPolicy,
      textMeasurer: createCanvasTextMeasurer(),
    }),
  );
  const didHydrateWorkspaceFileRef = useRef(false);
  const clipboardRef = useRef<GraphClipboardSnapshot | null>(null);
  const pasteSequenceRef = useRef(0);

  const [runtimeState, setRuntimeState] = useState<GraphWorkspaceRuntimeState>(() => runtimeRef.current.getState());
  const [historyRevision, setHistoryRevision] = useState(0);
  const [persistenceSnapshot, setPersistenceSnapshot] = useState<GraphWorkspaceStorageSnapshot>(() => {
    return storageRef.current.getSnapshot();
  });
  const [workspaceFileSnapshot, setWorkspaceFileSnapshot] = useState<GraphWorkspaceFileSnapshot | null>(null);
  const [clipboardSummary, setClipboardSummary] = useState<GraphClipboardSummary | null>(null);

  const { document, viewState } = runtimeState;

  const persistableState = useMemo<GraphWorkspaceRuntimeState>(() => {
    return {
      document,
      viewState: {
        ...viewState,
        connectionPreview: {
          active: false,
        },
        interaction: {
          draggingNodeIds: [],
        },
      },
    };
  }, [document, viewState]);

  useEffect(() => {
    const snapshot = storageRef.current.save(persistableState);
    setPersistenceSnapshot(snapshot);
  }, [persistableState]);

  const graphFrame = useMemo(() => {
    return frameBuilderRef.current.build(document, viewState, definitions);
  }, [definitions, document, viewState]);

  const selectionTarget = useMemo(() => {
    return bindingRef.current.getSelectionTarget(document, viewState, definitions);
  }, [definitions, document, viewState]);

  const commandSnapshot = useMemo(() => {
    return runtimeRef.current.getCommandSnapshot();
  }, [historyRevision]);

  const syncStateFromRuntime = (nextState: GraphWorkspaceRuntimeState, options?: { bumpHistory?: boolean }) => {
    setRuntimeState(nextState);
    if (options?.bumpHistory) {
      setHistoryRevision((value) => value + 1);
    }
  };

  useEffect(() => {
    if (didHydrateWorkspaceFileRef.current) {
      return;
    }

    didHydrateWorkspaceFileRef.current = true;
    let disposed = false;

    void (async () => {
      try {
        const info = await readWorkspaceGraphFileInfo();
        if (disposed) {
          return;
        }

        setWorkspaceFileSnapshot(info);

        const result = await readWorkspaceGraphFile();
        if (disposed) {
          return;
        }

        setWorkspaceFileSnapshot({
          path: result.path,
          exists: result.exists,
          backend: result.backend,
          readAt: result.readAt,
        });

        if (!result.content) {
          log("info", "graph", `当前没有正式 Graph 文件，继续使用${persistenceSnapshot.hasSavedSnapshot ? "本地草稿" : "bootstrap 初始图"}。`);
          return;
        }

        const nextState = deserializeGraphDocumentFile(result.content);
        runtimeRef.current.replaceState(cloneRuntimeState(nextState), { resetHistory: true });
        syncStateFromRuntime(runtimeRef.current.getState(), { bumpHistory: true });
        const snapshot = storageRef.current.save(runtimeRef.current.getState());
        setPersistenceSnapshot(snapshot);
        log("info", "graph", `已从正式 Graph 文件加载工作区：${result.path}`);
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件加载失败";
        log("error", "graph", `正式 Graph 文件加载失败：${message}`);
      }
    })();

    return () => {
      disposed = true;
    };
  }, [log, persistenceSnapshot.hasSavedSnapshot]);

  return {
    document,
    viewState,
    definitions,
    graphFrame,
    commandSnapshot,
    selectionTarget,
    persistenceSnapshot,
    workspaceFileSnapshot,
    clipboardSummary,
    execute(command) {
      const nextState = runtimeRef.current.execute(command);
      syncStateFromRuntime(nextState, { bumpHistory: true });
      log("info", "graph", `执行 Graph 命令：${command.type}`);
    },
    undo() {
      const nextState = runtimeRef.current.undo();
      if (!nextState) {
        return;
      }

      syncStateFromRuntime(nextState, { bumpHistory: true });
      log("info", "graph", "已执行 Graph Undo");
    },
    redo() {
      const nextState = runtimeRef.current.redo();
      if (!nextState) {
        return;
      }

      syncStateFromRuntime(nextState, { bumpHistory: true });
      log("info", "graph", "已执行 Graph Redo");
    },
    setSelection(selection) {
      const nextState = runtimeRef.current.setSelection(selection);
      syncStateFromRuntime(nextState);
    },
    patchViewport(patch) {
      const nextState = runtimeRef.current.patchViewport(patch);
      syncStateFromRuntime(nextState);
    },
    patchInteraction(patch) {
      const nextState = runtimeRef.current.patchInteraction(patch);
      syncStateFromRuntime(nextState);
    },
    centerViewportOnPoint(point, viewportSize) {
      const zoom = runtimeRef.current.getState().viewState.viewport.zoom;
      const nextState = runtimeRef.current.patchViewport({
        panX: viewportSize.width * 0.5 - point.x * zoom,
        panY: viewportSize.height * 0.5 - point.y * zoom,
      });
      syncStateFromRuntime(nextState);
    },
    setConnectionPreview(state) {
      const nextState = runtimeRef.current.setConnectionPreview(state);
      syncStateFromRuntime(nextState);
    },
    saveDraft() {
      const snapshot = storageRef.current.save(runtimeRef.current.getState());
      setPersistenceSnapshot(snapshot);
      log("info", "graph", `Graph Workspace 草稿已保存：${snapshot.savedAt ?? "unknown"}`);
    },
    loadDraft() {
      const stored = storageRef.current.load();
      if (!stored) {
        log("warn", "graph", "当前没有可恢复的 Graph Workspace 草稿。");
        return false;
      }

      runtimeRef.current.replaceState(cloneRuntimeState(stored.runtimeState), { resetHistory: true });
      syncStateFromRuntime(runtimeRef.current.getState(), { bumpHistory: true });
      setPersistenceSnapshot(storageRef.current.getSnapshot());
      log("info", "graph", `Graph Workspace 草稿已恢复：${stored.savedAt}`);
      return true;
    },
    async saveWorkspaceFile() {
      try {
        const content = serializeGraphDocumentFile(runtimeRef.current.getState());
        const result = await writeWorkspaceGraphFile({ content });
        setWorkspaceFileSnapshot({
          path: result.path,
          exists: result.exists,
          backend: result.backend,
          writtenAt: result.writtenAt,
        });
        const snapshot = storageRef.current.save(runtimeRef.current.getState());
        setPersistenceSnapshot(snapshot);
        log("info", "graph", `正式 Graph 文件已保存：${result.path}`);
        return true;
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件保存失败";
        log("error", "graph", `正式 Graph 文件保存失败：${message}`);
        return false;
      }
    },
    async loadWorkspaceFile() {
      try {
        const result = await readWorkspaceGraphFile();
        setWorkspaceFileSnapshot({
          path: result.path,
          exists: result.exists,
          backend: result.backend,
          readAt: result.readAt,
        });

        if (!result.content) {
          log("warn", "graph", "当前没有可加载的正式 Graph 文件。");
          return false;
        }

        const nextState = deserializeGraphDocumentFile(result.content);
        runtimeRef.current.replaceState(cloneRuntimeState(nextState), { resetHistory: true });
        syncStateFromRuntime(runtimeRef.current.getState(), { bumpHistory: true });
        const snapshot = storageRef.current.save(runtimeRef.current.getState());
        setPersistenceSnapshot(snapshot);
        log("info", "graph", `正式 Graph 文件已加载：${result.path}`);
        return true;
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件加载失败";
        log("error", "graph", `正式 Graph 文件加载失败：${message}`);
        return false;
      }
    },
    patchNodePayload(nodeId, payloadPatch) {
      const nextState = runtimeRef.current.execute({
        type: "graph.patch-node-payload",
        nodeId,
        payloadPatch,
      });
      syncStateFromRuntime(nextState, { bumpHistory: true });
      log("info", "graph", `已更新节点 Payload：${nodeId}`);
    },
    copySelection() {
      const snapshot = createGraphClipboardSnapshot(runtimeRef.current.getState().document, runtimeRef.current.getState().viewState.selection);
      if (!snapshot) {
        log("warn", "graph", "当前没有可复制的节点选择。请先选择至少一个节点。");
        return false;
      }

      clipboardRef.current = snapshot;
      const summary = getGraphClipboardSummary(snapshot);
      setClipboardSummary(summary);
      pasteSequenceRef.current = 0;
      log("info", "graph", `已复制 Graph 选择：${snapshot.nodes.length} 个节点，${snapshot.edges.length} 条连线。`);
      return true;
    },
    pasteClipboard() {
      if (!clipboardRef.current) {
        log("warn", "graph", "当前剪贴板为空，无法粘贴 Graph 选择。请先复制节点。");
        return false;
      }

      pasteSequenceRef.current += 1;
      const nextState = runtimeRef.current.execute({
        type: "graph.paste-clipboard",
        snapshot: clipboardRef.current,
        offset: {
          x: GRAPH_PASTE_OFFSET_STEP * pasteSequenceRef.current,
          y: GRAPH_PASTE_OFFSET_STEP * pasteSequenceRef.current,
        },
      });
      syncStateFromRuntime(nextState, { bumpHistory: true });
      log("info", "graph", `已粘贴 Graph 选择：第 ${pasteSequenceRef.current} 次偏移粘贴。`);
      return true;
    },
    autoLayoutSelectionOrAll() {
      const currentState = runtimeRef.current.getState();
      const command = buildGraphAutoLayoutCommand(
        currentState.document,
        currentState.viewState.selection.selectedNodeIds,
      );

      if (!command) {
        log("warn", "graph", "当前没有可自动布局的节点。");
        return false;
      }

      const nextState = runtimeRef.current.execute(command);
      syncStateFromRuntime(nextState, { bumpHistory: true });
      log(
        "info",
        "graph",
        currentState.viewState.selection.selectedNodeIds.length > 0
          ? `已对 ${currentState.viewState.selection.selectedNodeIds.length} 个选中节点执行自动布局。`
          : `已对全图 ${currentState.document.nodes.length} 个节点执行自动布局。`,
      );
      return true;
    },
    selectAllNodes() {
      const nextState = runtimeRef.current.selectAllNodes();
      syncStateFromRuntime(nextState);
      log("info", "graph", `已全选 Graph 节点：${nextState.viewState.selection.selectedNodeIds.length} 个。`);
    },
    resetToBootstrap() {
      storageRef.current.clear();
      runtimeRef.current.replaceState(cloneRuntimeState(bootstrapRuntimeState), { resetHistory: true });
      syncStateFromRuntime(runtimeRef.current.getState(), { bumpHistory: true });
      setPersistenceSnapshot(storageRef.current.getSnapshot());
      log("info", "graph", "Graph Workspace 已恢复到 bootstrap 初始状态。");
    },
    deleteSelection() {
      const nextState = runtimeRef.current.deleteSelection();
      syncStateFromRuntime(nextState, { bumpHistory: true });
    },
    disconnectNodeEdges(nodeIds) {
      const nextState = runtimeRef.current.disconnectNodeEdges(nodeIds);
      syncStateFromRuntime(nextState, { bumpHistory: true });
    },
    disconnectPortEdges(portIds) {
      const nextState = runtimeRef.current.disconnectPortEdges(portIds);
      syncStateFromRuntime(nextState, { bumpHistory: true });
    },
  };
}

