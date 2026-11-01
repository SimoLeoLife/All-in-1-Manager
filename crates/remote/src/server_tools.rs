//! The Tools page's server side: restarts, database backup / import, the firewall, recover SQL password, system and
//! hotel updates. (ServerTools.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use aio_core::ServerLayout;

use crate::server::{Log, Result, Server};

pub fn backup_directory() -> String {
    ServerLayout::current().backups.clone()
}

pub const KEPT_BACKUPS: usize = 10;

/// Sets $db: the hotel database from the emulator's config.ini, or the layout's database. (DatabaseName)
pub fn database_name() -> String {
    format!(
        "db=$(grep -sE '^db.database=' {}/config.ini | cut -d= -f2 | tr -d '[:space:]')\ndb=${{db:-{}}}",
        crate::emulator::directory(),
        ServerLayout::current().database
    )
}

/// Pulls a Docker image and recreates the container when it changed. (UpdateImageAsync)
pub async fn update_image(
    server: &dyn Server,
    name: &str,
    directory: &str,
    container: &str,
    version_command: &str,
    ready_check: &str,
    log: Log<'_>,
) -> Result<()> {
    let _ = (server, name, directory, container, version_command, ready_check, log);
    todo!("ServerTools.UpdateImageAsync")
}

// ---------------------------------------------------------------------------------------------------------------
