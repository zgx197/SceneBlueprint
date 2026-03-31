import { useEffect, useMemo, useState } from "react";
import type { AppInfo, PingResult, WorkspaceGraphFileInfo } from "../../host/types/host";
import { Panel } from "../../shared/components/Panel";
import {
  buildGraphNodeContentPatch,
  formatGraphNodeContentDraftValue,
  type GraphNodeContentDraftValue,
  type GraphNodeContentFieldDefinition,
} from "../graph/content/graphNodeContent";
import type { WorkspaceSelectionTarget } from "../graph/binding/graphInspectorBinding";
import type { GraphRuntimeBridgeContract } from "../graph/runtime/graphWorkspaceBridge";
import type { GraphWorkspaceIssue } from "../graph/runtime/graphWorkspaceExport";

interface InspectorPanelProps {
  appInfo: AppInfo | null;
  pingResult: PingResult | null;
  selectionTarget: WorkspaceSelectionTarget;
  bridgeContract: GraphRuntimeBridgeContract;
  bridgeIssues: GraphWorkspaceIssue[];
  workspaceFileInfo?: WorkspaceGraphFileInfo | null;
  onPatchNodePayload: (nodeId: string, patch: Record<string, unknown>) => void;
  onPatchEdgePayload: (edgeId: string, patch: Record<string, unknown>) => void;
  onPatchGroup: (groupId: string, patch: Record<string, unknown>) => void;
  onPatchComment: (commentId: string, patch: Record<string, unknown>) => void;
  onPatchSubgraph: (subgraphId: string, patch: Record<string, unknown>) => void;
}

function NumberField(props: {
  label: string;
  value: number;
  onCommit: (value: number) => void;
  min?: number;
  max?: number;
  step?: number;
}) {
  const { label, value, onCommit, min, max, step = 1 } = props;
  const [draft, setDraft] = useState(String(value));

  useEffect(() => {
    setDraft(String(value));
  }, [value]);

  return (
    <label className="sb-inspector-field">
      <span className="sb-inspector-field-label">{label}</span>
      <input
        type="number"
        className="sb-inspector-input"
        value={draft}
        min={min}
        max={max}
        step={step}
        onChange={(event) => {
          setDraft(event.target.value);
        }}
        onBlur={() => {
          const nextValue = Number.parseFloat(draft);
          if (Number.isFinite(nextValue)) {
            onCommit(nextValue);
          } else {
            setDraft(String(value));
          }
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            const nextValue = Number.parseFloat(draft);
            if (Number.isFinite(nextValue)) {
              onCommit(nextValue);
            }
          }
        }}
      />
    </label>
  );
}

function TextField(props: {
  label: string;
  value: string;
  placeholder?: string;
  onCommit: (value: string) => void;
}) {
  const { label, value, placeholder, onCommit } = props;
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  return (
    <label className="sb-inspector-field">
      <span className="sb-inspector-field-label">{label}</span>
      <input
        type="text"
        className="sb-inspector-input"
        value={draft}
        placeholder={placeholder}
        onChange={(event) => {
          setDraft(event.target.value);
        }}
        onBlur={() => {
          onCommit(draft);
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            onCommit(draft);
          }
        }}
      />
    </label>
  );
}

function TextAreaField(props: {
  label: string;
  value: string;
  placeholder?: string;
  onCommit: (value: string) => void;
}) {
  const { label, value, placeholder, onCommit } = props;
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  return (
    <label className="sb-inspector-field">
      <span className="sb-inspector-field-label">{label}</span>
      <textarea
        className="sb-inspector-input sb-inspector-textarea"
        value={draft}
        placeholder={placeholder}
        rows={4}
        onChange={(event) => {
          setDraft(event.target.value);
        }}
        onBlur={() => {
          onCommit(draft);
        }}
      />
    </label>
  );
}

