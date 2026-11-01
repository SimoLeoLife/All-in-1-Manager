//! (port in progress) The login at startup (LoginDialog.cs).

use std::sync::{Arc, Mutex};

use aio_remote::Server;
use eframe::egui;

use crate::gui::Shared;

/// Lets a task open the login (after the update check).
#[derive(Clone)]
pub struct LoginOpener(Arc<Mutex<bool>>);

impl LoginOpener {
    pub fn open(&self) {
        *self.0.lock().unwrap() = true;
    }
}

pub struct LoginDialog {
    open: bool,
    request: Arc<Mutex<bool>>,
    connected: Option<Arc<dyn Server>>,
    last_host: Option<String>,
}

impl LoginDialog {
    pub fn new(_shared: &Shared) -> Self {
        Self { open: false, request: Arc::new(Mutex::new(false)), connected: None, last_host: None }
    }

    pub fn opener(&self) -> LoginOpener {
        LoginOpener(self.request.clone())
    }

    /// True once after a task asked for the login.
    pub fn take_open_request(&mut self) -> bool {
        std::mem::take(&mut *self.request.lock().unwrap())
    }

    pub fn is_open(&self) -> bool {
        self.open
    }

    pub fn open(&mut self, _notice: Option<String>) {
        self.open = true;
    }

    /// The server of the last login, for "The connection to <host> was lost".
    pub fn last_host(&self) -> Option<String> {
        self.last_host.clone()
    }

    /// The new login, once.
    pub fn take_connected(&mut self) -> Option<Arc<dyn Server>> {
        self.connected.take()
    }

    pub fn show(&mut self, _ctx: &egui::Context, _shared: &Shared) {}
}
