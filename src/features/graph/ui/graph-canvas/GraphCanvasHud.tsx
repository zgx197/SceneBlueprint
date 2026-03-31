import type { PointerEvent as ReactPointerEvent, RefObject } from "react";
import type { NodeId } from "../../document/graphDocument";
import type { GraphFrame, GraphFrameEdge, GraphFrameMiniMap, GraphFrameNode } from "../../frame/graphFrame";
import { GRAPH_FRAME_LAYOUT } from "../../frame/graphFrameBuilder";
import { joinClassNames } from "./graphCanvasUtils";

interface GraphCanvasHudProps {
  searchInputRef: RefObject<HTMLInputElement | null>;
  searchQuery: string;
  searchFocused: boolean;
  setSearchQuery: (value: string) => void;
  setSearchFocused: (value: boolean) => void;
  searchResults: GraphFrameNode[];
  focusNode: (nodeId: NodeId) => void;
  summaryFrame: GraphFrame;
  selectedNodeCount: number;
  selectedEdgeCount: number;
  selectedGroupCount: number;
  selectedCommentCount: number;
  selectedSubgraphCount: number;
  selectionLabel: string;
  connectionHint: string;
  commandHistoryLength: number;
  commandRedoLength: number;
  clipboardLabel: string;
  backendLabel: string;
  measuredEdges: GraphFrameEdge[];
  nodes: GraphFrameNode[];
  searchHitNodeIds: Set<NodeId>;
  minimap: GraphFrameMiniMap | null;
  onOpenCommandPalette: () => void;
  onSelectAll: () => void;
  onCopy: () => void;
  onPaste: () => void;
  onAutoLayout: () => void;
  onCreateGroup: () => void;
  onCreateSubgraph: () => void;
  onCreateComment: () => void;
  onExportRuntimeContract: () => void;
  onMinimapPointerDown: (event: ReactPointerEvent<HTMLDivElement>) => void;
}

