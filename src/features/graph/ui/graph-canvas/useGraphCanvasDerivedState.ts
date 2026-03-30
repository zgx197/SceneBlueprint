import { useMemo } from "react";
import type { GraphPoint, NodeId, PortId } from "../../document/graphDocument";
import type { GraphFrame, GraphFrameEdge, GraphFrameOverlay, GraphFramePort, GraphFrameNode } from "../../frame/graphFrame";
import { buildGraphBezierGeometry } from "../../frame/graphEdgeGeometry";
import { GRAPH_FRAME_LAYOUT } from "../../frame/graphFrameBuilder";
import type { GraphCanvasViewportSize } from "./useGraphCanvasViewportSize";
import { clamp } from "./graphCanvasUtils";

export interface GraphCanvasDerivedState {
  displayNodeMap: Map<NodeId, GraphFrameNode>;
  displayPortMap: Map<PortId, GraphFramePort>;
  measuredDisplayEdges: GraphFrameEdge[];
  measuredDisplayOverlays: GraphFrameOverlay[];
  searchResults: GraphFrameNode[];
  searchHitNodeIds: Set<NodeId>;
  minimapViewport: { x: number; y: number; width: number; height: number } | null;
}

interface UseGraphCanvasDerivedStateOptions {
  displayFrame: GraphFrame;
  measuredPortAnchors: Map<PortId, GraphPoint>;
  viewportSize: GraphCanvasViewportSize;
  searchQuery: string;
}

export function useGraphCanvasDerivedState(options: UseGraphCanvasDerivedStateOptions): GraphCanvasDerivedState {
  const { displayFrame, measuredPortAnchors, viewportSize, searchQuery } = options;

  const displayNodeMap = useMemo(() => {
    return new Map(displayFrame.nodes.map((node) => [node.id, node]));
  }, [displayFrame.nodes]);

  const displayPortMap = useMemo(() => {
    const entries = new Map<PortId, GraphFramePort>();
    for (const node of displayFrame.nodes) {
      for (const port of [...node.inputs, ...node.outputs]) {
        entries.set(port.id, port);
      }
    }
    return entries;
  }, [displayFrame.nodes]);

  const measuredDisplayEdges = useMemo(() => {
    return displayFrame.edges.map((edge) => {
      /**
       * measuredPortAnchors 是当前最可信的锚点来源。
       *
       * `edge.start / edge.end` 仍然保留为兜底值，是为了首帧渲染、极端情况下某个 ref 尚未挂上时，
       * Graph 仍能继续显示，而不是整条边直接消失。
       * 但只要拿到了实测端口圆心，必须以实测值覆盖，否则又会回到“线连到估算位置”的老问题。
       */
      const sourceAnchor = measuredPortAnchors.get(edge.sourcePortId) ?? edge.start;
      const targetAnchor = measuredPortAnchors.get(edge.targetPortId) ?? edge.end;
      const geometry = buildGraphBezierGeometry(sourceAnchor, targetAnchor);

      return {
        ...edge,
        start: sourceAnchor,
        end: targetAnchor,
        path: geometry.path,
        midpoint: geometry.midpoint,
      };
    });
  }, [displayFrame.edges, measuredPortAnchors]);

  const measuredDisplayOverlays = useMemo(() => {
    return displayFrame.overlays.map((overlay) => {
      if (overlay.kind !== "connection-preview") {
        return overlay;
      }

      /**
       * 预览线和正式边必须共享同一套锚点真相。
       * 否则很容易出现“正式边是对的，但拖一根新线时起点又飘了”的二次回归。
       */
      const sourceAnchor = measuredPortAnchors.get(overlay.sourcePortId) ?? overlay.start;
      const geometry = buildGraphBezierGeometry(sourceAnchor, overlay.end);

      return {
        ...overlay,
        start: sourceAnchor,
        path: geometry.path,
      };
    });
  }, [displayFrame.overlays, measuredPortAnchors]);

  const searchResults = useMemo(() => {
    const keyword = searchQuery.trim().toLowerCase();
    if (!keyword) {
      return [];
    }

    return displayFrame.nodes.filter((node) => {
      return [node.id, node.title, node.typeId, node.category ?? "", node.summary ?? ""]
        .join(" ")
        .toLowerCase()
        .includes(keyword);
    });
  }, [displayFrame.nodes, searchQuery]);

  const searchHitNodeIds = useMemo(() => {
    return new Set(searchResults.map((node) => node.id));
  }, [searchResults]);

  const minimapViewport = useMemo(() => {
    if (viewportSize.width <= 0 || viewportSize.height <= 0) {
      return null;
    }

    return {
      x: clamp(-displayFrame.viewport.panX / displayFrame.viewport.zoom, 0, GRAPH_FRAME_LAYOUT.contentWidth),
      y: clamp(-displayFrame.viewport.panY / displayFrame.viewport.zoom, 0, GRAPH_FRAME_LAYOUT.contentHeight),
      width: Math.min(viewportSize.width / displayFrame.viewport.zoom, GRAPH_FRAME_LAYOUT.contentWidth),
      height: Math.min(viewportSize.height / displayFrame.viewport.zoom, GRAPH_FRAME_LAYOUT.contentHeight),
    };
  }, [displayFrame.viewport, viewportSize.height, viewportSize.width]);

  return {
    displayNodeMap,
    displayPortMap,
    measuredDisplayEdges,
    measuredDisplayOverlays,
    searchResults,
    searchHitNodeIds,
    minimapViewport,
  };
}
