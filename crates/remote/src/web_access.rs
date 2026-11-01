//! Docker Engine, NGINX on 127.0.0.1:85, Traefik in /opt/traefik, the Polaris whitelist and the tests through
//! Traefik. (WebAccessInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use crate::script::ScriptValues;
use crate::server::{Result, Server};

/// How the hotel is reached from the internet. With a CMS (AtomCMS, EpicNext, or your own CMS in /var/www/<custom_cms>)
/// the CMS is the site and Octane moves to /client. `cms_port` is the CMS' own NGINX site on 127.0.0.1 (85-95); 0 for
/// an AtomCMS set up before that, which the hotel site on port 85 still serves itself.
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct WebAccessSettings {
    pub address: String,
    pub https: bool,
    pub cloudflare: bool,
    pub atom_cms: bool,
    pub custom_cms: Option<String>,
    pub cms_port: i64,
    pub epic_next: bool,
}

impl WebAccessSettings {
    pub fn new(address: &str, https: bool, cloudflare: bool) -> Self {
        Self { address: address.into(), https, cloudflare, ..Default::default() }
    }

    pub fn scheme(&self) -> &'static str {
        if self.https {
            "https"
        } else {
            "http"
        }
    }

    pub fn has_cms(&self) -> bool {
        self.atom_cms || self.epic_next || self.custom_cms.is_some()
    }

    /// The CMS has its own NGINX site; Traefik sends it everything except /client, /gamedata, /custom and Polaris.
    pub fn cms_on_own_port(&self) -> bool {
        self.has_cms() && self.cms_port > 0
    }

    /// "AtomCMS", "EpicNext CMS", the custom CMS' folder name, or None.
    pub fn cms_name(&self) -> Option<String> {
        if self.atom_cms {
            Some("AtomCMS".into())
        } else if self.epic_next {
            Some("EpicNext CMS".into())
        } else {
            self.custom_cms.clone()
        }
    }

    /// Where Octane is served, "/" or "/client/".
    pub fn client_path(&self) -> &'static str {
        if self.has_cms() {
            "/client/"
        } else {
            "/"
        }
    }

    pub fn client_url(&self) -> String {
        format!("{}{}", self.url(), self.client_path())
    }

    pub fn url(&self) -> String {
        if self.address.contains(':') {
            format!("{}://[{}]", self.scheme(), self.address)
        } else {
            format!("{}://{}", self.scheme(), self.address)
        }
    }

    /// For a bare IP address Traefik answers for every host name.
    pub fn is_ip_address(&self) -> bool {
        self.address.parse::<std::net::IpAddr>().is_ok()
    }

    /// Reads /opt/traefik/hotel.env as printed with a conf_ prefix. None before the web access step.
    pub fn from_hotel_env(values: &ScriptValues) -> Option<Self> {
        let address = values.get("conf_ADDRESS")?;
        let cms = values.get("conf_CMS");
        Some(Self {
            address: address.to_string(),
            https: values.get("conf_SCHEME") == Some("https"),
            cloudflare: values.yes("conf_CLOUDFLARE"),
            atom_cms: cms == Some("atom"),
            custom_cms: if cms == Some("custom") { values.string("conf_CMS_NAME") } else { None },
            cms_port: values.int("conf_CMS_PORT"),
            epic_next: cms == Some("epicnext"),
        })
    }
}

pub const TRAEFIK_DIRECTORY: &str = "/opt/traefik";
pub const NGINX_PORT: &str = crate::hotel_site::PORT;
pub const POLARIS_PORT: &str = "2096";
pub const SITE_FILE: &str = crate::hotel_site::FILE;

/// (WaitForTraefikScript)
pub fn wait_for_traefik_script() -> String {
    todo!("WebAccessInstaller.WaitForTraefikScript")
}

/// (StaticConfigScript)
pub fn static_config_script(settings: &WebAccessSettings) -> String {
    let _ = settings;
    todo!("WebAccessInstaller.StaticConfigScript")
}

/// (ComposeScript)
pub fn compose_script() -> String {
    todo!("WebAccessInstaller.ComposeScript")
}

/// Cloudflare's ranges, comma separated, behind Cloudflare; "" otherwise. (PolarisTrustedProxiesAsync)
pub async fn polaris_trusted_proxies(server: &dyn Server, settings: &WebAccessSettings) -> Result<String> {
    let _ = (server, settings);
    todo!("WebAccessInstaller.PolarisTrustedProxiesAsync")
}

// ---------------------------------------------------------------------------------------------------------------