function SelectField(props: {
  label: string;
  value: string;
  options: Array<{ label: string; value: string }>;
  onCommit: (value: string) => void;
}) {
  const { label, value, options, onCommit } = props;
  return (
    <label className="sb-inspector-field">
      <span className="sb-inspector-field-label">{label}</span>
      <select
        className="sb-inspector-input"
        value={value}
        onChange={(event) => {
          onCommit(event.target.value);
        }}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

function NodeContentEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-node" }>;
  onPatchNodePayload: InspectorPanelProps["onPatchNodePayload"];
}) {
  const { selectionTarget, onPatchNodePayload } = props;
  const [draftValues, setDraftValues] = useState<Record<string, GraphNodeContentDraftValue>>({});

  const initialDraftValues = useMemo(() => {
    const nextValues: Record<string, GraphNodeContentDraftValue> = {};

    for (const section of selectionTarget.contentDefinition.sections) {
      for (const field of section.fields) {
        nextValues[field.key] = formatGraphNodeContentDraftValue(field, selectionTarget.payload);
      }
    }

    return nextValues;
  }, [selectionTarget.contentDefinition.sections, selectionTarget.payload]);

  useEffect(() => {
    setDraftValues(initialDraftValues);
  }, [initialDraftValues, selectionTarget.nodeId]);

  const commitField = (field: GraphNodeContentFieldDefinition) => {
    const draftValue = draftValues[field.key];
    const patch = buildGraphNodeContentPatch(
      field,
      draftValue ?? formatGraphNodeContentDraftValue(field, selectionTarget.payload),
    );

    if (!patch) {
      setDraftValues((current) => ({
        ...current,
        [field.key]: formatGraphNodeContentDraftValue(field, selectionTarget.payload),
      }));
      return;
    }

    onPatchNodePayload(selectionTarget.nodeId, patch);
  };

  return (
    <>
      {selectionTarget.contentDefinition.sections.map((section) => {
        return (
          <section key={section.id} className="sb-inspector-section">
            <div className="sb-inspector-section-title">{section.title}</div>
            <div className="sb-inspector-form">
              {section.fields.map((field) => {
                const value = draftValues[field.key] ?? formatGraphNodeContentDraftValue(field, selectionTarget.payload);

                return (
                  <label key={field.key} className="sb-inspector-field">
                    <span className="sb-inspector-field-label">{field.label}</span>
                    {field.kind === "number" ? (
                      <input
                        type="number"
                        className="sb-inspector-input"
                        value={typeof value === "string" ? value : "0"}
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
                    ) : field.kind === "boolean" ? (
                      <label className="sb-inspector-toggle">
                        <input
                          type="checkbox"
                          checked={value === true}
                          onChange={(event) => {
                            const checked = event.target.checked;
                            setDraftValues((current) => ({
                              ...current,
                              [field.key]: checked,
                            }));
                            onPatchNodePayload(selectionTarget.nodeId, { [field.key]: checked });
                          }}
                        />
                        <span>{value === true ? "已启用" : "未启用"}</span>
                      </label>
                    ) : field.kind === "select" ? (
                      <select
                        className="sb-inspector-input"
                        value={typeof value === "string" ? value : field.options[0]?.value ?? ""}
                        onChange={(event) => {
                          const nextValue = event.target.value;
                          setDraftValues((current) => ({
                            ...current,
                            [field.key]: nextValue,
                          }));
                          onPatchNodePayload(selectionTarget.nodeId, { [field.key]: nextValue });
                        }}
                      >
                        {field.options.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    ) : field.kind === "readonly" ? (
                      <div className="sb-inspector-readonly">{typeof value === "string" ? value : ""}</div>
                    ) : (
                      <input
                        type="text"
                        className="sb-inspector-input"
                        value={typeof value === "string" ? value : ""}
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
      })}
    </>
  );
}

function GroupEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-group" }>;
  onPatchGroup: InspectorPanelProps["onPatchGroup"];
}) {
  const { selectionTarget, onPatchGroup } = props;
  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">分组编辑</div>
      <div className="sb-inspector-form">
        <TextField label="标题" value={selectionTarget.group.title} onCommit={(value) => onPatchGroup(selectionTarget.groupId, { title: value })} />
        <TextField
          label="颜色"
          value={selectionTarget.group.color ?? ""}
          placeholder="rgba(175, 144, 96, 0.16)"
          onCommit={(value) => onPatchGroup(selectionTarget.groupId, { color: value || undefined })}
        />
        <NumberField
          label="Padding"
          value={selectionTarget.group.padding ?? 28}
          min={8}
          max={160}
          step={1}
          onCommit={(value) => onPatchGroup(selectionTarget.groupId, { padding: value })}
        />
      </div>
    </section>
  );
}

function CommentEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-comment" }>;
  onPatchComment: InspectorPanelProps["onPatchComment"];
}) {
  const { selectionTarget, onPatchComment } = props;
  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">注释编辑</div>
      <div className="sb-inspector-form">
        <TextAreaField label="内容" value={selectionTarget.comment.text} onCommit={(value) => onPatchComment(selectionTarget.commentId, { text: value })} />
        <SelectField
          label="语气"
          value={selectionTarget.comment.tone ?? "info"}
          options={[
            { label: "Neutral", value: "neutral" },
            { label: "Info", value: "info" },
            { label: "Success", value: "success" },
            { label: "Warning", value: "warning" },
            { label: "Danger", value: "danger" },
          ]}
          onCommit={(value) => onPatchComment(selectionTarget.commentId, { tone: value })}
        />
        <div className="sb-inspector-grid-2">
          <NumberField label="X" value={selectionTarget.comment.position.x} onCommit={(value) => onPatchComment(selectionTarget.commentId, { position: { ...selectionTarget.comment.position, x: value } })} />
          <NumberField label="Y" value={selectionTarget.comment.position.y} onCommit={(value) => onPatchComment(selectionTarget.commentId, { position: { ...selectionTarget.comment.position, y: value } })} />
          <NumberField label="Width" value={selectionTarget.comment.size.width} min={120} onCommit={(value) => onPatchComment(selectionTarget.commentId, { size: { ...selectionTarget.comment.size, width: value } })} />
          <NumberField label="Height" value={selectionTarget.comment.size.height} min={80} onCommit={(value) => onPatchComment(selectionTarget.commentId, { size: { ...selectionTarget.comment.size, height: value } })} />
        </div>
      </div>
    </section>
  );
}

function SubgraphEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-subgraph" }>;
  onPatchSubgraph: InspectorPanelProps["onPatchSubgraph"];
}) {
  const { selectionTarget, onPatchSubgraph } = props;
  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">子图编辑</div>
      <div className="sb-inspector-form">
        <TextField label="标题" value={selectionTarget.subgraph.title} onCommit={(value) => onPatchSubgraph(selectionTarget.subgraphId, { title: value })} />
        <TextField
          label="颜色"
          value={selectionTarget.subgraph.color ?? ""}
          placeholder="rgba(119, 143, 199, 0.18)"
          onCommit={(value) => onPatchSubgraph(selectionTarget.subgraphId, { color: value || undefined })}
        />
        <TextField
          label="入口节点"
          value={selectionTarget.subgraph.entryNodeId ?? ""}
          placeholder="node-id"
          onCommit={(value) => onPatchSubgraph(selectionTarget.subgraphId, { entryNodeId: value || undefined })}
        />
        <TextAreaField
          label="说明"
          value={selectionTarget.subgraph.description ?? ""}
          placeholder="描述子图职责"
          onCommit={(value) => onPatchSubgraph(selectionTarget.subgraphId, { description: value || undefined })}
        />
      </div>
    </section>
  );
}

