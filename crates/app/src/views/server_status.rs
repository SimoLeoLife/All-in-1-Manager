//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct ServerStatusView;

impl ServerStatusView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }
}

impl View for ServerStatusView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("server_status: not ported yet.");
    }
}
