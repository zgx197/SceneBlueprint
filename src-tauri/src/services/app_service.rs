use time::{format_description::well_known::Rfc3339, OffsetDateTime};

pub fn read_platform() -> String {
    std::env::consts::OS.to_string()
}

pub fn read_timestamp() -> String {
    OffsetDateTime::now_utc()
        .format(&Rfc3339)
        .unwrap_or_else(|_| "1970-01-01T00:00:00Z".to_string())
}
