use std::{fs, path::{Path, PathBuf}};

use tauri::{AppHandle, Manager, Runtime};

const DEV_WORKSPACE_DIR_NAME: &str = ".workspace";
const WORKSPACE_GRAPH_FILE_NAME: &str = "SceneBlueprint.graph.json";

fn ensure_parent_dir(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    }

    Ok(())
}

fn workspace_root_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")))
        .to_path_buf()
}

fn executable_dir_path() -> Result<PathBuf, String> {
    let executable_path = std::env::current_exe().map_err(|error| error.to_string())?;
    let executable_dir = executable_path
        .parent()
        .ok_or_else(|| "无法解析可执行文件目录".to_string())?;

    Ok(executable_dir.to_path_buf())
}

fn preferred_workspace_graph_file_path() -> Result<PathBuf, String> {
    if cfg!(debug_assertions) {
        return Ok(
            workspace_root_path()
                .join(DEV_WORKSPACE_DIR_NAME)
                .join(WORKSPACE_GRAPH_FILE_NAME),
        );
    }

    Ok(executable_dir_path()?.join(WORKSPACE_GRAPH_FILE_NAME))
}

fn fallback_workspace_graph_file_path<R: Runtime>(app: &AppHandle<R>) -> Result<PathBuf, String> {
    let base_dir = app
        .path()
        .app_local_data_dir()
        .map_err(|error| error.to_string())?;

    Ok(base_dir.join("workspace").join(WORKSPACE_GRAPH_FILE_NAME))
}

fn normalize_target_path(target_path: &str) -> Result<PathBuf, String> {
    let candidate = PathBuf::from(target_path);
    if candidate.is_absolute() {
        return Ok(candidate);
    }

    if cfg!(debug_assertions) {
        return Ok(workspace_root_path().join(candidate));
    }

    Ok(executable_dir_path()?.join(candidate))
}

pub fn resolve_workspace_graph_file_path<R: Runtime>(
    app: &AppHandle<R>,
    target_path: Option<&str>,
) -> Result<PathBuf, String> {
    if let Some(target_path) = target_path {
        return normalize_target_path(target_path);
    }

    let preferred_path = preferred_workspace_graph_file_path()?;
    if preferred_path.exists() {
        return Ok(preferred_path);
    }

    let fallback_path = fallback_workspace_graph_file_path(app)?;
    if fallback_path.exists() {
        return Ok(fallback_path);
    }

    Ok(preferred_path)
}

fn resolve_writable_workspace_graph_file_path<R: Runtime>(
    app: &AppHandle<R>,
    target_path: Option<&str>,
) -> Result<PathBuf, String> {
    if let Some(target_path) = target_path {
        let path = normalize_target_path(target_path)?;
        ensure_parent_dir(&path)?;
        return Ok(path);
    }

    let preferred_path = preferred_workspace_graph_file_path()?;
    if ensure_parent_dir(&preferred_path).is_ok() {
        return Ok(preferred_path);
    }

    let fallback_path = fallback_workspace_graph_file_path(app)?;
    ensure_parent_dir(&fallback_path)?;
    Ok(fallback_path)
}

pub fn read_workspace_graph_file<R: Runtime>(
    app: &AppHandle<R>,
    target_path: Option<&str>,
) -> Result<(PathBuf, Option<String>), String> {
    let path = resolve_workspace_graph_file_path(app, target_path)?;
    if !path.exists() {
        return Ok((path, None));
    }

    let content = fs::read_to_string(&path).map_err(|error| error.to_string())?;
    Ok((path, Some(content)))
}

pub fn write_workspace_graph_file<R: Runtime>(
    app: &AppHandle<R>,
    target_path: Option<&str>,
    content: &str,
) -> Result<PathBuf, String> {
    let path = resolve_writable_workspace_graph_file_path(app, target_path)?;
    fs::write(&path, content).map_err(|error| error.to_string())?;
    Ok(path)
}
