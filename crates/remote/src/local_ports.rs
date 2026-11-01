//! The local ports (127.0.0.1) for the web apps behind Traefik: the first free one from 85 to 95. (LocalPorts.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

pub const FIRST: u16 = 85;
pub const LAST: u16 = 95;

/// `pick_port PREVIOUS` and its helpers. (Functions)
pub fn functions() -> String {
    todo!("LocalPorts.Functions")
}

// ---------------------------------------------------------------------------------------------------------------
