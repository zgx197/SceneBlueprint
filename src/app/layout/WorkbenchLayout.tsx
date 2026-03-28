import type { ReactNode } from "react";
import { Toolbar } from "../../shared/components/Toolbar";

interface WorkbenchLayoutProps {
  toolbarRight?: ReactNode;
  graph: ReactNode;
  scene: ReactNode;
  inspector: ReactNode;
  bottomPanels: ReactNode;
}

export function WorkbenchLayout(props: WorkbenchLayoutProps) {
  const { toolbarRight, graph, scene, inspector, bottomPanels } = props;

  return (
    <div className="sb-shell">
      <Toolbar rightSlot={toolbarRight} />
      <main className="sb-workbench">
        <section className="sb-area sb-area-graph">{graph}</section>
        <section className="sb-area sb-area-scene">{scene}</section>
        <aside className="sb-area sb-area-inspector">{inspector}</aside>
        <section className="sb-area sb-area-bottom">{bottomPanels}</section>
      </main>
    </div>
  );
}
