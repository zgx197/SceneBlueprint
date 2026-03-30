import { useMemo, useRef, useState } from "react";
import type { GraphWorkspaceController } from "../GraphWorkspaceController";
import type { GraphFramePort } from "../frame/graphFrame";
import { GRAPH_FRAME_LAYOUT } from "../frame/graphFrameBuilder";
import { applyGraphNodeDragPreview } from "../interaction/graphNodeDragHandler";
import { clearGraphConnectionPreview } from "../interaction/graphConnectionPreviewHandler";
import { GraphCanvasContextMenu } from "./GraphCanvasContextMenu";
import { GraphCanvasEdgeLayer } from "./graph-canvas/GraphCanvasEdgeLayer";
import { GraphCanvasHud } from "./graph-canvas/GraphCanvasHud";
import { GraphCanvasNodeLayer } from "./graph-canvas/GraphCanvasNodeLayer";
import { joinClassNames } from "./graph-canvas/graphCanvasUtils";
import { useGraphCanvasContextMenu } from "./graph-canvas/useGraphCanvasContextMenu";
import { useGraphCanvasControllerActions } from "./graph-canvas/useGraphCanvasControllerActions";
import { useGraphCanvasDerivedState } from "./graph-canvas/useGraphCanvasDerivedState";
import { useGraphCanvasKeyboardShortcuts } from "./graph-canvas/useGraphCanvasKeyboardShortcuts";
import { useGraphCanvasMeasuredAnchors } from "./graph-canvas/useGraphCanvasMeasuredAnchors";
import { useGraphCanvasPointerInteractions } from "./graph-canvas/useGraphCanvasPointerInteractions";
import { useGraphCanvasViewportSize } from "./graph-canvas/useGraphCanvasViewportSize";

interface GraphCanvasProps {
  controller: GraphWorkspaceController;
}

