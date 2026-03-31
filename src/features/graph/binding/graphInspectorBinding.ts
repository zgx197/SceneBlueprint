import {
  projectGraphNodeContent,
  readGraphNodePayloadRecord,
  type GraphNodeContentDefinition,
  type GraphNodeContentProjection,
} from "../content/graphNodeContent";
import type {
  EdgeId,
  GraphComment,
  GraphCommentId,
  GraphDocument,
  GraphEdge,
  GraphGroup,
  GraphGroupId,
  GraphNode,
  GraphSubgraph,
  GraphSubgraphId,
  NodeId,
} from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type {
  GraphRuntimeBridgeContract,
  GraphRuntimeMarkerBridgeContract,
  GraphRuntimeSceneBridgeContract,
} from "../runtime/graphWorkspaceBridge";
import type { GraphWorkspaceIssue } from "../runtime/graphWorkspaceExport";
import type { GraphViewState } from "../state/graphViewState";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function readEdgePayloadRecord(value: unknown): Record<string, unknown> {
  return isRecord(value) ? value : {};
}

function readNodeDisplayName(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
  nodeId: NodeId,
): string {
  const node = document.nodes.find((entry) => entry.id === nodeId);
  if (!node) {
    return nodeId;
  }

  return definitions.getNode(node.typeId)?.displayName ?? node.typeId;
}

export type WorkspaceSelectionTarget =
  | { kind: "none" }
  | {
      kind: "graph-node";
      nodeId: NodeId;
      node: GraphNode;
      displayName: string;
      category?: string;
      description?: string;
      payload: Record<string, unknown>;
      contentDefinition: GraphNodeContentDefinition;
      contentProjection: GraphNodeContentProjection;
    }
  | {
      kind: "graph-edge";
      edgeId: EdgeId;
      edge: GraphEdge;
      sourceNodeTitle: string;
      targetNodeTitle: string;
      payload: Record<string, unknown>;
      label: string;
      diagnosticTone?: "info" | "warning" | "error";
    }
  | {
      kind: "graph-group";
      groupId: GraphGroupId;
      group: GraphGroup;
      memberDisplayNames: string[];
    }
  | {
      kind: "graph-comment";
      commentId: GraphCommentId;
      comment: GraphComment;
    }
  | {
      kind: "graph-subgraph";
      subgraphId: GraphSubgraphId;
      subgraph: GraphSubgraph;
      memberDisplayNames: string[];
      entryNodeDisplayName?: string;
    }
  | {
      kind: "scene-marker";
      markerId: string;
      marker: GraphRuntimeMarkerBridgeContract;
      scene: GraphRuntimeSceneBridgeContract | null;
      graphNodeDisplayName: string;
      issueMessages: string[];
    }
  | { kind: "scene-object"; objectId: string };

function collectBridgeIssueMessages(
  issues: GraphWorkspaceIssue[],
  bridge: GraphRuntimeBridgeContract,
  marker: GraphRuntimeMarkerBridgeContract,
  scene: GraphRuntimeSceneBridgeContract | null,
): string[] {
  return issues
    .filter((issue) => {
      if (issue.location.entityKind === "marker" && issue.location.entityId === marker.id) {
        return true;
      }

      if (scene && issue.location.entityKind === "scene" && issue.location.entityId === scene.id) {
        return true;
      }

      return issue.location.entityKind === "project" && issue.location.entityId === bridge.project.graphId;
    })
    .map((issue) => issue.message);
}

export function buildSceneMarkerSelectionTarget(
  document: GraphDocument,
  definitions: GraphDefinitionRegistry,
  bridge: GraphRuntimeBridgeContract,
  issues: GraphWorkspaceIssue[],
  markerId: string,
): WorkspaceSelectionTarget {
  const marker = bridge.markers.find((entry) => entry.id === markerId);
  if (!marker) {
    return { kind: "none" };
  }

  const scene = bridge.scenes.find((entry) => entry.id === marker.sceneBindingId) ?? null;

  return {
    kind: "scene-marker",
    markerId: marker.id,
    marker,
    scene,
    graphNodeDisplayName: readNodeDisplayName(document, definitions, marker.nodeId),
    issueMessages: collectBridgeIssueMessages(issues, bridge, marker, scene),
  };
}

