import type { AppLogLevel } from "../../../shared/logging/LogContext";
import type {
  ReadWorkspaceGraphFileResult,
  WorkspaceGraphFileInfo,
  WriteWorkspaceGraphFileResult,
  WriteWorkspaceRuntimeContractFileResult,
} from "../../../host/types/host";
import type {
  GraphComment,
  GraphGroup,
  GraphPoint,
  GraphSubgraph,
  NodeId,
  PortId,
} from "../document/graphDocument";
import type { GraphWorkspaceCommand, GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphClipboardSnapshot } from "../runtime/graphClipboard";
import type {
  GraphConnectionPreviewState,
  GraphInteractionState,
  GraphSelectionState,
  GraphViewportState,
} from "../state/graphViewState";
import type { GraphWorkspaceKernel } from "../runtime/graphWorkspaceKernel";
import type { GraphWorkspaceStorage, GraphWorkspaceStorageSnapshot } from "../storage/graphWorkspaceStorage";
import {
  exportGraphRuntimeContractToHost,
  hydrateGraphWorkspaceFromHost,
  loadGraphWorkspaceFromHost,
  saveGraphWorkspaceToHost,
  type GraphRuntimeContractFileSnapshot,
  type GraphWorkspaceFileSnapshot,
} from "./graphWorkspaceHostPersistence";
import {
  buildCreateCommentAtViewportCenterCommand,
  buildCreateGroupFromSelectionCommand,
  buildCreateSubgraphFromSelectionCommand,
  buildGraphCopySelectionResult,
  buildGraphPasteClipboardCommand,
  formatAutoLayoutAppliedMessage,
} from "./graphWorkspaceControllerActions";

interface SyncStateOptions {
  bumpHistory?: boolean;
}

interface ReplaceRuntimeStateOptions {
  resetHistory?: boolean;
  bumpHistory?: boolean;
  persistenceSnapshot?: GraphWorkspaceStorageSnapshot;
  logLevel?: AppLogLevel;
  logMessage?: string;
}

interface GraphClipboardSummaryLike {
  nodeCount: number;
  edgeCount: number;
  copiedAt: string;
}

export interface GraphWorkspaceControllerOrchestrator {
  executeCommand(command: GraphWorkspaceCommand, message: string): void;
  undo(): void;
  redo(): void;
  replaceRuntimeState(state: GraphWorkspaceRuntimeState, options?: ReplaceRuntimeStateOptions): void;
  setSelection(selection: Partial<GraphSelectionState>): void;
  patchViewport(patch: Partial<GraphViewportState>): void;
  patchInteraction(patch: Partial<GraphInteractionState>): void;
  centerViewportOnPoint(point: GraphPoint, viewportSize: { width: number; height: number }): void;
  setConnectionPreview(state: GraphConnectionPreviewState): void;
  saveDraft(): void;
  loadDraft(): boolean;
  hydrateWorkspaceFileFromHost(hasSavedDraft: boolean): Promise<void>;
  saveWorkspaceFile(): Promise<boolean>;
  exportRuntimeContractFile(): Promise<boolean>;
  loadWorkspaceFile(): Promise<boolean>;
  patchNodePayload(nodeId: NodeId, payloadPatch: Record<string, unknown>): void;
  patchEdgePayload(edgeId: string, payloadPatch: Record<string, unknown>): void;
  patchGroup(groupId: string, patch: Partial<GraphGroup>): void;
  patchComment(commentId: string, patch: Partial<GraphComment>): void;
  patchSubgraph(subgraphId: string, patch: Partial<GraphSubgraph>): void;
  createGroupFromSelection(): boolean;
  createSubgraphFromSelection(): boolean;
  createCommentAtViewportCenter(viewportSize: { width: number; height: number }): boolean;
  copySelection(): boolean;
  pasteClipboard(): boolean;
  autoLayoutSelectionOrAll(): Promise<boolean>;
  selectAllNodes(): void;
  resetToBootstrap(): void;
  deleteSelection(): void;
  disconnectNodeEdges(nodeIds: NodeId[]): void;
  disconnectPortEdges(portIds: PortId[]): void;
}

