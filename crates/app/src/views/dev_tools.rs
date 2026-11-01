//! (port in progress)

use std::sync::Arc;

use crate::gui::step::InstallStep;

pub fn step() -> Arc<dyn InstallStep> {
    super::pending::step("dev_tools", "")
}