export function GraphCanvasHud(props: GraphCanvasHudProps) {
  const {
    searchInputRef,
    searchQuery,
    searchFocused,
    setSearchQuery,
    setSearchFocused,
    searchResults,
    focusNode,
    summaryFrame,
    selectedNodeCount,
    selectedEdgeCount,
    selectedGroupCount,
    selectedCommentCount,
    selectedSubgraphCount,
    selectionLabel,
    connectionHint,
    commandHistoryLength,
    commandRedoLength,
    clipboardLabel,
    backendLabel,
    measuredEdges,
    nodes,
    searchHitNodeIds,
    minimap,
    onOpenCommandPalette,
    onSelectAll,
    onCopy,
    onPaste,
    onAutoLayout,
    onCreateGroup,
    onCreateSubgraph,
    onCreateComment,
    onExportRuntimeContract,
    onMinimapPointerDown,
  } = props;

  const showSearchResults = searchFocused || searchQuery.trim().length > 0;

  return (
    <>
      <div className="sb-graph-canvas-toolbar">
        <div className="sb-graph-canvas-toolbar-group">
          <button type="button" className="sb-graph-action-button" onClick={onOpenCommandPalette}>命令面板</button>
          <button type="button" className="sb-graph-action-button" onClick={onCreateGroup}>创建分组</button>
          <button type="button" className="sb-graph-action-button" onClick={onCreateSubgraph}>创建子图</button>
          <button type="button" className="sb-graph-action-button" onClick={onCreateComment}>创建注释</button>
          <button type="button" className="sb-graph-action-button" onClick={onSelectAll}>全选</button>
          <button type="button" className="sb-graph-action-button" onClick={onCopy}>复制</button>
          <button type="button" className="sb-graph-action-button" onClick={onPaste}>粘贴</button>
          <button type="button" className="sb-graph-action-button" onClick={onAutoLayout}>自动布局</button>
          <button type="button" className="sb-graph-action-button" onClick={onExportRuntimeContract}>导出 Contract</button>
        </div>
        <div className="sb-graph-search-shell">
          <input
            ref={searchInputRef}
            className="sb-graph-search-input"
            value={searchQuery}
            placeholder="搜索节点，Ctrl/Cmd + F"
            onChange={(event) => {
              setSearchQuery(event.target.value);
            }}
            onFocus={() => {
              setSearchFocused(true);
            }}
            onBlur={() => {
              window.setTimeout(() => {
                setSearchFocused(false);
              }, 120);
            }}
          />
          {showSearchResults ? (
            <div className="sb-graph-search-results">
              {searchResults.length > 0 ? (
                searchResults.slice(0, 8).map((node) => {
                  return (
                    <button
                      key={node.id}
                      type="button"
                      className="sb-graph-search-result"
                      onMouseDown={(event) => {
                        event.preventDefault();
                      }}
                      onClick={() => {
                        focusNode(node.id);
                        setSearchFocused(false);
                      }}
                    >
                      <strong>{node.title}</strong>
                      <span>{node.id}</span>
                    </button>
                  );
                })
              ) : searchQuery.trim().length > 0 ? (
                <div className="sb-graph-search-empty">没有匹配的节点</div>
              ) : (
                <div className="sb-graph-search-empty">输入关键字以搜索节点</div>
              )}
            </div>
          ) : null}
        </div>
      </div>

      <div className="sb-graph-canvas-overlay sb-graph-canvas-overlay-top">
        <span>Nodes {summaryFrame.summary.nodeCount}</span>
        <span>Edges {summaryFrame.summary.edgeCount}</span>
        <span>Groups {summaryFrame.groups.length}</span>
        <span>Comments {summaryFrame.comments.length}</span>
        <span>Subgraphs {summaryFrame.subgraphs.length}</span>
        <span>Zoom {summaryFrame.viewport.zoom.toFixed(2)}</span>
      </div>
      <div className="sb-graph-canvas-overlay sb-graph-canvas-overlay-bottom">
        <span>{selectionLabel}</span>
        <span>{connectionHint}</span>
      </div>

      <div className="sb-graph-diagnostics">
        <span>Selected N{selectedNodeCount}</span>
        <span>Selected E{selectedEdgeCount}</span>
        <span>Selected G{selectedGroupCount}</span>
        <span>Selected C{selectedCommentCount}</span>
        <span>Selected S{selectedSubgraphCount}</span>
        <span>Undo {commandHistoryLength}</span>
        <span>Redo {commandRedoLength}</span>
        <span>Clipboard {clipboardLabel}</span>
        <span>{backendLabel}</span>
      </div>

      {minimap?.enabled ? (
        <div className="sb-graph-minimap" onPointerDown={onMinimapPointerDown}>
          <svg viewBox={`0 0 ${minimap.contentWidth} ${minimap.contentHeight}`} preserveAspectRatio="none">
            <rect x="0" y="0" width={GRAPH_FRAME_LAYOUT.contentWidth} height={GRAPH_FRAME_LAYOUT.contentHeight} className="sb-graph-minimap-bg" />
            {measuredEdges.map((edge) => (
              <line
                key={`minimap-edge-${edge.id}`}
                x1={edge.start.x}
                y1={edge.start.y}
                x2={edge.end.x}
                y2={edge.end.y}
                className={joinClassNames("sb-graph-minimap-edge", edge.selected && "sb-graph-minimap-edge-selected")}
              />
            ))}
            {nodes.map((node) => (
              <rect
                key={`minimap-node-${node.id}`}
                x={node.bounds.x}
                y={node.bounds.y}
                width={node.bounds.width}
                height={node.bounds.height}
                rx="16"
                ry="16"
                className={joinClassNames(
                  "sb-graph-minimap-node",
                  node.selected && "sb-graph-minimap-node-selected",
                  searchHitNodeIds.has(node.id) && "sb-graph-minimap-node-search-hit",
                )}
              />
            ))}
            {minimap.viewportRect ? (
              <rect
                x={minimap.viewportRect.x}
                y={minimap.viewportRect.y}
                width={minimap.viewportRect.width}
                height={minimap.viewportRect.height}
                className="sb-graph-minimap-viewport"
              />
            ) : null}
          </svg>
        </div>
      ) : null}
    </>
  );
}