interface CreateGraphWorkspaceControllerOrchestratorOptions {
  kernel: GraphWorkspaceKernel;
  storage: GraphWorkspaceStorage;
  bootstrapRuntimeState: GraphWorkspaceRuntimeState;
  clipboardRef: { current: GraphClipboardSnapshot | null };
  pasteSequenceRef: { current: number };
  cloneRuntimeState: (state: GraphWorkspaceRuntimeState) => GraphWorkspaceRuntimeState;
  serializeGraphDocumentFile: (state: GraphWorkspaceRuntimeState) => string;
  deserializeGraphDocumentFile: (content: string) => GraphWorkspaceRuntimeState;
  readWorkspaceGraphFileInfo: () => Promise<WorkspaceGraphFileInfo>;
  readWorkspaceGraphFile: () => Promise<ReadWorkspaceGraphFileResult>;
  writeWorkspaceGraphFile: (request: { content: string }) => Promise<WriteWorkspaceGraphFileResult>;
  writeWorkspaceRuntimeContractFile: (request: { content: string }) => Promise<WriteWorkspaceRuntimeContractFileResult>;
  syncStateFromRuntime: (nextState: GraphWorkspaceRuntimeState, options?: SyncStateOptions) => void;
  setPersistenceSnapshot: (snapshot: GraphWorkspaceStorageSnapshot) => void;
  setClipboardSummary: (summary: GraphClipboardSummaryLike | null) => void;
  setWorkspaceFileSnapshot: (snapshot: GraphWorkspaceFileSnapshot | null) => void;
  setRuntimeContractFileSnapshot: (snapshot: GraphRuntimeContractFileSnapshot | null) => void;
  log: (level: AppLogLevel, scope: string, message: string) => void;
  canApplyHostIoState?: () => boolean;
}

