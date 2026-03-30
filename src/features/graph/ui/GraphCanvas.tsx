
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent,
  type PointerEvent as ReactPointerEvent,
  type WheelEvent,
} from "react";
import type { GraphPoint, NodeId, PortId } from "../document/graphDocument";
import type { GraphNodeDefinition } from "../definitions/graphDefinitions";
import type { GraphWorkspaceController } from "../GraphWorkspaceController";
import type { GraphFrameNode, GraphFramePort } from "../frame/graphFrame";
import { buildGraphBezierGeometry } from "../frame/graphEdgeGeometry";
import { GRAPH_FRAME_LAYOUT } from "../frame/graphFrameBuilder";
import {
  buildGraphPanViewportPatch,
  buildGraphZoomViewportPatch,
  createGraphPanSession,
  screenToGraphLocal,
  screenToGraphWorld,
  type GraphPanSession,
} from "../interaction/graphPanZoomHandler";
import {
  applyGraphNodeDragPreview,
  buildGraphNodeMoveCommand,
  createGraphNodeDragSession,
  updateGraphNodeDragSession,
  type GraphNodeDragSession,
} from "../interaction/graphNodeDragHandler";
import {
  clearGraphConnectionPreview,
  createGraphConnectionPreview,
  updateGraphConnectionPreviewPointer,
} from "../interaction/graphConnectionPreviewHandler";
import { GraphCanvasContextMenu } from "./GraphCanvasContextMenu";
import {
  isGraphAdditiveSelectionPointer,
  isGraphAutoLayoutShortcut,
  isGraphCancelKey,
  isGraphCopyShortcut,
  isGraphDeleteKey,
  isGraphFindShortcut,
  isGraphPasteShortcut,
  isGraphPrimaryPointer,
  isGraphSelectAllShortcut,
  shouldIgnoreGraphHotkeys,
  shouldStartGraphPanGesture,
} from "../../../host/input/graphInput";
import {
  buildGraphContextMenuModel,
  buildGraphNodeDefinitionGroups,
  createGraphHitTarget,
  getSelectionForGraphHitTarget,
  type GraphContextMenuActionId,
  type GraphContextMenuState,
  type GraphHitTarget,
  type GraphHitTargetDescriptor,
} from "./graphCanvasContextMenuModel";

interface GraphCanvasProps {
  controller: GraphWorkspaceController;
}

interface MarqueeSession {
  startLocal: GraphPoint;
  currentLocal: GraphPoint;
  additive: boolean;
}

function renderPortLabel(port: GraphFramePort): string {
  return `${port.name} / ${port.kind}`;
}

function renderPortRowLabel(port: GraphFramePort): string {
  return port.name;
}

function joinClassNames(...classNames: Array<string | false | null | undefined>) {
  return classNames.filter(Boolean).join(" ");
}

