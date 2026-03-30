mod commands;
mod errors;
mod events;
mod services;
mod state;

use tauri::Emitter;

pub fn run() {
    tauri::Builder::default()
        .on_menu_event(|app, event| {
            services::menu_service::handle_menu_event(app, event.id().as_ref());
        })
        .manage(state::app_state::AppState::default())
        .setup(|app| {
            let menu = services::menu_service::build_app_menu(app.handle())?;
            let _ = app.set_menu(menu);
            let _ = services::log_service::append_log_entry(
                app.handle(),
                &services::app_service::read_timestamp(),
                "info",
                "host",
                "Rust 宿主启动完成。",
            );
            app.emit("app-ready", events::app_events::AppReadyEvent::new())?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::app::get_app_info,
            commands::app::ping_host,
            commands::app::write_log_entry,
            commands::app::get_log_file_path,
            commands::workspace::get_workspace_graph_file_info,
            commands::workspace::read_workspace_graph_file,
            commands::workspace::write_workspace_graph_file,
        ])
        .run(tauri::generate_context!())
        .expect("failed to run SceneBlueprint desktop shell");
}
