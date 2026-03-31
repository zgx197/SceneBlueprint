import type { MouseEvent, PointerEvent as ReactPointerEvent } from "react";
import type { GraphPoint, NodeId, PortId } from "../../document/graphDocument";
import type { GraphFrameNode, GraphFramePort } from "../../frame/graphFrame";
import { renderPortLabel, renderPortRowLabel, joinClassNames } from "./graphCanvasUtils";

interface GraphCanvasNodeLayerProps {
  nodes: GraphFrameNode[];
  searchHitNodeIds: Set<NodeId>;
  buildPortAnchorClassName: (port: GraphFramePort) => string;
  buildPortLabelClassName: (port: GraphFramePort) => string;
  bindPortAnchorElement: (portId: PortId) => (element: HTMLButtonElement | null) => void;
  onNodeClick: (event: MouseEvent<HTMLElement>, nodeId: NodeId) => void;
  onNodeContextMenu: (clientPoint: GraphPoint, nodeId: NodeId) => void;
  onNodePointerEnter: (nodeId: NodeId) => void;
  onNodePointerLeave: () => void;
  onBeginNodeDrag: (event: ReactPointerEvent<HTMLElement>, nodeId: NodeId) => void;
  onPortPointerEnter: (portId: PortId) => void;
  onPortPointerLeave: () => void;
  onPortContextMenu: (clientPoint: GraphPoint, nodeId: NodeId, portId: PortId, direction: "input" | "output") => void;
  onStartConnectionPreview: (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => void;
  onCompleteConnectionPreview: (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => void;
}

export function GraphCanvasNodeLayer(props: GraphCanvasNodeLayerProps) {
  const {
    nodes,
    searchHitNodeIds,
    buildPortAnchorClassName,
    buildPortLabelClassName,
    bindPortAnchorElement,
    onNodeClick,
    onNodeContextMenu,
    onNodePointerEnter,
    onNodePointerLeave,
    onBeginNodeDrag,
    onPortPointerEnter,
    onPortPointerLeave,
    onPortContextMenu,
    onStartConnectionPreview,
    onCompleteConnectionPreview,
  } = props;

  return (
    <>
      {nodes.map((node) => {
        const summaryText = node.content.summaryText ?? node.description ?? "当前节点暂未提供内容摘要。";

        return (
          <article
            key={node.id}
            className={joinClassNames(
              "sb-graph-node",
              node.selected && "sb-graph-node-selected",
              searchHitNodeIds.has(node.id) && "sb-graph-node-search-hit",
            )}
            onClick={(event) => {
              onNodeClick(event, node.id);
            }}
            onContextMenu={(event) => {
              event.preventDefault();
              event.stopPropagation();
              onNodeContextMenu({ x: event.clientX, y: event.clientY }, node.id);
            }}
            style={{
              left: node.bounds.x,
              top: node.bounds.y,
              width: node.bounds.width,
              minHeight: node.bounds.height,
            }}
            onPointerEnter={() => {
              onNodePointerEnter(node.id);
            }}
            onPointerLeave={() => {
              onNodePointerLeave();
            }}
          >
            <header className="sb-graph-node-header" onPointerDown={(event) => onBeginNodeDrag(event, node.id)}>
              <div>
                <strong>{node.title}</strong>
                <span>{node.typeId}</span>
              </div>
              <span className="sb-graph-node-category">{node.category ?? "Uncategorized"}</span>
            </header>
            <div className="sb-graph-node-summary">
              <div className="sb-graph-node-summary-text">{summaryText}</div>
              {node.content.detailLines.map((line) => {
                return (
                  <div key={`${node.id}-${line.key}`} className="sb-graph-node-detail-line">
                    <span className="sb-graph-node-detail-label">{line.label}</span>
                    <span className="sb-graph-node-detail-value">{line.value}</span>
                  </div>
                );
              })}
            </div>
            <div className="sb-graph-node-ports">
              {node.rows.map((row, index) => {
                return (
                  <div key={`${node.id}-row-${index}`} className="sb-graph-port-pair-row">
                    <div className="sb-graph-port-slot sb-graph-port-slot-input">
                      {row.input ? (
                        <>
                          <button
                            ref={bindPortAnchorElement(row.input.id)}
                            type="button"
                            className={buildPortAnchorClassName(row.input)}
                            onClick={(event) => onCompleteConnectionPreview(event, node.id, row.input!.id)}
                            onPointerEnter={() => {
                              onPortPointerEnter(row.input!.id);
                            }}
                            onPointerLeave={() => {
                              onPortPointerLeave();
                            }}
                            onContextMenu={(event) => {
                              event.preventDefault();
                              event.stopPropagation();
                              onPortContextMenu(
                                { x: event.clientX, y: event.clientY },
                                node.id,
                                row.input!.id,
                                "input",
                              );
                            }}
                            title={renderPortLabel(row.input)}
                            aria-label={`连接到输入端点 ${row.input.name}`}
                          >
                            <span className="sb-graph-port-anchor-dot" />
                          </button>
                          <span
                            className={buildPortLabelClassName(row.input)}
                            title={renderPortLabel(row.input)}
                            onContextMenu={(event) => {
                              event.preventDefault();
                              event.stopPropagation();
                              onPortContextMenu(
                                { x: event.clientX, y: event.clientY },
                                node.id,
                                row.input!.id,
                                "input",
                              );
                            }}
                          >
                            {renderPortRowLabel(row.input)}
                            {row.input.connectedEdgeCount > 0 ? (
                              <span className="sb-graph-port-count">{row.input.connectedEdgeCount}</span>
                            ) : null}
                          </span>
                        </>
                      ) : (
                        <div className="sb-graph-port-slot-empty" />
                      )}
                    </div>
                    <div className="sb-graph-port-slot sb-graph-port-slot-output">
                      {row.output ? (
                        <>
                          <span
                            className={buildPortLabelClassName(row.output)}
                            title={renderPortLabel(row.output)}
                            onContextMenu={(event) => {
                              event.preventDefault();
                              event.stopPropagation();
                              onPortContextMenu(
                                { x: event.clientX, y: event.clientY },
                                node.id,
                                row.output!.id,
                                "output",
                              );
                            }}
                          >
                            {renderPortRowLabel(row.output)}
                            {row.output.connectedEdgeCount > 0 ? (
                              <span className="sb-graph-port-count">{row.output.connectedEdgeCount}</span>
                            ) : null}
                          </span>
                          <button
                            ref={bindPortAnchorElement(row.output.id)}
                            type="button"
                            className={buildPortAnchorClassName(row.output)}
                            onClick={(event) => onStartConnectionPreview(event, node.id, row.output!.id)}
                            onPointerEnter={() => {
                              onPortPointerEnter(row.output!.id);
                            }}
                            onPointerLeave={() => {
                              onPortPointerLeave();
                            }}
                            onContextMenu={(event) => {
                              event.preventDefault();
                              event.stopPropagation();
                              onPortContextMenu(
                                { x: event.clientX, y: event.clientY },
                                node.id,
                                row.output!.id,
                                "output",
                              );
                            }}
                            title={renderPortLabel(row.output)}
                            aria-label={`从输出端点 ${row.output.name} 开始连线`}
                          >
                            <span className="sb-graph-port-anchor-dot" />
                          </button>
                        </>
                      ) : (
                        <div className="sb-graph-port-slot-empty" />
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
            <div className="sb-graph-node-meta">
              <span>ID: {node.id}</span>
              <span>Pos: ({Math.round(node.bounds.x)}, {Math.round(node.bounds.y)})</span>
            </div>
          </article>
        );
      })}
    </>
  );
}
