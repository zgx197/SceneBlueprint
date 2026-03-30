import type { GraphPoint } from "../../document/graphDocument";
import type { GraphFrameNode, GraphFramePort } from "../../frame/graphFrame";

export function renderPortLabel(port: GraphFramePort): string {
  return `${port.name} / ${port.kind}`;
}

export function renderPortRowLabel(port: GraphFramePort): string {
  return port.name;
}

export function joinClassNames(...classNames: Array<string | false | null | undefined>) {
  return classNames.filter(Boolean).join(" ");
}

export function unique<TValue>(values: TValue[]) {
  return [...new Set(values)];
}

export function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

export function pointToClient(local: GraphPoint, element: HTMLElement): GraphPoint {
  const rect = element.getBoundingClientRect();
  return {
    x: rect.left + local.x,
    y: rect.top + local.y,
  };
}

export function createLocalRect(start: GraphPoint, end: GraphPoint) {
  return {
    x: Math.min(start.x, end.x),
    y: Math.min(start.y, end.y),
    width: Math.abs(end.x - start.x),
    height: Math.abs(end.y - start.y),
  };
}

export function intersectsNodeRect(node: GraphFrameNode, worldRect: { x: number; y: number; width: number; height: number }) {
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
export function measurePortAnchorWorldPosition(
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
