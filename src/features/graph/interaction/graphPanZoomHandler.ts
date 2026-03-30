import type { GraphPoint } from "../document/graphDocument";
import type { GraphViewportState } from "../state/graphViewState";
import { GRAPH_FRAME_LAYOUT } from "../frame/graphFrameBuilder";

export interface GraphPanSession {
  startClient: GraphPoint;
  startPan: GraphPoint;
}

export function clampGraphZoom(value: number): number {
  return Math.max(GRAPH_FRAME_LAYOUT.zoomMin, Math.min(GRAPH_FRAME_LAYOUT.zoomMax, value));
}

export function screenToGraphWorld(
  clientPoint: GraphPoint,
  viewportElement: HTMLDivElement,
  viewport: GraphViewportState,
): GraphPoint {
  const rect = viewportElement.getBoundingClientRect();
  return {
    x: (clientPoint.x - rect.left - viewport.panX) / viewport.zoom,
    y: (clientPoint.y - rect.top - viewport.panY) / viewport.zoom,
  };
}

export function screenToGraphLocal(clientPoint: GraphPoint, viewportElement: HTMLDivElement): GraphPoint {
  const rect = viewportElement.getBoundingClientRect();
  return {
    x: clientPoint.x - rect.left,
    y: clientPoint.y - rect.top,
  };
}

export function createGraphPanSession(startClient: GraphPoint, viewport: GraphViewportState): GraphPanSession {
  return {
    startClient,
    startPan: {
      x: viewport.panX,
      y: viewport.panY,
    },
  };
}

export function buildGraphPanViewportPatch(
  session: GraphPanSession,
  clientPoint: GraphPoint,
): Partial<GraphViewportState> {
  return {
    panX: Math.round(session.startPan.x + (clientPoint.x - session.startClient.x)),
    panY: Math.round(session.startPan.y + (clientPoint.y - session.startClient.y)),
  };
}

export function buildGraphZoomViewportPatch(
  clientPoint: GraphPoint,
  viewportElement: HTMLDivElement,
  viewport: GraphViewportState,
  deltaY: number,
): Partial<GraphViewportState> {
  const pointerWorld = screenToGraphWorld(clientPoint, viewportElement, viewport);
  const nextZoom = clampGraphZoom(viewport.zoom + (deltaY < 0 ? 0.08 : -0.08));
  const rect = viewportElement.getBoundingClientRect();
  const nextPanX = clientPoint.x - rect.left - pointerWorld.x * nextZoom;
  const nextPanY = clientPoint.y - rect.top - pointerWorld.y * nextZoom;

  return {
    zoom: Number(nextZoom.toFixed(2)),
    panX: Math.round(nextPanX),
    panY: Math.round(nextPanY),
  };
}
