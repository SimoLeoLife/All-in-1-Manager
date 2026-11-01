//! (port in progress) The Tools page (ToolsView.cs), with Server status, Emulator & wired and the SSH terminal.

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Page, Shared, View};

/// What the dashboard's Update buttons update.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Component {
    Emulator,
    Octane,
    AtomCms,
    Traefik,
    Gitea,
    CrowdSec,
}

pub struct ToolsView {
    page: Page,
}

impl ToolsView {
    pub fn new(_shared: &Shared) -> Self {
        Self { page: Page::Tools }
    }

    pub fn refresh(&mut self, _shared: &Shared) {}

    /// Tools, Server status, Emulator & wired or the SSH terminal.
    pub fn set_page(&mut self, page: Page) {
        self.page = page;
    }

    /// The sub-page on screen (Back returns to Tools).
    pub fn page(&self) -> Page {
        self.page
    }

    /// Runs an update with its question and progress on this page.
    pub fn update(&mut self, _component: Component, _shared: &Shared) {}

    /// The last update check for this login (None: not checked yet).
    pub fn last_updates(&self) -> Option<Vec<aio_remote::server_status::RepositoryUpdate>> {
        None
    }

    /// The last Docker image check for this login.
    pub fn last_images(&self) -> Option<Vec<aio_remote::server_status::ImageStatus>> {
        None
    }

    /// Fetches GitHub again (the dashboard's Check again).
    pub fn check_updates_again(&mut self, _shared: &Shared) {}
}

impl View for ToolsView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("Tools: not ported yet.");
    }
}
