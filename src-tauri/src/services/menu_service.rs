use serde::Serialize;
use tauri::{
    menu::{Menu, MenuBuilder, MenuItemBuilder, SubmenuBuilder},
    AppHandle, Emitter, Manager, Runtime,
};

use crate::services::{app_service, log_service};

pub const NATIVE_MENU_COMMAND_EVENT: &str = "sceneblueprint://menu-command";
pub const COMMAND_FILE_NEW_PROJECT: &str = "file.new-project";
pub const COMMAND_FILE_OPEN_PROJECT: &str = "file.open-project";
pub const COMMAND_FILE_SAVE_PROJECT: &str = "file.save-project";
pub const COMMAND_FILE_EXPORT_RUNTIME_CONTRACT: &str = "file.export-runtime-contract";
pub const COMMAND_FILE_EXIT: &str = "file.exit";
pub const COMMAND_EDIT_UNDO: &str = "edit.undo";
pub const COMMAND_EDIT_REDO: &str = "edit.redo";
pub const COMMAND_VIEW_RESET_LAYOUT: &str = "view.reset-layout";
pub const COMMAND_TOOLS_PROJECT_SETTINGS: &str = "tools.project-settings";
pub const COMMAND_TOOLS_PREFERENCES: &str = "tools.preferences";
pub const COMMAND_DEVELOP_PING_HOST: &str = "develop.ping-host";
pub const COMMAND_DEVELOP_PRINT_LOG_PATH: &str = "develop.print-log-path";
pub const COMMAND_HELP_ABOUT: &str = "help.about";

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NativeMenuCommandPayload {
    pub command_id: String,
}

fn write_menu_log<R: Runtime>(app: &AppHandle<R>, message: &str) {
    let _ = log_service::append_log_entry(
        app,
        &app_service::read_timestamp(),
        "info",
        "menu",
        message,
    );
}

fn unavailable_text(text: &str) -> String {
    format!("{text} [不可用]")
}

fn unavailable_menu_item<R: Runtime, M: Manager<R>>(
    manager: &M,
    id: &str,
    text: &str,
) -> tauri::Result<tauri::menu::MenuItem<R>> {
    MenuItemBuilder::with_id(id, unavailable_text(text))
        .enabled(false)
        .build(manager)
}

pub fn build_app_menu<R: Runtime, M: Manager<R>>(manager: &M) -> tauri::Result<Menu<R>> {
    let file_new_project = unavailable_menu_item(manager, COMMAND_FILE_NEW_PROJECT, "新建项目")?;
    let file_open_project = unavailable_menu_item(manager, COMMAND_FILE_OPEN_PROJECT, "打开项目")?;
    let file_save_project = MenuItemBuilder::with_id(COMMAND_FILE_SAVE_PROJECT, "保存项目").build(manager)?;
    let file_export_runtime_contract =
        MenuItemBuilder::with_id(COMMAND_FILE_EXPORT_RUNTIME_CONTRACT, "导出 Runtime Contract").build(manager)?;
    let edit_undo = MenuItemBuilder::with_id(COMMAND_EDIT_UNDO, "撤销").build(manager)?;
    let edit_redo = MenuItemBuilder::with_id(COMMAND_EDIT_REDO, "重做").build(manager)?;
    let tools_project_settings =
        unavailable_menu_item(manager, COMMAND_TOOLS_PROJECT_SETTINGS, "项目设置")?;
    let tools_preferences =
        unavailable_menu_item(manager, COMMAND_TOOLS_PREFERENCES, "偏好设置")?;

    let file_menu = SubmenuBuilder::new(manager, "文件")
        .item(&file_new_project)
        .item(&file_open_project)
        .separator()
        .item(&file_save_project)
        .item(&file_export_runtime_contract)
        .separator()
        .text(COMMAND_FILE_EXIT, "退出")
        .build()?;

    let edit_menu = SubmenuBuilder::new(manager, "编辑")
        .item(&edit_undo)
        .item(&edit_redo)
        .build()?;

    let view_menu = SubmenuBuilder::new(manager, "视图")
        .text(COMMAND_VIEW_RESET_LAYOUT, "重置布局")
        .build()?;

    let tools_menu = SubmenuBuilder::new(manager, "工具")
        .item(&tools_project_settings)
        .item(&tools_preferences)
        .build()?;

    let develop_menu = SubmenuBuilder::new(manager, "开发")
        .text(COMMAND_DEVELOP_PING_HOST, "验证宿主通信")
        .text(COMMAND_DEVELOP_PRINT_LOG_PATH, "输出日志路径")
        .build()?;

    let help_menu = SubmenuBuilder::new(manager, "帮助")
        .text(COMMAND_HELP_ABOUT, "关于 SceneBlueprint")
        .build()?;

    MenuBuilder::new(manager)
        .item(&file_menu)
        .item(&edit_menu)
        .item(&view_menu)
        .item(&tools_menu)
        .item(&develop_menu)
        .item(&help_menu)
        .build()
}

pub fn handle_menu_event<R: Runtime>(app: &AppHandle<R>, event_id: &str) {
    write_menu_log(app, &format!("收到原生菜单命令：{event_id}"));

    if event_id == COMMAND_FILE_EXIT {
        write_menu_log(app, "原生菜单请求退出应用。");
        app.exit(0);
        return;
    }

    let _ = app.emit(
        NATIVE_MENU_COMMAND_EVENT,
        NativeMenuCommandPayload {
            command_id: event_id.to_string(),
        },
    );
}
