import type {
  EdgeId,
  GraphCommentId,
  GraphDocument,
  GraphGroupId,
  GraphSubgraphId,
  NodeId,
} from "../document/graphDocument";
import type { GraphSelectionState } from "../state/graphViewState";
import { createGraphDocumentIndex } from "./graphDocumentIndex";

export interface GraphSelectionManager {
  clear(): GraphSelectionState;
  selectNode(nodeId: NodeId): GraphSelectionState;
  selectNodes(nodeIds: NodeId[]): GraphSelectionState;
  selectEdge(edgeId: EdgeId): GraphSelectionState;
  selectGroup(groupId: GraphGroupId): GraphSelectionState;
  selectComment(commentId: GraphCommentId): GraphSelectionState;
  selectSubgraph(subgraphId: GraphSubgraphId): GraphSelectionState;
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

function createEmptySelection(): GraphSelectionState {
  return {
    selectedNodeIds: [],
    selectedEdgeIds: [],
    selectedGroupIds: [],
    selectedCommentIds: [],
    selectedSubgraphIds: [],
    primarySelectedNodeId: undefined,
    primarySelectedEdgeId: undefined,
    primarySelectedGroupId: undefined,
    primarySelectedCommentId: undefined,
    primarySelectedSubgraphId: undefined,
  };
}

function buildNodeSelection(nodeIds: NodeId[], primaryNodeId?: NodeId): GraphSelectionState {
  const selection = createEmptySelection();
  const uniqueNodeIds = unique(nodeIds);
  selection.selectedNodeIds = uniqueNodeIds;
  selection.primarySelectedNodeId = primaryNodeId && uniqueNodeIds.includes(primaryNodeId)
    ? primaryNodeId
    : uniqueNodeIds.at(-1);
  return selection;
}

function buildEdgeSelection(edgeIds: EdgeId[], primaryEdgeId?: EdgeId): GraphSelectionState {
  const selection = createEmptySelection();
  const uniqueEdgeIds = unique(edgeIds);
  selection.selectedEdgeIds = uniqueEdgeIds;
  selection.primarySelectedEdgeId = primaryEdgeId && uniqueEdgeIds.includes(primaryEdgeId)
    ? primaryEdgeId
    : uniqueEdgeIds.at(-1);
  return selection;
}

function buildGroupSelection(groupIds: GraphGroupId[], primaryGroupId?: GraphGroupId): GraphSelectionState {
  const selection = createEmptySelection();
  const uniqueGroupIds = unique(groupIds);
  selection.selectedGroupIds = uniqueGroupIds;
  selection.primarySelectedGroupId = primaryGroupId && uniqueGroupIds.includes(primaryGroupId)
    ? primaryGroupId
    : uniqueGroupIds.at(-1);
  return selection;
}

function buildCommentSelection(commentIds: GraphCommentId[], primaryCommentId?: GraphCommentId): GraphSelectionState {
  const selection = createEmptySelection();
  const uniqueCommentIds = unique(commentIds);
  selection.selectedCommentIds = uniqueCommentIds;
  selection.primarySelectedCommentId = primaryCommentId && uniqueCommentIds.includes(primaryCommentId)
    ? primaryCommentId
    : uniqueCommentIds.at(-1);
  return selection;
}

function buildSubgraphSelection(subgraphIds: GraphSubgraphId[], primarySubgraphId?: GraphSubgraphId): GraphSelectionState {
  const selection = createEmptySelection();
  const uniqueSubgraphIds = unique(subgraphIds);
  selection.selectedSubgraphIds = uniqueSubgraphIds;
  selection.primarySelectedSubgraphId = primarySubgraphId && uniqueSubgraphIds.includes(primarySubgraphId)
    ? primarySubgraphId
    : uniqueSubgraphIds.at(-1);
  return selection;
}

export function createGraphSelectionManager(): GraphSelectionManager {
  return {
    clear() {
      return createEmptySelection();
    },
    selectNode(nodeId) {
      return buildNodeSelection([nodeId], nodeId);
    },
    selectNodes(nodeIds) {
      return buildNodeSelection(nodeIds);
    },
    selectEdge(edgeId) {
      return buildEdgeSelection([edgeId], edgeId);
    },
    selectGroup(groupId) {
      return buildGroupSelection([groupId], groupId);
    },
    selectComment(commentId) {
      return buildCommentSelection([commentId], commentId);
    },
    selectSubgraph(subgraphId) {
      return buildSubgraphSelection([subgraphId], subgraphId);
    },
    toggleNode(selection, nodeId, document) {
      const nextNodeIds = selection.selectedNodeIds.includes(nodeId)
        ? selection.selectedNodeIds.filter((entry) => entry !== nodeId)
        : [...selection.selectedNodeIds, nodeId];

      return this.replace(buildNodeSelection(nextNodeIds, nodeId), document);
    },
    toggleEdge(selection, edgeId, document) {
      const nextEdgeIds = selection.selectedEdgeIds.includes(edgeId)
        ? selection.selectedEdgeIds.filter((entry) => entry !== edgeId)
        : [...selection.selectedEdgeIds, edgeId];

      return this.replace(buildEdgeSelection(nextEdgeIds, edgeId), document);
    },
    appendNodes(selection, nodeIds, document) {
      const primaryNodeId = nodeIds.at(-1) ?? selection.primarySelectedNodeId;
      return this.replace(buildNodeSelection([...selection.selectedNodeIds, ...nodeIds], primaryNodeId), document);
    },
    selectAllNodes(document) {
      return buildNodeSelection(document.nodes.map((node) => node.id));
    },
    replace(selection, document) {
      if (selection.selectedNodeIds.length > 0) {
        return this.prune(document, buildNodeSelection(selection.selectedNodeIds, selection.primarySelectedNodeId));
      }

      if (selection.selectedEdgeIds.length > 0) {
        return this.prune(document, buildEdgeSelection(selection.selectedEdgeIds, selection.primarySelectedEdgeId));
      }

      if (selection.selectedGroupIds.length > 0) {
        return this.prune(document, buildGroupSelection(selection.selectedGroupIds, selection.primarySelectedGroupId));
      }

      if (selection.selectedCommentIds.length > 0) {
        return this.prune(document, buildCommentSelection(selection.selectedCommentIds, selection.primarySelectedCommentId));
      }

      if (selection.selectedSubgraphIds.length > 0) {
        return this.prune(document, buildSubgraphSelection(selection.selectedSubgraphIds, selection.primarySelectedSubgraphId));
      }

      return createEmptySelection();
    },
    prune(document, selection) {
      const index = createGraphDocumentIndex(document);
      const selectedNodeIds = unique(selection.selectedNodeIds).filter((nodeId) => index.findNode(nodeId) !== undefined);
      const selectedEdgeIds = unique(selection.selectedEdgeIds).filter((edgeId) => index.findEdge(edgeId) !== undefined);
      const selectedGroupIds = unique(selection.selectedGroupIds).filter((groupId) => index.findGroup(groupId) !== undefined);
      const selectedCommentIds = unique(selection.selectedCommentIds).filter((commentId) => index.findComment(commentId) !== undefined);
      const selectedSubgraphIds = unique(selection.selectedSubgraphIds).filter((subgraphId) => index.findSubgraph(subgraphId) !== undefined);

      if (selectedNodeIds.length > 0) {
        return buildNodeSelection(selectedNodeIds, selection.primarySelectedNodeId);
      }

      if (selectedEdgeIds.length > 0) {
        return buildEdgeSelection(selectedEdgeIds, selection.primarySelectedEdgeId);
      }

      if (selectedGroupIds.length > 0) {
        return buildGroupSelection(selectedGroupIds, selection.primarySelectedGroupId);
      }

      if (selectedCommentIds.length > 0) {
        return buildCommentSelection(selectedCommentIds, selection.primarySelectedCommentId);
      }

      if (selectedSubgraphIds.length > 0) {
        return buildSubgraphSelection(selectedSubgraphIds, selection.primarySelectedSubgraphId);
      }

      return createEmptySelection();
    },
  };
}
