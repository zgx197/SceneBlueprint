import type { GraphConnectionPreviewState } from "../state/graphViewState";
import type { GraphPoint, NodeId, PortId } from "../document/graphDocument";

export function createGraphConnectionPreview(nodeId: NodeId, portId: PortId, pointer: GraphPoint): GraphConnectionPreviewState {
  return {
    active: true,
    fromNodeId: nodeId,
    fromPortId: portId,
    pointer,
  };
}

export function updateGraphConnectionPreviewPointer(
  preview: GraphConnectionPreviewState,
  pointer: GraphPoint,
): GraphConnectionPreviewState {
  return {
    ...preview,
    active: true,
    pointer,
  };
}

export function clearGraphConnectionPreview(): GraphConnectionPreviewState {
  return {
    active: false,
  };
}
