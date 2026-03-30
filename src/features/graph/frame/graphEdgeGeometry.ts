import type { GraphPoint } from "../document/graphDocument";

export interface GraphBezierGeometry {
  path: string;
  midpoint: GraphPoint;
}

/**
 * Graph Canvas 中所有正式连线与预览连线都必须满足同一条约束：
 * 线段的起点和终点都来自端口锚点中心，而不是节点外框、标签文字或任意视觉近似位置。
 *
 * 这个几何函数被 FrameBuilder 与拖拽预览共同复用，目的是避免后续改动时
 * 一边改了锚点算法、另一边仍然保留旧逻辑，最终出现“节点端口位置正确，
 * 但边的起点/预览线起点漂移”的回归问题。
 */
export function buildGraphBezierGeometry(start: GraphPoint, end: GraphPoint): GraphBezierGeometry {
  const distance = Math.max(52, Math.abs(end.x - start.x) * 0.45);
  const controlA = { x: start.x + distance, y: start.y };
  const controlB = { x: end.x - distance, y: end.y };
  const t = 0.5;
  const inverse = 1 - t;

  return {
    path: `M ${start.x} ${start.y} C ${controlA.x} ${controlA.y}, ${controlB.x} ${controlB.y}, ${end.x} ${end.y}`,
    midpoint: {
      x:
        inverse * inverse * inverse * start.x +
        3 * inverse * inverse * t * controlA.x +
        3 * inverse * t * t * controlB.x +
        t * t * t * end.x,
      y:
        inverse * inverse * inverse * start.y +
        3 * inverse * inverse * t * controlA.y +
        3 * inverse * t * t * controlB.y +
        t * t * t * end.y,
    },
  };
}
