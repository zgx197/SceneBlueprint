import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphDocument } from "../document/graphDocument";
import type {
  GraphBridgeBindingState,
  GraphBridgeMarkerFacingMode,
  GraphBridgeMarkerProjection,
  GraphBridgeNodeProjection,
  GraphBridgeDocumentContext,
} from "../bridge/graphBridgeMapping";

export const GRAPH_RUNTIME_BRIDGE_SCHEMA = "sceneblueprint.graph-bridge.v1" as const;

export type GraphRuntimeMarkerFacingMode = GraphBridgeMarkerFacingMode;
export type GraphRuntimeBridgeBindingState = GraphBridgeBindingState;

export interface GraphRuntimeProjectBridgeContract {
  graphId: string;
  requestedProjectId: string | null;
  source: "graph-document" | "document-metadata";
}

export interface GraphRuntimeSceneBridgeContract {
  id: string;
  requestedSceneId: string | null;
  projectId: string | null;
  sourceNodeIds: string[];
  bindingState: GraphRuntimeBridgeBindingState;
}

export interface GraphRuntimeMarkerBridgeContract extends Omit<GraphBridgeMarkerProjection, "kind"> {}

export interface GraphRuntimeBridgeContract {
  schema: typeof GRAPH_RUNTIME_BRIDGE_SCHEMA;
  project: GraphRuntimeProjectBridgeContract;
  scenes: GraphRuntimeSceneBridgeContract[];
  markers: GraphRuntimeMarkerBridgeContract[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readOptionalString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function readBridgeMetadata(document: GraphDocument) {
  const metadata = isRecord(document.metadata) ? document.metadata : {};
  const bridge = isRecord(metadata.bridge) ? metadata.bridge : {};

  return {
    requestedProjectId: readOptionalString(bridge.projectId ?? metadata.projectId),
    requestedSceneId: readOptionalString(bridge.sceneId ?? metadata.sceneId),
  };
}

function buildSceneBindingId(documentId: string): string {
  return `${documentId}:scene-default`;
}

function projectBridgeNodes(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
  context: GraphBridgeDocumentContext,
): GraphBridgeNodeProjection[] {
  return document.nodes.flatMap((node) => {
    const definition = definitions.getNode(node.typeId);
    const projection = definition?.bridge?.project(node, context) ?? null;
    return projection ? [projection] : [];
  });
}

function isMarkerProjection(projection: GraphBridgeNodeProjection): projection is GraphBridgeMarkerProjection {
  return projection.kind === "marker";
}

export function createGraphRuntimeBridgeContract(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
): GraphRuntimeBridgeContract {
  const metadata = readBridgeMetadata(document);
  const sceneBindingId = buildSceneBindingId(document.id);
  const context: GraphBridgeDocumentContext = {
    graphId: document.id,
    requestedProjectId: metadata.requestedProjectId,
    requestedSceneId: metadata.requestedSceneId,
    sceneBindingId,
  };
  const nodeProjections = projectBridgeNodes(document, definitions, context);
  const markers = nodeProjections
    .filter(isMarkerProjection)
    .map(({ kind: _kind, ...marker }) => marker);

  const scenes = markers.length > 0 || metadata.requestedSceneId !== null || metadata.requestedProjectId !== null
    ? [
        {
          id: sceneBindingId,
          requestedSceneId: metadata.requestedSceneId,
          projectId: metadata.requestedProjectId,
          sourceNodeIds: markers.map((marker) => marker.nodeId),
          bindingState: "pending-provider" as const,
        },
      ]
    : [];

  return {
    schema: GRAPH_RUNTIME_BRIDGE_SCHEMA,
    project: {
      graphId: document.id,
      requestedProjectId: metadata.requestedProjectId,
      source: metadata.requestedProjectId === null ? "graph-document" : "document-metadata",
    },
    scenes,
    markers,
  };
}