import { projectGraphNodeContent, readGraphNodePayloadRecord } from "../content/graphNodeContent";
import type { GraphNode } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphRenderConfig } from "../profile/graphProfile";
import { estimateWrappedLineCount, type TextMeasurer } from "../../../host/measurement/textMeasurer";

export interface GraphNodePresentationMetrics {
  title: string;
  category?: string;
  summaryText?: string;
  detailLines: Array<{ key: string; label: string; value: string }>;
  inputLabels: string[];
  outputLabels: string[];
  width: number;
  height: number;
  portSectionTopOffset: number;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

export function measureGraphNodePresentation(
  node: GraphNode,
  definitions: GraphDefinitionRegistry,
  textMeasurer: TextMeasurer,
  renderConfig: GraphRenderConfig,
): GraphNodePresentationMetrics {
  const definition = definitions.getNode(node.typeId);
  const payload = readGraphNodePayloadRecord(node.payload);
  const contentProjection = definition
    ? projectGraphNodeContent(definition.content, payload)
    : { summaryText: undefined, detailLines: [] };
  const inputLabels = node.ports.filter((port) => port.direction === "input").map((port) => port.name);
  const outputLabels = node.ports.filter((port) => port.direction === "output").map((port) => port.name);
  const title = definition?.displayName ?? node.typeId;
  const category = definition?.category;
  const summaryText = contentProjection.summaryText;
  const detailLines = contentProjection.detailLines;
  const layout = renderConfig.layout;

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
  const summaryWidth = summaryText
    ? textMeasurer.measure(summaryText, { fontSize: 12, fontWeight: 500 }) + 40
    : 0;
  const detailWidth = detailLines.length > 0
    ? Math.max(
        ...detailLines.map((line) => {
          return textMeasurer.measure(`${line.label} ${line.value}`, { fontSize: 11, fontWeight: 400 }) + 40;
        }),
      )
    : 0;

  const width = clamp(
    Math.max(
      layout.nodeMinWidth,
      headerWidth + layout.nodePaddingX * 2,
      inputColumnWidth + outputColumnWidth + layout.nodePaddingX * 2 + 16,
      summaryWidth,
      detailWidth,
    ),
    layout.nodeMinWidth,
    layout.nodeMaxWidth,
  );

  const summaryAvailableWidth = width - layout.nodePaddingX * 2;
  const summaryLineCount = summaryText
    ? clamp(
        estimateWrappedLineCount(summaryText, summaryAvailableWidth, textMeasurer, {
          fontSize: 12,
          fontWeight: 500,
        }),
        1,
        layout.nodeSummaryMaxLines,
      )
    : 0;
  const summaryHeight = summaryLineCount * layout.nodeSummaryLineHeight;
  const detailLineHeight = 16;
  const detailHeight = detailLines.length * detailLineHeight;
  const hasContent = !!summaryText || detailLines.length > 0;
  const rowCount = Math.max(inputLabels.length, outputLabels.length, 1);
  const portSectionTopOffset =
    layout.nodeHeaderHeight +
    (hasContent ? layout.nodeSummaryTopGap + summaryHeight + detailHeight + layout.portSectionGap : 14);
  const height =
    portSectionTopOffset +
    rowCount * layout.portRowHeight +
    layout.nodeMetaHeight +
    layout.nodePaddingBottom;

  return {
    title,
    category,
    summaryText,
    detailLines,
    inputLabels,
    outputLabels,
    width: node.ui?.width ?? width,
    height: node.ui?.height ?? height,
    portSectionTopOffset,
  };
}
