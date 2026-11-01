//! CrowdSec in Docker with the Traefik bouncer plugin. (CrowdSecInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

pub const DIRECTORY: &str = "/opt/crowdsec";
pub const CONTAINER: &str = "crowdsec";
pub const IMAGE: &str = "crowdsecurity/crowdsec:latest";
pub const CONSOLE_URL: &str = "https://app.crowdsec.net";
pub const BOUNCER_NAME: &str = "traefik";
pub const ALLOWLIST_NAME: &str = "all-in-1";
pub const MIDDLEWARE: &str = "crowdsec";
pub const PLUGIN: &str = "crowdsec";
pub const PLUGIN_MODULE: &str = "github.com/maxlerebourg/crowdsec-bouncer-traefik-plugin";
pub const PLUGIN_VERSION: &str = "v1.8.1";
pub const ACCESS_LOG_DIRECTORY: &str = "/var/log/traefik";
pub const ACCESS_LOG: &str = "/var/log/traefik/access.log";
pub const COLLECTIONS: &str = "crowdsecurity/traefik crowdsecurity/http-cve crowdsecurity/whitelist-good-actors";
/// The version that runs: "v1.8.1".
pub const VERSION_COMMAND: &str = "docker exec crowdsec cscli version 2>&1 | awk '/^version:/ {print $2}' | cut -d- -f1";

pub fn middleware_file() -> String {
    format!("{}/dynamic/crowdsec.yml", crate::web_access::TRAEFIK_DIRECTORY)
}

// ---------------------------------------------------------------------------------------------------------------
