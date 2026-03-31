import type { TextMeasurer } from "../../../host/measurement/textMeasurer";
import {
  createGraphInspectorBinding,
  type GraphInspectorBinding,
  type WorkspaceSelectionTarget,
} from "../binding/graphInspectorBinding";
import type {
  GraphCommandBusSnapshot,
  GraphWorkspaceCommand,
  GraphWorkspaceRuntimeState,
} from "../commands/graphCommands";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphFrame } from "../frame/graphFrame";
import { createGraphFrameBuilder, type GraphFrameBuilder } from "../frame/graphFrameBuilder";
import { createDefaultGraphProfile, type GraphProfile } from "../profile/graphProfile";
import { createGraphLayoutService, type GraphLayoutService } from "../services/graphLayoutService";
import { createGraphNodeSearchService, type GraphNodeSearchService } from "../services/graphNodeSearchService";
import {
  createGraphShortcutBindingService,
  type GraphShortcutBindingService,
} from "../services/graphShortcutBindingService";
import type {
  GraphConnectionPreviewState,
  GraphInteractionState,
  GraphSelectionState,
  GraphViewportState,
} from "../state/graphViewState";
import {
  getConnectedComponents,
  getLeafNodeIds,
  getRootNodeIds,
  hasCycle,
  topologicalSort,
} from "./graphAlgorithms";
import { createGraphBehavior, type GraphBehavior } from "./graphBehavior";
import { createGraphSelectionManager, type GraphSelectionManager } from "./graphSelectionManager";
import { createGraphSubgraphRuntime, type GraphSubgraphRuntime } from "./graphSubgraphRuntime";
import {
  buildGraphWorkspaceExportResult,
  buildGraphWorkspaceValidation,
  type GraphWorkspaceAnalysisSnapshot,
  type GraphWorkspaceExportResult,
  type GraphWorkspaceIssue,
  type GraphWorkspaceValidationResult,
} from "./graphWorkspaceExport";
import { createGraphWorkspaceRuntime, type GraphWorkspaceRuntime } from "./graphWorkspaceRuntime";

export type GraphWorkspaceKernelAnalysis = GraphWorkspaceAnalysisSnapshot;
export type GraphWorkspaceExportPreflightIssue = GraphWorkspaceIssue;

export interface GraphWorkspaceExportPreflight extends GraphWorkspaceValidationResult {
  analysis: GraphWorkspaceKernelAnalysis;
}

export interface GraphWorkspaceKernelSnapshot {
  state: GraphWorkspaceRuntimeState;
  frame: GraphFrame;
  selectionTarget: WorkspaceSelectionTarget;
  commandSnapshot: GraphCommandBusSnapshot;
  analysis: GraphWorkspaceKernelAnalysis;
}

export interface GraphWorkspaceKernel {
  readonly definitions: GraphDefinitionRegistry;
  readonly profile: GraphProfile;
  readonly behavior: GraphBehavior;
  readonly selectionManager: GraphSelectionManager;
  readonly runtime: GraphWorkspaceRuntime;
  readonly frameBuilder: GraphFrameBuilder;
  readonly binding: GraphInspectorBinding;
  readonly layoutService: GraphLayoutService;
  readonly nodeSearchService: GraphNodeSearchService;
  readonly shortcutBindingService: GraphShortcutBindingService;
  readonly subgraphRuntime: GraphSubgraphRuntime;

  getState(): GraphWorkspaceRuntimeState;
  getFrame(): GraphFrame;
  getSelectionTarget(): WorkspaceSelectionTarget;
  getCommandSnapshot(): GraphCommandBusSnapshot;
  getAnalysis(): GraphWorkspaceKernelAnalysis;
  validateForExport(): GraphWorkspaceExportPreflight;
  compileForExport(options?: { generatedAt?: string }): GraphWorkspaceExportResult;
  getSnapshot(): GraphWorkspaceKernelSnapshot;
  execute(command: GraphWorkspaceCommand): GraphWorkspaceRuntimeState;
  undo(): GraphWorkspaceRuntimeState | undefined;
  redo(): GraphWorkspaceRuntimeState | undefined;
  replaceState(state: GraphWorkspaceRuntimeState, options?: { resetHistory?: boolean }): void;
  setSelection(selection: Partial<GraphSelectionState>): GraphWorkspaceRuntimeState;
  patchViewport(patch: Partial<GraphViewportState>): GraphWorkspaceRuntimeState;
  patchInteraction(patch: Partial<GraphInteractionState>): GraphWorkspaceRuntimeState;
  setConnectionPreview(state: GraphConnectionPreviewState): GraphWorkspaceRuntimeState;
  deleteSelection(): GraphWorkspaceRuntimeState;
  disconnectNodeEdges(nodeIds: string[]): GraphWorkspaceRuntimeState;
  disconnectPortEdges(portIds: string[]): GraphWorkspaceRuntimeState;
  selectAllNodes(): GraphWorkspaceRuntimeState;
}

