import type { TextMeasurer } from "../../../host/measurement/textMeasurer";
import {
  createGraphDocument,
  createGraphPoint,
  type GraphDocument,
  type GraphNode,
} from "../document/graphDocument";
import { defaultGraphNodeDefinitions } from "../definitions/defaultGraphNodeDefinitions";
import {
  createGraphDefinitionRegistry,
  instantiateGraphNode,
  type GraphDefinitionRegistry,
} from "../definitions/graphDefinitions";
import { createInitialGraphViewState, type GraphViewState } from "../state/graphViewState";
import type { GraphWorkspaceRuntimeState } from "../commands/graphCommands";

const TEST_DEFINITION_REGISTRY = createGraphDefinitionRegistry(defaultGraphNodeDefinitions);

export function createTestDefinitionRegistry(): GraphDefinitionRegistry {
  return TEST_DEFINITION_REGISTRY;
}

export function instantiateTestNode(
  typeId: string,
  id: string,
  position: { x: number; y: number },
  payload?: unknown,
): GraphNode {
  const definition = TEST_DEFINITION_REGISTRY.getNode(typeId);
  if (!definition) {
    throw new Error(`Missing test node definition: ${typeId}`);
  }

  return instantiateGraphNode(definition, {
    id,
    position: createGraphPoint(position.x, position.y),
    payload,
  });
}

export function createTestDocument(nodes: GraphNode[] = []): GraphDocument {
  return createGraphDocument({
    id: "graph-test-document",
    nodes,
  });
}

export function createTestRuntimeState(
  document: GraphDocument,
  viewState: GraphViewState = createInitialGraphViewState(),
): GraphWorkspaceRuntimeState {
  return {
    document,
    viewState,
  };
}

export function createTestTextMeasurer(characterWidth = 7): TextMeasurer {
  return {
    measure(text, options) {
      const fontSize = options?.fontSize ?? 12;
      return text.length * Math.max(characterWidth, fontSize * 0.56);
    },
  };
}
