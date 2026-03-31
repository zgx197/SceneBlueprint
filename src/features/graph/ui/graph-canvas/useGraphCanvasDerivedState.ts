import { useMemo } from "react";
import type { GraphPoint, NodeId, PortId } from "../../document/graphDocument";
import type { GraphFrame, GraphFrameEdge, GraphFrameNode, GraphFrameOverlay, GraphFramePort } from "../../frame/graphFrame";
import { buildGraphBezierGeometry } from "../../frame/graphEdgeGeometry";

export interface GraphCanvasDerivedState {
  displayNodeMap: Map<NodeId, GraphFrameNode>;
  displayPortMap: Map<PortId, GraphFramePort>;
  measuredDisplayEdges: GraphFrameEdge[];
  measuredDisplayOverlays: GraphFrameOverlay[];
  searchResults: GraphFrameNode[];
  searchHitNodeIds: Set<NodeId>;
}

interface UseGraphCanvasDerivedStateOptions {
  displayFrame: GraphFrame;
  measuredPortAnchors: Map<PortId, GraphPoint>;
  searchResultNodeIds: NodeId[];
}

export function useGraphCanvasDerivedState(options: UseGraphCanvasDerivedStateOptions): GraphCanvasDerivedState {
  const { displayFrame, measuredPortAnchors, searchResultNodeIds } = options;

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
      const sourceAnchor = measuredPortAnchors.get(edge.sourcePortId) ?? edge.start;
      const targetAnchor = measuredPortAnchors.get(edge.targetPortId) ?? edge.end;
      const geometry = buildGraphBezierGeometry(sourceAnchor, targetAnchor);

      return {
        ...edge,
        start: sourceAnchor,
        end: targetAnchor,
        path: geometry.path,
        midpoint: geometry.midpoint,
        label: edge.label
          ? {
              ...edge.label,
              position: {
                x: geometry.midpoint.x,
                y: geometry.midpoint.y - 14,
              },
            }
          : undefined,
      };
    });
  }, [displayFrame.edges, measuredPortAnchors]);

  const measuredDisplayOverlays = useMemo(() => {
    return displayFrame.overlays.map((overlay) => {
      if (overlay.kind !== "connection-preview") {
        return overlay;
      }

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
    if (searchResultNodeIds.length === 0) {
      return [];
    }

    const nodeMap = new Map(displayFrame.nodes.map((node) => [node.id, node]));
    return searchResultNodeIds.flatMap((nodeId) => {
      const node = nodeMap.get(nodeId);
      return node ? [node] : [];
    });
  }, [displayFrame.nodes, searchResultNodeIds]);

  const searchHitNodeIds = useMemo(() => {
    return new Set(searchResults.map((node) => node.id));
  }, [searchResults]);

  return {
    displayNodeMap,
    displayPortMap,
    measuredDisplayEdges,
    measuredDisplayOverlays,
    searchResults,
    searchHitNodeIds,
  };
}
