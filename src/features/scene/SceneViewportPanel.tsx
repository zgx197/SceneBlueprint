import { Panel } from "../../shared/components/Panel";

export function SceneViewportPanel() {
  return (
    <Panel
      title="Scene Viewport"
      description="预留场景白模与 Marker 空间视窗，后续承接相机、空间绑定与场景选择态。"
    >
      <div className="sb-placeholder">
        <p>当前阶段：先建立正式 Scene 工作区。</p>
        <p>后续阶段：白模加载、相机控制、Marker 预览、空间绑定。</p>
      </div>
    </Panel>
  );
}
