import { createSceneSpawnMarkerBridgeDefinition } from "../bridge/graphBridgeMapping";
import type { GraphNodeDefinition } from "./graphDefinitions";
import type { GraphNodeContentProjection } from "../content/graphNodeContent";

function formatDelaySeconds(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? `${value.toFixed(1)}s` : "0.0s";
}

function formatBooleanLabel(value: unknown, truthyLabel: string, falsyLabel: string) {
  return value === true ? truthyLabel : falsyLabel;
}

function buildProjection(summaryText: string, detailLines: GraphNodeContentProjection["detailLines"]): GraphNodeContentProjection {
  return {
    summaryText,
    detailLines,
  };
}

export const defaultGraphNodeDefinitions: GraphNodeDefinition[] = [
  {
    typeId: "flow.start",
    displayName: "Start",
    category: "Flow",
    description: "图入口节点，负责启动整个场景蓝图流程。",
    ports: [
      {
        key: "next",
        name: "Next",
        direction: "output",
        kind: "control",
        capacity: "multiple",
      },
    ],
    defaultPayload: () => ({
      label: "Entry",
      autoStart: true,
    }),
    content: {
      description: "定义图入口节点的标签与启动行为。",
      sections: [
        {
          id: "general",
          title: "入口配置",
          fields: [
            {
              key: "label",
              label: "入口标签",
              kind: "text",
              placeholder: "例如：Main Entry",
              description: "用于给入口节点命名，便于在复杂图中快速识别。",
            },
            {
              key: "autoStart",
              label: "自动启动",
              kind: "boolean",
              description: "启用后，图加载完成时会直接从该入口开始执行。",
            },
            {
              key: "runtimeHint",
              label: "运行提示",
              kind: "readonly",
              description: "该字段由节点内容协议根据当前配置自动生成。",
              readValue: (payload) => {
                const label = typeof payload.label === "string" && payload.label.trim() ? payload.label.trim() : "Entry";
                return payload.autoStart === true
                  ? `进入时立即从 ${label} 启动。`
                  : `加载后等待外部命令触发 ${label}。`;
              },
            },
          ],
        },
      ],
      buildProjection(payload) {
        const label = typeof payload.label === "string" && payload.label.trim() ? payload.label.trim() : "Entry";
        return buildProjection(`入口：${label}`, [
          {
            key: "autoStart",
            label: "启动方式",
            value: payload.autoStart === true ? "自动" : "手动",
          },
        ]);
      },
    },
  },
  {
    typeId: "scene.spawn-marker",
    displayName: "Spawn Marker",
    category: "Scene",
    description: "在场景中引用一个空间标记，并驱动后续刷怪或触发逻辑。",
    ports: [
      {
        key: "in",
        name: "In",
        direction: "input",
        kind: "control",
      },
      {
        key: "marker",
        name: "Marker",
        direction: "input",
        kind: "data",
        dataType: "marker-ref",
      },
      {
        key: "completed",
        name: "Completed",
        direction: "output",
        kind: "control",
        capacity: "multiple",
      },
    ],
    defaultPayload: () => ({
      markerId: "marker_spawn_a",
      delaySeconds: 0,
      snapToGround: true,
      facingMode: "marker-forward",
    }),
    bridge: createSceneSpawnMarkerBridgeDefinition(),
    content: {
      description: "配置节点与 Scene Marker 的绑定方式，以及触发前的准备行为。",
      sections: [
        {
          id: "binding",
          title: "Marker 绑定",
          fields: [
            {
              key: "markerId",
              label: "Marker Id",
              kind: "text",
              placeholder: "marker_spawn_a",
              description: "后续会与 Scene Viewport 中的空间标记建立正式绑定。",
            },
            {
              key: "bindingState",
              label: "绑定状态",
              kind: "readonly",
              description: "当前仍为模拟状态，后续会改成真实的 Scene Marker 绑定结果。",
              readValue: (payload) => {
                const markerId = typeof payload.markerId === "string" && payload.markerId.trim()
                  ? payload.markerId.trim()
                  : "<未指定>";
                return `等待场景侧确认 Marker：${markerId}`;
              },
            },
          ],
        },
        {
          id: "spawn",
          title: "生成行为",
          fields: [
            {
              key: "delaySeconds",
              label: "延迟秒数",
              kind: "number",
              min: 0,
              step: 0.1,
              description: "进入该节点后等待多少秒再继续输出 Completed。",
            },
            {
              key: "snapToGround",
              label: "吸附地面",
              kind: "boolean",
              description: "启用后，会优先将白模预览落到地面高度。",
            },
            {
              key: "facingMode",
              label: "朝向模式",
              kind: "select",
              description: "控制生成对象进入场景后的默认朝向。",
              options: [
                { value: "marker-forward", label: "沿 Marker 正方向" },
                { value: "camera-facing", label: "朝向当前相机" },
                { value: "custom-rotation", label: "使用自定义旋转" },
              ],
            },
          ],
        },
      ],
      buildProjection(payload) {
        const markerId = typeof payload.markerId === "string" && payload.markerId.trim()
          ? payload.markerId.trim()
          : "未指定 Marker";
        const facingModeMap: Record<string, string> = {
          "marker-forward": "沿 Marker",
          "camera-facing": "朝向相机",
          "custom-rotation": "自定义旋转",
        };
        return buildProjection(`Marker：${markerId}`, [
          {
            key: "delaySeconds",
            label: "延迟",
            value: formatDelaySeconds(payload.delaySeconds),
          },
          {
            key: "snapToGround",
            label: "落地",
            value: formatBooleanLabel(payload.snapToGround, "吸附", "保持原高"),
          },
          {
            key: "facingMode",
            label: "朝向",
            value: facingModeMap[typeof payload.facingMode === "string" ? payload.facingMode : ""] ?? "沿 Marker",
          },
        ]);
      },
    },
  },
  {
    typeId: "flow.wait-signal",
    displayName: "Wait Signal",
    category: "Flow",
    description: "等待一个信号或条件，再继续向后执行。",
    ports: [
      {
        key: "in",
        name: "In",
        direction: "input",
        kind: "control",
      },
      {
        key: "signal",
        name: "Signal",
        direction: "input",
        kind: "data",
        dataType: "signal-tag",
      },
      {
        key: "out",
        name: "Out",
        direction: "output",
        kind: "control",
        capacity: "multiple",
      },
    ],
    defaultPayload: () => ({
      signalTag: "",
      timeoutSeconds: 0,
      blocking: true,
      resumeMode: "once",
    }),
    content: {
      description: "定义当前节点等待的信号标签，以及超时和恢复策略。",
      sections: [
        {
          id: "signal",
          title: "等待条件",
          fields: [
            {
              key: "signalTag",
              label: "Signal Tag",
              kind: "text",
              placeholder: "combat.cleared",
              description: "用于表达当前节点等待的业务信号标签。",
            },
            {
              key: "timeoutSeconds",
              label: "超时秒数",
              kind: "number",
              min: 0,
              step: 0.5,
              description: "为 0 表示一直等待，非 0 表示达到时间后自动放行。",
            },
            {
              key: "blocking",
              label: "阻塞执行",
              kind: "boolean",
              description: "启用后，后续控制流会在此节点上完全暂停。",
            },
            {
              key: "resumeMode",
              label: "恢复模式",
              kind: "select",
              description: "控制信号满足后，节点如何恢复后续流程。",
              options: [
                { value: "once", label: "仅恢复一次" },
                { value: "repeatable", label: "可重复恢复" },
              ],
            },
          ],
        },
      ],
      buildProjection(payload) {
        const signalTag = typeof payload.signalTag === "string" && payload.signalTag.trim()
          ? payload.signalTag.trim()
          : "等待任意信号";
        return buildProjection(`Signal：${signalTag}`, [
          {
            key: "timeoutSeconds",
            label: "超时",
            value: typeof payload.timeoutSeconds === "number" && Number.isFinite(payload.timeoutSeconds) && payload.timeoutSeconds > 0
              ? `${payload.timeoutSeconds.toFixed(1)}s`
              : "无限等待",
          },
          {
            key: "blocking",
            label: "行为",
            value: formatBooleanLabel(payload.blocking, "阻塞", "非阻塞"),
          },
          {
            key: "resumeMode",
            label: "恢复",
            value: payload.resumeMode === "repeatable" ? "可重复" : "单次",
          },
        ]);
      },
    },
  },
];
