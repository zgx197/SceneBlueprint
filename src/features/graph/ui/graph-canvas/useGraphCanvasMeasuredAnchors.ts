import { useLayoutEffect, useRef, useState } from "react";
import type { GraphPoint, PortId } from "../../document/graphDocument";
import type { GraphFrameNode } from "../../frame/graphFrame";
import { measurePortAnchorWorldPosition } from "./graphCanvasUtils";

export interface GraphCanvasViewportSize {
  width: number;
  height: number;
}

export function useGraphCanvasMeasuredAnchors(
  nodes: GraphFrameNode[],
  zoom: number,
  viewportSize: GraphCanvasViewportSize,
) {
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const portAnchorElementRefs = useRef(new Map<PortId, HTMLButtonElement | null>());
  const [measuredPortAnchors, setMeasuredPortAnchors] = useState<Map<PortId, GraphPoint>>(new Map());

  useLayoutEffect(() => {
    const surfaceElement = surfaceRef.current;
    if (!surfaceElement || zoom <= 0) {
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
        measurePortAnchorWorldPosition(anchorElement, surfaceElement, zoom),
      );
    }

    setMeasuredPortAnchors(nextMeasuredAnchors);
  }, [nodes, zoom, viewportSize.height, viewportSize.width]);

  const bindPortAnchorElement = (portId: PortId) => {
    return (element: HTMLButtonElement | null) => {
      // 每个 portId 都直接绑定到真实 socket 按钮，供连线锚点测量使用。
      portAnchorElementRefs.current.set(portId, element);
    };
  };

  return {
    surfaceRef,
    measuredPortAnchors,
    bindPortAnchorElement,
  };
}
