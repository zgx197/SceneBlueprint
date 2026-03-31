import { Panel } from "../../shared/components/Panel";
import type { GraphWorkspaceController } from "./GraphWorkspaceController";
import { GraphCanvas } from "./ui/GraphCanvas";
import { GraphWorkspaceIssueBanner } from "./ui/GraphWorkspaceIssueBanner";

interface GraphPanelProps {
  controller: GraphWorkspaceController;
}

export function GraphPanel(props: GraphPanelProps) {
  const { controller } = props;

  return (
    <Panel
      title="Graph Workspace"
      description="节点图主创作区"
      bodyClassName="sb-graph-panel-body"
    >
      <div className="sb-graph-panel-shell">
        <GraphWorkspaceIssueBanner preflight={controller.exportPreflight} />
        <div className="sb-graph-panel-canvas">
          <GraphCanvas controller={controller} />
        </div>
      </div>
    </Panel>
  );
}
