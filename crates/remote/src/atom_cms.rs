//! AtomCMS: clone, .env, composer, migrations and settings, the theme build, Octane on /client and the tests.
//! (AtomCmsInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

use aio_core::ServerLayout;

pub const REPOSITORY: &str = "https://github.com/ObjectRetros/atomcms.git";
pub const SITE_NAME: &str = "atomcms";

pub fn directory() -> String {
    ServerLayout::current().atom_cms.clone()
}

pub fn public() -> String {
    format!("{}/public", directory())
}

// ---------------------------------------------------------------------------------------------------------------
