import type { ReactNode } from "react";
import { Toolbar } from "../../shared/components/Toolbar";

interface WorkbenchLayoutProps {
  toolbarRight?: ReactNode;
  graph: ReactNode;
  inspector: ReactNode;
  timeline: ReactNode;
  log: ReactNode;
}

export function WorkbenchLayout(props: WorkbenchLayoutProps) {
  const { toolbarRight, graph, inspector, timeline, log } = props;

  return (
    <div className="sb-shell">
      <Toolbar rightSlot={toolbarRight} />
      <main className="sb-workbench">
        <section className="sb-area sb-area-graph">{graph}</section>
        <aside className="sb-area sb-area-inspector">{inspector}</aside>
        <section className="sb-area sb-area-timeline">{timeline}</section>
        <section className="sb-area sb-area-log">{log}</section>
      </main>
    </div>
  );
}
