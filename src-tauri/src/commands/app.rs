use serde::Serialize;

use crate::services::app_service;

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
