export const appCommandIds = {
  fileNewProject: "file.new-project",
  fileOpenProject: "file.open-project",
  fileSaveProject: "file.save-project",
  fileExportRuntimeContract: "file.export-runtime-contract",
  fileExit: "file.exit",
  editUndo: "edit.undo",
  editRedo: "edit.redo",
  viewResetLayout: "view.reset-layout",
  toolsProjectSettings: "tools.project-settings",
  toolsPreferences: "tools.preferences",
  developPingHost: "develop.ping-host",
  developPrintLogPath: "develop.print-log-path",
  helpAbout: "help.about",
} as const;

export type AppCommandId = (typeof appCommandIds)[keyof typeof appCommandIds];
export type AppCommandGroup = "file" | "edit" | "view" | "tools" | "develop" | "help";

export interface AppCommandDefinition {
  id: AppCommandId;
  label: string;
  description: string;
  group: AppCommandGroup;
}

export interface NativeMenuCommandPayload {
  commandId: string;
}

export const NATIVE_MENU_COMMAND_EVENT = "sceneblueprint://menu-command";

export const appCommandDefinitions: Record<AppCommandId, AppCommandDefinition> = {
  [appCommandIds.fileNewProject]: {
    id: appCommandIds.fileNewProject,
    label: "新建项目",
    description: "创建新的 SceneBlueprint 项目。",
    group: "file",
  },
  [appCommandIds.fileOpenProject]: {
    id: appCommandIds.fileOpenProject,
    label: "打开项目",
    description: "打开本地 SceneBlueprint 项目。",
    group: "file",
  },
  [appCommandIds.fileSaveProject]: {
    id: appCommandIds.fileSaveProject,
    label: "保存项目",
    description: "保存当前项目内容。",
    group: "file",
  },
  [appCommandIds.fileExportRuntimeContract]: {
    id: appCommandIds.fileExportRuntimeContract,
    label: "导出 Runtime Contract",
    description: "将当前 Graph 编译并导出为 runtime contract 文件。",
    group: "file",
  },
  [appCommandIds.fileExit]: {
    id: appCommandIds.fileExit,
    label: "退出",
    description: "退出当前应用。",
    group: "file",
  },
  [appCommandIds.editUndo]: {
    id: appCommandIds.editUndo,
    label: "撤销",
    description: "撤销最近一次编辑操作。",
    group: "edit",
  },
  [appCommandIds.editRedo]: {
    id: appCommandIds.editRedo,
    label: "重做",
    description: "恢复最近一次被撤销的编辑操作。",
    group: "edit",
  },
  [appCommandIds.viewResetLayout]: {
    id: appCommandIds.viewResetLayout,
    label: "重置布局",
    description: "清除当前工作台分栏布局缓存。",
    group: "view",
  },
  [appCommandIds.toolsProjectSettings]: {
    id: appCommandIds.toolsProjectSettings,
    label: "项目设置",
    description: "打开当前项目设置入口。",
    group: "tools",
  },
  [appCommandIds.toolsPreferences]: {
    id: appCommandIds.toolsPreferences,
    label: "偏好设置",
    description: "打开全局偏好设置。",
    group: "tools",
  },
  [appCommandIds.developPingHost]: {
    id: appCommandIds.developPingHost,
    label: "验证宿主通信",
    description: "验证前端与 Rust 宿主之间的通信链路。",
    group: "develop",
  },
  [appCommandIds.developPrintLogPath]: {
    id: appCommandIds.developPrintLogPath,
    label: "输出日志路径",
    description: "将当前日志文件路径输出到运行日志。",
    group: "develop",
  },
  [appCommandIds.helpAbout]: {
    id: appCommandIds.helpAbout,
    label: "关于 SceneBlueprint",
    description: "显示当前应用的基础信息。",
    group: "help",
  },
};

export function getAppCommandDefinition(commandId: string) {
  return Object.values(appCommandDefinitions).find((definition) => definition.id === commandId) ?? null;
}