function EdgeEditor(props: {
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-edge" }>;
  onPatchEdgePayload: InspectorPanelProps["onPatchEdgePayload"];
}) {
  const { selectionTarget, onPatchEdgePayload } = props;
  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">连线编辑</div>
      <div className="sb-inspector-form">
        <TextField label="标签" value={selectionTarget.label} onCommit={(value) => onPatchEdgePayload(selectionTarget.edgeId, { label: value })} />
        <SelectField
          label="诊断语气"
          value={selectionTarget.diagnosticTone ?? "none"}
          options={[
            { label: "无", value: "none" },
            { label: "Info", value: "info" },
            { label: "Warning", value: "warning" },
            { label: "Error", value: "error" },
          ]}
          onCommit={(value) => onPatchEdgePayload(selectionTarget.edgeId, { diagnosticTone: value === "none" ? undefined : value })}
        />
      </div>
    </section>
  );
}

function collectBridgeIssueMessages(
  bridgeContract: GraphRuntimeBridgeContract,
  bridgeIssues: GraphWorkspaceIssue[],
  markerId: string,
  sceneBindingId: string,
): string[] {
  return bridgeIssues
    .filter((issue) => {
      if (issue.location.entityKind === "marker" && issue.location.entityId === markerId) {
        return true;
      }

      if (issue.location.entityKind === "scene" && issue.location.entityId === sceneBindingId) {
        return true;
      }

      return issue.location.entityKind === "project" && issue.location.entityId === bridgeContract.project.graphId;
    })
    .map((issue) => issue.message);
}

