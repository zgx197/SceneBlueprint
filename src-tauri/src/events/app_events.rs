use serde::Serialize;

#[derive(Clone, Serialize)]
pub struct AppReadyEvent {
    message: String,
}

impl AppReadyEvent {
    pub fn new() -> Self {
        Self {
            message: "SceneBlueprint desktop shell is ready.".into(),
        }
    }
}
