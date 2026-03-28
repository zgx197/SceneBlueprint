import { Panel } from "../../shared/components/Panel";

export function GraphPanel() {
  return (
    <Panel
      title="Graph Workspace"
      description="主 Authoring 画布区域，后续承接节点图、选择态、命令系统与编辑交互。"
    >
      <div className="sb-placeholder">
        <p>当前阶段：先明确正式编辑器主画布位置。</p>
        <p>后续这里会承接 Node Graph、Selection、Command、Undo/Redo。</p>
      </div>
    </Panel>
  );
}
