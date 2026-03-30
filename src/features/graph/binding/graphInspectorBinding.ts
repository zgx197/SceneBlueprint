import type { EdgeId, GraphDocument, GraphEdge, GraphNode, NodeId } from "../document/graphDocument";
import type {
  GraphDefinitionRegistry,
  GraphNodeInspectorSchema,
} from "../definitions/graphDefinitions";
import type { GraphViewState } from "../state/graphViewState";

function readPayloadRecord(payload: unknown): Record<string, unknown> {
  return typeof payload === "object" && payload !== null && !Array.isArray(payload)
    ? (payload as Record<string, unknown>)
    : {};
}

export type WorkspaceSelectionTarget =
  | { kind: "none" }
  | {
      kind: "graph-node";
      nodeId: NodeId;
      node: GraphNode;
      displayName: string;
      category?: string;
      summary?: string;
      payload: Record<string, unknown>;
      inspectorSchema?: GraphNodeInspectorSchema;
    }
  | {
      kind: "graph-edge";
      edgeId: EdgeId;
      edge: GraphEdge;
      sourceNodeTitle: string;
      targetNodeTitle: string;
    }
  | { kind: "scene-marker"; markerId: string }
  | { kind: "scene-object"; objectId: string };

export interface GraphInspectorBinding {
  getSelectionTarget(
    document: GraphDocument,
    viewState: GraphViewState,
    definitions: GraphDefinitionRegistry,
  ): WorkspaceSelectionTarget;
}

export function createGraphInspectorBinding(): GraphInspectorBinding {
  return {
    getSelectionTarget(document, viewState, definitions) {
      const selectedNodeId = viewState.selection.selectedNodeIds[0];
      if (selectedNodeId) {
        const node = document.nodes.find((entry) => entry.id === selectedNodeId);
        if (node) {
          const definition = definitions.getNode(node.typeId);
          return {
            kind: "graph-node",
            nodeId: node.id,
            node,
            displayName: definition?.displayName ?? node.typeId,
            category: definition?.category,
            summary: definition?.summary,
            payload: readPayloadRecord(node.payload),
            inspectorSchema: definition?.inspector,
          };
        }
      }

      const selectedEdgeId = viewState.selection.selectedEdgeIds[0];
      if (selectedEdgeId) {
        const edge = document.edges.find((entry) => entry.id === selectedEdgeId);
        if (edge) {
          const sourceNode = document.nodes.find((entry) => entry.id === edge.sourceNodeId);
          const targetNode = document.nodes.find((entry) => entry.id === edge.targetNodeId);

          return {
            kind: "graph-edge",
            edgeId: edge.id,
            edge,
            sourceNodeTitle:
              sourceNode ? definitions.getNode(sourceNode.typeId)?.displayName ?? sourceNode.typeId : edge.sourceNodeId,
            targetNodeTitle:
              targetNode ? definitions.getNode(targetNode.typeId)?.displayName ?? targetNode.typeId : edge.targetNodeId,
          };
        }
      }

      return { kind: "none" };
    },
  };
}
