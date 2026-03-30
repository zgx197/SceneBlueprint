import { lazy, Suspense, useEffect, useState } from "react";
import { RuntimeErrorBoundary } from "../../shared/components/RuntimeErrorBoundary";
import { useAppLogContext } from "../../shared/logging/LogContext";
import { Panel } from "../../shared/components/Panel";

const SceneViewportCanvas = lazy(async () => {
  const module = await import("./SceneViewportCanvas");
  return { default: module.SceneViewportCanvas };
});

const VIEWPORT_BOOT_DELAY_MS = 320;

function SceneViewportFallback() {
  return (
    <div className="sb-scene-fallback sb-scene-fallback-error">
      Scene Viewport 挂载失败，已降级为安全占位视图，请查看运行日志。
    </div>
  );
}

function SceneViewportBooting() {
  return <div className="sb-scene-fallback">正在准备 Scene Viewport 运行时...</div>;
}

export function SceneViewportPanel() {
  const { log } = useAppLogContext();
  const [shouldRenderCanvas, setShouldRenderCanvas] = useState(false);

  useEffect(() => {
    log("info", "scene-viewport", "Scene Viewport 面板已挂载，准备延迟启动 3D 视口。");

    const timer = window.setTimeout(() => {
      setShouldRenderCanvas(true);
      log("info", "scene-viewport", "开始挂载 Scene Viewport 3D 画布。");
    }, VIEWPORT_BOOT_DELAY_MS);

    return () => {
      window.clearTimeout(timer);
    };
  }, [log]);

  return (
    <Panel
      title="Scene Viewport"
      description="场景白模与 Marker 视窗"
      className="sb-scene-panel"
      bodyClassName="sb-scene-panel-body"
    >
      <RuntimeErrorBoundary scope="scene-viewport" fallback={<SceneViewportFallback />}>
        {shouldRenderCanvas ? (
          <Suspense fallback={<div className="sb-scene-fallback">正在加载场景视口...</div>}>
            <SceneViewportCanvas />
          </Suspense>
        ) : (
          <SceneViewportBooting />
        )}
      </RuntimeErrorBoundary>
    </Panel>
  );
}