function renderNodeBridgeSummary(
  selectionTarget: Extract<WorkspaceSelectionTarget, { kind: "graph-node" }>,
  bridgeContract: GraphRuntimeBridgeContract,
  bridgeIssues: GraphWorkspaceIssue[],
) {
  const markers = bridgeContract.markers.filter((marker) => marker.nodeId === selectionTarget.nodeId);
  if (markers.length === 0) {
    return null;
  }

  return (
    <section className="sb-inspector-section">
      <div className="sb-inspector-section-title">Bridge 摘要</div>
      {markers.map((marker) => {
        const scene = bridgeContract.scenes.find((entry) => entry.id === marker.sceneBindingId) ?? null;
        const issueMessages = collectBridgeIssueMessages(bridgeContract, bridgeIssues, marker.id, marker.sceneBindingId);

        return (
          <div key={marker.id} className="sb-inspector-summary-lines">
            <div className="sb-inspector-summary-line">
              <span>Bridge Marker</span>
              <strong>{marker.requestedMarkerId ?? "<未指定>"}</strong>
            </div>
            <div className="sb-inspector-summary-line">
              <span>Project / Scene</span>
              <strong>{marker.projectId ?? "<未指定>"} / {scene?.requestedSceneId ?? "<未指定>"}</strong>
            </div>
            <div className="sb-inspector-summary-line">
              <span>Binding</span>
              <strong>{marker.bindingState}</strong>
            </div>
            <div className="sb-inspector-summary-line">
              <span>Ports</span>
              <strong>{marker.inputPortId ?? "<缺失>"} / {marker.markerPortId ?? "<缺失>"} / {marker.completedPortId ?? "<缺失>"}</strong>
            </div>
            <div className="sb-inspector-summary-line">
              <span>Issues</span>
              <strong>{issueMessages.length > 0 ? issueMessages.length : "0"}</strong>
            </div>
          </div>
        );
      })}
    </section>
  );
}
function renderSelectionSummary(
  selectionTarget: WorkspaceSelectionTarget,
  bridgeContract: GraphRuntimeBridgeContract,
  bridgeIssues: GraphWorkspaceIssue[],
  actions: Pick<
    InspectorPanelProps,
    "onPatchNodePayload" | "onPatchEdgePayload" | "onPatchGroup" | "onPatchComment" | "onPatchSubgraph"
  >,
) {
  switch (selectionTarget.kind) {
    case "graph-node": {
      const { node, displayName, category, description, payload, contentProjection } = selectionTarget;
      return (
        <>
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">节点概览</div>
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
              <dt>节点说明</dt>
              <dd>{description ?? "当前节点暂未提供说明。"}</dd>
              <dt>内容摘要</dt>
              <dd>{contentProjection.summaryText ?? "当前节点暂未提供内容摘要。"}</dd>
            </dl>
            {contentProjection.detailLines.length > 0 ? (
              <div className="sb-inspector-summary-lines">
                {contentProjection.detailLines.map((line) => (
                  <div key={line.key} className="sb-inspector-summary-line">
                    <span>{line.label}</span>
                    <strong>{line.value}</strong>
                  </div>
                ))}
              </div>
            ) : null}
          </section>
          {renderNodeBridgeSummary(selectionTarget, bridgeContract, bridgeIssues)}
          <NodeContentEditor selectionTarget={selectionTarget} onPatchNodePayload={actions.onPatchNodePayload} />
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">原始 Payload</div>
            <pre className="sb-inspector-payload-preview">{JSON.stringify(payload, null, 2)}</pre>
          </section>
        </>
      );
    }

    case "graph-edge": {
      const { edge, sourceNodeTitle, targetNodeTitle, payload } = selectionTarget;
      return (
        <>
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">连线概览</div>
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
            </dl>
          </section>
          <EdgeEditor selectionTarget={selectionTarget} onPatchEdgePayload={actions.onPatchEdgePayload} />
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">原始 Payload</div>
            <pre className="sb-inspector-payload-preview">{JSON.stringify(payload, null, 2)}</pre>
          </section>
        </>
      );
    }

    case "graph-group": {
      return (
        <>
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">分组概览</div>
            <dl className="sb-kv">
              <dt>分组 Id</dt>
              <dd>{selectionTarget.groupId}</dd>
              <dt>成员数</dt>
              <dd>{selectionTarget.group.nodeIds.length}</dd>
              <dt>成员</dt>
              <dd>{selectionTarget.memberDisplayNames.join(", ") || "空"}</dd>
            </dl>
          </section>
          <GroupEditor selectionTarget={selectionTarget} onPatchGroup={actions.onPatchGroup} />
        </>
      );
    }

    case "graph-comment": {
      return (
        <>
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">注释概览</div>
            <dl className="sb-kv">
              <dt>注释 Id</dt>
              <dd>{selectionTarget.commentId}</dd>
              <dt>位置</dt>
              <dd>
                ({selectionTarget.comment.position.x}, {selectionTarget.comment.position.y})
              </dd>
              <dt>尺寸</dt>
              <dd>
                {selectionTarget.comment.size.width} x {selectionTarget.comment.size.height}
              </dd>
            </dl>
          </section>
          <CommentEditor selectionTarget={selectionTarget} onPatchComment={actions.onPatchComment} />
        </>
      );
    }

    case "graph-subgraph": {
      return (
        <>
          <section className="sb-inspector-section">
            <div className="sb-inspector-section-title">子图概览</div>
            <dl className="sb-kv">
              <dt>子图 Id</dt>
              <dd>{selectionTarget.subgraphId}</dd>
              <dt>成员数</dt>
              <dd>{selectionTarget.subgraph.nodeIds.length}</dd>
              <dt>入口</dt>
              <dd>{selectionTarget.entryNodeDisplayName ?? "未设置"}</dd>
              <dt>成员</dt>
              <dd>{selectionTarget.memberDisplayNames.join(", ") || "空"}</dd>
            </dl>
          </section>
          <SubgraphEditor selectionTarget={selectionTarget} onPatchSubgraph={actions.onPatchSubgraph} />
        </>
      );
    }

    case "scene-marker": {
      return (
        <section className="sb-inspector-section">
          <div className="sb-inspector-section-title">Scene Marker Bridge</div>
          <dl className="sb-kv">
            <dt>Bridge Id</dt>
            <dd>{selectionTarget.markerId}</dd>
            <dt>Graph Node</dt>
            <dd>{selectionTarget.graphNodeDisplayName}</dd>
            <dt>Requested Marker</dt>
            <dd>{selectionTarget.marker.requestedMarkerId ?? "<未指定>"}</dd>
            <dt>Project</dt>
            <dd>{selectionTarget.marker.projectId ?? "<未指定>"}</dd>
            <dt>Scene</dt>
            <dd>{selectionTarget.scene?.requestedSceneId ?? "<未指定>"}</dd>
            <dt>Binding</dt>
            <dd>{selectionTarget.marker.bindingState}</dd>
            <dt>Delay</dt>
            <dd>{selectionTarget.marker.delaySeconds.toFixed(1)}s</dd>
            <dt>Snap</dt>
            <dd>{selectionTarget.marker.snapToGround ? "开启" : "关闭"}</dd>
            <dt>Facing</dt>
            <dd>{selectionTarget.marker.facingMode}</dd>
            <dt>Ports</dt>
            <dd>{selectionTarget.marker.inputPortId ?? "<缺失>"} / {selectionTarget.marker.markerPortId ?? "<缺失>"} / {selectionTarget.marker.completedPortId ?? "<缺失>"}</dd>
          </dl>
          {selectionTarget.issueMessages.length > 0 ? (
            <div className="sb-inspector-summary-lines">
              {selectionTarget.issueMessages.map((message, index) => (
                <div key={`${selectionTarget.markerId}:${index}`} className="sb-inspector-summary-line">
                  <span>Issue</span>
                  <strong>{message}</strong>
                </div>
              ))}
            </div>
          ) : (
            <div className="sb-placeholder">
              <p>当前 Scene Marker 已进入正式 bridge object 主路径。</p>
              <p>暂未发现该 Marker 的 bridge warning / blocking issue。</p>
            </div>
          )}
        </section>
      );
    }

    default:
      return (
        <div className="sb-placeholder">
          <p>当前还没有工作区对象被选中。</p>
          <p>Graph Node、Scene Marker、Graph Edge、Group、Comment、Subgraph 都会在这里进入统一编辑。</p>
        </div>
      );
  }
}

export function InspectorPanel(props: InspectorPanelProps) {
  const {
    appInfo,
    pingResult,
    selectionTarget,
    bridgeContract,
    bridgeIssues,
    workspaceFileInfo,
    onPatchNodePayload,
    onPatchEdgePayload,
    onPatchGroup,
    onPatchComment,
    onPatchSubgraph,
  } = props;

  return (
    <Panel title="Inspector" description="当前选择对象详情" bodyClassName="sb-inspector-panel-body">
      {renderSelectionSummary(selectionTarget, bridgeContract, bridgeIssues, {
        onPatchNodePayload,
        onPatchEdgePayload,
        onPatchGroup,
        onPatchComment,
        onPatchSubgraph,
      })}

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

