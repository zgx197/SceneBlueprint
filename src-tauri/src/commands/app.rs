use serde::{Deserialize, Serialize};
use tauri::AppHandle;

use crate::services::{app_service, log_service};

#[derive(Serialize)]
pub struct AppInfoDto {
    name: String,
    version: String,
    runtime: String,
    platform: String,
}

#[derive(Serialize)]
pub struct PingResultDto {
    message: String,
    timestamp: String,
}

#[derive(Deserialize)]
pub struct LogEntryDto {
    timestamp: String,
    level: String,
    scope: String,
    message: String,
}

#[tauri::command]
pub fn get_app_info() -> AppInfoDto {
    AppInfoDto {
        name: "SceneBlueprint".into(),
        version: env!("CARGO_PKG_VERSION").into(),
        runtime: "Tauri".into(),
        platform: app_service::read_platform(),
    }
}

#[tauri::command]
pub fn ping_host() -> PingResultDto {
    PingResultDto {
        message: "Rust 宿主通信正常。".into(),
        timestamp: app_service::read_timestamp(),
    }
}

#[tauri::command]
pub fn write_log_entry(app: AppHandle, entry: LogEntryDto) -> Result<(), String> {
    log_service::append_log_entry(
        &app,
        &entry.timestamp,
        &entry.level,
        &entry.scope,
        &entry.message,
    )
    .map(|_| ())
}

#[tauri::command]
pub fn get_log_file_path(app: AppHandle) -> Result<String, String> {
    log_service::resolve_log_file_path(&app).map(|path| path.display().to_string())
}