export interface CreateGraphWorkspaceKernelOptions {
  initialState: GraphWorkspaceRuntimeState;
  definitions: GraphDefinitionRegistry;
  textMeasurer: TextMeasurer;
  profile?: GraphProfile;
  behavior?: GraphBehavior;
  selectionManager?: GraphSelectionManager;
  binding?: GraphInspectorBinding;
  subgraphRuntime?: GraphSubgraphRuntime;
  frameBuilder?: GraphFrameBuilder;
  layoutService?: GraphLayoutService;
  nodeSearchService?: GraphNodeSearchService;
  shortcutBindingService?: GraphShortcutBindingService;
}

export function createGraphWorkspaceKernel(options: CreateGraphWorkspaceKernelOptions): GraphWorkspaceKernel {
  const definitions = options.definitions;
  const profile = options.profile ?? createDefaultGraphProfile();
  const selectionManager = options.selectionManager ?? createGraphSelectionManager();
  const behavior = options.behavior ?? createGraphBehavior({ topologyPolicy: profile.topologyPolicy });
  const binding = options.binding ?? createGraphInspectorBinding();
  const subgraphRuntime = options.subgraphRuntime ?? createGraphSubgraphRuntime();
  const runtime = createGraphWorkspaceRuntime({
    initialState: options.initialState,
    definitions,
    profile,
    selectionManager,
    behavior,
    subgraphRuntime,
  });
  const frameBuilder = options.frameBuilder ?? createGraphFrameBuilder({
    profile,
    connectionPolicy: behavior.connectionPolicy,
    textMeasurer: options.textMeasurer,
  });
  const layoutService = options.layoutService ?? createGraphLayoutService({
    definitions,
    profile,
    textMeasurer: options.textMeasurer,
  });
  const nodeSearchService = options.nodeSearchService ?? createGraphNodeSearchService({ definitions });
  const shortcutBindingService = options.shortcutBindingService ?? createGraphShortcutBindingService();

  const buildAnalysis = (): GraphWorkspaceKernelAnalysis => {
    const state = runtime.getState();
    const subgraphAnalysis = subgraphRuntime.analyze(state.document);
    return {
      topologyPolicy: behavior.topologyPolicy,
      hasCycle: hasCycle(state.document),
      topologicalOrder: topologicalSort(state.document),
      rootNodeIds: getRootNodeIds(state.document),
      leafNodeIds: getLeafNodeIds(state.document),
      connectedComponents: getConnectedComponents(state.document),
      subgraphAnalysis,
    };
  };

  return {
    definitions,
    profile,
    behavior,
    selectionManager,
    runtime,
    frameBuilder,
    binding,
    layoutService,
    nodeSearchService,
    shortcutBindingService,
    subgraphRuntime,
    getState() {
      return runtime.getState();
    },
    getFrame() {
      const state = runtime.getState();
      return frameBuilder.build(state.document, state.viewState, definitions);
    },
    getSelectionTarget() {
      const state = runtime.getState();
      return binding.getSelectionTarget(state.document, state.viewState, definitions);
    },
    getCommandSnapshot() {
      return runtime.getCommandSnapshot();
    },
    getAnalysis() {
      return buildAnalysis();
    },
    validateForExport() {
      const analysis = buildAnalysis();
      const validation = buildGraphWorkspaceValidation(
        analysis.subgraphAnalysis.normalizedDocument,
        definitions,
        analysis,
      );

      return {
        ...validation,
        analysis,
      };
    },
    compileForExport(compileOptions) {
      return buildGraphWorkspaceExportResult({
        state: runtime.getState(),
        definitions,
        analysis: buildAnalysis(),
        generatedAt: compileOptions?.generatedAt,
      });
    },
    getSnapshot() {
      return {
        state: runtime.getState(),
        frame: this.getFrame(),
        selectionTarget: this.getSelectionTarget(),
        commandSnapshot: runtime.getCommandSnapshot(),
        analysis: buildAnalysis(),
      };
    },
    execute(command) {
      return runtime.execute(command);
    },
    undo() {
      return runtime.undo();
    },
    redo() {
      return runtime.redo();
    },
    replaceState(state, replaceOptions) {
      runtime.replaceState(state, replaceOptions);
    },
    setSelection(selection) {
      return runtime.setSelection(selection);
    },
    patchViewport(patch) {
      return runtime.patchViewport(patch);
    },
    patchInteraction(patch) {
      return runtime.patchInteraction(patch);
    },
    setConnectionPreview(state) {
      return runtime.setConnectionPreview(state);
    },
    deleteSelection() {
      return runtime.deleteSelection();
    },
    disconnectNodeEdges(nodeIds) {
      return runtime.disconnectNodeEdges(nodeIds);
    },
    disconnectPortEdges(portIds) {
      return runtime.disconnectPortEdges(portIds);
    },
    selectAllNodes() {
      return runtime.selectAllNodes();
    },
  };
}