function unique<TValue>(values: TValue[]) {
  return [...new Set(values)];
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function pointToClient(local: GraphPoint, element: HTMLElement): GraphPoint {
  const rect = element.getBoundingClientRect();
  return {
    x: rect.left + local.x,
    y: rect.top + local.y,
  };
}

function createLocalRect(start: GraphPoint, end: GraphPoint) {
  return {
    x: Math.min(start.x, end.x),
    y: Math.min(start.y, end.y),
    width: Math.abs(end.x - start.x),
    height: Math.abs(end.y - start.y),
  };
}

function intersectsNodeRect(node: GraphFrameNode, worldRect: { x: number; y: number; width: number; height: number }) {
  return !(
    node.bounds.x + node.bounds.width < worldRect.x ||
    node.bounds.x > worldRect.x + worldRect.width ||
    node.bounds.y + node.bounds.height < worldRect.y ||
    node.bounds.y > worldRect.y + worldRect.height
  );
}

/**
 * 这次 Graph 连线起点曾经出现过“跑到节点中心附近”的回归问题，根因不是贝塞尔曲线算法，
 * 而是我们一度试图只靠 frame builder 的布局常量去估算端口圆心。
 *
 * 这种做法会在 CSS 调整后立刻失真：
 * 1. 端口按钮的真实 DOM 位置可能已经变化；
 * 2. surface 又叠加了 translate + scale；
 * 3. 结果就是边的起点不再贴着真正的端口圆心。
 *
 * 因此这里明确改为“以真实 DOM 渲染结果为准”：
 * 从端口按钮元素取圆心，再反算回 Graph 世界坐标。
 * 后续若再遇到起点/终点漂移问题，优先检查这条测量链路，而不是先去怀疑曲线绘制函数。
 */
function measurePortAnchorWorldPosition(
  anchorElement: HTMLElement,
  surfaceElement: HTMLElement,
  zoom: number,
): GraphPoint {
  const anchorRect = anchorElement.getBoundingClientRect();
  const surfaceRect = surfaceElement.getBoundingClientRect();

  return {
    x: (anchorRect.left + anchorRect.width * 0.5 - surfaceRect.left) / zoom,
    y: (anchorRect.top + anchorRect.height * 0.5 - surfaceRect.top) / zoom,
  };
}

export function GraphCanvas(props: GraphCanvasProps) {
  const { controller } = props;
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const contextMenuRef = useRef<HTMLDivElement | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const portAnchorElementRefs = useRef(new Map<PortId, HTMLButtonElement | null>());
  const [dragSession, setDragSession] = useState<GraphNodeDragSession | null>(null);
  const [panSession, setPanSession] = useState<GraphPanSession | null>(null);
  const [marqueeSession, setMarqueeSession] = useState<MarqueeSession | null>(null);
  const [contextMenu, setContextMenu] = useState<GraphContextMenuState | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [searchFocused, setSearchFocused] = useState(false);
  const [viewportSize, setViewportSize] = useState({ width: 0, height: 0 });
  const [measuredPortAnchors, setMeasuredPortAnchors] = useState<Map<PortId, GraphPoint>>(new Map());
  const didFinishDragRef = useRef(false);

  const { graphFrame, viewState } = controller;
  const displayFrame = useMemo(() => applyGraphNodeDragPreview(graphFrame, dragSession), [graphFrame, dragSession]);

  const groupedDefinitions = useMemo(() => {
    return buildGraphNodeDefinitionGroups(controller.definitions.listNodes());
  }, [controller.definitions]);

  const contextMenuModel = useMemo(() => {
    if (!contextMenu) {
      return null;
    }

    return buildGraphContextMenuModel({
      target: contextMenu.target,
      definitionGroups: groupedDefinitions,
      activeCategory: contextMenu.activeCategory,
    });
  }, [contextMenu, groupedDefinitions]);

  const displayNodeMap = useMemo(() => {
    return new Map(displayFrame.nodes.map((node) => [node.id, node]));
  }, [displayFrame.nodes]);

  const displayPortMap = useMemo(() => {
    const entries = new Map<PortId, GraphFramePort>();
    for (const node of displayFrame.nodes) {
      for (const port of [...node.inputs, ...node.outputs]) {
        entries.set(port.id, port);
      }
    }
    return entries;
  }, [displayFrame.nodes]);

  useLayoutEffect(() => {
    const surfaceElement = surfaceRef.current;
    if (!surfaceElement || displayFrame.viewport.zoom <= 0) {
      return;
    }

    /**
     * 这里必须放在 layout 阶段做测量，而不是继续复用纯数据层的 anchor：
     * 当前节点、端口、缩放、拖拽预览都会影响真实像素位置，只有 DOM 已经完成本轮布局后，
     * 我们才能得到可信的 socket 圆心。
     */

    const nextMeasuredAnchors = new Map<PortId, GraphPoint>();
    for (const [portId, anchorElement] of portAnchorElementRefs.current.entries()) {
      if (!anchorElement?.isConnected) {
        continue;
      }

      nextMeasuredAnchors.set(
        portId,
        measurePortAnchorWorldPosition(anchorElement, surfaceElement, displayFrame.viewport.zoom),
      );
    }

    setMeasuredPortAnchors(nextMeasuredAnchors);
  }, [displayFrame.nodes, displayFrame.viewport.zoom, viewportSize.height, viewportSize.width]);

  const measuredDisplayEdges = useMemo(() => {
    return displayFrame.edges.map((edge) => {
      /**
       * measuredPortAnchors 是当前最可信的锚点来源。
       *
       * `edge.start / edge.end` 仍然保留为兜底值，是为了首帧渲染、极端情况下某个 ref 尚未挂上时，
       * Graph 仍能继续显示，而不是整条边直接消失。
       * 但只要拿到了实测端口圆心，必须以实测值覆盖，否则又会回到“线连到估算位置”的老问题。
       */
      const sourceAnchor = measuredPortAnchors.get(edge.sourcePortId) ?? edge.start;
      const targetAnchor = measuredPortAnchors.get(edge.targetPortId) ?? edge.end;
      const geometry = buildGraphBezierGeometry(sourceAnchor, targetAnchor);

      return {
        ...edge,
        start: sourceAnchor,
        end: targetAnchor,
        path: geometry.path,
        midpoint: geometry.midpoint,
      };
    });
  }, [displayFrame.edges, measuredPortAnchors]);

  const measuredDisplayOverlays = useMemo(() => {
    return displayFrame.overlays.map((overlay) => {
      if (overlay.kind !== "connection-preview") {
        return overlay;
      }

      /**
       * 预览线和正式边必须共享同一套锚点真相。
       * 否则很容易出现“正式边是对的，但拖一根新线时起点又飘了”的二次回归。
       */

      const sourceAnchor = measuredPortAnchors.get(overlay.sourcePortId) ?? overlay.start;
      const geometry = buildGraphBezierGeometry(sourceAnchor, overlay.end);

      return {
        ...overlay,
        start: sourceAnchor,
        path: geometry.path,
      };
    });
  }, [displayFrame.overlays, measuredPortAnchors]);

  const selectedNodeCount = viewState.selection.selectedNodeIds.length;
  const selectedEdgeCount = viewState.selection.selectedEdgeIds.length;

  const searchResults = useMemo(() => {
    const keyword = searchQuery.trim().toLowerCase();
    if (!keyword) {
      return [];
    }

    return displayFrame.nodes.filter((node) => {
      return [node.id, node.title, node.typeId, node.category ?? "", node.summary ?? ""]
        .join(" ")
        .toLowerCase()
        .includes(keyword);
    });
  }, [displayFrame.nodes, searchQuery]);

  const searchHitNodeIds = useMemo(() => {
    return new Set(searchResults.map((node) => node.id));
  }, [searchResults]);

  const minimapViewport = useMemo(() => {
    if (viewportSize.width <= 0 || viewportSize.height <= 0) {
      return null;
    }

    return {
      x: clamp(-displayFrame.viewport.panX / displayFrame.viewport.zoom, 0, GRAPH_FRAME_LAYOUT.contentWidth),
      y: clamp(-displayFrame.viewport.panY / displayFrame.viewport.zoom, 0, GRAPH_FRAME_LAYOUT.contentHeight),
      width: Math.min(viewportSize.width / displayFrame.viewport.zoom, GRAPH_FRAME_LAYOUT.contentWidth),
      height: Math.min(viewportSize.height / displayFrame.viewport.zoom, GRAPH_FRAME_LAYOUT.contentHeight),
    };
  }, [displayFrame.viewport, viewportSize.height, viewportSize.width]);

  useEffect(() => {
    const viewportElement = viewportRef.current;
    if (!viewportElement || typeof ResizeObserver === "undefined") {
      return;
    }

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }

      setViewportSize({
        width: entry.contentRect.width,
        height: entry.contentRect.height,
      });
    });

    observer.observe(viewportElement);
    return () => {
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    if (!dragSession && !panSession && !marqueeSession) {
      return;
    }

    const handlePointerMove = (event: PointerEvent) => {
      const viewportElement = viewportRef.current;
      if (!viewportElement) {
        return;
      }

      if (dragSession) {
        const world = screenToGraphWorld(
          { x: event.clientX, y: event.clientY },
          viewportElement,
          controller.viewState.viewport,
        );
        setDragSession((current) => {
          if (!current) {
            return current;
          }

          return updateGraphNodeDragSession(current, world);
        });
        return;
      }

      if (panSession) {
        controller.patchViewport(
          buildGraphPanViewportPatch(panSession, { x: event.clientX, y: event.clientY }),
        );
        return;
      }

      if (marqueeSession) {
        const local = screenToGraphLocal({ x: event.clientX, y: event.clientY }, viewportElement);
        setMarqueeSession((current) => {
          if (!current) {
            return current;
          }

          return {
            ...current,
            currentLocal: local,
          };
        });
        controller.patchInteraction({
          marqueeSelection: {
            startX: marqueeSession.startLocal.x,
            startY: marqueeSession.startLocal.y,
            endX: local.x,
            endY: local.y,
          },
        });
      }
    };

    const handlePointerUp = () => {
      const viewportElement = viewportRef.current;

      if (dragSession) {
        const moveCommand = buildGraphNodeMoveCommand(dragSession);
        if (moveCommand) {
          controller.execute(moveCommand);
          didFinishDragRef.current = true;
        }

        controller.patchInteraction({ draggingNodeIds: [] });
        setDragSession(null);
      }

      if (panSession) {
        setPanSession(null);
      }

      if (marqueeSession && viewportElement) {
        const localRect = createLocalRect(marqueeSession.startLocal, marqueeSession.currentLocal);
        const additive = marqueeSession.additive;

        if (localRect.width < 4 && localRect.height < 4) {
          if (!additive) {
            controller.setSelection({ selectedNodeIds: [], selectedEdgeIds: [] });
          }
        } else {
          const startWorld = screenToGraphWorld(
            pointToClient({ x: localRect.x, y: localRect.y }, viewportElement),
            viewportElement,
            viewState.viewport,
          );
          const endWorld = screenToGraphWorld(
            pointToClient({ x: localRect.x + localRect.width, y: localRect.y + localRect.height }, viewportElement),
            viewportElement,
            viewState.viewport,
          );
          const worldRect = {
            x: Math.min(startWorld.x, endWorld.x),
            y: Math.min(startWorld.y, endWorld.y),
            width: Math.abs(endWorld.x - startWorld.x),
            height: Math.abs(endWorld.y - startWorld.y),
          };
          const hitNodeIds = displayFrame.nodes
            .filter((node) => intersectsNodeRect(node, worldRect))
            .map((node) => node.id);

          controller.setSelection({
            selectedNodeIds: additive
              ? unique([...viewState.selection.selectedNodeIds, ...hitNodeIds])
              : hitNodeIds,
            selectedEdgeIds: [],
          });
        }

        controller.patchInteraction({ marqueeSelection: undefined });
        setMarqueeSession(null);
      }
    };

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", handlePointerUp);
    window.addEventListener("pointercancel", handlePointerUp);

    return () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", handlePointerUp);
      window.removeEventListener("pointercancel", handlePointerUp);
    };
  }, [controller, displayFrame.nodes, dragSession, marqueeSession, panSession, viewState.selection.selectedNodeIds, viewState.viewport]);

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      if (!contextMenu) {
        return;
      }

      const target = event.target;
      if (contextMenuRef.current && target instanceof Node && contextMenuRef.current.contains(target)) {
        return;
      }

      setContextMenu(null);
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (shouldIgnoreGraphHotkeys(event.target)) {
        return;
      }

      if (isGraphFindShortcut(event)) {
        event.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
        return;
      }

      if (isGraphSelectAllShortcut(event)) {
        event.preventDefault();
        controller.selectAllNodes();
        return;
      }

      if (isGraphCopyShortcut(event)) {
        event.preventDefault();
        controller.copySelection();
        return;
      }

      if (isGraphPasteShortcut(event)) {
        event.preventDefault();
        controller.pasteClipboard();
        return;
      }

      if (isGraphAutoLayoutShortcut(event)) {
        event.preventDefault();
        controller.autoLayoutSelectionOrAll();
        return;
      }

      if (isGraphDeleteKey(event)) {
        if (selectedNodeCount > 0 || selectedEdgeCount > 0) {
          event.preventDefault();
          controller.deleteSelection();
        }
        return;
      }

      if (!isGraphCancelKey(event)) {
        return;
      }

      setContextMenu(null);
      setSearchQuery("");
      setSearchFocused(false);
      controller.patchInteraction({ marqueeSelection: undefined });
      if (controller.viewState.connectionPreview.active) {
        controller.setConnectionPreview(clearGraphConnectionPreview());
      }
    };

    window.addEventListener("pointerdown", handlePointerDown);
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("pointerdown", handlePointerDown);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [contextMenu, controller, selectedEdgeCount, selectedNodeCount]);

  const syncSelectionForContextTarget = (target: GraphHitTarget) => {
    const selection = getSelectionForGraphHitTarget(target);
    if (selection) {
      controller.setSelection(selection);
    }
  };

  const openContextMenu = (
    clientPoint: GraphPoint,
    descriptor: GraphHitTargetDescriptor,
    options: { syncSelection?: boolean } = {},
  ) => {
    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return null;
    }

    const local = screenToGraphLocal(clientPoint, viewportElement);
    const world = screenToGraphWorld(clientPoint, viewportElement, viewState.viewport);
    const target = createGraphHitTarget(world, descriptor);
    const viewportRect = viewportElement.getBoundingClientRect();
    const menuWidth = 420;
    const menuHeight = 360;
    const clampedX = Math.min(Math.max(local.x, 12), Math.max(12, viewportRect.width - menuWidth - 12));
    const clampedY = Math.min(Math.max(local.y, 12), Math.max(12, viewportRect.height - menuHeight - 12));

    if (options.syncSelection ?? descriptor.kind !== "canvas") {
      syncSelectionForContextTarget(target);
    }

    setContextMenu({
      screenX: clampedX,
      screenY: clampedY,
      target,
      activeCategory: groupedDefinitions[0]?.category,
    });

    return target;
  };

  const handleViewportWheel = (event: WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    setContextMenu(null);

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    controller.patchViewport(
      buildGraphZoomViewportPatch(
        { x: event.clientX, y: event.clientY },
        viewportElement,
        controller.viewState.viewport,
        event.deltaY,
      ),
    );
  };

  const handleViewportPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!viewState.connectionPreview.active) {
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const pointer = screenToGraphWorld(
      { x: event.clientX, y: event.clientY },
      viewportElement,
      viewState.viewport,
    );

    controller.setConnectionPreview(updateGraphConnectionPreviewPointer(viewState.connectionPreview, pointer));
  };

  const handleViewportPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    setContextMenu(null);

    if (shouldStartGraphPanGesture(event)) {
      setPanSession(createGraphPanSession({ x: event.clientX, y: event.clientY }, viewState.viewport));
      return;
    }

    if (!isGraphPrimaryPointer(event) || event.target !== event.currentTarget) {
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const local = screenToGraphLocal({ x: event.clientX, y: event.clientY }, viewportElement);
    const additive = isGraphAdditiveSelectionPointer(event);

    controller.setConnectionPreview(clearGraphConnectionPreview());
    controller.patchInteraction({
      hoveredPortId: undefined,
      marqueeSelection: {
        startX: local.x,
        startY: local.y,
        endX: local.x,
        endY: local.y,
      },
    });
    setMarqueeSession({ startLocal: local, currentLocal: local, additive });
  };

  const handleViewportContextMenu = (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();
    openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "canvas" }, { syncSelection: false });
  };

  const beginNodeDrag = (event: ReactPointerEvent<HTMLElement>, nodeId: NodeId) => {
    if (!isGraphPrimaryPointer(event)) {
      return;
    }

    const viewportElement = viewportRef.current;
    if (!viewportElement) {
      return;
    }

    const world = screenToGraphWorld(
      { x: event.clientX, y: event.clientY },
      viewportElement,
      viewState.viewport,
    );

    const selection = viewState.selection.selectedNodeIds.includes(nodeId)
      ? viewState.selection.selectedNodeIds
      : [nodeId];

    controller.setSelection({ selectedNodeIds: selection, selectedEdgeIds: [] });
    controller.patchInteraction({ draggingNodeIds: selection, hoveredNodeId: nodeId });
    setDragSession(createGraphNodeDragSession(selection, world));
  };

  const startConnectionPreview = (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => {
    event.stopPropagation();

    const sourcePort = displayPortMap.get(portId);
    if (!sourcePort) {
      return;
    }

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
    controller.setConnectionPreview(
      createGraphConnectionPreview(
        nodeId,
        portId,
        // 新建预览线时也必须优先采用实测 socket 圆心，避免起点瞬间从节点中部跳出来。
        measuredPortAnchors.get(portId) ?? sourcePort.anchor,
      ),
    );
  };

  const completeConnectionPreview = (event: MouseEvent<HTMLButtonElement>, nodeId: NodeId, portId: PortId) => {
    event.stopPropagation();

    const preview = viewState.connectionPreview;
    if (!preview.active || !preview.fromNodeId || !preview.fromPortId) {
      return;
    }

    const targetPort = displayPortMap.get(portId);
    if (!targetPort || !targetPort.connectable) {
      return;
    }

    controller.execute({
      type: "graph.connect-ports",
      sourceNodeId: preview.fromNodeId,
      sourcePortId: preview.fromPortId,
      targetNodeId: nodeId,
      targetPortId: portId,
    });
    controller.setConnectionPreview(clearGraphConnectionPreview());
    controller.patchInteraction({ hoveredPortId: undefined });
  };

  const executeContextMenuAction = (actionId: GraphContextMenuActionId) => {
    if (!contextMenu) {
      return;
    }

    switch (actionId) {
      case "context.disconnect-node-edges": {
        if (contextMenu.target.kind === "node") {
          controller.disconnectNodeEdges([contextMenu.target.nodeId]);
        }
        setContextMenu(null);
        return;
      }
      case "context.disconnect-port-edges": {
        if (contextMenu.target.kind === "port") {
          controller.disconnectPortEdges([contextMenu.target.portId]);
        }
        setContextMenu(null);
        return;
      }
      case "context.delete-node": {
        if (contextMenu.target.kind === "node") {
          controller.execute({ type: "graph.remove-nodes", nodeIds: [contextMenu.target.nodeId] });
        }
        setContextMenu(null);
        return;
      }
      case "context.delete-edge": {
        if (contextMenu.target.kind === "edge") {
          controller.execute({ type: "graph.remove-edges", edgeIds: [contextMenu.target.edgeId] });
        }
        setContextMenu(null);
        return;
      }
      case "context.reset-viewport": {
        controller.patchViewport({ zoom: 1, panX: 0, panY: 0 });
        setContextMenu(null);
        return;
      }
      default:
        return actionId satisfies never;
    }
  };

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

  const bindPortAnchorElement = (portId: PortId) => {
    return (element: HTMLButtonElement | null) => {
      // 每个 portId 都直接绑定到真实 socket 按钮，供连线锚点测量使用。
      portAnchorElementRefs.current.set(portId, element);
    };
  };

  const createNodeFromContextMenu = (definition: GraphNodeDefinition) => {
    if (!contextMenu) {
      return;
    }

    controller.execute({
      type: "graph.add-node",
      nodeTypeId: definition.typeId,
      position: {
        x: Math.round(contextMenu.target.world.x),
        y: Math.round(contextMenu.target.world.y),
      },
    });
    setContextMenu(null);
  };

  const focusNode = (nodeId: NodeId) => {
    const node = displayNodeMap.get(nodeId);
    const viewportElement = viewportRef.current;
    if (!node || !viewportElement) {
      return;
    }

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
    controller.centerViewportOnPoint(
      {
        x: node.bounds.x + node.bounds.width * 0.5,
        y: node.bounds.y + node.bounds.height * 0.5,
      },
      { width: viewportElement.clientWidth, height: viewportElement.clientHeight },
    );
  };

  const handleNodeClick = (event: MouseEvent<HTMLElement>, nodeId: NodeId) => {
    if (didFinishDragRef.current) {
      didFinishDragRef.current = false;
      return;
    }

    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedNodeIds.includes(nodeId);
      controller.setSelection({
        selectedNodeIds: exists
          ? viewState.selection.selectedNodeIds.filter((entry) => entry !== nodeId)
          : [...viewState.selection.selectedNodeIds, nodeId],
        selectedEdgeIds: [],
      });
      return;
    }

    controller.setSelection({ selectedNodeIds: [nodeId], selectedEdgeIds: [] });
  };

  const handleEdgeClick = (event: MouseEvent<SVGPathElement | SVGGElement>, edgeId: string) => {
    event.stopPropagation();
    if (isGraphAdditiveSelectionPointer(event)) {
      const exists = viewState.selection.selectedEdgeIds.includes(edgeId);
      controller.setSelection({
        selectedNodeIds: [],
        selectedEdgeIds: exists
          ? viewState.selection.selectedEdgeIds.filter((entry) => entry !== edgeId)
          : [...viewState.selection.selectedEdgeIds, edgeId],
      });
      return;
    }

    controller.setSelection({ selectedNodeIds: [], selectedEdgeIds: [edgeId] });
  };

  const handleMinimapPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();

    if (viewportSize.width <= 0 || viewportSize.height <= 0) {
      return;
    }

    const rect = event.currentTarget.getBoundingClientRect();
    const localX = clamp(event.clientX - rect.left, 0, rect.width);
    const localY = clamp(event.clientY - rect.top, 0, rect.height);
    const worldX = (localX / rect.width) * GRAPH_FRAME_LAYOUT.contentWidth;
    const worldY = (localY / rect.height) * GRAPH_FRAME_LAYOUT.contentHeight;

    controller.centerViewportOnPoint(
      { x: worldX, y: worldY },
      { width: viewportSize.width, height: viewportSize.height },
    );
  };

  const showSearchResults = searchFocused || searchQuery.trim().length > 0;
  const marqueeRect = marqueeSession ? createLocalRect(marqueeSession.startLocal, marqueeSession.currentLocal) : null;

  return (
    <div className="sb-graph-canvas-shell">
      <div
        ref={viewportRef}
        className="sb-graph-canvas-viewport"
        onWheel={handleViewportWheel}
        onPointerMove={handleViewportPointerMove}
        onPointerDown={handleViewportPointerDown}
        onContextMenu={handleViewportContextMenu}
      >
        <div className="sb-graph-canvas-toolbar">
          <div className="sb-graph-canvas-toolbar-group">
            <button type="button" className="sb-graph-action-button" onClick={() => controller.selectAllNodes()}>全选</button>
            <button type="button" className="sb-graph-action-button" onClick={() => controller.copySelection()}>复制</button>
            <button type="button" className="sb-graph-action-button" onClick={() => controller.pasteClipboard()}>粘贴</button>
            <button type="button" className="sb-graph-action-button" onClick={() => controller.autoLayoutSelectionOrAll()}>自动布局</button>
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
            {measuredDisplayEdges.map((edge) => {
              return (
                <g key={edge.id}>
                  <path
                    d={edge.path}
                    className="sb-graph-edge-hit"
                    onClick={(event) => {
                      handleEdgeClick(event, edge.id);
                    }}
                    onContextMenu={(event) => {
                      event.preventDefault();
                      event.stopPropagation();
                      openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "edge", edgeId: edge.id });
                    }}
                  />
                  {edge.selected ? <path d={edge.path} className="sb-graph-edge-outline" /> : null}
                  <path d={edge.path} className={`sb-graph-edge${edge.selected ? " sb-graph-edge-selected" : ""}`} />
                  {edge.selected ? (
                    <g
                      className="sb-graph-edge-handle"
                      onClick={(event) => {
                        handleEdgeClick(event, edge.id);
                      }}
                      onContextMenu={(event) => {
                        event.preventDefault();
                        event.stopPropagation();
                        openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "edge", edgeId: edge.id });
                      }}
                    >
                      <circle cx={edge.midpoint.x} cy={edge.midpoint.y} r="8" className="sb-graph-edge-handle-ring" />
                      <circle cx={edge.midpoint.x} cy={edge.midpoint.y} r="3.2" className="sb-graph-edge-handle-core" />
                    </g>
                  ) : null}
                </g>
              );
            })}
            {measuredDisplayOverlays.map((overlay, index) => {
              if (overlay.kind !== "connection-preview") {
                return null;
              }

              return <path key={`overlay-${index}`} d={overlay.path} className="sb-graph-edge sb-graph-edge-preview" />;
            })}
          </svg>

          {displayFrame.nodes.map((node) => {
            return (
              <article
                key={node.id}
                className={joinClassNames(
                  "sb-graph-node",
                  node.selected && "sb-graph-node-selected",
                  searchHitNodeIds.has(node.id) && "sb-graph-node-search-hit",
                )}
                onClick={(event) => {
                  handleNodeClick(event, node.id);
                }}
                onContextMenu={(event) => {
                  event.preventDefault();
                  event.stopPropagation();
                  openContextMenu({ x: event.clientX, y: event.clientY }, { kind: "node", nodeId: node.id });
                }}
                style={{
                  left: node.bounds.x,
                  top: node.bounds.y,
                  width: node.bounds.width,
                  minHeight: node.bounds.height,
                }}
                onPointerEnter={() => {
                  controller.patchInteraction({ hoveredNodeId: node.id });
                }}
                onPointerLeave={() => {
                  controller.patchInteraction({ hoveredNodeId: undefined });
                }}
              >
                <header className="sb-graph-node-header" onPointerDown={(event) => beginNodeDrag(event, node.id)}>
                  <div>
                    <strong>{node.title}</strong>
                    <span>{node.typeId}</span>
                  </div>
                  <span className="sb-graph-node-category">{node.category ?? "Uncategorized"}</span>
                </header>
                <div className="sb-graph-node-summary">{node.summary ?? "当前节点暂未提供摘要说明。"}</div>
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
                                onClick={(event) => completeConnectionPreview(event, node.id, row.input!.id)}
                                onPointerEnter={() => {
                                  controller.patchInteraction({ hoveredPortId: row.input!.id });
                                }}
                                onPointerLeave={() => {
                                  controller.patchInteraction({ hoveredPortId: undefined });
                                }}
                                onContextMenu={(event) => {
                                  event.preventDefault();
                                  event.stopPropagation();
                                  openContextMenu(
                                    { x: event.clientX, y: event.clientY },
                                    { kind: "port", nodeId: node.id, portId: row.input!.id, direction: "input" },
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
                                  openContextMenu(
                                    { x: event.clientX, y: event.clientY },
                                    { kind: "port", nodeId: node.id, portId: row.input!.id, direction: "input" },
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
                                  openContextMenu(
                                    { x: event.clientX, y: event.clientY },
                                    { kind: "port", nodeId: node.id, portId: row.output!.id, direction: "output" },
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
                                onClick={(event) => startConnectionPreview(event, node.id, row.output!.id)}
                                onPointerEnter={() => {
                                  controller.patchInteraction({ hoveredPortId: row.output!.id });
                                }}
                                onPointerLeave={() => {
                                  controller.patchInteraction({ hoveredPortId: undefined });
                                }}
                                onContextMenu={(event) => {
                                  event.preventDefault();
                                  event.stopPropagation();
                                  openContextMenu(
                                    { x: event.clientX, y: event.clientY },
                                    { kind: "port", nodeId: node.id, portId: row.output!.id, direction: "output" },
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
        </div>

        {marqueeRect ? (
          <div className="sb-graph-marquee" style={{ left: marqueeRect.x, top: marqueeRect.y, width: marqueeRect.width, height: marqueeRect.height }} />
        ) : null}

        <div className="sb-graph-canvas-overlay sb-graph-canvas-overlay-top">
          <span>Nodes {displayFrame.summary.nodeCount}</span>
          <span>Edges {displayFrame.summary.edgeCount}</span>
          <span>Zoom {displayFrame.viewport.zoom.toFixed(2)}</span>
          <span>Selected N{selectedNodeCount} / E{selectedEdgeCount}</span>
        </div>
        <div className="sb-graph-canvas-overlay sb-graph-canvas-overlay-bottom">
          <span>{selectionLabel}</span>
          <span>{connectionHint}</span>
        </div>

        <div className="sb-graph-diagnostics">
          <span>Undo {controller.commandSnapshot.historyLength}</span>
          <span>Redo {controller.commandSnapshot.redoLength}</span>
          <span>
            Clipboard {controller.clipboardSummary ? `${controller.clipboardSummary.nodeCount}N/${controller.clipboardSummary.edgeCount}E` : "Empty"}
          </span>
          <span>{controller.workspaceFileSnapshot?.backend ?? "memory"}</span>
        </div>

        <div className="sb-graph-minimap" onPointerDown={handleMinimapPointerDown}>
          <svg viewBox={`0 0 ${GRAPH_FRAME_LAYOUT.contentWidth} ${GRAPH_FRAME_LAYOUT.contentHeight}`} preserveAspectRatio="none">
            <rect x="0" y="0" width={GRAPH_FRAME_LAYOUT.contentWidth} height={GRAPH_FRAME_LAYOUT.contentHeight} className="sb-graph-minimap-bg" />
            {measuredDisplayEdges.map((edge) => (
              <line
                key={`minimap-edge-${edge.id}`}
                x1={edge.start.x}
                y1={edge.start.y}
                x2={edge.end.x}
                y2={edge.end.y}
                className={joinClassNames("sb-graph-minimap-edge", edge.selected && "sb-graph-minimap-edge-selected")}
              />
            ))}
            {displayFrame.nodes.map((node) => (
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
            {minimapViewport ? (
              <rect
                x={minimapViewport.x}
                y={minimapViewport.y}
                width={minimapViewport.width}
                height={minimapViewport.height}
                className="sb-graph-minimap-viewport"
              />
            ) : null}
          </svg>
        </div>

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
