import { useMemo, useRef, useState } from "react";
import type { GraphWorkspaceController } from "../GraphWorkspaceController";
import type { GraphFramePort } from "../frame/graphFrame";
import { GRAPH_FRAME_LAYOUT } from "../frame/graphFrameBuilder";
import { applyGraphNodeDragPreview } from "../interaction/graphNodeDragHandler";
import { clearGraphConnectionPreview } from "../interaction/graphConnectionPreviewHandler";
import { GraphCommandPalette } from "./GraphCommandPalette";
import { GraphCanvasContextMenu } from "./GraphCanvasContextMenu";
import { GraphCanvasEdgeLayer } from "./graph-canvas/GraphCanvasEdgeLayer";
import { GraphCanvasHud } from "./graph-canvas/GraphCanvasHud";
import { GraphCanvasNodeLayer } from "./graph-canvas/GraphCanvasNodeLayer";
import { GraphCanvasStructureLayer } from "./graph-canvas/GraphCanvasStructureLayer";
import { joinClassNames } from "./graph-canvas/graphCanvasUtils";
import { useGraphCanvasContextMenu } from "./graph-canvas/useGraphCanvasContextMenu";
import { useGraphCanvasControllerActions } from "./graph-canvas/useGraphCanvasControllerActions";
import { useGraphCanvasDerivedState } from "./graph-canvas/useGraphCanvasDerivedState";
import { useGraphCanvasKeyboardShortcuts } from "./graph-canvas/useGraphCanvasKeyboardShortcuts";
import { useGraphCanvasMeasuredAnchors } from "./graph-canvas/useGraphCanvasMeasuredAnchors";
import { useGraphCanvasPointerInteractions } from "./graph-canvas/useGraphCanvasPointerInteractions";
import { useGraphCanvasViewportSize } from "./graph-canvas/useGraphCanvasViewportSize";
import { buildGraphCommandPaletteModel, type GraphCommandPaletteItem } from "./graphCommandPaletteModel";

interface GraphCanvasProps {
  controller: GraphWorkspaceController;
}

function getSelectionLabel(kind: GraphWorkspaceController["selectionTarget"]["kind"]) {
  switch (kind) {
    case "graph-node":
      return "当前选择：节点";
    case "graph-edge":
      return "当前选择：连线";
    case "graph-group":
      return "当前选择：分组";
    case "graph-comment":
      return "当前选择：注释";
    case "graph-subgraph":
      return "当前选择：子图";
    case "none":
      return "未选择对象";
    default:
      return `当前选择：${kind}`;
  }
}

