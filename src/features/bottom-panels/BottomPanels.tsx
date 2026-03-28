import { TimelinePanel } from "../timeline/TimelinePanel";
import { LogPanel } from "../log/LogPanel";

interface BottomPanelsProps {
  logs: string[];
}

export function BottomPanels({ logs }: BottomPanelsProps) {
  return (
    <div className="sb-bottom-panels">
      <div className="sb-bottom-panel-column">
        <TimelinePanel />
      </div>
      <div className="sb-bottom-panel-column">
        <LogPanel logs={logs} />
      </div>
    </div>
  );
}
