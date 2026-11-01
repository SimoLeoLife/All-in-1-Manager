//! (port in progress)

use std::sync::Arc;

use crate::gui::step::InstallStep;

/// Polaris, Octane and Octane-Renderer.
pub fn steps() -> Vec<Arc<dyn InstallStep>> {
    vec![super::pending::step("Polaris", "Emulator"), super::pending::step("Octane", "Client"), super::pending::step("Renderer", "Octane-Renderer")]
}
