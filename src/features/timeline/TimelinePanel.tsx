import { Panel } from "../../shared/components/Panel";

export function TimelinePanel() {
  return (
    <Panel
      title="Timeline / Sequence"
      description="后续承接时间线、顺序编排和与图流程相关的底部编辑能力。"
    >
      <div className="sb-placeholder">
        <p>当前阶段：仅保留底部编排区位置。</p>
        <p>后续阶段：Timeline、Sequence、Playback 等能力进入。</p>
      </div>
    </Panel>
  );
}
