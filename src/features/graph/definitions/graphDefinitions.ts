import {
  createGraphNode,
  createGraphPort,
  type GraphNode,
  type GraphPoint,
  type PortCapacity,
  type PortDirection,
  type PortKind,
} from "../document/graphDocument";

export type GraphInspectorFieldKind = "text" | "number";

export interface GraphNodeInspectorFieldDefinition {
  key: string;
  label: string;
  kind: GraphInspectorFieldKind;
  description?: string;
  placeholder?: string;
  min?: number;
  max?: number;
  step?: number;
}

export interface GraphNodeInspectorSchema {
  fields: GraphNodeInspectorFieldDefinition[];
}

export interface GraphNodeDefinition {
  typeId: string;
  displayName: string;
  category?: string;
  summary?: string;
  ports: GraphPortDefinition[];
  defaultPayload?: () => unknown;
  inspector?: GraphNodeInspectorSchema;
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
        return [definition.typeId, definition.displayName, definition.category ?? "", definition.summary ?? ""]
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
