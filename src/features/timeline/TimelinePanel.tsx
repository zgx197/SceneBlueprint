import { Panel } from "../../shared/components/Panel";

export function TimelinePanel() {
  return (
    <Panel
      title="Timeline 区域"
      description="第一阶段仅保留未来能力位置，不提前实现复杂时间轴交互。"
    >
      <div className="sb-placeholder">
        <p>时间轴能力后置进入。</p>
        <p>当前只验证工作台结构是否合理。</p>
      </div>
    </Panel>
  );
}
