//! yarn install, Octane's configuration and the gamedata folders, yarn build, and serving dist/. (OctaneInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use aio_core::ServerLayout;

pub fn octane() -> String {
    ServerLayout::current().octane.clone()
}

pub fn renderer() -> String {
    ServerLayout::current().renderer.clone()
}

pub fn gamedata() -> String {
    ServerLayout::current().gamedata.clone()
}

pub fn dist() -> String {
    format!("{}/dist", octane())
}

pub fn configuration_directory() -> String {
    format!("{}/public/configuration", octane())
}

pub const PERMISSIONS_SCRIPT: &str = "/usr/local/sbin/gamedata-permissions";

// ---------------------------------------------------------------------------------------------------------------
