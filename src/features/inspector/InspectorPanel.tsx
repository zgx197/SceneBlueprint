import { useEffect, useMemo, useState } from "react";
import type { AppInfo, PingResult, WorkspaceGraphFileInfo } from "../../host/types/host";
import { Panel } from "../../shared/components/Panel";
import type {
  GraphNodeInspectorFieldDefinition,
} from "../graph/definitions/graphDefinitions";
import type { WorkspaceSelectionTarget } from "../graph/binding/graphInspectorBinding";

interface InspectorPanelProps {
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
  selectionTarget: WorkspaceSelectionTarget;
  workspaceFileInfo?: WorkspaceGraphFileInfo | null;
  onPatchNodePayload: (nodeId: string, patch: Record<string, unknown>) => void;
}

function formatFieldValue(field: GraphNodeInspectorFieldDefinition, value: unknown) {
  if (field.kind === "number") {
    return typeof value === "number" && Number.isFinite(value) ? String(value) : "0";
  }

  return typeof value === "string" ? value : "";
}

function NodePayloadEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-node" }>;
  onPatchNodePayload: InspectorPanelProps["onPatchNodePayload"];
}) {
  const { selectionTarget, onPatchNodePayload } = props;
  const schema = selectionTarget.inspectorSchema;
  const [draftValues, setDraftValues] = useState<Record<string, string>>({});

  const initialDraftValues = useMemo(() => {
    const nextValues: Record<string, string> = {};

    for (const field of schema?.fields ?? []) {
      nextValues[field.key] = formatFieldValue(field, selectionTarget.payload[field.key]);
    }

    return nextValues;
  }, [schema?.fields, selectionTarget.payload]);

  useEffect(() => {
    setDraftValues(initialDraftValues);
  }, [initialDraftValues, selectionTarget.nodeId]);

  if (!schema || schema.fields.length === 0) {
    return null;
  }

  const commitField = (field: GraphNodeInspectorFieldDefinition) => {
    const rawValue = draftValues[field.key] ?? "";

    if (field.kind === "number") {
      const nextValue = Number(rawValue);
      if (!Number.isFinite(nextValue)) {
        setDraftValues((current) => ({
          ...current,
          [field.key]: formatFieldValue(field, selectionTarget.payload[field.key]),
        }));
        return;
      }

      onPatchNodePayload(selectionTarget.nodeId, {
        [field.key]: nextValue,
      });
      return;
    }

    onPatchNodePayload(selectionTarget.nodeId, {
      [field.key]: rawValue,
    });
  };

  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">节点属性</div>
      <div className="sb-inspector-form">
        {schema.fields.map((field) => {
          const value = draftValues[field.key] ?? "";

          return (
            <label key={field.key} className="sb-inspector-field">
              <span className="sb-inspector-field-label">{field.label}</span>
              {field.kind === "number" ? (
                <input
                  type="number"
                  className="sb-inspector-input"
                  value={value}
                  min={field.min}
                  max={field.max}
                  step={field.step ?? 1}
                  onChange={(event) => {
                    const nextValue = event.target.value;
                    setDraftValues((current) => ({
                      ...current,
                      [field.key]: nextValue,
                    }));
                  }}
                  onBlur={() => {
                    commitField(field);
                  }}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      commitField(field);
                    }
                  }}
                />
              ) : (
                <input
                  type="text"
                  className="sb-inspector-input"
                  value={value}
                  placeholder={field.placeholder}
                  onChange={(event) => {
                    const nextValue = event.target.value;
                    setDraftValues((current) => ({
                      ...current,
                      [field.key]: nextValue,
                    }));
                  }}
                  onBlur={() => {
                    commitField(field);
                  }}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      commitField(field);
                    }
                  }}
                />
              )}
              {field.description ? <span className="sb-inspector-field-description">{field.description}</span> : null}
            </label>
          );
        })}
      </div>
    </section>
  );
}

function renderSelectionSummary(
  selectionTarget: WorkspaceSelectionTarget,
  onPatchNodePayload: InspectorPanelProps["onPatchNodePayload"],
) {
  switch (selectionTarget.kind) {
    case "graph-node": {
      const { node, displayName, category, summary, payload } = selectionTarget;
      return (
        <>
          <dl className="sb-kv">
            <dt>选择对象</dt>
            <dd>Graph Node</dd>
            <dt>显示名</dt>
            <dd>{displayName}</dd>
            <dt>节点 Id</dt>
            <dd>{node.id}</dd>
            <dt>类型</dt>
            <dd>{node.typeId}</dd>
            <dt>分类</dt>
            <dd>{category ?? "未分类"}</dd>
            <dt>位置</dt>
            <dd>
              ({node.position.x}, {node.position.y})
            </dd>
            <dt>端口</dt>
            <dd>{node.ports.map((port) => port.key).join(", ")}</dd>
            <dt>摘要</dt>
            <dd>{summary ?? "当前节点暂未提供摘要。"}</dd>
            <dt>Payload</dt>
            <dd>{JSON.stringify(payload)}</dd>
          </dl>
          <NodePayloadEditor selectionTarget={selectionTarget} onPatchNodePayload={onPatchNodePayload} />
        </>
      );
    }

    case "graph-edge": {
      const { edge, sourceNodeTitle, targetNodeTitle } = selectionTarget;
      return (
        <dl className="sb-kv">
          <dt>选择对象</dt>
          <dd>Graph Edge</dd>
          <dt>边 Id</dt>
          <dd>{edge.id}</dd>
          <dt>源节点</dt>
          <dd>{sourceNodeTitle}</dd>
          <dt>目标节点</dt>
          <dd>{targetNodeTitle}</dd>
          <dt>源端口</dt>
          <dd>{edge.sourcePortId}</dd>
          <dt>目标端口</dt>
          <dd>{edge.targetPortId}</dd>
          <dt>当前职责</dt>
          <dd>这里将继续承接连线规则、条件和调试信息。</dd>
        </dl>
      );
    }

    default:
      return (
        <div className="sb-placeholder">
          <p>当前还没有工作区对象被选中。</p>
          <p>后续 Graph Node、Graph Edge、Scene Marker、Scene Object 都会汇入这里。</p>
        </div>
      );
  }
}

export function InspectorPanel(props: InspectorPanelProps) {
  const { appInfo, pingResult, selectionTarget, workspaceFileInfo, onPatchNodePayload } = props;

  return (
    <Panel title="Inspector" description="当前选择对象详情" bodyClassName="sb-inspector-panel-body">
      {renderSelectionSummary(selectionTarget, onPatchNodePayload)}

      <section className="sb-inspector-section">
        <div className="sb-inspector-section-title">宿主状态</div>
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
          <dt>统一选择态</dt>
          <dd>{selectionTarget.kind}</dd>
          <dt>工作区文件</dt>
          <dd>{workspaceFileInfo?.path ?? "未建立"}</dd>
          <dt>文件后端</dt>
          <dd>{workspaceFileInfo?.backend ?? "unknown"}</dd>
        </dl>
      </section>
    </Panel>
  );
}
