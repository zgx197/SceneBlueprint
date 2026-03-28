import { Panel } from "../../shared/components/Panel";

export function GraphPanel() {
  return (
    <Panel
      title="Graph 区域"
      description="第一阶段先建立工作台主区域占位，后续在这里逐步填入节点图能力。"
    >
      <div className="sb-placeholder">
        <p>当前阶段：Tauri 盒子骨架</p>
        <p>后续阶段：Node Graph / Selection / Command / Undo</p>
      </div>
    </Panel>
  );
}
