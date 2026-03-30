import type { GraphDocument, EdgeId, NodeId } from "../document/graphDocument";
import type { GraphSelectionState } from "../state/graphViewState";
import { createGraphDocumentIndex } from "./graphDocumentIndex";

export interface GraphSelectionManager {
  clear(): GraphSelectionState;
  selectNode(nodeId: NodeId): GraphSelectionState;
  selectNodes(nodeIds: NodeId[]): GraphSelectionState;
  selectEdge(edgeId: EdgeId): GraphSelectionState;
  toggleNode(selection: GraphSelectionState, nodeId: NodeId, document: GraphDocument): GraphSelectionState;
  toggleEdge(selection: GraphSelectionState, edgeId: EdgeId, document: GraphDocument): GraphSelectionState;
  appendNodes(selection: GraphSelectionState, nodeIds: NodeId[], document: GraphDocument): GraphSelectionState;
  selectAllNodes(document: GraphDocument): GraphSelectionState;
  replace(selection: GraphSelectionState, document: GraphDocument): GraphSelectionState;
  prune(document: GraphDocument, selection: GraphSelectionState): GraphSelectionState;
}

function unique<TValue>(values: TValue[]): TValue[] {
  return [...new Set(values)];
}

export function createGraphSelectionManager(): GraphSelectionManager {
  return {
    clear() {
      return {
        selectedNodeIds: [],
        selectedEdgeIds: [],
      };
    },
    selectNode(nodeId) {
      return {
        selectedNodeIds: [nodeId],
        selectedEdgeIds: [],
      };
    },
    selectNodes(nodeIds) {
      return {
        selectedNodeIds: unique(nodeIds),
        selectedEdgeIds: [],
      };
    },
    selectEdge(edgeId) {
      return {
        selectedNodeIds: [],
        selectedEdgeIds: [edgeId],
      };
    },
    toggleNode(selection, nodeId, document) {
      const nextNodeIds = selection.selectedNodeIds.includes(nodeId)
        ? selection.selectedNodeIds.filter((entry) => entry !== nodeId)
        : [...selection.selectedNodeIds, nodeId];

      return this.replace(
        {
          selectedNodeIds: nextNodeIds,
          selectedEdgeIds: [],
        },
        document,
      );
    },
    toggleEdge(selection, edgeId, document) {
      const nextEdgeIds = selection.selectedEdgeIds.includes(edgeId)
        ? selection.selectedEdgeIds.filter((entry) => entry !== edgeId)
        : [...selection.selectedEdgeIds, edgeId];

      return this.replace(
        {
          selectedNodeIds: [],
          selectedEdgeIds: nextEdgeIds,
        },
        document,
      );
    },
    appendNodes(selection, nodeIds, document) {
      return this.replace(
        {
          selectedNodeIds: [...selection.selectedNodeIds, ...nodeIds],
          selectedEdgeIds: [],
        },
        document,
      );
    },
    selectAllNodes(document) {
      return {
        selectedNodeIds: document.nodes.map((node) => node.id),
        selectedEdgeIds: [],
      };
    },
    replace(selection, document) {
      return this.prune(document, {
        selectedNodeIds: unique(selection.selectedNodeIds),
        selectedEdgeIds: unique(selection.selectedEdgeIds),
      });
    },
    prune(document, selection) {
      const index = createGraphDocumentIndex(document);
      return {
        selectedNodeIds: unique(selection.selectedNodeIds).filter((nodeId) => index.findNode(nodeId) !== undefined),
        selectedEdgeIds: unique(selection.selectedEdgeIds).filter((edgeId) => index.findEdge(edgeId) !== undefined),
      };
    },
  };
}