export function createGraphWorkspaceControllerOrchestrator(
  options: CreateGraphWorkspaceControllerOrchestratorOptions,
): GraphWorkspaceControllerOrchestrator {
  const logGraph = (level: AppLogLevel, message: string) => {
    options.log(level, "graph", message);
  };
  // host I/O 是异步的，组件已卸载时不再回写 React state，避免落入过期提交。
  const canApplyHostIoState = () => options.canApplyHostIoState?.() ?? true;

  const syncRuntimeState = (nextState: GraphWorkspaceRuntimeState, syncOptions?: SyncStateOptions) => {
    options.syncStateFromRuntime(nextState, syncOptions);
  };

  const executeRuntimeMutation = (
    run: () => GraphWorkspaceRuntimeState,
    mutationOptions?: {
      bumpHistory?: boolean;
      logLevel?: AppLogLevel;
      logMessage?: string;
    },
  ) => {
    const nextState = run();
    syncRuntimeState(nextState, { bumpHistory: mutationOptions?.bumpHistory });
    if (mutationOptions?.logMessage) {
      logGraph(mutationOptions.logLevel ?? "info", mutationOptions.logMessage);
    }
    return nextState;
  };

  const replaceRuntimeState = (state: GraphWorkspaceRuntimeState, replaceOptions?: ReplaceRuntimeStateOptions) => {
    // 所有“整包替换 runtime state”的入口统一走这里，确保 history / persistence / log 口径一致。
    options.kernel.replaceState(options.cloneRuntimeState(state), {
      resetHistory: replaceOptions?.resetHistory,
    });
    syncRuntimeState(options.kernel.getState(), { bumpHistory: replaceOptions?.bumpHistory });
    if (replaceOptions?.persistenceSnapshot) {
      options.setPersistenceSnapshot(replaceOptions.persistenceSnapshot);
    }
    if (replaceOptions?.logMessage) {
      logGraph(replaceOptions.logLevel ?? "info", replaceOptions.logMessage);
    }
  };

  return {
    executeCommand(command, message) {
      executeRuntimeMutation(() => options.kernel.execute(command), {
        bumpHistory: true,
        logMessage: message,
      });
    },
    undo() {
      const nextState = options.kernel.undo();
      if (!nextState) {
        return;
      }

      syncRuntimeState(nextState, { bumpHistory: true });
      logGraph("info", "已执行 Graph Undo");
    },
    redo() {
      const nextState = options.kernel.redo();
      if (!nextState) {
        return;
      }

      syncRuntimeState(nextState, { bumpHistory: true });
      logGraph("info", "已执行 Graph Redo");
    },
    replaceRuntimeState,
    setSelection(selection) {
      executeRuntimeMutation(() => options.kernel.setSelection(selection));
    },
    patchViewport(patch) {
      executeRuntimeMutation(() => options.kernel.patchViewport(patch));
    },
    patchInteraction(patch) {
      executeRuntimeMutation(() => options.kernel.patchInteraction(patch));
    },
    centerViewportOnPoint(point, viewportSize) {
      const zoom = options.kernel.getState().viewState.viewport.zoom;
      executeRuntimeMutation(() => options.kernel.patchViewport({
        panX: viewportSize.width * 0.5 - point.x * zoom,
        panY: viewportSize.height * 0.5 - point.y * zoom,
      }));
    },
    setConnectionPreview(state) {
      executeRuntimeMutation(() => options.kernel.setConnectionPreview(state));
    },
    saveDraft() {
      const snapshot = options.storage.save(options.kernel.getState());
      options.setPersistenceSnapshot(snapshot);
      logGraph("info", `Graph Workspace 草稿已保存：${snapshot.savedAt ?? "unknown"}`);
    },
    loadDraft() {
      const stored = options.storage.load();
      if (!stored) {
        logGraph("warn", "当前没有可恢复的 Graph Workspace 草稿。");
        return false;
      }

      replaceRuntimeState(stored.runtimeState, {
        resetHistory: true,
        bumpHistory: true,
        persistenceSnapshot: options.storage.getSnapshot(),
        logMessage: `Graph Workspace 草稿已恢复：${stored.savedAt}`,
      });
      return true;
    },
    async hydrateWorkspaceFileFromHost(hasSavedDraft) {
      try {
        const result = await hydrateGraphWorkspaceFromHost({
          readWorkspaceGraphFileInfo: options.readWorkspaceGraphFileInfo,
          readWorkspaceGraphFile: options.readWorkspaceGraphFile,
          deserializeGraphDocumentFile: options.deserializeGraphDocumentFile,
          storage: options.storage,
        });
        if (!canApplyHostIoState()) {
          return;
        }

        options.setWorkspaceFileSnapshot(result.workspaceFileSnapshot);

        if (!result.nextState) {
          logGraph(
            "info",
            `当前没有正式 Graph 文件，继续使用${hasSavedDraft ? "本地草稿" : "bootstrap 初始图"}。`,
          );
          return;
        }

        replaceRuntimeState(result.nextState, {
          resetHistory: true,
          bumpHistory: true,
          persistenceSnapshot: result.persistenceSnapshot ?? undefined,
          logMessage: `已从正式 Graph 文件加载工作区：${result.workspaceFileSnapshot.path}`,
        });
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件加载失败";
        logGraph("error", `正式 Graph 文件加载失败：${message}`);
      }
    },
    async saveWorkspaceFile() {
      try {
        const result = await saveGraphWorkspaceToHost({
          state: options.kernel.getState(),
          serializeGraphDocumentFile: options.serializeGraphDocumentFile,
          writeWorkspaceGraphFile: options.writeWorkspaceGraphFile,
          storage: options.storage,
        });
        if (!canApplyHostIoState()) {
          return false;
        }

        options.setWorkspaceFileSnapshot(result.workspaceFileSnapshot);
        options.setPersistenceSnapshot(result.persistenceSnapshot);
        logGraph("info", `正式 Graph 文件已保存：${result.workspaceFileSnapshot.path}`);
        return true;
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件保存失败";
        logGraph("error", `正式 Graph 文件保存失败：${message}`);
        return false;
      }
    },
    async exportRuntimeContractFile() {
      try {
        const result = await exportGraphRuntimeContractToHost({
          exportResult: options.kernel.compileForExport(),
          writeWorkspaceRuntimeContractFile: options.writeWorkspaceRuntimeContractFile,
        });
        if (!canApplyHostIoState()) {
          return false;
        }
        if (!result.ok) {
          logGraph("error", `Runtime contract 导出失败：${result.summary}`);
          return false;
        }

        options.setRuntimeContractFileSnapshot(result.runtimeContractFileSnapshot);
        if (result.warningCount > 0) {
          logGraph(
            "warn",
            `Runtime contract 已导出：${result.runtimeContractFileSnapshot.path}；当前仍存在 ${result.warningCount} 个非阻塞问题。`,
          );
        } else {
          logGraph("info", `Runtime contract 已导出：${result.runtimeContractFileSnapshot.path}`);
        }
        return true;
      } catch (error) {
        const message = error instanceof Error ? error.message : "Runtime contract 导出失败";
        logGraph("error", `Runtime contract 导出失败：${message}`);
        return false;
      }
    },
    async loadWorkspaceFile() {
      try {
        const result = await loadGraphWorkspaceFromHost({
          readWorkspaceGraphFile: options.readWorkspaceGraphFile,
          deserializeGraphDocumentFile: options.deserializeGraphDocumentFile,
          storage: options.storage,
        });
        if (!canApplyHostIoState()) {
          return false;
        }

        options.setWorkspaceFileSnapshot(result.workspaceFileSnapshot);

        if (!result.nextState) {
          logGraph("warn", "当前没有可加载的正式 Graph 文件。");
          return false;
        }

        replaceRuntimeState(result.nextState, {
          resetHistory: true,
          bumpHistory: true,
          persistenceSnapshot: result.persistenceSnapshot ?? undefined,
          logMessage: `正式 Graph 文件已加载：${result.workspaceFileSnapshot.path}`,
        });
        return true;
      } catch (error) {
        const message = error instanceof Error ? error.message : "正式 Graph 文件加载失败";
        logGraph("error", `正式 Graph 文件加载失败：${message}`);
        return false;
      }
    },
    patchNodePayload(nodeId, payloadPatch) {
      executeRuntimeMutation(() => options.kernel.execute({
        type: "graph.patch-node-payload",
        nodeId,
        payloadPatch,
      }), {
        bumpHistory: true,
        logMessage: `已更新节点 Payload：${nodeId}`,
      });
    },
    patchEdgePayload(edgeId, payloadPatch) {
      executeRuntimeMutation(() => options.kernel.execute({
        type: "graph.patch-edge-payload",
        edgeId,
        payloadPatch,
      }), {
        bumpHistory: true,
        logMessage: `已更新连线 Payload：${edgeId}`,
      });
    },
    patchGroup(groupId, patch) {
      executeRuntimeMutation(() => options.kernel.execute({
        type: "graph.patch-group",
        groupId,
        patch,
      }), {
        bumpHistory: true,
        logMessage: `已更新分组：${groupId}`,
      });
    },
    patchComment(commentId, patch) {
      executeRuntimeMutation(() => options.kernel.execute({
        type: "graph.patch-comment",
        commentId,
        patch,
      }), {
        bumpHistory: true,
        logMessage: `已更新注释：${commentId}`,
      });
    },
    patchSubgraph(subgraphId, patch) {
      executeRuntimeMutation(() => options.kernel.execute({
        type: "graph.patch-subgraph",
        subgraphId,
        patch,
      }), {
        bumpHistory: true,
        logMessage: `已更新子图：${subgraphId}`,
      });
    },
    createGroupFromSelection() {
      const result = buildCreateGroupFromSelectionCommand(options.kernel.getState());
      if (!result.ok) {
        logGraph("warn", "当前没有节点选择，无法创建分组。");
        return false;
      }

      executeRuntimeMutation(() => options.kernel.execute(result.command), {
        bumpHistory: true,
        logMessage: `已根据 ${result.group.nodeIds.length} 个节点创建分组：${result.group.id}`,
      });
      return true;
    },
    createSubgraphFromSelection() {
      const result = buildCreateSubgraphFromSelectionCommand(options.kernel.getState());
      if (!result.ok) {
        logGraph("warn", "当前没有节点选择，无法创建子图。");
        return false;
      }

      executeRuntimeMutation(() => options.kernel.execute(result.command), {
        bumpHistory: true,
        logMessage: `已根据 ${result.subgraph.nodeIds.length} 个节点创建子图：${result.subgraph.id}`,
      });
      return true;
    },
    createCommentAtViewportCenter(viewportSize) {
      const result = buildCreateCommentAtViewportCenterCommand(options.kernel.getState(), viewportSize);
      if (!result.ok) {
        logGraph("warn", "当前视口尺寸无效，无法创建注释。");
        return false;
      }

      executeRuntimeMutation(() => options.kernel.execute(result.command), {
        bumpHistory: true,
        logMessage: `已在视口中心创建注释：${result.comment.id}`,
      });
      return true;
    },
    copySelection() {
      const result = buildGraphCopySelectionResult(options.kernel.getState());
      if (!result.ok) {
        logGraph("warn", "当前没有可复制的节点选择。请先选择至少一个节点。");
        return false;
      }

      options.clipboardRef.current = result.snapshot;
      options.setClipboardSummary(result.summary);
      options.pasteSequenceRef.current = 0;
      logGraph("info", `已复制 Graph 选择：${result.snapshot.nodes.length} 个节点，${result.snapshot.edges.length} 条连线。`);
      return true;
    },
    pasteClipboard() {
      if (!options.clipboardRef.current) {
        logGraph("warn", "当前剪贴板为空，无法粘贴 Graph 选择。请先复制节点。");
        return false;
      }

      options.pasteSequenceRef.current += 1;
      executeRuntimeMutation(() => options.kernel.execute(
        buildGraphPasteClipboardCommand(options.clipboardRef.current!, options.pasteSequenceRef.current),
      ), {
        bumpHistory: true,
        logMessage: `已粘贴 Graph 选择：第 ${options.pasteSequenceRef.current} 次偏移粘贴。`,
      });
      return true;
    },
    async autoLayoutSelectionOrAll() {
      const currentState = options.kernel.getState();
      const result = await options.kernel.layoutService.buildApplyLayoutCommand(
        currentState.document,
        currentState.viewState.selection.selectedNodeIds,
      );

      if (!result) {
        logGraph("warn", "当前没有可自动布局的节点，或布局 provider 未返回有效结果。");
        return false;
      }

      executeRuntimeMutation(() => options.kernel.execute(result.command), {
        bumpHistory: true,
        logMessage: formatAutoLayoutAppliedMessage(
          result.providerId,
          currentState.viewState.selection.selectedNodeIds.length,
          currentState.document.nodes.length,
        ),
      });
      return true;
    },
    selectAllNodes() {
      const nextState = executeRuntimeMutation(() => options.kernel.selectAllNodes());
      logGraph("info", `已全选 Graph 节点：${nextState.viewState.selection.selectedNodeIds.length} 个。`);
    },
    resetToBootstrap() {
      options.storage.clear();
      replaceRuntimeState(options.bootstrapRuntimeState, {
        resetHistory: true,
        bumpHistory: true,
        persistenceSnapshot: options.storage.getSnapshot(),
        logMessage: "Graph Workspace 已恢复到 bootstrap 初始状态。",
      });
    },
    deleteSelection() {
      executeRuntimeMutation(() => options.kernel.deleteSelection(), { bumpHistory: true });
    },
    disconnectNodeEdges(nodeIds) {
      executeRuntimeMutation(() => options.kernel.disconnectNodeEdges(nodeIds), { bumpHistory: true });
    },
    disconnectPortEdges(portIds) {
      executeRuntimeMutation(() => options.kernel.disconnectPortEdges(portIds), { bumpHistory: true });
    },
  };
}

