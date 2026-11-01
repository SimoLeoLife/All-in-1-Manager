//! A CMS' own NGINX site on the first free port from 85 to 95. (CmsSite.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

pub fn file(name: &str) -> String {
    format!("/etc/nginx/sites-available/cms-{name}")
}

// ---------------------------------------------------------------------------------------------------------------
