//! The hotel's NGINX site: Octane on /, or a CMS with Octane on /client/. (HotelSite.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use crate::web_access::WebAccessSettings;

pub const FILE: &str = "/etc/nginx/sites-available/hotel";
pub const PORT: &str = "85";
pub const LOG_DIRECTORY: &str = "/var/log/nginx/hotel";
/// The proxies NGINX takes the visitor's address from: 127.0.0.1 (Traefik), and Cloudflare's ranges behind Cloudflare.
pub const REAL_IP_FILE: &str = "/etc/nginx/hotel-real-ip.conf";
pub const DEFAULT_ROOT: &str = "/usr/share/nginx/html";

/// Written by Polaris, served by NGINX.
pub fn camera() -> String {
    format!("{}/camera", crate::octane::gamedata())
}

/// The script that writes the hotel site with this root (None: keep what the site has). (WriteScript)
pub fn write_script(site: &WebAccessSettings, root: Option<&str>) -> String {
    let _ = (site, root);
    todo!("HotelSite.WriteScript")
}

/// (RealIpScript)
pub fn real_ip_script(site: &WebAccessSettings) -> String {
    let _ = site;
    todo!("HotelSite.RealIpScript")
}

/// (PhpLocations)
pub fn php_locations() -> String {
    todo!("HotelSite.PhpLocations")
}

// ---------------------------------------------------------------------------------------------------------------
