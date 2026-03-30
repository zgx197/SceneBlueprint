import { Panel } from "../../shared/components/Panel";
import type { GraphWorkspaceController } from "./GraphWorkspaceController";
import { GraphCanvas } from "./ui/GraphCanvas";

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
      <GraphCanvas controller={controller} />
    </Panel>
  );
}

