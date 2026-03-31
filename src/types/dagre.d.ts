declare module "dagre" {
  export interface GraphLabel {
    rankdir?: string;
    align?: string;
    ranksep?: number;
    nodesep?: number;
    edgesep?: number;
    marginx?: number;
    marginy?: number;
  }

  export interface NodeLabel {
    width: number;
    height: number;
    x?: number;
    y?: number;
  }

  export interface GraphInstance {
    setGraph(label: GraphLabel): void;
    setDefaultEdgeLabel(newDefault: () => Record<string, never>): void;
    setNode(name: string, label: NodeLabel): void;
    setEdge(from: string, to: string): void;
    node(name: string): NodeLabel | undefined;
  }

  const dagre: {
    graphlib: {
      Graph: new () => GraphInstance;
    };
    layout(graph: GraphInstance): void;
  };

  export default dagre;
}
