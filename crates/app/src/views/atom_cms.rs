//! (port in progress)

use std::sync::Arc;

use crate::gui::wizard::Wizard;
use crate::gui::Shared;

pub fn wizard(shared: &Shared) -> Wizard {
    Wizard::new(shared.runtime.clone(), &shared.ctx, &shared.dialogs, vec![super::pending::step("atom_cms", "")], false, Arc::new(super::pending::finish), "", "Completed")
}