export function GraphCanvas(props: GraphCanvasProps) {
  const { controller } = props;
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const contextMenuRef = useRef<HTMLDivElement | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false);
  const [commandPaletteQuery, setCommandPaletteQuery] = useState("");

  const viewportSize = useGraphCanvasViewportSize(viewportRef);
  const { graphFrame, viewState } = controller;

  const {
    contextMenu,
    setContextMenu,
    contextMenuModel,
    openContextMenu,
    executeContextMenuAction,
    createNodeFromContextMenu,
  } = useGraphCanvasContextMenu({ controller, viewportRef });

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

  const { surfaceRef, measuredPortAnchors, bindPortAnchorElement } = useGraphCanvasMeasuredAnchors(
    displayFrame.nodes,
    displayFrame.viewport.zoom,
    viewportSize,
  );

  const searchMatches = useMemo(() => {
    return controller.nodeSearchService.search(controller.document, searchQuery, { limit: 8 });
  }, [controller.document, controller.nodeSearchService, searchQuery]);

  const displayState = useGraphCanvasDerivedState({
    displayFrame,
    measuredPortAnchors,
    searchResultNodeIds: searchMatches.map((entry) => entry.nodeId),
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

  const selection = viewState.selection;
  const selectedNodeCount = (selection.selectedNodeIds ?? []).length;
  const selectedEdgeCount = (selection.selectedEdgeIds ?? []).length;
  const selectedGroupCount = (selection.selectedGroupIds ?? []).length;
  const selectedCommentCount = (selection.selectedCommentIds ?? []).length;
  const selectedSubgraphCount = (selection.selectedSubgraphIds ?? []).length;
  const totalSelectionCount =
    selectedNodeCount +
    selectedEdgeCount +
    selectedGroupCount +
    selectedCommentCount +
    selectedSubgraphCount;
  const clipboardLabel = controller.clipboardSummary
    ? `${controller.clipboardSummary.nodeCount}N/${controller.clipboardSummary.edgeCount}E`
    : "Empty";

  const paletteNodeResults = useMemo(() => {
    return controller.nodeSearchService.search(controller.document, commandPaletteQuery, { limit: 8 });
  }, [commandPaletteQuery, controller.document, controller.nodeSearchService]);

  const commandPaletteModel = useMemo(() => {
    return buildGraphCommandPaletteModel({
      definitions: controller.definitions.listNodes(),
      nodeResults: paletteNodeResults,
      selectedNodeCount,
      selectedEdgeCount,
      clipboardLabel,
      searchQuery: commandPaletteQuery,
    });
  }, [
    clipboardLabel,
    commandPaletteQuery,
    controller.definitions,
    paletteNodeResults,
    selectedEdgeCount,
    selectedNodeCount,
  ]);

  const selectionLabel = getSelectionLabel(controller.selectionTarget.kind);
  const connectionHint = displayFrame.summary.activeOutputPortId
    ? "已高亮可连接输入端点，左键连接，右键或 Esc 取消连线"
    : "滚轮缩放，Alt / Shift / 中键平移，Ctrl / Cmd 进行多选";

  const focusSearchInput = () => {
    searchInputRef.current?.focus();
    searchInputRef.current?.select();
  };

  const openCommandPalette = () => {
    setContextMenu(null);
    setCommandPaletteOpen(true);
  };

  const closeCommandPalette = () => {
    setCommandPaletteOpen(false);
    setCommandPaletteQuery("");
  };

  const handleCommandPaletteOpenChange = (open: boolean) => {
    if (open) {
      openCommandPalette();
      return;
    }

    closeCommandPalette();
  };

  const handleCommandPaletteSelect = (item: GraphCommandPaletteItem) => {
    closeCommandPalette();

    switch (item.kind) {
      case "focus-node":
        actions.focusNode(item.nodeId);
        return;
      case "create-node":
        actions.createNodeAtViewportCenter(item.nodeTypeId);
        return;
      case "action":
        switch (item.actionId) {
          case "palette.focus-search":
            window.requestAnimationFrame(() => {
              focusSearchInput();
            });
            return;
          case "palette.select-all":
            controller.selectAllNodes();
            return;
          case "palette.copy-selection":
            controller.copySelection();
            return;
          case "palette.paste-clipboard":
            controller.pasteClipboard();
            return;
          case "palette.auto-layout":
            void controller.autoLayoutSelectionOrAll();
            return;
          case "palette.reset-viewport":
            actions.resetViewport();
            return;
          case "palette.save-draft":
            controller.saveDraft();
            return;
          case "palette.load-draft":
            controller.loadDraft();
            return;
          case "palette.save-workspace-file":
            void controller.saveWorkspaceFile();
            return;
          case "palette.export-runtime-contract":
            void controller.exportRuntimeContractFile();
            return;
          case "palette.load-workspace-file":
            void controller.loadWorkspaceFile();
            return;
          case "palette.reset-bootstrap":
            controller.resetToBootstrap();
            return;
          default:
            return;
        }
      default:
        return;
    }
  };

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
    shortcutBindingService: controller.shortcutBindingService,
    contextMenuOpen: !!contextMenu,
    commandPaletteOpen,
    contextMenuRef,
    searchInputRef,
    totalSelectionCount,
    hasActiveConnectionPreview: controller.viewState.connectionPreview.active,
    onDismissContextMenu: () => setContextMenu(null),
    onOpenCommandPalette: openCommandPalette,
    onCloseCommandPalette: closeCommandPalette,
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
          selectedGroupCount={selectedGroupCount}
          selectedCommentCount={selectedCommentCount}
          selectedSubgraphCount={selectedSubgraphCount}
          selectionLabel={selectionLabel}
          connectionHint={connectionHint}
          commandHistoryLength={controller.commandSnapshot.historyLength}
          commandRedoLength={controller.commandSnapshot.redoLength}
          clipboardLabel={clipboardLabel}
          backendLabel={controller.workspaceFileSnapshot?.backend ?? "memory"}
          measuredEdges={displayState.measuredDisplayEdges}
          nodes={displayFrame.nodes}
          searchHitNodeIds={displayState.searchHitNodeIds}
          minimap={displayFrame.minimap}
          onOpenCommandPalette={openCommandPalette}
          onSelectAll={() => controller.selectAllNodes()}
          onCopy={() => controller.copySelection()}
          onPaste={() => controller.pasteClipboard()}
          onAutoLayout={() => controller.autoLayoutSelectionOrAll()}
          onCreateGroup={() => controller.createGroupFromSelection()}
          onCreateSubgraph={() => controller.createSubgraphFromSelection()}
          onCreateComment={() => controller.createCommentAtViewportCenter(viewportSize)}
          onExportRuntimeContract={() => controller.exportRuntimeContractFile()}
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
          <GraphCanvasStructureLayer
            groups={displayFrame.groups}
            comments={displayFrame.comments}
            subgraphs={displayFrame.subgraphs}
            onGroupClick={actions.handleGroupClick}
            onGroupContextMenu={actions.handleGroupContextMenu}
            onGroupPointerEnter={actions.handleGroupPointerEnter}
            onGroupPointerLeave={actions.handleGroupPointerLeave}
            onCommentClick={actions.handleCommentClick}
            onCommentContextMenu={actions.handleCommentContextMenu}
            onCommentPointerEnter={actions.handleCommentPointerEnter}
            onCommentPointerLeave={actions.handleCommentPointerLeave}
            onSubgraphClick={actions.handleSubgraphClick}
            onSubgraphContextMenu={actions.handleSubgraphContextMenu}
            onSubgraphPointerEnter={actions.handleSubgraphPointerEnter}
            onSubgraphPointerLeave={actions.handleSubgraphPointerLeave}
          />

          <svg className="sb-graph-canvas-edges" width={GRAPH_FRAME_LAYOUT.contentWidth} height={GRAPH_FRAME_LAYOUT.contentHeight}>
            <GraphCanvasEdgeLayer
              edges={displayState.measuredDisplayEdges}
              overlays={displayState.measuredDisplayOverlays}
              decorations={displayFrame.decorations}
              onEdgeClick={actions.handleEdgeClick}
              onEdgeContextMenu={actions.handleEdgeContextMenu}
            />
          </svg>

          <GraphCanvasNodeLayer
            nodes={displayFrame.nodes}
            searchHitNodeIds={displayState.searchHitNodeIds}
            buildPortAnchorClassName={buildPortAnchorClassName}
            buildPortLabelClassName={buildPortLabelClassName}
            bindPortAnchorElement={bindPortAnchorElement}
            onNodeClick={actions.handleNodeClick}
            onNodeContextMenu={actions.handleNodeContextMenu}
            onNodePointerEnter={actions.handleNodePointerEnter}
            onNodePointerLeave={actions.handleNodePointerLeave}
            onBeginNodeDrag={pointerInteractions.beginNodeDrag}
            onPortPointerEnter={actions.handlePortPointerEnter}
            onPortPointerLeave={actions.handlePortPointerLeave}
            onPortContextMenu={actions.handlePortContextMenu}
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

        <GraphCommandPalette
          open={commandPaletteOpen}
          query={commandPaletteQuery}
          model={commandPaletteModel}
          onOpenChange={handleCommandPaletteOpenChange}
          onQueryChange={setCommandPaletteQuery}
          onSelectItem={handleCommandPaletteSelect}
        />
      </div>
    </div>
  );
}

