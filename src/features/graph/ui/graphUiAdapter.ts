import type { GraphDocument, EdgeId, NodeId, PortId, PortKind } from "../document/graphDocument";
import type { GraphDefinitionRegistry } from "../definitions/graphDefinitions";
import type { GraphViewState, GraphViewportState } from "../state/graphViewState";

export interface GraphCanvasPortViewModel {
  id: PortId;
  key: string;
  name: string;
  kind: PortKind;
  dataType?: string;
}

export interface GraphCanvasNodeViewModel {
  id: NodeId;
  title: string;
  typeId: string;
  category?: string;
  summary?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  selected: boolean;
  inputs: GraphCanvasPortViewModel[];
  outputs: GraphCanvasPortViewModel[];
}

export interface GraphCanvasEdgeViewModel {
  id: EdgeId;
  sourceNodeId: NodeId;
  sourcePortId: PortId;
  targetNodeId: NodeId;
  targetPortId: PortId;
  selected: boolean;
}

export interface GraphCanvasModel {
  nodes: GraphCanvasNodeViewModel[];
  edges: GraphCanvasEdgeViewModel[];
  viewport: GraphViewportState;
}

export interface GraphUiAdapter {
  buildCanvasModel(
    document: GraphDocument,
    viewState: GraphViewState,
    definitions: GraphDefinitionRegistry,
  ): GraphCanvasModel;
}

function computeNodeHeight(inputCount: number, outputCount: number): number {
  const rowCount = Math.max(inputCount, outputCount, 1);
  return 152 + rowCount * 40;
}

export function createGraphUiAdapter(): GraphUiAdapter {
  return {
    buildCanvasModel(document, viewState, definitions) {
      return {
        nodes: document.nodes.map((node) => {
          const definition = definitions.getNode(node.typeId);
          const selected = viewState.selection.selectedNodeIds.includes(node.id);
          const inputs = node.ports
            .filter((port) => port.direction === "input")
            .map((port) => ({
              id: port.id,
              key: port.key,
              name: port.name,
              kind: port.kind,
              dataType: port.dataType,
            }));
          const outputs = node.ports
            .filter((port) => port.direction === "output")
            .map((port) => ({
              id: port.id,
              key: port.key,
              name: port.name,
              kind: port.kind,
              dataType: port.dataType,
            }));

          return {
            id: node.id,
            title: definition?.displayName ?? node.typeId,
            typeId: node.typeId,
            category: definition?.category,
            summary: definition?.summary,
            x: node.position.x,
            y: node.position.y,
            width: node.ui?.width ?? 292,
            height: node.ui?.height ?? computeNodeHeight(inputs.length, outputs.length),
            selected,
            inputs,
            outputs,
          };
        }),
        edges: document.edges.map((edge) => ({
          id: edge.id,
          sourceNodeId: edge.sourceNodeId,
          sourcePortId: edge.sourcePortId,
          targetNodeId: edge.targetNodeId,
          targetPortId: edge.targetPortId,
          selected: viewState.selection.selectedEdgeIds.includes(edge.id),
        })),
        viewport: viewState.viewport,
      };
    },
  };
}