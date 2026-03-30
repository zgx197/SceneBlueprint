import type { GraphDocument, GraphEdge, GraphNode, GraphPort, NodeId, PortId } from "../document/graphDocument";

export interface GraphDocumentIndex {
  readonly document: GraphDocument;
  readonly nodesById: ReadonlyMap<NodeId, GraphNode>;
  readonly edgesById: ReadonlyMap<string, GraphEdge>;
  readonly portsById: ReadonlyMap<PortId, GraphPort>;
  readonly portNodeIds: ReadonlyMap<PortId, NodeId>;

  findNode(nodeId: NodeId): GraphNode | undefined;
  findEdge(edgeId: string): GraphEdge | undefined;
  findPort(portId: PortId): GraphPort | undefined;
  findPortInNode(nodeId: NodeId, portId: PortId): GraphPort | undefined;
  getNodeEdges(nodeId: NodeId): GraphEdge[];
  getPortEdges(portId: PortId): GraphEdge[];
}

export function createGraphDocumentIndex(document: GraphDocument): GraphDocumentIndex {
  const nodesById = new Map<NodeId, GraphNode>();
  const edgesById = new Map<string, GraphEdge>();
  const portsById = new Map<PortId, GraphPort>();
  const portNodeIds = new Map<PortId, NodeId>();

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

  return {
    document,
    nodesById,
    edgesById,
    portsById,
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
