//! JDK 25, the emulator account, build.sh, the database account, config.ini, the systemd service and the first
//! start of Polaris. (EmulatorInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use aio_core::ServerLayout;

use crate::server::{Log, Result, Server};

pub fn repository() -> String {
    ServerLayout::current().emulator_repository.clone()
}

pub fn directory() -> String {
    format!("{}/Emulator", repository())
}

pub fn log_directory() -> String {
    ServerLayout::current().emulator_logs.clone()
}

pub fn account() -> String {
    ServerLayout::current().emulator_account.clone()
}

pub const FIND_JAVA: &str = "ls -d /usr/lib/jvm/java-25-openjdk-* /usr/lib/jvm/temurin-25-jdk-* 2>/dev/null | head -1";

/// Shell helpers that control the emulator (`emulator_running`, `stop_emulator`, `start_emulator` ...). The calling
/// script must set $d and $log. (ControlFunctions)
pub fn control_functions() -> String {
    todo!("EmulatorInstaller.ControlFunctions")
}

/// Writes `emulator_settings` rows (stopping the emulator while they are written when it runs). Returns true when
/// something changed. (SetSettingsAsync)
pub async fn set_settings(server: &dyn Server, settings: &[(String, String)], what: &str, log: Log<'_>, setup: &str) -> Result<bool> {
    let _ = (server, settings, what, log, setup);
    todo!("EmulatorInstaller.SetSettingsAsync")
}

/// One `emulator_settings` value, or None. (ReadSettingAsync)
pub async fn read_setting(server: &dyn Server, key: &str) -> Result<Option<String>> {
    let _ = (server, key);
    todo!("EmulatorInstaller.ReadSettingAsync")
}

/// "key=value,..." sorted by key, as the server's GROUP_CONCAT of the imager keys prints it. (ImagerSettingsLine)
pub fn imager_settings_line(site_url: &str) -> String {
    let _ = site_url;
    todo!("EmulatorInstaller.ImagerSettingsLine")
}

/// The imager keys as SQL literals, comma separated. (ImagerKeysSql)
pub fn imager_keys_sql() -> String {
    todo!("EmulatorInstaller.ImagerKeysSql")
}

// ---------------------------------------------------------------------------------------------------------------
