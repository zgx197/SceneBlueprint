import { readGraphNodePayloadRecord } from "../content/graphNodeContent";
import type { GraphNode } from "../document/graphDocument";

export type GraphBridgeBindingState = "pending-provider";

export type GraphBridgeMarkerFacingMode =
  | "marker-forward"
  | "camera-facing"
  | "custom-rotation";

export interface GraphBridgeDocumentContext {
  graphId: string;
  requestedProjectId: string | null;
  requestedSceneId: string | null;
  sceneBindingId: string;
}

export interface GraphBridgeMarkerProjection {
  kind: "marker";
  id: string;
  nodeId: string;
  requestedMarkerId: string | null;
  sceneBindingId: string;
  projectId: string | null;
  bindingState: GraphBridgeBindingState;
  markerPortId: string | null;
  inputPortId: string | null;
  completedPortId: string | null;
  delaySeconds: number;
  snapToGround: boolean;
  facingMode: GraphBridgeMarkerFacingMode;
}

export type GraphBridgeNodeProjection = GraphBridgeMarkerProjection;

export interface GraphNodeBridgeDefinition {
  project(node: GraphNode, context: GraphBridgeDocumentContext): GraphBridgeNodeProjection | null;
}

function readOptionalString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function readDelaySeconds(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) && value >= 0 ? value : 0;
}

function readFacingMode(value: unknown): GraphBridgeMarkerFacingMode {
  switch (value) {
    case "camera-facing":
    case "custom-rotation":
    case "marker-forward":
      return value;
    default:
      return "marker-forward";
  }
}

function findPortId(node: GraphNode, key: string): string | null {
  return node.ports.find((port) => port.key === key)?.id ?? null;
}

export function createSceneSpawnMarkerBridgeDefinition(): GraphNodeBridgeDefinition {
  return {
    project(node, context) {
      const payload = readGraphNodePayloadRecord(node.payload);

      return {
        kind: "marker",
        id: `bridge-marker:${node.id}`,
        nodeId: node.id,
        requestedMarkerId: readOptionalString(payload.markerId),
        sceneBindingId: context.sceneBindingId,
        projectId: context.requestedProjectId,
        bindingState: "pending-provider",
        markerPortId: findPortId(node, "marker"),
        inputPortId: findPortId(node, "in"),
        completedPortId: findPortId(node, "completed"),
        delaySeconds: readDelaySeconds(payload.delaySeconds),
        snapToGround: payload.snapToGround === true,
        facingMode: readFacingMode(payload.facingMode),
      };
    },
  };
}
