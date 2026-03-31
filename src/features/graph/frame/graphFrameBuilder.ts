import { readGraphNodePayloadRecord } from "../content/graphNodeContent";
import type { GraphDocument, GraphPoint, PortId } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphProfile, GraphRenderConfig } from "../profile/graphProfile";
import { DEFAULT_GRAPH_PROFILE } from "../profile/graphProfile";
import type { GraphConnectionPolicy } from "../runtime/graphConnectionPolicy";
import type { GraphViewState } from "../state/graphViewState";
import type {
  GraphFrame,
  GraphFrameBounds,
  GraphFrameComment,
  GraphFrameDecoration,
  GraphFrameEdge,
  GraphFrameGroup,
  GraphFrameNode,
  GraphFramePort,
  GraphFrameSubgraph,
} from "./graphFrame";
import { buildGraphBezierGeometry } from "./graphEdgeGeometry";
import { measureGraphNodePresentation } from "../services/graphNodePresentation";
import { type TextMeasurer } from "../../../host/measurement/textMeasurer";

export const GRAPH_FRAME_LAYOUT = DEFAULT_GRAPH_PROFILE.render.layout;
const GRAPH_SUBGRAPH_PADDING = 64;
const GRAPH_SUBGRAPH_HEADER_OFFSET = 28;

export interface GraphFrameBuilder {
  build(document: GraphDocument, viewState: GraphViewState, definitions: GraphDefinitionRegistry): GraphFrame;
}

