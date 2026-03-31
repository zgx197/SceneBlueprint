import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";
import type { GraphWorkspaceStorage } from "../storage/graphWorkspaceStorage";
import { createBootstrapGraphWorkspaceRuntimeState } from "../runtime/graphWorkspaceRuntime";

export function cloneGraphWorkspaceRuntimeState(
  state: GraphWorkspaceRuntimeState,
): GraphWorkspaceRuntimeState {
  return JSON.parse(JSON.stringify(state)) as GraphWorkspaceRuntimeState;
}

export function getInitialGraphWorkspaceRuntimeState(
  definitions: GraphDefinitionRegistry,
  storage: GraphWorkspaceStorage,
): GraphWorkspaceRuntimeState {
  const stored = storage.load();
  if (stored) {
    return cloneGraphWorkspaceRuntimeState(stored.runtimeState);
  }

  return createBootstrapGraphWorkspaceRuntimeState(definitions);
}
