use serde::{Deserialize, Serialize};
use tauri::AppHandle;

use crate::services::{app_service, log_service, workspace_service};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkspaceGraphFileInfoDto {
    path: String,
    exists: bool,
    backend: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReadWorkspaceGraphFileResultDto {
    path: String,
    exists: bool,
    backend: String,
    content: Option<String>,
    read_at: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WriteWorkspaceGraphFileResultDto {
    path: String,
    exists: bool,
    backend: String,
    written_at: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WriteWorkspaceRuntimeContractFileResultDto {
    path: String,
    exists: bool,
    backend: String,
    written_at: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReadWorkspaceGraphFileRequestDto {
    target_path: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WriteWorkspaceGraphFileRequestDto {
    content: String,
    target_path: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WriteWorkspaceRuntimeContractFileRequestDto {
    content: String,
    target_path: Option<String>,
}

fn write_workspace_log(app: &AppHandle, message: &str) {
    let _ = log_service::append_log_entry(
        app,
        &app_service::read_timestamp(),
        "info",
        "workspace",
        message,
    );
}

#[tauri::command]
pub fn get_workspace_graph_file_info(app: AppHandle) -> Result<WorkspaceGraphFileInfoDto, String> {
    let path = workspace_service::resolve_workspace_graph_file_path(&app, None)?;

    Ok(WorkspaceGraphFileInfoDto {
        path: path.display().to_string(),
        exists: path.exists(),
        backend: "tauri-file-system".into(),
    })
}

#[tauri::command]
pub fn read_workspace_graph_file(
    app: AppHandle,
    request: ReadWorkspaceGraphFileRequestDto,
) -> Result<ReadWorkspaceGraphFileResultDto, String> {
    let (path, content) = workspace_service::read_workspace_graph_file(&app, request.target_path.as_deref())?;
    write_workspace_log(&app, &format!("读取 Graph 工作区文件：{}", path.display()));

    Ok(ReadWorkspaceGraphFileResultDto {
        path: path.display().to_string(),
        exists: content.is_some(),
        backend: "tauri-file-system".into(),
        content,
        read_at: app_service::read_timestamp(),
    })
}

#[tauri::command]
pub fn write_workspace_graph_file(
    app: AppHandle,
    request: WriteWorkspaceGraphFileRequestDto,
) -> Result<WriteWorkspaceGraphFileResultDto, String> {
    let path = workspace_service::write_workspace_graph_file(&app, request.target_path.as_deref(), &request.content)?;
    write_workspace_log(&app, &format!("保存 Graph 工作区文件：{}", path.display()));

    Ok(WriteWorkspaceGraphFileResultDto {
        path: path.display().to_string(),
        exists: true,
        backend: "tauri-file-system".into(),
        written_at: app_service::read_timestamp(),
    })
}

#[tauri::command]
pub fn write_workspace_runtime_contract_file(
    app: AppHandle,
    request: WriteWorkspaceRuntimeContractFileRequestDto,
) -> Result<WriteWorkspaceRuntimeContractFileResultDto, String> {
    let path = workspace_service::write_workspace_runtime_contract_file(
        &app,
        request.target_path.as_deref(),
        &request.content,
    )?;
    write_workspace_log(&app, &format!("导出 runtime contract 文件：{}", path.display()));

    Ok(WriteWorkspaceRuntimeContractFileResultDto {
        path: path.display().to_string(),
        exists: true,
        backend: "tauri-file-system".into(),
        written_at: app_service::read_timestamp(),
    })
}
