import type { GraphPoint, PortId } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphViewState } from "../state/graphViewState";
import type { GraphDocument } from "../document/graphDocument";
import type { GraphConnectionPolicy } from "../runtime/graphConnectionPolicy";
import { createGraphDocumentIndex } from "../runtime/graphDocumentIndex";
import type { GraphFrame, GraphFrameEdge, GraphFrameNode, GraphFramePort } from "./graphFrame";
import { buildGraphBezierGeometry } from "./graphEdgeGeometry";
import { estimateWrappedLineCount, type TextMeasurer } from "../../../host/measurement/textMeasurer";

export const GRAPH_FRAME_LAYOUT = {
  contentWidth: 3200,
  contentHeight: 2200,
  zoomMin: 0.45,
  zoomMax: 1.65,
  nodeMinWidth: 248,
  nodeMaxWidth: 372,
  nodeHeaderHeight: 42,
  nodeMetaHeight: 24,
  nodePaddingX: 16,
  nodePaddingBottom: 14,
  nodeSummaryTopGap: 10,
  nodeSummaryLineHeight: 16,
  nodeSummaryMaxLines: 3,
  portRowHeight: 36,
  portSectionGap: 12,
  // Socket 圆心位于节点左右边界本身，连线从边界端口直接出入。
  portAnchorInsetX: 0,
} as const;

export interface GraphFrameBuilder {
  build(document: GraphDocument, viewState: GraphViewState, definitions: GraphDefinitionRegistry): GraphFrame;
}

export interface CreateGraphFrameBuilderOptions {
  connectionPolicy: GraphConnectionPolicy;
  textMeasurer: TextMeasurer;
}

interface NodeMetrics {
  width: number;
  height: number;
  portSectionTopOffset: number;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function buildPortAnchor(
  nodeBounds: GraphFrameNode["bounds"],
  rowIndex: number,
  direction: "input" | "output",
  portSectionTopOffset: number,
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
        ? nodeBounds.x + GRAPH_FRAME_LAYOUT.portAnchorInsetX
        : nodeBounds.x + nodeBounds.width - GRAPH_FRAME_LAYOUT.portAnchorInsetX,
    y:
      nodeBounds.y +
      portSectionTopOffset +
      rowIndex * GRAPH_FRAME_LAYOUT.portRowHeight +
      GRAPH_FRAME_LAYOUT.portRowHeight * 0.5,
  };
}

function computeNodeMetrics(
  title: string,
  category: string | undefined,
  summary: string | undefined,
  inputLabels: string[],
  outputLabels: string[],
  textMeasurer: TextMeasurer,
): NodeMetrics {
  const headerWidth =
    textMeasurer.measure(title, { fontSize: 13, fontWeight: 600 }) +
    (category ? textMeasurer.measure(category, { fontSize: 11, fontWeight: 500 }) + 72 : 48);
  const inputColumnWidth = Math.max(
    92,
    ...inputLabels.map((label) => textMeasurer.measure(label, { fontSize: 12, fontWeight: 500 }) + 44),
  );
  const outputColumnWidth = Math.max(
    92,
    ...outputLabels.map((label) => textMeasurer.measure(label, { fontSize: 12, fontWeight: 500 }) + 44),
  );
  const summaryWidth = summary ? textMeasurer.measure(summary, { fontSize: 12, fontWeight: 400 }) + 40 : 0;

  const width = clamp(
    Math.max(
      GRAPH_FRAME_LAYOUT.nodeMinWidth,
      headerWidth + GRAPH_FRAME_LAYOUT.nodePaddingX * 2,
      inputColumnWidth + outputColumnWidth + GRAPH_FRAME_LAYOUT.nodePaddingX * 2 + 16,
      summaryWidth,
    ),
    GRAPH_FRAME_LAYOUT.nodeMinWidth,
    GRAPH_FRAME_LAYOUT.nodeMaxWidth,
  );

  const summaryAvailableWidth = width - GRAPH_FRAME_LAYOUT.nodePaddingX * 2;
  const summaryLineCount = summary
    ? clamp(
        estimateWrappedLineCount(summary, summaryAvailableWidth, textMeasurer, {
          fontSize: 12,
          fontWeight: 400,
        }),
        1,
        GRAPH_FRAME_LAYOUT.nodeSummaryMaxLines,
      )
    : 0;
  const summaryHeight = summaryLineCount * GRAPH_FRAME_LAYOUT.nodeSummaryLineHeight;
  const rowCount = Math.max(inputLabels.length, outputLabels.length, 1);
  const portSectionTopOffset =
    GRAPH_FRAME_LAYOUT.nodeHeaderHeight +
    (summary ? GRAPH_FRAME_LAYOUT.nodeSummaryTopGap + summaryHeight + GRAPH_FRAME_LAYOUT.portSectionGap : 14);
  const height =
    portSectionTopOffset +
    rowCount * GRAPH_FRAME_LAYOUT.portRowHeight +
    GRAPH_FRAME_LAYOUT.nodeMetaHeight +
    GRAPH_FRAME_LAYOUT.nodePaddingBottom;

  return {
    width,
    height,
    portSectionTopOffset,
  };
}

export function createGraphFrameBuilder(options: CreateGraphFrameBuilderOptions): GraphFrameBuilder {
  const { connectionPolicy, textMeasurer } = options;

  return {
    build(document, viewState, definitions) {
      const index = createGraphDocumentIndex(document);
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
        const metrics = computeNodeMetrics(
          definition?.displayName ?? node.typeId,
          definition?.category,
          definition?.summary,
          inputPorts.map((port) => port.name),
          outputPorts.map((port) => port.name),
          textMeasurer,
        );
        const bounds = {
          x: node.position.x,
          y: node.position.y,
          width: node.ui?.width ?? metrics.width,
          height: node.ui?.height ?? metrics.height,
        };

        const frameNode: GraphFrameNode = {
          id: node.id,
          title: definition?.displayName ?? node.typeId,
          typeId: node.typeId,
          category: definition?.category,
          summary: definition?.summary,
          bounds,
          selected,
          hovered,
          rows: [],
          inputs: [],
          outputs: [],
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
            anchor: buildPortAnchor(bounds, rowIndex, "input", metrics.portSectionTopOffset),
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
            anchor: buildPortAnchor(bounds, rowIndex, "output", metrics.portSectionTopOffset),
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

      const edges = document.edges.flatMap<GraphFrameEdge>((edge) => {
        const sourceNode = nodeMap.get(edge.sourceNodeId);
        const targetNode = nodeMap.get(edge.targetNodeId);
        const sourcePort = portMap.get(edge.sourcePortId);
        const targetPort = portMap.get(edge.targetPortId);
        if (!sourceNode || !targetNode || !sourcePort || !targetPort) {
          return [];
        }

        const geometry = buildGraphBezierGeometry(sourcePort.anchor, targetPort.anchor);
        return [
          {
            id: edge.id,
            sourceNodeId: edge.sourceNodeId,
            sourcePortId: edge.sourcePortId,
            targetNodeId: edge.targetNodeId,
            targetPortId: edge.targetPortId,
            start: sourcePort.anchor,
            end: targetPort.anchor,
            path: geometry.path,
            midpoint: geometry.midpoint,
            selected: viewState.selection.selectedEdgeIds.includes(edge.id),
          },
        ];
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

      return {
        viewport: viewState.viewport,
        nodes,
        edges,
        overlays,
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