export interface GraphInspectorBinding {
  getSelectionTarget(
    document: GraphDocument,
    viewState: GraphViewState,
    definitions: GraphDefinitionRegistry,
  ): WorkspaceSelectionTarget;
}

export function createGraphInspectorBinding(): GraphInspectorBinding {
  return {
    getSelectionTarget(document, viewState, definitions) {
      const selectedNodeId = viewState.selection.primarySelectedNodeId ?? viewState.selection.selectedNodeIds[0];
      if (selectedNodeId) {
        const node = document.nodes.find((entry) => entry.id === selectedNodeId);
        if (node) {
          const definition = definitions.getNode(node.typeId);
          if (!definition) {
            return { kind: "none" };
          }

          const payload = readGraphNodePayloadRecord(node.payload);
          return {
            kind: "graph-node",
            nodeId: node.id,
            node,
            displayName: definition.displayName,
            category: definition.category,
            description: definition.description,
            payload,
            contentDefinition: definition.content,
            contentProjection: projectGraphNodeContent(definition.content, payload),
          };
        }
      }

      const selectedEdgeId = viewState.selection.primarySelectedEdgeId ?? viewState.selection.selectedEdgeIds[0];
      if (selectedEdgeId) {
        const edge = document.edges.find((entry) => entry.id === selectedEdgeId);
        if (edge) {
          const payload = readEdgePayloadRecord(edge.payload);
          const diagnosticTone =
            payload.diagnosticTone === "info" || payload.diagnosticTone === "warning" || payload.diagnosticTone === "error"
              ? payload.diagnosticTone
              : undefined;

          return {
            kind: "graph-edge",
            edgeId: edge.id,
            edge,
            sourceNodeTitle: readNodeDisplayName(document, definitions, edge.sourceNodeId),
            targetNodeTitle: readNodeDisplayName(document, definitions, edge.targetNodeId),
            payload,
            label: typeof payload.label === "string" ? payload.label : "",
            diagnosticTone,
          };
        }
      }

      const selectedGroupId = viewState.selection.primarySelectedGroupId ?? viewState.selection.selectedGroupIds[0];
      if (selectedGroupId) {
        const group = document.groups.find((entry) => entry.id === selectedGroupId);
        if (group) {
          return {
            kind: "graph-group",
            groupId: group.id,
            group,
            memberDisplayNames: group.nodeIds.map((nodeId) => readNodeDisplayName(document, definitions, nodeId)),
          };
        }
      }

      const selectedCommentId = viewState.selection.primarySelectedCommentId ?? viewState.selection.selectedCommentIds[0];
      if (selectedCommentId) {
        const comment = document.comments.find((entry) => entry.id === selectedCommentId);
        if (comment) {
          return {
            kind: "graph-comment",
            commentId: comment.id,
            comment,
          };
        }
      }

      const selectedSubgraphId = viewState.selection.primarySelectedSubgraphId ?? viewState.selection.selectedSubgraphIds[0];
      if (selectedSubgraphId) {
        const subgraph = document.subgraphs.find((entry) => entry.id === selectedSubgraphId);
        if (subgraph) {
          return {
            kind: "graph-subgraph",
            subgraphId: subgraph.id,
            subgraph,
            memberDisplayNames: subgraph.nodeIds.map((nodeId) => readNodeDisplayName(document, definitions, nodeId)),
            entryNodeDisplayName: subgraph.entryNodeId
              ? readNodeDisplayName(document, definitions, subgraph.entryNodeId)
              : undefined,
          };
        }
      }

      return { kind: "none" };
    },
  };
}
