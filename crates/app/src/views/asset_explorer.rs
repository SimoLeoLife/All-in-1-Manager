//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct AssetExplorerView;

impl AssetExplorerView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }

    pub fn refresh(&mut self, _shared: &Shared) {}
}

impl View for AssetExplorerView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("asset_explorer: not ported yet.");
    }
}
