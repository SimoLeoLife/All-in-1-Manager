//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct TerminalView;

impl TerminalView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }
}

impl View for TerminalView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("terminal: not ported yet.");
    }
}
