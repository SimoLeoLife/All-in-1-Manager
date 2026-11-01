//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct DebugView;

impl DebugView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }

    pub fn refresh(&mut self, _shared: &Shared) {}
}

impl View for DebugView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("debug: not ported yet.");
    }
}
