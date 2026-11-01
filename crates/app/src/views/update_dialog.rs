//! (port in progress) "A new version is available" at startup (UpdateDialog.cs), then the login.

use crate::gui::Shared;
use super::login::LoginOpener;

/// Asks GitHub for a newer installer; when there is none (or after "Later"), opens the login.
pub fn check_at_startup(_shared: &Shared, login: LoginOpener) {
    login.open();
}
