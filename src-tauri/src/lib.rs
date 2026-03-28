mod commands;
mod errors;
mod events;
mod services;
mod state;

use tauri::Emitter;

pub fn run() {
    tauri::Builder::default()
        .manage(state::app_state::AppState::default())
        .setup(|app| {
            app.emit("app-ready", events::app_events::AppReadyEvent::new())?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::app::get_app_info,
            commands::app::ping_host,
        ])
        .run(tauri::generate_context!())
        .expect("failed to run SceneBlueprint desktop shell");
}
