import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import { createInitialGraphViewState } from "../state/graphViewState";

export interface GraphWorkspacePersistedSnapshot {
  version: 1;
  savedAt: string;
  runtimeState: GraphWorkspaceRuntimeState;
}

export interface GraphWorkspaceStorageSnapshot {
  hasSavedSnapshot: boolean;
  storageKey: string;
  savedAt?: string;
}

export interface GraphWorkspaceStorage {
  getSnapshot(): GraphWorkspaceStorageSnapshot;
  load(): GraphWorkspacePersistedSnapshot | null;
  save(runtimeState: GraphWorkspaceRuntimeState): GraphWorkspaceStorageSnapshot;
  clear(): GraphWorkspaceStorageSnapshot;
}

export interface CreateGraphWorkspaceStorageOptions {
  storageKey: string;
}

function sanitizeRuntimeState(runtimeState: GraphWorkspaceRuntimeState): GraphWorkspaceRuntimeState {
  return {
    document: JSON.parse(JSON.stringify(runtimeState.document)),
    viewState: {
      ...JSON.parse(JSON.stringify(runtimeState.viewState)),
      connectionPreview: {
        active: false,
      },
      interaction: createInitialGraphViewState().interaction,
    },
  };
}

function readRawSnapshot(storageKey: string): GraphWorkspacePersistedSnapshot | null {
  if (typeof window === "undefined") {
    return null;
  }

  const raw = window.localStorage.getItem(storageKey);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as GraphWorkspacePersistedSnapshot;
    if (parsed.version !== 1 || !parsed.runtimeState?.document?.id) {
      return null;
    }

    return {
      ...parsed,
      runtimeState: sanitizeRuntimeState(parsed.runtimeState),
    };
  } catch {
    return null;
  }
}

export function createGraphWorkspaceStorage(
  options: CreateGraphWorkspaceStorageOptions,
): GraphWorkspaceStorage {
  const { storageKey } = options;

  const buildSnapshot = (): GraphWorkspaceStorageSnapshot => {
    const stored = readRawSnapshot(storageKey);
    return {
      hasSavedSnapshot: stored !== null,
      storageKey,
      savedAt: stored?.savedAt,
    };
  };

  return {
    getSnapshot() {
      return buildSnapshot();
    },
    load() {
      return readRawSnapshot(storageKey);
    },
    save(runtimeState) {
      if (typeof window === "undefined") {
        return buildSnapshot();
      }

      const payload: GraphWorkspacePersistedSnapshot = {
        version: 1,
        savedAt: new Date().toISOString(),
        runtimeState: sanitizeRuntimeState(runtimeState),
      };
      window.localStorage.setItem(storageKey, JSON.stringify(payload));
      return buildSnapshot();
    },
    clear() {
      if (typeof window !== "undefined") {
        window.localStorage.removeItem(storageKey);
      }

      return buildSnapshot();
    },
  };
}
