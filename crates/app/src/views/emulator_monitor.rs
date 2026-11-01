//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct EmulatorMonitorView;

impl EmulatorMonitorView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }
}

impl View for EmulatorMonitorView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("emulator_monitor: not ported yet.");
    }
}
