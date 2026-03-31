import type {
  GraphComment,
  GraphCommentId,
  GraphDocument,
  GraphEdge,
  GraphGroup,
  GraphGroupId,
  GraphNode,
  GraphPort,
  GraphSubgraph,
  GraphSubgraphId,
  NodeId,
  PortId,
} from "../document/graphDocument";

export interface GraphDocumentIndex {
  readonly document: GraphDocument;
  readonly nodesById: ReadonlyMap<NodeId, GraphNode>;
  readonly edgesById: ReadonlyMap<string, GraphEdge>;
  readonly portsById: ReadonlyMap<PortId, GraphPort>;
  readonly groupsById: ReadonlyMap<GraphGroupId, GraphGroup>;
  readonly commentsById: ReadonlyMap<GraphCommentId, GraphComment>;
  readonly subgraphsById: ReadonlyMap<GraphSubgraphId, GraphSubgraph>;
  readonly portNodeIds: ReadonlyMap<PortId, NodeId>;

  findNode(nodeId: NodeId): GraphNode | undefined;
  findEdge(edgeId: string): GraphEdge | undefined;
  findPort(portId: PortId): GraphPort | undefined;
  findGroup(groupId: GraphGroupId): GraphGroup | undefined;
  findComment(commentId: GraphCommentId): GraphComment | undefined;
  findSubgraph(subgraphId: GraphSubgraphId): GraphSubgraph | undefined;
  findPortInNode(nodeId: NodeId, portId: PortId): GraphPort | undefined;
  getNodeEdges(nodeId: NodeId): GraphEdge[];
  getPortEdges(portId: PortId): GraphEdge[];
}

export function createGraphDocumentIndex(document: GraphDocument): GraphDocumentIndex {
  const nodesById = new Map<NodeId, GraphNode>();
  const edgesById = new Map<string, GraphEdge>();
  const portsById = new Map<PortId, GraphPort>();
  const portNodeIds = new Map<PortId, NodeId>();
  const groupsById = new Map<GraphGroupId, GraphGroup>();
  const commentsById = new Map<GraphCommentId, GraphComment>();
  const subgraphsById = new Map<GraphSubgraphId, GraphSubgraph>();

  for (const node of document.nodes) {
    nodesById.set(node.id, node);
    for (const port of node.ports) {
      portsById.set(port.id, port);
      portNodeIds.set(port.id, node.id);
    }
  }

  for (const edge of document.edges) {
    edgesById.set(edge.id, edge);
  }

  for (const group of document.groups) {
    groupsById.set(group.id, group);
  }

  for (const comment of document.comments) {
    commentsById.set(comment.id, comment);
  }

  for (const subgraph of document.subgraphs) {
    subgraphsById.set(subgraph.id, subgraph);
  }

  return {
    document,
    nodesById,
    edgesById,
    portsById,
    groupsById,
    commentsById,
    subgraphsById,
    portNodeIds,
    findNode(nodeId) {
      return nodesById.get(nodeId);
    },
    findEdge(edgeId) {
      return edgesById.get(edgeId);
    },
    findPort(portId) {
      return portsById.get(portId);
    },
    findGroup(groupId) {
      return groupsById.get(groupId);
    },
    findComment(commentId) {
      return commentsById.get(commentId);
    },
    findSubgraph(subgraphId) {
      return subgraphsById.get(subgraphId);
    },
    findPortInNode(nodeId, portId) {
      const node = nodesById.get(nodeId);
      return node?.ports.find((port) => port.id === portId);
    },
    getNodeEdges(nodeId) {
      return document.edges.filter((edge) => edge.sourceNodeId === nodeId || edge.targetNodeId === nodeId);
    },
    getPortEdges(portId) {
      return document.edges.filter((edge) => edge.sourcePortId === portId || edge.targetPortId === portId);
    },
  };
}
