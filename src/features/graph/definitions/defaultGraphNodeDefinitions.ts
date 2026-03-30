import type { GraphNodeDefinition } from "./graphDefinitions";

export const defaultGraphNodeDefinitions: GraphNodeDefinition[] = [
  {
    typeId: "flow.start",
    displayName: "Start",
    category: "Flow",
    summary: "图入口节点，负责启动整个场景蓝图流程。",
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
    }),
    inspector: {
      fields: [
        {
          key: "label",
          label: "入口标签",
          kind: "text",
          placeholder: "例如：Main Entry",
          description: "用于给入口节点命名，便于在复杂图中快速识别。",
        },
      ],
    },
  },
  {
    typeId: "scene.spawn-marker",
    displayName: "Spawn Marker",
    category: "Scene",
    summary: "在场景中引用一个空间标记，并驱动后续刷怪或触发逻辑。",
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
    }),
    inspector: {
      fields: [
        {
          key: "markerId",
          label: "Marker Id",
          kind: "text",
          placeholder: "marker_spawn_a",
          description: "后续会与 Scene Viewport 中的空间标记建立正式绑定。",
        },
        {
          key: "delaySeconds",
          label: "延迟秒数",
          kind: "number",
          min: 0,
          step: 0.1,
          description: "进入该节点后等待多少秒再继续输出 Completed。",
        },
      ],
    },
  },
  {
    typeId: "flow.wait-signal",
    displayName: "Wait Signal",
    category: "Flow",
    summary: "等待一个信号或条件，再继续向后执行。",
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
    }),
    inspector: {
      fields: [
        {
          key: "signalTag",
          label: "Signal Tag",
          kind: "text",
          placeholder: "combat.cleared",
          description: "用于表达当前节点等待的业务信号标签。",
        },
      ],
    },
  },
];
