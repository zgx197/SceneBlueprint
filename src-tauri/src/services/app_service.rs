pub fn read_platform() -> String {
    std::env::consts::OS.to_string()
}

pub fn read_timestamp() -> String {
    format!("{:?}", std::time::SystemTime::now())
}
