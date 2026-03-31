import type { MouseEvent } from "react";
import type { GraphPoint } from "../../document/graphDocument";
import type { GraphFrameDecoration, GraphFrameEdge, GraphFrameOverlay } from "../../frame/graphFrame";

interface GraphCanvasEdgeLayerProps {
  edges: GraphFrameEdge[];
  overlays: GraphFrameOverlay[];
  decorations: GraphFrameDecoration[];
  onEdgeClick: (event: MouseEvent<SVGPathElement | SVGGElement>, edgeId: string) => void;
  onEdgeContextMenu: (clientPoint: GraphPoint, edgeId: string) => void;
}

export function GraphCanvasEdgeLayer(props: GraphCanvasEdgeLayerProps) {
  const { edges, overlays, decorations, onEdgeClick, onEdgeContextMenu } = props;

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
            {edge.label ? (
              <g className="sb-graph-edge-label-group">
                <rect
                  x={edge.label.position.x - 44}
                  y={edge.label.position.y - 11}
                  width="88"
                  height="22"
                  rx="11"
                  ry="11"
                  className="sb-graph-edge-label-bg"
                />
                <text
                  x={edge.label.position.x}
                  y={edge.label.position.y + 0.5}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  className="sb-graph-edge-label-text"
                >
                  {edge.label.text}
                </text>
              </g>
            ) : null}
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
      {decorations.map((decoration) => (
        <g key={decoration.id} className={`sb-graph-decoration sb-graph-decoration-${decoration.tone}`}>
          <rect
            x={decoration.position.x - 34}
            y={decoration.position.y - 9}
            width="68"
            height="18"
            rx="9"
            ry="9"
            className="sb-graph-decoration-bg"
          />
          <text
            x={decoration.position.x}
            y={decoration.position.y + 0.5}
            textAnchor="middle"
            dominantBaseline="middle"
            className="sb-graph-decoration-text"
          >
            {decoration.tone.toUpperCase()}
          </text>
        </g>
      ))}
      {overlays.map((overlay, index) => {
        if (overlay.kind !== "connection-preview") {
          return null;
        }

        return <path key={`overlay-${index}`} d={overlay.path} className="sb-graph-edge sb-graph-edge-preview" />;
      })}
    </>
  );
}
