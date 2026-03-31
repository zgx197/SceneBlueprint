export type GraphTopologyPolicy = "dag" | "directed" | "freeform";

export interface GraphFeatureFlags {
  search: boolean;
  minimap: boolean;
  autoLayout: boolean;
  diagnostics: boolean;
  edgeLabels: boolean;
}

export interface GraphLayoutConfig {
  contentWidth: number;
  contentHeight: number;
  zoomMin: number;
  zoomMax: number;
  nodeMinWidth: number;
  nodeMaxWidth: number;
  nodeHeaderHeight: number;
  nodeMetaHeight: number;
  nodePaddingX: number;
  nodePaddingBottom: number;
  nodeSummaryTopGap: number;
  nodeSummaryLineHeight: number;
  nodeSummaryMaxLines: number;
  portRowHeight: number;
  portSectionGap: number;
  portAnchorInsetX: number;
}

export interface GraphBackgroundConfig {
  gridSize: number;
  backgroundColor: string;
  minorLineColor: string;
  majorLineColor: string;
}

export interface GraphRenderConfig {
  layout: GraphLayoutConfig;
  background: GraphBackgroundConfig;
}

export interface GraphProfile {
  id: string;
  displayName: string;
  topologyPolicy: GraphTopologyPolicy;
  features: GraphFeatureFlags;
  render: GraphRenderConfig;
}

export const DEFAULT_GRAPH_LAYOUT: GraphLayoutConfig = {
  contentWidth: 3200,
  contentHeight: 2200,
  zoomMin: 0.45,
  zoomMax: 1.65,
  nodeMinWidth: 248,
  nodeMaxWidth: 372,
  nodeHeaderHeight: 42,
  nodeMetaHeight: 24,
  nodePaddingX: 16,
  nodePaddingBottom: 14,
  nodeSummaryTopGap: 10,
  nodeSummaryLineHeight: 16,
  nodeSummaryMaxLines: 3,
  portRowHeight: 36,
  portSectionGap: 12,
  // Socket 圆心位于节点左右边界本身，连线从边界端口直接出入。
  portAnchorInsetX: 0,
};

export const DEFAULT_GRAPH_BACKGROUND: GraphBackgroundConfig = {
  gridSize: 32,
  backgroundColor: "#f5efe5",
  minorLineColor: "rgba(160, 134, 93, 0.08)",
  majorLineColor: "rgba(160, 134, 93, 0.16)",
};

export const DEFAULT_GRAPH_PROFILE: GraphProfile = {
  id: "sceneblueprint.graph.default",
  displayName: "SceneBlueprint Graph",
  topologyPolicy: "dag",
  features: {
    search: true,
    minimap: true,
    autoLayout: true,
    diagnostics: true,
    edgeLabels: false,
  },
  render: {
    layout: DEFAULT_GRAPH_LAYOUT,
    background: DEFAULT_GRAPH_BACKGROUND,
  },
};

export function createDefaultGraphProfile(): GraphProfile {
  return {
    ...DEFAULT_GRAPH_PROFILE,
    features: { ...DEFAULT_GRAPH_PROFILE.features },
    render: {
      layout: { ...DEFAULT_GRAPH_PROFILE.render.layout },
      background: { ...DEFAULT_GRAPH_PROFILE.render.background },
    },
  };
}