export interface CreateGraphFrameBuilderOptions {
  profile: GraphProfile;
  connectionPolicy: GraphConnectionPolicy;
  textMeasurer: TextMeasurer;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function readEdgePayloadRecord(value: unknown): Record<string, unknown> {
  return isRecord(value) ? value : {};
}

function readDiagnosticTone(value: unknown): GraphFrameDecoration["tone"] | undefined {
  return value === "info" || value === "warning" || value === "error" ? value : undefined;
}

function buildBoundsFromNodes(
  nodeIds: string[],
  nodeMap: ReadonlyMap<string, GraphFrameNode>,
  padding: number,
  headerOffset = 0,
): GraphFrameBounds | null {
  const nodes = nodeIds
    .map((nodeId) => nodeMap.get(nodeId))
    .filter((node): node is GraphFrameNode => node !== undefined);
  if (nodes.length === 0) {
    return null;
  }

  const minX = Math.min(...nodes.map((node) => node.bounds.x));
  const minY = Math.min(...nodes.map((node) => node.bounds.y));
  const maxX = Math.max(...nodes.map((node) => node.bounds.x + node.bounds.width));
  const maxY = Math.max(...nodes.map((node) => node.bounds.y + node.bounds.height));

  return {
    x: minX - padding,
    y: minY - padding - headerOffset,
    width: maxX - minX + padding * 2,
    height: maxY - minY + padding * 2 + headerOffset,
  };
}

function buildPortAnchor(
  nodeBounds: GraphFrameNode["bounds"],
  rowIndex: number,
  direction: "input" | "output",
  portSectionTopOffset: number,
  renderConfig: GraphRenderConfig,
): GraphPoint {
  /**
   * 根约束：Graph 的真实连接锚点必须位于节点左右边界的 socket 圆心。
   *
   * 端口标签是节点内部信息展示，socket 才是连接语义的真实落点。
   * 只要把锚点放到内容区内部，边就必然会被节点卡片背景遮住，视觉上看起来像“没有接到端口”。
   * 因此这里明确采用“边界 socket + 内部标签”的结构，后续不要再把 anchor 改回标签区内部。
   */
  return {
    x:
      direction === "input"
        ? nodeBounds.x + renderConfig.layout.portAnchorInsetX
        : nodeBounds.x + nodeBounds.width - renderConfig.layout.portAnchorInsetX,
    y:
      nodeBounds.y +
      portSectionTopOffset +
      rowIndex * renderConfig.layout.portRowHeight +
      renderConfig.layout.portRowHeight * 0.5,
  };
}


function buildMinimapViewport(
  renderConfig: GraphRenderConfig,
  viewState: GraphViewState,
): GraphFrameBounds | null {
  const { viewport } = viewState;
  if (viewport.zoom <= 0) {
    return null;
  }

  return {
    x: clamp(-viewport.panX / viewport.zoom, 0, renderConfig.layout.contentWidth),
    y: clamp(-viewport.panY / viewport.zoom, 0, renderConfig.layout.contentHeight),
    width: Math.min(renderConfig.layout.contentWidth, renderConfig.layout.contentWidth / viewport.zoom),
    height: Math.min(renderConfig.layout.contentHeight, renderConfig.layout.contentHeight / viewport.zoom),
  };
}

export function createGraphFrameBuilder(options: CreateGraphFrameBuilderOptions): GraphFrameBuilder {
  const { profile, connectionPolicy, textMeasurer } = options;
  const renderConfig = profile.render;

  return {
    build(document, viewState, definitions) {
      const connectedPortCounts = new Map<PortId, number>();
      for (const edge of document.edges) {
        connectedPortCounts.set(edge.sourcePortId, (connectedPortCounts.get(edge.sourcePortId) ?? 0) + 1);
        connectedPortCounts.set(edge.targetPortId, (connectedPortCounts.get(edge.targetPortId) ?? 0) + 1);
      }

      const preview = viewState.connectionPreview;
      const hoveredPortId = viewState.interaction.hoveredPortId;
      const activeOutputPortId = preview.active ? preview.fromPortId : hoveredPortId;
      const activeOutputNodeId = preview.active ? preview.fromNodeId : undefined;

      const nodes = document.nodes.map<GraphFrameNode>((node) => {
        const definition = definitions.getNode(node.typeId);
        const selected = viewState.selection.selectedNodeIds.includes(node.id);
        const hovered = viewState.interaction.hoveredNodeId === node.id;
        const inputPorts = node.ports.filter((port) => port.direction === "input");
        const outputPorts = node.ports.filter((port) => port.direction === "output");
        const metrics = measureGraphNodePresentation(node, definitions, textMeasurer, renderConfig);
        const bounds = {
          x: node.position.x,
          y: node.position.y,
          width: metrics.width,
          height: metrics.height,
        };

        const frameNode: GraphFrameNode = {
          id: node.id,
          title: metrics.title,
          typeId: node.typeId,
          category: metrics.category,
          description: definition?.description,
          bounds,
          selected,
          hovered,
          rows: [],
          inputs: [],
          outputs: [],
          content: {
            mode: "summary",
            summaryText: metrics.summaryText,
            detailLines: metrics.detailLines,
          },
        };

        frameNode.inputs = inputPorts.map((port, rowIndex) => {
          const connectable =
            !!activeOutputPortId &&
            !!activeOutputNodeId &&
            connectionPolicy.evaluate(document, {
              sourceNodeId: activeOutputNodeId,
              sourcePortId: activeOutputPortId,
              targetNodeId: node.id,
              targetPortId: port.id,
            }).accepted;

          const framePort: GraphFramePort = {
            id: port.id,
            key: port.key,
            name: port.name,
            direction: port.direction,
            kind: port.kind,
            dataType: port.dataType,
            anchor: buildPortAnchor(bounds, rowIndex, "input", metrics.portSectionTopOffset, renderConfig),
            connectedEdgeCount: connectedPortCounts.get(port.id) ?? 0,
            connected: (connectedPortCounts.get(port.id) ?? 0) > 0,
            hovered: hoveredPortId === port.id,
            connectable,
            source: activeOutputPortId === port.id,
          };
          return framePort;
        });

        frameNode.outputs = outputPorts.map((port, rowIndex) => {
          const framePort: GraphFramePort = {
            id: port.id,
            key: port.key,
            name: port.name,
            direction: port.direction,
            kind: port.kind,
            dataType: port.dataType,
            anchor: buildPortAnchor(bounds, rowIndex, "output", metrics.portSectionTopOffset, renderConfig),
            connectedEdgeCount: connectedPortCounts.get(port.id) ?? 0,
            connected: (connectedPortCounts.get(port.id) ?? 0) > 0,
            hovered: hoveredPortId === port.id,
            connectable: false,
            source: activeOutputPortId === port.id,
          };
          return framePort;
        });

        const rowCount = Math.max(frameNode.inputs.length, frameNode.outputs.length, 1);
        frameNode.rows = Array.from({ length: rowCount }, (_, rowIndex) => ({
          input: frameNode.inputs[rowIndex],
          output: frameNode.outputs[rowIndex],
        }));

        return frameNode;
      });

      const nodeMap = new Map(nodes.map((node) => [node.id, node]));
      const portMap = new Map<PortId, GraphFramePort>();
      for (const node of nodes) {
        for (const port of [...node.inputs, ...node.outputs]) {
          portMap.set(port.id, port);
        }
      }

      const groups = document.groups.flatMap<GraphFrameGroup>((group) => {
        const padding = group.padding ?? 28;
        const bounds = buildBoundsFromNodes(group.nodeIds, nodeMap, padding);
        if (!bounds) {
          return [];
        }

        return [{
          id: group.id,
          title: group.title,
          bounds,
          nodeIds: [...group.nodeIds],
          color: group.color,
          padding,
          selected: viewState.selection.selectedGroupIds.includes(group.id),
          hovered: viewState.interaction.hoveredGroupId === group.id,
        }];
      });

      const subgraphs = document.subgraphs.flatMap<GraphFrameSubgraph>((subgraph) => {
        const bounds = buildBoundsFromNodes(subgraph.nodeIds, nodeMap, GRAPH_SUBGRAPH_PADDING, GRAPH_SUBGRAPH_HEADER_OFFSET);
        if (!bounds) {
          return [];
        }

        return [{
          id: subgraph.id,
          title: subgraph.title,
          bounds,
          nodeIds: [...subgraph.nodeIds],
          color: subgraph.color,
          description: subgraph.description,
          entryNodeId: subgraph.entryNodeId,
          selected: viewState.selection.selectedSubgraphIds.includes(subgraph.id),
          hovered: viewState.interaction.hoveredSubgraphId === subgraph.id,
        }];
      });

      const comments = document.comments.map<GraphFrameComment>((comment) => ({
        id: comment.id,
        text: comment.text,
        bounds: {
          x: comment.position.x,
          y: comment.position.y,
          width: comment.size.width,
          height: comment.size.height,
        },
        tone: comment.tone ?? "info",
        selected: viewState.selection.selectedCommentIds.includes(comment.id),
        hovered: viewState.interaction.hoveredCommentId === comment.id,
      }));

      const edges = document.edges.flatMap<GraphFrameEdge>((edge) => {
        const sourceNode = nodeMap.get(edge.sourceNodeId);
        const targetNode = nodeMap.get(edge.targetNodeId);
        const sourcePort = portMap.get(edge.sourcePortId);
        const targetPort = portMap.get(edge.targetPortId);
        if (!sourceNode || !targetNode || !sourcePort || !targetPort) {
          return [];
        }

        const geometry = buildGraphBezierGeometry(sourcePort.anchor, targetPort.anchor);
        const payload = readEdgePayloadRecord(edge.payload);
        const label = typeof payload.label === "string" && payload.label.trim().length > 0
          ? {
              text: payload.label.trim(),
              position: {
                x: geometry.midpoint.x,
                y: geometry.midpoint.y - 14,
              },
            }
          : undefined;

        return [{
          id: edge.id,
          sourceNodeId: edge.sourceNodeId,
          sourcePortId: edge.sourcePortId,
          targetNodeId: edge.targetNodeId,
          targetPortId: edge.targetPortId,
          start: sourcePort.anchor,
          end: targetPort.anchor,
          path: geometry.path,
          midpoint: geometry.midpoint,
          label,
          selected: viewState.selection.selectedEdgeIds.includes(edge.id),
        }];
      });

      const overlays = [] as GraphFrame["overlays"];
      if (preview.active && preview.fromNodeId && preview.fromPortId && preview.pointer) {
        const sourcePort = portMap.get(preview.fromPortId);
        const sourceNode = sourcePort ? nodeMap.get(preview.fromNodeId) : undefined;
        if (sourcePort && sourceNode) {
          const geometry = buildGraphBezierGeometry(sourcePort.anchor, preview.pointer);
          overlays.push({
            kind: "connection-preview",
            sourceNodeId: preview.fromNodeId,
            sourcePortId: preview.fromPortId,
            start: sourcePort.anchor,
            end: preview.pointer,
            path: geometry.path,
          });
        }
      }

      const decorations = document.edges.flatMap<GraphFrameDecoration>((edge) => {
        const payload = readEdgePayloadRecord(edge.payload);
        const tone = readDiagnosticTone(payload.diagnosticTone);
        if (!tone) {
          return [];
        }

        const frameEdge = edges.find((entry) => entry.id === edge.id);
        if (!frameEdge) {
          return [];
        }

        return [{
          id: `decoration-${edge.id}`,
          target: "edge",
          targetId: edge.id,
          position: {
            x: frameEdge.midpoint.x,
            y: frameEdge.midpoint.y + 18,
          },
          tone,
          label: typeof payload.label === "string" && payload.label.trim().length > 0 ? payload.label.trim() : tone,
        }];
      });

      return {
        viewport: viewState.viewport,
        background: {
          width: renderConfig.layout.contentWidth,
          height: renderConfig.layout.contentHeight,
          gridSize: renderConfig.background.gridSize,
          backgroundColor: renderConfig.background.backgroundColor,
          minorLineColor: renderConfig.background.minorLineColor,
          majorLineColor: renderConfig.background.majorLineColor,
        },
        groups,
        subgraphs,
        comments,
        nodes,
        edges,
        overlays,
        decorations,
        minimap: profile.features.minimap
          ? {
              enabled: true,
              contentWidth: renderConfig.layout.contentWidth,
              contentHeight: renderConfig.layout.contentHeight,
              viewportRect: buildMinimapViewport(renderConfig, viewState),
            }
          : null,
        summary: {
          nodeCount: nodes.length,
          edgeCount: edges.length,
          hasActiveConnectionPreview: preview.active,
          activeOutputPortId,
        },
      };
    },
  };
}




