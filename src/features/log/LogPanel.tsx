import { Panel } from "../../shared/components/Panel";

interface LogPanelProps {
  logs: string[];
}

export function LogPanel({ logs }: LogPanelProps) {
  return (
    <Panel
      title="日志与验证"
      description="记录当前阶段宿主启动、命令调用和结构验证结果。"
    >
      <div className="sb-log-list">
        {logs.map((log) => (
          <p key={log}>{log}</p>
        ))}
      </div>
    </Panel>
  );
}
