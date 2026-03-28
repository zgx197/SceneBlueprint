import type { AppInfo, PingResult } from "../../host/types/host";
import { Panel } from "../../shared/components/Panel";

interface InspectorPanelProps {
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
}

export function InspectorPanel(props: InspectorPanelProps) {
  const { appInfo, pingResult } = props;

  return (
    <Panel
      title="Inspector 区域"
      description="第一阶段先用来观察宿主状态与前后端通信结果。"
    >
      <dl className="sb-kv">
        <dt>应用</dt>
        <dd>{appInfo?.name ?? "未读取"}</dd>
        <dt>版本</dt>
        <dd>{appInfo?.version ?? "未读取"}</dd>
        <dt>平台</dt>
        <dd>{appInfo?.platform ?? "未读取"}</dd>
        <dt>运行时</dt>
        <dd>{appInfo?.runtime ?? "未读取"}</dd>
        <dt>通信结果</dt>
        <dd>{pingResult?.message ?? "尚未验证"}</dd>
      </dl>
    </Panel>
  );
}
