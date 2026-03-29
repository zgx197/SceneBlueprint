use std::{
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
};

use tauri::{AppHandle, Manager, Runtime};

const DEV_LOG_FILE_NAME: &str = "SceneBlueprint.dev.log";
const RELEASE_LOG_FILE_NAME: &str = "SceneBlueprint.log";

fn ensure_parent_dir(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    }

    Ok(())
}

fn ensure_log_file(path: &Path) -> Result<(), String> {
    ensure_parent_dir(path)?;
    OpenOptions::new()
        .create(true)
        .append(true)
        .open(path)
        .map(|_| ())
        .map_err(|error| error.to_string())
}

fn workspace_root_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")))
        .to_path_buf()
}

fn preferred_log_file_path() -> Result<PathBuf, String> {
    if cfg!(debug_assertions) {
        return Ok(workspace_root_path().join(".logs").join(DEV_LOG_FILE_NAME));
    }

    let executable_path = std::env::current_exe().map_err(|error| error.to_string())?;
    let executable_dir = executable_path
        .parent()
        .ok_or_else(|| "无法解析可执行文件目录".to_string())?;

    Ok(executable_dir.join(RELEASE_LOG_FILE_NAME))
}

fn fallback_log_file_path<R: Runtime>(app: &AppHandle<R>) -> Result<PathBuf, String> {
    let base_dir = app
        .path()
        .app_local_data_dir()
        .map_err(|error| error.to_string())?;

    Ok(base_dir.join("logs").join(if cfg!(debug_assertions) {
        DEV_LOG_FILE_NAME
    } else {
        RELEASE_LOG_FILE_NAME
    }))
}

pub fn resolve_log_file_path<R: Runtime>(app: &AppHandle<R>) -> Result<PathBuf, String> {
    let preferred_path = preferred_log_file_path()?;
    if ensure_log_file(&preferred_path).is_ok() {
        return Ok(preferred_path);
    }

    let fallback_path = fallback_log_file_path(app)?;
    ensure_log_file(&fallback_path)?;
    Ok(fallback_path)
}

fn sanitize(message: &str) -> String {
    message.replace('\r', " ").replace('\n', " ")
}

pub fn append_log_entry<R: Runtime>(
    app: &AppHandle<R>,
    timestamp: &str,
    level: &str,
    scope: &str,
    message: &str,
) -> Result<PathBuf, String> {
    let log_file_path = resolve_log_file_path(app)?;
    let mut file = OpenOptions::new()
        .create(true)
        .append(true)
        .open(&log_file_path)
        .map_err(|error| error.to_string())?;

    let line = format!("[{timestamp}][{level}][{scope}] {}\n", sanitize(message));

    file.write_all(line.as_bytes())
        .map_err(|error| error.to_string())?;

    Ok(log_file_path)
}
