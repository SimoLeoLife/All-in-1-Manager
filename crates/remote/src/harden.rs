//! The ufw firewall, Polaris' ws.whitelist / ws.ip.header, the real-IP setup and the sysctl SYN flood settings.
//! (HardenInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use crate::web_access::WebAccessSettings;

/// The hotel's address without "www." (Polaris drops it itself).
pub fn whitelist_host(site: &WebAccessSettings) -> String {
    if site.address.len() >= 4 && site.address.is_char_boundary(4) && site.address[..4].eq_ignore_ascii_case("www.") {
        site.address[4..].to_string()
    } else {
        site.address.clone()
    }
}

// ---------------------------------------------------------------------------------------------------------------
