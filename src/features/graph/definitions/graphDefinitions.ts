import {
  createGraphNode,
  createGraphPort,
  type GraphNode,
  type GraphPoint,
  type PortCapacity,
  type PortDirection,
  type PortKind,
} from "../document/graphDocument";
import type { GraphNodeContentDefinition } from "../content/graphNodeContent";
import type { GraphNodeBridgeDefinition } from "../bridge/graphBridgeMapping";

export type {
  GraphNodeBooleanFieldDefinition,
  GraphNodeContentDefinition,
  GraphNodeContentFieldDefinition,
  GraphNodeContentFieldKind,
  GraphNodeContentLine,
  GraphNodeContentProjection,
  GraphNodeContentSectionDefinition,
  GraphNodeContentSelectOption,
  GraphNodeNumberFieldDefinition,
  GraphNodeReadonlyFieldDefinition,
  GraphNodeSelectFieldDefinition,
  GraphNodeTextFieldDefinition,
} from "../content/graphNodeContent";

export interface GraphNodeDefinition {
  typeId: string;
  displayName: string;
  category?: string;
  description?: string;
  ports: GraphPortDefinition[];
  defaultPayload?: () => unknown;
  bridge?: GraphNodeBridgeDefinition;
  content: GraphNodeContentDefinition;
}

export interface GraphPortDefinition {
  key: string;
  name: string;
  direction: PortDirection;
  kind: PortKind;
  dataType?: string;
  capacity?: PortCapacity;
}

export interface GraphDefinitionRegistry {
  getNode(typeId: string): GraphNodeDefinition | undefined;
  listNodes(): GraphNodeDefinition[];
  searchNodes(keyword: string): GraphNodeDefinition[];
}

export interface InstantiateGraphNodeOptions {
  id: string;
  position: GraphPoint;
  payload?: unknown;
}

export function createGraphDefinitionRegistry(definitions: GraphNodeDefinition[]): GraphDefinitionRegistry {
  const map = new Map<string, GraphNodeDefinition>();

  for (const definition of definitions) {
    map.set(definition.typeId, definition);
  }

  return {
    getNode(typeId) {
      return map.get(typeId);
    },
    listNodes() {
      return [...map.values()];
    },
    searchNodes(keyword) {
      const normalizedKeyword = keyword.trim().toLowerCase();
      if (!normalizedKeyword) {
        return [...map.values()];
      }

      return [...map.values()].filter((definition) => {
        return [definition.typeId, definition.displayName, definition.category ?? "", definition.description ?? ""]
          .join(" ")
          .toLowerCase()
          .includes(normalizedKeyword);
      });
    },
  };
}

export function instantiateGraphNode(
  definition: GraphNodeDefinition,
  options: InstantiateGraphNodeOptions,
): GraphNode {
  const { id, position, payload = definition.defaultPayload?.() } = options;

  return createGraphNode({
    id,
    typeId: definition.typeId,
    position,
    payload,
    ports: definition.ports.map((port) => {
      return createGraphPort({
        id: `${id}:${port.key}`,
        key: port.key,
        name: port.name,
        direction: port.direction,
        kind: port.kind,
        dataType: port.dataType,
        capacity: port.capacity ?? "single",
      });
    }),
  });
}

