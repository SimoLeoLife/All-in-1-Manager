//! (port in progress)

use eframe::egui;

use crate::gui::theme::Palette;
use crate::gui::{Shared, View};

pub struct SqlEditorView;

impl SqlEditorView {
    pub fn new(_shared: &Shared) -> Self {
        Self
    }

    pub fn refresh(&mut self, _shared: &Shared) {}
}

impl View for SqlEditorView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label("sql_editor: not ported yet.");
    }
}
