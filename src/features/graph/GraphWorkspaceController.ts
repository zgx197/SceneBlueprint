import { useEffect, useMemo, useRef, useState } from "react";
import { createGraphDefinitionRegistry } from "./definitions/graphDefinitions";
import { defaultGraphNodeDefinitions } from "./definitions/defaultGraphNodeDefinitions";
import type { GraphWorkspaceRuntimeState } from "./commands/graphCommands";
import {
  createGraphWorkspaceStorage,
  type GraphWorkspaceStorage,
  type GraphWorkspaceStorageSnapshot,
} from "./storage/graphWorkspaceStorage";
import { useAppLogContext } from "../../shared/logging/LogContext";
import { createCanvasTextMeasurer } from "../../host/measurement/textMeasurer";
import {
  readWorkspaceGraphFile,
  readWorkspaceGraphFileInfo,
  writeWorkspaceGraphFile,
  writeWorkspaceRuntimeContractFile,
} from "../../host/api/commands";
import {
  deserializeGraphDocumentFile,
  serializeGraphDocumentFile,
} from "./serialization/graphDocumentFile";
import type { GraphClipboardSnapshot } from "./runtime/graphClipboard";
import { createBootstrapGraphWorkspaceRuntimeState } from "./runtime/graphWorkspaceRuntime";
import {
  createGraphWorkspaceKernel,
  type GraphWorkspaceKernel,
} from "./runtime/graphWorkspaceKernel";
import { createDefaultGraphProfile, type GraphProfile } from "./profile/graphProfile";
import { createGraphRuntimeBridgeContract } from "./runtime/graphWorkspaceBridge";
import { createGraphWorkspaceControllerOrchestrator } from "./controller/graphWorkspaceControllerOrchestrator";
import {
  createGraphWorkspaceControllerFacade,
  type GraphClipboardSummary,
  type GraphWorkspaceController,
} from "./controller/graphWorkspaceControllerFacade";
import {
  cloneGraphWorkspaceRuntimeState,
  getInitialGraphWorkspaceRuntimeState,
} from "./controller/graphWorkspaceControllerRuntime";
import {
  type GraphRuntimeContractFileSnapshot,
  type GraphWorkspaceFileSnapshot,
} from "./controller/graphWorkspaceHostPersistence";

const GRAPH_WORKSPACE_STORAGE_KEY = "sceneblueprint.graph-workspace.draft";

export type {
  GraphClipboardSummary,
  GraphWorkspaceController,
} from "./controller/graphWorkspaceControllerFacade";

export function useGraphWorkspaceController(): GraphWorkspaceController {
  const { log } = useAppLogContext();
  const definitions = useMemo(() => createGraphDefinitionRegistry(defaultGraphNodeDefinitions), []);
  const profileRef = useRef<GraphProfile>(createDefaultGraphProfile());
  const storageRef = useRef<GraphWorkspaceStorage>(
    createGraphWorkspaceStorage({
      storageKey: GRAPH_WORKSPACE_STORAGE_KEY,
    }),
  );
  const bootstrapRuntimeState = useMemo<GraphWorkspaceRuntimeState>(() => {
    // bootstrap 基线只用于 reset/无文件兜底；真正的 kernel initialState 允许优先从 storage 恢复。
    return createBootstrapGraphWorkspaceRuntimeState(definitions);
  }, [definitions]);
  const textMeasurerRef = useRef(createCanvasTextMeasurer());
  const kernelRef = useRef<GraphWorkspaceKernel>(
    createGraphWorkspaceKernel({
      initialState: getInitialGraphWorkspaceRuntimeState(definitions, storageRef.current),
      definitions,
      textMeasurer: textMeasurerRef.current,
      profile: profileRef.current,
    }),
  );
  const didHydrateWorkspaceFileRef = useRef(false);
  const canApplyHostIoStateRef = useRef(true);
  const clipboardRef = useRef<GraphClipboardSnapshot | null>(null);
  const pasteSequenceRef = useRef(0);

  const [runtimeState, setRuntimeState] = useState<GraphWorkspaceRuntimeState>(() => kernelRef.current.getState());
  const [historyRevision, setHistoryRevision] = useState(0);
  const [persistenceSnapshot, setPersistenceSnapshot] = useState<GraphWorkspaceStorageSnapshot>(() => {
    return storageRef.current.getSnapshot();
  });
  const [workspaceFileSnapshot, setWorkspaceFileSnapshot] = useState<GraphWorkspaceFileSnapshot | null>(null);
  const [runtimeContractFileSnapshot, setRuntimeContractFileSnapshot] = useState<GraphRuntimeContractFileSnapshot | null>(null);
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
    return kernelRef.current.getFrame();
  }, [document, viewState]);

  const selectionTarget = useMemo(() => {
    return kernelRef.current.getSelectionTarget();
  }, [document, viewState]);

  const commandSnapshot = useMemo(() => {
    return kernelRef.current.getCommandSnapshot();
  }, [historyRevision]);

  const analysis = useMemo(() => {
    return kernelRef.current.getAnalysis();
  }, [document, viewState]);

  const bridgeContract = useMemo(() => {
    return createGraphRuntimeBridgeContract(document, definitions);
  }, [definitions, document]);

  const exportPreflight = useMemo(() => {
    return kernelRef.current.validateForExport();
  }, [document, viewState]);

  const syncStateFromRuntime = (nextState: GraphWorkspaceRuntimeState, options?: { bumpHistory?: boolean }) => {
    setRuntimeState(nextState);
    if (options?.bumpHistory) {
      setHistoryRevision((value) => value + 1);
    }
  };

  const orchestrator = createGraphWorkspaceControllerOrchestrator({
    kernel: kernelRef.current,
    storage: storageRef.current,
    bootstrapRuntimeState,
    clipboardRef,
    pasteSequenceRef,
    cloneRuntimeState: cloneGraphWorkspaceRuntimeState,
    serializeGraphDocumentFile,
    deserializeGraphDocumentFile,
    readWorkspaceGraphFileInfo,
    readWorkspaceGraphFile,
    writeWorkspaceGraphFile,
    writeWorkspaceRuntimeContractFile,
    syncStateFromRuntime,
    setPersistenceSnapshot,
    setClipboardSummary,
    setWorkspaceFileSnapshot,
    setRuntimeContractFileSnapshot,
    log,
    canApplyHostIoState: () => canApplyHostIoStateRef.current,
  });

  useEffect(() => {
    return () => {
      canApplyHostIoStateRef.current = false;
    };
  }, []);

  useEffect(() => {
    if (didHydrateWorkspaceFileRef.current) {
      return;
    }

    didHydrateWorkspaceFileRef.current = true;
    void orchestrator.hydrateWorkspaceFileFromHost(persistenceSnapshot.hasSavedSnapshot);
  }, [orchestrator, persistenceSnapshot.hasSavedSnapshot]);

  return createGraphWorkspaceControllerFacade({
    document,
    viewState,
    definitions,
    profile: profileRef.current,
    graphFrame,
    commandSnapshot,
    selectionTarget,
    analysis,
    exportPreflight,
    bridgeContract,
    persistenceSnapshot,
    workspaceFileSnapshot,
    runtimeContractFileSnapshot,
    clipboardSummary,
    layoutService: kernelRef.current.layoutService,
    nodeSearchService: kernelRef.current.nodeSearchService,
    shortcutBindingService: kernelRef.current.shortcutBindingService,
    orchestrator,
  });
}

