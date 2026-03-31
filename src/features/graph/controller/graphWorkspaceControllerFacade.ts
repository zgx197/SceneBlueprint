import type {
  GraphComment,
  GraphDocument,
  GraphGroup,
  GraphPoint,
  GraphSubgraph,
  NodeId,
  PortId,
} from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type {
  GraphCommandBusSnapshot,
  GraphWorkspaceCommand,
} from "../commands/graphCommands";
import type {
  GraphConnectionPreviewState,
  GraphInteractionState,
  GraphSelectionState,
  GraphViewState,
  GraphViewportState,
} from "../state/graphViewState";
import type { WorkspaceSelectionTarget } from "../binding/graphInspectorBinding";
import type {
  GraphWorkspaceStorageSnapshot,
} from "../storage/graphWorkspaceStorage";
import type { GraphFrame } from "../frame/graphFrame";
import type { GraphProfile } from "../profile/graphProfile";
import type { GraphRuntimeBridgeContract } from "../runtime/graphWorkspaceBridge";
import type { GraphLayoutService } from "../services/graphLayoutService";
import type { GraphNodeSearchService } from "../services/graphNodeSearchService";
import type {
  GraphShortcutBindingService,
} from "../services/graphShortcutBindingService";
import type {
  GraphWorkspaceExportPreflight,
  GraphWorkspaceKernelAnalysis,
} from "../runtime/graphWorkspaceKernel";
import type { GraphWorkspaceControllerOrchestrator } from "./graphWorkspaceControllerOrchestrator";
import type {
  GraphRuntimeContractFileSnapshot,
  GraphWorkspaceFileSnapshot,
} from "./graphWorkspaceHostPersistence";

export interface GraphClipboardSummary {
  nodeCount: number;
  edgeCount: number;
  copiedAt: string;
}

export interface GraphWorkspaceController {
  readonly document: GraphDocument;
  readonly viewState: GraphViewState;
  readonly definitions: GraphDefinitionRegistry;
  readonly profile: GraphProfile;
  readonly graphFrame: GraphFrame;
  readonly commandSnapshot: GraphCommandBusSnapshot;
  readonly selectionTarget: WorkspaceSelectionTarget;
  readonly analysis: GraphWorkspaceKernelAnalysis;
  readonly exportPreflight: GraphWorkspaceExportPreflight;
  readonly bridgeContract: GraphRuntimeBridgeContract;
  readonly persistenceSnapshot: GraphWorkspaceStorageSnapshot;
  readonly workspaceFileSnapshot: GraphWorkspaceFileSnapshot | null;
  readonly runtimeContractFileSnapshot: GraphRuntimeContractFileSnapshot | null;
  readonly clipboardSummary: GraphClipboardSummary | null;
  readonly layoutService: GraphLayoutService;
  readonly nodeSearchService: GraphNodeSearchService;
  readonly shortcutBindingService: GraphShortcutBindingService;

