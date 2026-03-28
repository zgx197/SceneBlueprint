#[derive(Default)]
pub struct AppState {
    pub phase: &'static str,
}

impl AppState {
    #[allow(dead_code)]
    pub fn phase_name(&self) -> &'static str {
        if self.phase.is_empty() {
            "phase-1-tauri-shell"
        } else {
            self.phase
        }
    }
}
