import { useEffect, useState, type RefObject } from "react";

export interface GraphCanvasViewportSize {
  width: number;
  height: number;
}

export function useGraphCanvasViewportSize(viewportRef: RefObject<HTMLDivElement | null>) {
  const [viewportSize, setViewportSize] = useState<GraphCanvasViewportSize>({ width: 0, height: 0 });

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
  }, [viewportRef]);

  return viewportSize;
}