export function GraphCanvas(props: GraphCanvasProps) {
  const { controller } = props;
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const contextMenuRef = useRef<HTMLDivElement | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);

  const viewportSize = useGraphCanvasViewportSize(viewportRef);
  const { graphFrame, viewState } = controller;

  const pointerInteractions = useGraphCanvasPointerInteractions({
    controller,
    viewportRef,
    displayNodes: graphFrame.nodes,
    viewState,
    onDismissContextMenu: () => setContextMenu(null),
  });

  const displayFrame = useMemo(() => {
    return applyGraphNodeDragPreview(graphFrame, pointerInteractions.dragSession);
  }, [graphFrame, pointerInteractions.dragSession]);

  const {
    contextMenu,
    setContextMenu,
    contextMenuModel,
    openContextMenu,
    executeContextMenuAction,
    handleEdgeContextMenu,
    handleNodeContextMenu,
    handlePortContextMenu,
    createNodeFromContextMenu,
  } = useGraphCanvasContextMenu({ controller, viewportRef });

  const { surfaceRef, measuredPortAnchors, bindPortAnchorElement } = useGraphCanvasMeasuredAnchors(
    displayFrame.nodes,
    displayFrame.viewport.zoom,
    viewportSize,
  );

  const displayState = useGraphCanvasDerivedState({
    displayFrame,
    measuredPortAnchors,
    viewportSize,
    searchQuery,
  });

  const actions = useGraphCanvasControllerActions({
    controller,
    viewportRef,
    viewportSize,
    viewState,
    measuredPortAnchors,
    displayState,
    didFinishDragRef: pointerInteractions.didFinishDragRef,
    openContextMenu,
  });

  const selectedNodeCount = viewState.selection.selectedNodeIds.length;
  const selectedEdgeCount = viewState.selection.selectedEdgeIds.length;

  const selectionLabel = controller.selectionTarget.kind === "none" ? "未选择对象" : controller.selectionTarget.kind;
  const connectionHint = displayFrame.summary.activeOutputPortId
    ? "已高亮可连接输入端点，右键可创建新节点"
    : "滚轮缩放，Alt / Shift / 中键平移，Ctrl / Cmd 进行多选";

  const buildPortAnchorClassName = (port: GraphFramePort) => {
    return joinClassNames(
      "sb-graph-port-anchor",
      port.direction === "input" ? "sb-graph-port-anchor-input" : "sb-graph-port-anchor-output",
      port.connected ? "sb-graph-port-anchor-connected" : "sb-graph-port-anchor-idle",
      port.hovered && "sb-graph-port-anchor-hovered",
      port.source && "sb-graph-port-anchor-source",
      port.connectable && "sb-graph-port-anchor-connectable",
    );
  };

  const buildPortLabelClassName = (port: GraphFramePort) => {
    return joinClassNames(
      "sb-graph-port-label",
      port.connectable && "sb-graph-port-label-connectable",
      port.hovered && "sb-graph-port-label-hovered",
    );
  };

  useGraphCanvasKeyboardShortcuts({
    contextMenuOpen: !!contextMenu,
    contextMenuRef,
    searchInputRef,
    selectedNodeCount,
    selectedEdgeCount,
    hasActiveConnectionPreview: controller.viewState.connectionPreview.active,
    onDismissContextMenu: () => setContextMenu(null),
    onClearSearch: () => {
      setSearchQuery("");
      setSearchFocused(false);
    },
    onResetTransientInteraction: pointerInteractions.clearTransientInteraction,
    onSelectAll: () => controller.selectAllNodes(),
    onCopy: () => controller.copySelection(),
    onPaste: () => controller.pasteClipboard(),
    onAutoLayout: () => controller.autoLayoutSelectionOrAll(),
    onDeleteSelection: () => controller.deleteSelection(),
    onClearConnectionPreview: () => controller.setConnectionPreview(clearGraphConnectionPreview()),
  });

  return (
    <div className="sb-graph-canvas-shell">
      <div
        ref={viewportRef}
        className="sb-graph-canvas-viewport"
        onWheel={pointerInteractions.handleViewportWheel}
        onPointerMove={pointerInteractions.handleViewportPointerMove}
        onPointerDown={pointerInteractions.handleViewportPointerDown}
        onContextMenu={actions.handleViewportContextMenu}
      >
        <GraphCanvasHud
          searchInputRef={searchInputRef}
          searchQuery={searchQuery}
          searchFocused={searchFocused}
          setSearchQuery={setSearchQuery}
          setSearchFocused={setSearchFocused}
          searchResults={displayState.searchResults}
          focusNode={actions.focusNode}
          summaryFrame={displayFrame}
          selectedNodeCount={selectedNodeCount}
          selectedEdgeCount={selectedEdgeCount}
          selectionLabel={selectionLabel}
          connectionHint={connectionHint}
          commandHistoryLength={controller.commandSnapshot.historyLength}
          commandRedoLength={controller.commandSnapshot.redoLength}
          clipboardLabel={controller.clipboardSummary ? `${controller.clipboardSummary.nodeCount}N/${controller.clipboardSummary.edgeCount}E` : "Empty"}
          backendLabel={controller.workspaceFileSnapshot?.backend ?? "memory"}
          measuredEdges={displayState.measuredDisplayEdges}
          nodes={displayFrame.nodes}
          searchHitNodeIds={displayState.searchHitNodeIds}
          minimapViewport={displayState.minimapViewport}
          onSelectAll={() => controller.selectAllNodes()}
          onCopy={() => controller.copySelection()}
          onPaste={() => controller.pasteClipboard()}
          onAutoLayout={() => controller.autoLayoutSelectionOrAll()}
          onMinimapPointerDown={actions.handleMinimapPointerDown}
        />

        <div
          ref={surfaceRef}
          className="sb-graph-canvas-surface"
          style={{
            width: GRAPH_FRAME_LAYOUT.contentWidth,
            height: GRAPH_FRAME_LAYOUT.contentHeight,
            transform: `translate(${displayFrame.viewport.panX}px, ${displayFrame.viewport.panY}px) scale(${displayFrame.viewport.zoom})`,
          }}
        >
          <svg className="sb-graph-canvas-edges" width={GRAPH_FRAME_LAYOUT.contentWidth} height={GRAPH_FRAME_LAYOUT.contentHeight}>
            <GraphCanvasEdgeLayer
              edges={displayState.measuredDisplayEdges}
              overlays={displayState.measuredDisplayOverlays}
              onEdgeClick={actions.handleEdgeClick}
              onEdgeContextMenu={handleEdgeContextMenu}
            />
          </svg>

          <GraphCanvasNodeLayer
            nodes={displayFrame.nodes}
            searchHitNodeIds={displayState.searchHitNodeIds}
            buildPortAnchorClassName={buildPortAnchorClassName}
            buildPortLabelClassName={buildPortLabelClassName}
            bindPortAnchorElement={bindPortAnchorElement}
            onNodeClick={actions.handleNodeClick}
            onNodeContextMenu={handleNodeContextMenu}
            onNodePointerEnter={actions.handleNodePointerEnter}
            onNodePointerLeave={actions.handleNodePointerLeave}
            onBeginNodeDrag={pointerInteractions.beginNodeDrag}
            onPortPointerEnter={actions.handlePortPointerEnter}
            onPortPointerLeave={actions.handlePortPointerLeave}
            onPortContextMenu={handlePortContextMenu}
            onStartConnectionPreview={actions.startConnectionPreview}
            onCompleteConnectionPreview={actions.completeConnectionPreview}
          />
        </div>

        {pointerInteractions.marqueeRect ? (
          <div
            className="sb-graph-marquee"
            style={{
              left: pointerInteractions.marqueeRect.x,
              top: pointerInteractions.marqueeRect.y,
              width: pointerInteractions.marqueeRect.width,
              height: pointerInteractions.marqueeRect.height,
            }}
          />
        ) : null}

        {contextMenu && contextMenuModel ? (
          <GraphCanvasContextMenu
            containerRef={contextMenuRef}
            state={contextMenu}
            model={contextMenuModel}
            onAction={executeContextMenuAction}
            onCategoryChange={(category: string) => {
              setContextMenu((current) => (current ? { ...current, activeCategory: category } : current));
            }}
            onCreateNode={createNodeFromContextMenu}
          />
        ) : null}
      </div>
    </div>
  );
}