  execute(command: GraphWorkspaceCommand): void;
  undo(): void;
  redo(): void;
  setSelection(selection: Partial<GraphSelectionState>): void;
  patchViewport(patch: Partial<GraphViewportState>): void;
  patchInteraction(patch: Partial<GraphInteractionState>): void;
  centerViewportOnPoint(point: GraphPoint, viewportSize: { width: number; height: number }): void;
  setConnectionPreview(state: GraphConnectionPreviewState): void;
  saveDraft(): void;
  loadDraft(): boolean;
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

interface CreateGraphWorkspaceControllerFacadeOptions {
  document: GraphDocument;
  viewState: GraphViewState;
  definitions: GraphDefinitionRegistry;
  profile: GraphProfile;
  graphFrame: GraphFrame;
  commandSnapshot: GraphCommandBusSnapshot;
  selectionTarget: WorkspaceSelectionTarget;
  analysis: GraphWorkspaceKernelAnalysis;
  exportPreflight: GraphWorkspaceExportPreflight;
  bridgeContract: GraphRuntimeBridgeContract;
  persistenceSnapshot: GraphWorkspaceStorageSnapshot;
  workspaceFileSnapshot: GraphWorkspaceFileSnapshot | null;
  runtimeContractFileSnapshot: GraphRuntimeContractFileSnapshot | null;
  clipboardSummary: GraphClipboardSummary | null;
  layoutService: GraphLayoutService;
  nodeSearchService: GraphNodeSearchService;
  shortcutBindingService: GraphShortcutBindingService;
  orchestrator: GraphWorkspaceControllerOrchestrator;
}

export function createGraphWorkspaceControllerFacade(
  options: CreateGraphWorkspaceControllerFacadeOptions,
): GraphWorkspaceController {
  return {
    document: options.document,
    viewState: options.viewState,
    definitions: options.definitions,
    profile: options.profile,
    graphFrame: options.graphFrame,
    commandSnapshot: options.commandSnapshot,
    selectionTarget: options.selectionTarget,
    analysis: options.analysis,
    exportPreflight: options.exportPreflight,
    bridgeContract: options.bridgeContract,
    persistenceSnapshot: options.persistenceSnapshot,
    workspaceFileSnapshot: options.workspaceFileSnapshot,
    runtimeContractFileSnapshot: options.runtimeContractFileSnapshot,
    clipboardSummary: options.clipboardSummary,
    layoutService: options.layoutService,
    nodeSearchService: options.nodeSearchService,
    shortcutBindingService: options.shortcutBindingService,
    execute(command) {
      options.orchestrator.executeCommand(command, `执行 Graph 命令：${command.type}`);
    },
    undo() {
      options.orchestrator.undo();
    },
    redo() {
      options.orchestrator.redo();
    },
    setSelection(selection) {
      options.orchestrator.setSelection(selection);
    },
    patchViewport(patch) {
      options.orchestrator.patchViewport(patch);
    },
    patchInteraction(patch) {
      options.orchestrator.patchInteraction(patch);
    },
    centerViewportOnPoint(point, viewportSize) {
      options.orchestrator.centerViewportOnPoint(point, viewportSize);
    },
    setConnectionPreview(state) {
      options.orchestrator.setConnectionPreview(state);
    },
    saveDraft() {
      options.orchestrator.saveDraft();
    },
    loadDraft() {
      return options.orchestrator.loadDraft();
    },
    async saveWorkspaceFile() {
      return options.orchestrator.saveWorkspaceFile();
    },
    async exportRuntimeContractFile() {
      return options.orchestrator.exportRuntimeContractFile();
    },
    async loadWorkspaceFile() {
      return options.orchestrator.loadWorkspaceFile();
    },
    patchNodePayload(nodeId, payloadPatch) {
      options.orchestrator.patchNodePayload(nodeId, payloadPatch);
    },
    patchEdgePayload(edgeId, payloadPatch) {
      options.orchestrator.patchEdgePayload(edgeId, payloadPatch);
    },
    patchGroup(groupId, patch) {
      options.orchestrator.patchGroup(groupId, patch);
    },
    patchComment(commentId, patch) {
      options.orchestrator.patchComment(commentId, patch);
    },
    patchSubgraph(subgraphId, patch) {
      options.orchestrator.patchSubgraph(subgraphId, patch);
    },
    createGroupFromSelection() {
      return options.orchestrator.createGroupFromSelection();
    },
    createSubgraphFromSelection() {
      return options.orchestrator.createSubgraphFromSelection();
    },
    createCommentAtViewportCenter(viewportSize) {
      return options.orchestrator.createCommentAtViewportCenter(viewportSize);
    },
    copySelection() {
      return options.orchestrator.copySelection();
    },
    pasteClipboard() {
      return options.orchestrator.pasteClipboard();
    },
    async autoLayoutSelectionOrAll() {
      return options.orchestrator.autoLayoutSelectionOrAll();
    },
    selectAllNodes() {
      options.orchestrator.selectAllNodes();
    },
    resetToBootstrap() {
      options.orchestrator.resetToBootstrap();
    },
    deleteSelection() {
      options.orchestrator.deleteSelection();
    },
    disconnectNodeEdges(nodeIds) {
      options.orchestrator.disconnectNodeEdges(nodeIds);
    },
    disconnectPortEdges(portIds) {
      options.orchestrator.disconnectPortEdges(portIds);
    },
  };
}
