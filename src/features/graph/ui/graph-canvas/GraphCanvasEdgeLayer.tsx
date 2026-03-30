import type { MouseEvent } from "react";
import type { GraphPoint } from "../../document/graphDocument";
import type { GraphFrameEdge, GraphFrameOverlay } from "../../frame/graphFrame";

interface GraphCanvasEdgeLayerProps {
  edges: GraphFrameEdge[];
  overlays: GraphFrameOverlay[];
  onEdgeClick: (event: MouseEvent<SVGPathElement | SVGGElement>, edgeId: string) => void;
  onEdgeContextMenu: (clientPoint: GraphPoint, edgeId: string) => void;
}

export function GraphCanvasEdgeLayer(props: GraphCanvasEdgeLayerProps) {
  const { edges, overlays, onEdgeClick, onEdgeContextMenu } = props;

  return (
    <>
      {edges.map((edge) => {
        return (
          <g key={edge.id}>
            <path
              d={edge.path}
              className="sb-graph-edge-hit"
              onClick={(event) => {
                onEdgeClick(event, edge.id);
              }}
              onContextMenu={(event) => {
                event.preventDefault();
                event.stopPropagation();
                onEdgeContextMenu({ x: event.clientX, y: event.clientY }, edge.id);
              }}
            />
            {edge.selected ? <path d={edge.path} className="sb-graph-edge-outline" /> : null}
            <path d={edge.path} className={`sb-graph-edge${edge.selected ? " sb-graph-edge-selected" : ""}`} />
            {edge.selected ? (
              <g
                className="sb-graph-edge-handle"
                onClick={(event) => {
                  onEdgeClick(event, edge.id);
                }}
                onContextMenu={(event) => {
                  event.preventDefault();
                  event.stopPropagation();
                  onEdgeContextMenu({ x: event.clientX, y: event.clientY }, edge.id);
                }}
              >
                <circle cx={edge.midpoint.x} cy={edge.midpoint.y} r="8" className="sb-graph-edge-handle-ring" />
                <circle cx={edge.midpoint.x} cy={edge.midpoint.y} r="3.2" className="sb-graph-edge-handle-core" />
              </g>
            ) : null}
          </g>
        );
      })}
      {overlays.map((overlay, index) => {
        if (overlay.kind !== "connection-preview") {
          return null;
        }

        return <path key={`overlay-${index}`} d={overlay.path} className="sb-graph-edge sb-graph-edge-preview" />;
      })}
    </>
  );
}
