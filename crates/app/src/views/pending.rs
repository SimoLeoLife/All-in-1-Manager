//! A step or page that is not ported yet (used while the port is in progress).

use std::sync::Arc;

use aio_remote::Server;
use async_trait::async_trait;
use eframe::egui;

use crate::gui::icons::Icon;
use crate::gui::step::{InstallStep, Intro, StepOutcome, StepUi};
use crate::gui::theme::Palette;
use crate::gui::wizard::{FinishInfo, FinishPage};
use crate::gui::{Shared, View};

pub struct PendingStep {
    pub title: &'static str,
    pub caption: &'static str,
}

#[async_trait]
impl InstallStep for PendingStep {
    fn title(&self) -> String {
        self.title.into()
    }
    fn caption(&self) -> String {
        self.caption.into()
    }
    fn install_label(&self) -> String {
        format!("Install {}", self.title)
    }
    fn intro(&self) -> Intro {
        Intro { icon: Icon::Cube, heading: self.title.into(), description: "Not ported yet.".into(), bullets: vec![], footnote: None }
    }
    fn checks(&self) -> Vec<String> {
        vec![]
    }
    async fn detect(&self, _server: Arc<dyn Server>, _ui: StepUi) -> anyhow::Result<bool> {
        Ok(false)
    }
    async fn run(&self, _server: Arc<dyn Server>, _ui: StepUi) -> anyhow::Result<StepOutcome> {
        anyhow::bail!("not ported yet")
    }
}

pub fn step(title: &'static str, caption: &'static str) -> Arc<dyn InstallStep> {
    Arc::new(PendingStep { title, caption })
}

pub struct PendingFinish;

impl FinishPage for PendingFinish {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette) {
        ui.label("Not ported yet.");
    }
}

pub fn finish(_info: FinishInfo) -> Box<dyn FinishPage> {
    Box::new(PendingFinish)
}

pub struct PendingView(pub &'static str);

impl View for PendingView {
    fn show(&mut self, ui: &mut egui::Ui, _palette: &Palette, _shared: &Shared) {
        ui.label(format!("{}: not ported yet.", self.0));
    }
}
