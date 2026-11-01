//! Deploy Assets: the downloader's files, the client's UI texts and the emulator's images, three steps in one wizard.
//! (AssetSyncStep.cs, UiTextsStep.cs, EmulatorAssetsStep.cs, InstallerWindow._deployWizard)

use std::collections::{BTreeSet, HashMap, HashSet};
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::Instant;

use aio_remote::asset_sync::{self, AssetPlan};
use aio_remote::emulator_assets::{self, EmulatorAssetFolder, EmulatorAssetState};
use aio_remote::server::{shell_quote, Log};
use aio_remote::script::{self, fill};
use aio_remote::{fail, octane, ui_texts, Server};
use async_trait::async_trait;
use eframe::egui::{self, Color32, RichText};

use crate::gui::icons::Icon;
use crate::gui::step::{InstallStep, Intro, StepOutcome, StepUi};
use crate::gui::theme::Palette;
use crate::gui::widgets::ResultKind;
use crate::gui::wizard::Wizard;
use crate::gui::Shared;
use crate::i18n::{t, tf};

/// The client images of Program/Assets, packed into the binary by build.rs (entries relative to Assets/).
static CLIENT_ASSETS: &[u8] = include_bytes!(concat!(env!("OUT_DIR"), "/ClientAssets.zip"));

/// "1.2 GB", "15 MB", "300 KB". (ServerStatusView.Bytes)
fn bytes(bytes: i64) -> String {
    let b = bytes as f64;
    if bytes >= 1 << 40 {
        format!("{:.1} TB", b / (1u64 << 40) as f64)
    } else if bytes >= 1 << 30 {
        format!("{:.1} GB", b / (1u64 << 30) as f64)
    } else if bytes >= 1 << 20 {
        format!("{:.0} MB", b / (1u64 << 20) as f64)
    } else {
        format!("{:.0} KB", b / 1024.0)
    }
}

/// "2.50 MB/s". (ServerStatusView.Rate)
fn rate(bytes_per_second: f64) -> String {
    if bytes_per_second >= 1024.0 * 1024.0 {
        format!("{:.2} MB/s", bytes_per_second / (1024.0 * 1024.0))
    } else if bytes_per_second >= 1024.0 {
        format!("{:.1} KB/s", bytes_per_second / 1024.0)
    } else {
        format!("{bytes_per_second:.0} B/s")
    }
}

fn small(ui: &mut egui::Ui, text: &str, size: f32, color: Color32, strong: bool) {
    let mut text = RichText::new(text).size(size).color(color);
    if strong {
        text = text.strong();
    }
    ui.label(text);
}

// ---------------------------------------------------------------------------------------------------------------
// Sync from the All-in-1 downloader (AssetSyncStep.cs)

#[derive(Clone)]
struct PlanRow {
    name: String,
    target: String,
    here: String,
    upload: String,
    uploads: bool,
}

#[derive(Default)]
struct SyncForm {
    overwrite_config: bool,
    source: String,
    rows: Vec<PlanRow>,
    // Unticked folders, by mapping name; every folder starts ticked.
    selected: HashMap<String, bool>,
}

#[derive(Default)]
pub struct AssetSyncStep {
    form: Mutex<SyncForm>,
    summary: Mutex<Option<String>>,
}

fn downloader_folder() -> Option<PathBuf> {
    asset_sync::downloader_folder(aio_core::InstallerSettings::load().downloader_path.as_deref())
}

/// The "To upload" column of a folder.
fn upload_text(plan: &AssetPlan) -> String {
    if plan.upload_count() == 0 {
        return if plan.local_count == 0 { t("nothing downloaded").into() } else { t("up to date").into() };
    }
    let mut text = tf("{count} new", &[("count", &plan.new.len().to_string())]);
    if !plan.changed.is_empty() {
        text.push_str(&tf(", {count} changed", &[("count", &plan.changed.len().to_string())]));
    }
    format!("{text} · {}", bytes(plan.upload_bytes()))
}

impl AssetSyncStep {
    fn summary(&self) -> Option<String> {
        self.summary.lock().unwrap().clone()
    }

    fn show_plans(&self, folder: &Path, plans: &[AssetPlan]) {
        let mut form = self.form.lock().unwrap();
        form.source = tf(
            "From {source} to {gamedata} on the server:",
            &[("source", &folder.join(asset_sync::DEFAULT_FOLDER).display().to_string()), ("gamedata", &octane::gamedata())],
        );
        form.rows = plans
            .iter()
            .map(|plan| PlanRow {
                name: plan.mapping.name.clone(),
                target: format!("{}/", plan.mapping.target),
                here: tf("{count} files · {size}", &[("count", &plan.local_count.to_string()), ("size", &bytes(plan.local_bytes))]),
                upload: upload_text(plan),
                uploads: plan.upload_count() > 0,
            })
            .collect();
    }
}

#[async_trait]
impl InstallStep for AssetSyncStep {
    fn title(&self) -> String {
        t("Sync").into()
    }

    fn caption(&self) -> String {
        t("Downloader").into()
    }

    fn install_label(&self) -> String {
        t("Upload").into()
    }

    fn rerun_label(&self) -> String {
        t("Sync again").into()
    }

    fn intro(&self) -> Intro {
        Intro {
            icon: Icon::Download,
            heading: t("Sync from the All-in-1 downloader").into(),
            description: tf(
                "Copies the Habbo assets the All-in-1 downloader saved in its Habbo_Default folder to {gamedata} on the server, where Octane loads them.",
                &[("gamedata", &octane::gamedata())],
            ),
            bullets: vec![
                t("Compares every folder with the server first: only files that are not there yet, or that differ (size, or newer here), are sent.").into(),
                t("Sends them per folder as one stream over the SSH connection (no FTP, no temporary files); the files keep their dates.").into(),
                t("Never deletes anything on the server: your own custom furniture, badges and the emulator's files stay.").into(),
                t("Config JSON (FurnitureData, ProductData, ExternalTexts ...) is only added when missing, unless you tick that it may be replaced: hotels and the emulator edit those files.").into(),
                tf("Then gives the emulator and NGINX access again ({script}).", &[("script", octane::PERMISSIONS_SCRIPT)]),
            ],
            footnote: Some(t("Set the downloader's executable in Settings first. New furniture also needs rows in items_base and catalog_items: the All-in-1 converter makes the SQL (Hotel Tools → Generate SQL).").into()),
        }
    }

    fn checks(&self) -> Vec<String> {
        ["Compare the downloader with the server", "Check the free space", "Upload new and changed files", "Permissions (www-data)"]
            .iter()
            .map(|c| t(c).to_string())
            .collect()
    }

    fn options(&self, ui: &mut egui::Ui, palette: &Palette) {
        let mut form = self.form.lock().unwrap();
        let form = &mut *form;
        ui.add_space(2.0);
        if !form.source.is_empty() {
            ui.add(egui::Label::new(RichText::new(&form.source).size(12.0).color(palette.subtle)).wrap());
        }
        if !form.rows.is_empty() {
            egui::Grid::new("asset_sync_plans").num_columns(4).spacing(egui::vec2(16.0, 6.0)).show(ui, |ui| {
                for heading in ["Folder", "Server", "Here", "To upload"] {
                    small(ui, t(heading), 11.5, palette.muted, true);
                }
                ui.end_row();
                for row in &form.rows {
                    let selected = form.selected.entry(row.name.clone()).or_insert(true);
                    ui.add_enabled(row.uploads, egui::Checkbox::new(selected, &row.name));
                    small(ui, &row.target, 12.0, palette.muted, false);
                    small(ui, &row.here, 12.0, palette.muted, false);
                    small(ui, &row.upload, 12.0, if row.uploads { palette.text } else { palette.subtle }, row.uploads);
                    ui.end_row();
                }
            });
        }
        ui.checkbox(
            &mut form.overwrite_config,
            t("Also replace config JSON files that are already on the server (FurnitureData, ProductData, ExternalTexts ...)"),
        );
    }

    async fn detect(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<bool> {
        let Some(folder) = downloader_folder() else {
            {
                let mut form = self.form.lock().unwrap();
                form.source.clear();
                form.rows.clear();
            }
            ui.detected(
                ResultKind::Warning,
                t("The All-in-1 downloader is not set"),
                t("Open Settings → All-in-1 downloader and choose its executable (its folder holds Habbo_Default), then come back here."),
            );
            return Ok(false);
        };
        let overwrite = self.form.lock().unwrap().overwrite_config;
        let plans = asset_sync::plan(server.as_ref(), &folder, overwrite).await?;
        self.show_plans(&folder, &plans);
        let count: usize = plans.iter().map(|p| p.upload_count()).sum();
        if count == 0 {
            let local: usize = plans.iter().map(|p| p.local_count).sum();
            ui.detected(
                ResultKind::Success,
                t("The server has everything the downloader has"),
                &tf(
                    "{count} files in {folder}, all on the server. Download more with the All-in-1 downloader and sync again.",
                    &[("count", &local.to_string()), ("folder", asset_sync::DEFAULT_FOLDER)],
                ),
            );
            return Ok(true);
        }
        let total: i64 = plans.iter().map(|p| p.upload_bytes()).sum();
        ui.detected(
            ResultKind::Info,
            &tf("{count} files to upload ({size})", &[("count", &count.to_string()), ("size", &bytes(total))]),
            t("Untick the folders you do not want, then upload."),
        );
        Ok(false)
    }

    async fn run(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<StepOutcome> {
        let log = ui.logger();
        let server = server.as_ref();
        let Some(folder) = downloader_folder() else { fail!("{}", t("Set the All-in-1 downloader in Settings first.")) };
        log(&format!("> Comparing {} with {} ...", folder.join(asset_sync::DEFAULT_FOLDER).display(), octane::gamedata()));
        let (overwrite, selected) = {
            let form = self.form.lock().unwrap();
            (form.overwrite_config, form.selected.clone())
        };
        let plans = asset_sync::plan(server, &folder, overwrite).await?;
        let is_chosen = |p: &AssetPlan| p.upload_count() > 0 && selected.get(&p.mapping.name).copied() != Some(false);
        for plan in plans.iter().filter(|p| p.local_count > 0) {
            let skipped = if plan.upload_count() > 0 && !is_chosen(plan) { " (skipped: unticked)" } else { "" };
            log(&format!(
                "  {:<24} {:>6} here · {} new, {} changed{skipped}",
                plan.mapping.name,
                plan.local_count,
                plan.new.len(),
                plan.changed.len()
            ));
        }
        let chosen: Vec<&AssetPlan> = plans.iter().filter(|p| is_chosen(p)).collect();
        let total: i64 = chosen.iter().map(|p| p.upload_bytes()).sum();
        let files: usize = chosen.iter().map(|p| p.upload_count()).sum();
        ui.done(&tf("{count} files, {size}", &[("count", &files.to_string()), ("size", &bytes(total))]));

        if files == 0 {
            ui.done(t("not needed"));
            ui.done(t("nothing to send"));
            ui.done(t("not needed"));
            let summary = t("Nothing to upload: the server already has every file.").to_string();
            *self.summary.lock().unwrap() = Some(summary.clone());
            return Ok(ui.succeeded(t("The server is up to date"), &summary));
        }

        let free = asset_sync::free_space(server).await?;
        log(&format!("  {} free on the server, {} needed", bytes(free), bytes(total)));
        if free < total.saturating_add(512 << 20) {
            fail!(
                "{}",
                tf(
                    "Not enough disk space on the server: {total} to upload, {free} free (keeping 512 MB spare).",
                    &[("total", &bytes(total)), ("free", &bytes(free))]
                )
            );
        }
        ui.done(&tf("{size} free", &[("size", &bytes(free))]));

        let started = Instant::now();
        let mut done: i64 = 0;
        for plan in &chosen {
            log(&format!(
                "> {}: {} files ({}) to {}/{}",
                plan.mapping.name,
                plan.upload_count(),
                bytes(plan.upload_bytes()),
                octane::gamedata(),
                plan.mapping.target
            ));
            let before = done;
            let last = Mutex::new((-1i64, Instant::now()));
            let progress = |sent: u64| {
                let so_far = before + sent as i64;
                let percent = so_far * 100 / total.max(1);
                let mut last = last.lock().unwrap();
                if percent == last.0 || (last.1.elapsed().as_secs_f64() < 3.0 && percent < 100) {
                    return;
                }
                *last = (percent, Instant::now());
                let seconds = started.elapsed().as_secs_f64().max(0.1);
                log(&format!("  {percent}% · {} of {} · {}", bytes(so_far), bytes(total), rate(so_far as f64 / seconds)));
            };
            asset_sync::upload(server, plan, &progress).await?;
            done += plan.upload_bytes();
        }
        let elapsed = started.elapsed().as_secs_f64();
        log(&format!("  {files} files in {elapsed:.0} seconds ({})", rate(total as f64 / elapsed.max(0.1))));
        ui.done(&tf("{count} files", &[("count", &files.to_string())]));

        asset_sync::fix_permissions(server, &log).await?;
        ui.done("www-data");

        let per_folder: Vec<String> = chosen.iter().map(|p| format!("{} {}", p.mapping.name.to_lowercase(), p.upload_count())).collect();
        let summary = format!(
            "{}{}.",
            tf("{count} files ({size}) uploaded: ", &[("count", &files.to_string()), ("size", &bytes(total))]),
            per_folder.join(", ")
        );
        *self.summary.lock().unwrap() = Some(summary.clone());
        Ok(ui.succeeded(
            t("The assets are on the server"),
            &format!(
                "{summary} {}",
                t("Players see them after reloading the hotel. New furniture also needs its items_base / catalog_items rows (All-in-1 converter → Generate SQL).")
            ),
        ))
    }
}

// ---------------------------------------------------------------------------------------------------------------
// The client's UI texts (UiTextsStep.cs)

const DEFAULT_LANGUAGE: &str = "en";

struct TextsForm {
    codes: Vec<String>,
    selected: Option<usize>,
    note: String,
}

impl Default for TextsForm {
    fn default() -> Self {
        let codes: Vec<String> = ui_texts::KNOWN_LANGUAGES.iter().map(|c| c.to_string()).collect();
        let selected = codes.iter().position(|c| c == DEFAULT_LANGUAGE);
        Self { codes, selected, note: String::new() }
    }
}

impl TextsForm {
    /// New languages: the installed one wins over the one chosen before, then the default. (ShowLanguages)
    fn set_languages(&mut self, codes: Vec<String>, select: Option<&str>) {
        let keep = self.selected.and_then(|i| self.codes.get(i)).cloned();
        self.codes = codes;
        let wanted = select.map(str::to_string).or(keep).unwrap_or_else(|| DEFAULT_LANGUAGE.to_string());
        self.selected = self
            .codes
            .iter()
            .position(|c| *c == wanted)
            .or_else(|| self.codes.iter().position(|c| c == DEFAULT_LANGUAGE))
            .or(if self.codes.is_empty() { None } else { Some(0) });
    }
}

#[derive(Default)]
pub struct UiTextsStep {
    form: Mutex<TextsForm>,
    summary: Mutex<Option<String>>,
}

impl UiTextsStep {
    fn summary(&self) -> Option<String> {
        self.summary.lock().unwrap().clone()
    }
}

#[async_trait]
impl InstallStep for UiTextsStep {
    fn title(&self) -> String {
        t("UI texts").into()
    }

    fn caption(&self) -> String {
        t("Language").into()
    }

    fn install_label(&self) -> String {
        t("Install the UI texts").into()
    }

    fn rerun_label(&self) -> String {
        t("Change the language").into()
    }

    fn intro(&self) -> Intro {
        Intro {
            icon: Icon::Globe,
            heading: t("The client's UI texts").into(),
            description: format!(
                "{}{}",
                tf("Octane loads its own interface texts (buttons, windows, messages) from {target}. ", &[("target", &ui_texts::target())]),
                t("Octane comes with them in several languages; choose the one for your hotel.")
            ),
            bullets: vec![
                tf(
                    "Takes UITexts_<language>.jsonc.example from {directory} (the Octane clone), or downloads it from github.com/duckietm/Octane when the clone is not there.",
                    &[("directory", &octane::configuration_directory())],
                ),
                tf("Installs it as UITexts.jsonc in {gamedata}/config, readable by NGINX.", &[("gamedata", &octane::gamedata())]),
                t("A UITexts.jsonc of your own (one that is none of Octane's languages) is kept as UITexts.jsonc.bak-<time>, so your changes are not lost.").into(),
            ],
            footnote: Some(t("Players see the new texts after reloading the hotel. The game's own texts (furniture names, badges) are in ExternalTexts.json.").into()),
        }
    }

    fn checks(&self) -> Vec<String> {
        vec![t("Find the language file").into(), t("Install UITexts.jsonc").into()]
    }

    fn options(&self, ui: &mut egui::Ui, palette: &Palette) {
        let mut form = self.form.lock().unwrap();
        let form = &mut *form;
        ui.add_space(2.0);
        super::atom_cms::field_label(ui, palette, t("Language"));
        let current = form.selected.and_then(|i| form.codes.get(i)).map(|c| ui_texts::name(c)).unwrap_or_default();
        egui::ComboBox::from_id_salt("ui_texts_language").width(300.0).height(340.0).selected_text(current).show_ui(ui, |ui| {
            for (i, code) in form.codes.iter().enumerate() {
                ui.selectable_value(&mut form.selected, Some(i), ui_texts::name(code));
            }
        });
        if !form.note.is_empty() {
            ui.add(egui::Label::new(RichText::new(&form.note).size(11.5).color(palette.subtle)).wrap());
        }
    }

    fn validate(&self) -> Option<String> {
        let form = self.form.lock().unwrap();
        form.selected.filter(|&i| i < form.codes.len()).is_none().then(|| t("Choose a language.").to_string())
    }

    async fn detect(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<bool> {
        let state = ui_texts::inspect(server.as_ref()).await?;
        {
            let mut form = self.form.lock().unwrap();
            let select = state.current.as_deref().filter(|c| *c != "custom");
            form.set_languages(state.languages.clone(), select);
            form.note = if state.from_clone {
                tf("{count} languages in the Octane clone on the server.", &[("count", &form.codes.len().to_string())])
            } else {
                t("No Octane clone with UITexts files on the server: the file is downloaded from GitHub.").into()
            };
        }
        match state.current.as_deref() {
            None => {
                ui.detected(
                    ResultKind::Info,
                    t("There is no UITexts.jsonc yet"),
                    t("Octane's renderer-config loads it from gamedata/config: choose a language and install it."),
                );
                Ok(false)
            }
            Some("custom") => {
                ui.detected(
                    ResultKind::Info,
                    t("UITexts.jsonc is your own version"),
                    t("It matches none of Octane's languages (edited, or from an older Octane). Installing a language keeps it as a backup."),
                );
                Ok(false)
            }
            Some(code) => {
                ui.detected(
                    ResultKind::Success,
                    &tf("UITexts.jsonc is {language}", &[("language", &ui_texts::name(code))]),
                    t("It is the same as Octane's file. Choose another language to change it, or skip."),
                );
                Ok(true)
            }
        }
    }

    async fn run(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<StepOutcome> {
        let code = {
            let form = self.form.lock().unwrap();
            form.selected.and_then(|i| form.codes.get(i)).cloned()
        };
        let Some(code) = code else { fail!("{}", t("Choose a language.")) };
        let log = ui.logger();
        log(&format!("> UITexts: {}", ui_texts::name(&code)));
        ui.done(&code);
        let backup = ui_texts::install(server.as_ref(), &code, &log).await?;
        ui.done(&ui_texts::target());
        let mut summary = tf("UITexts.jsonc is {language}.", &[("language", &ui_texts::name(&code))]);
        if let Some(backup) = backup {
            summary.push_str(&format!(" {}", tf("The previous file is {backup}.", &[("backup", &backup)])));
        }
        *self.summary.lock().unwrap() = Some(summary.clone());
        Ok(ui.succeeded(t("The UI texts are installed"), &format!("{summary} {}", t("Players see them after reloading the hotel."))))
    }
}

// ---------------------------------------------------------------------------------------------------------------
// The emulator's images and group badge parts (EmulatorAssetsStep.cs)
//
// The remote `emulator_assets` module reads only Assets/ next to the exe. The images packed into the binary and that
// folder are merged into a temporary Assets folder, and the module's inspect/install are repeated here for that
// folder (same scripts, same log lines).

/// Extracts the packed images into `into` (emptied first), then copies `beside_exe` over them: images of your own win.
fn prepare_assets(packed: &[u8], beside_exe: &Path, into: &Path) -> std::io::Result<()> {
    if into.exists() {
        std::fs::remove_dir_all(into)?;
    }
    std::fs::create_dir_all(into)?;
    let mut archive = zip::ZipArchive::new(std::io::Cursor::new(packed)).map_err(std::io::Error::other)?;
    for i in 0..archive.len() {
        let mut entry = archive.by_index(i).map_err(std::io::Error::other)?;
        let Some(relative) = entry.enclosed_name() else { continue };
        let path = into.join(relative);
        if entry.is_dir() {
            std::fs::create_dir_all(&path)?;
            continue;
        }
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)?;
        }
        let mut file = std::fs::File::create(&path)?;
        std::io::copy(&mut entry, &mut file)?;
    }
    if beside_exe.is_dir() {
        copy_tree(beside_exe, into)?;
    }
    Ok(())
}

fn copy_tree(from: &Path, to: &Path) -> std::io::Result<()> {
    std::fs::create_dir_all(to)?;
    for entry in std::fs::read_dir(from)?.flatten() {
        let path = entry.path();
        let target = to.join(entry.file_name());
        if path.is_dir() {
            copy_tree(&path, &target)?;
        } else {
            std::fs::copy(&path, &target)?;
        }
    }
    Ok(())
}

/// The merged Assets folder in the temp folder, made again for each check (Assets/ next to the exe may change).
async fn local_assets() -> anyhow::Result<PathBuf> {
    let into = std::env::temp_dir().join(format!("all-in-1-installer-{}", std::process::id())).join("Assets");
    let beside = emulator_assets::local_assets();
    let target = into.clone();
    tokio::task::spawn_blocking(move || prepare_assets(CLIENT_ASSETS, &beside, &target)).await??;
    Ok(into)
}

/// `*` and `?`, case-insensitive on Windows. (asset_sync::matches_pattern)
fn matches_pattern(pattern: &str, name: &str) -> bool {
    fn fold(text: &str) -> Vec<char> {
        if cfg!(windows) {
            text.chars().flat_map(char::to_lowercase).collect()
        } else {
            text.chars().collect()
        }
    }
    fn go(p: &[char], n: &[char]) -> bool {
        match p.first() {
            None => n.is_empty(),
            Some('*') => (0..=n.len()).any(|i| go(&p[1..], &n[i..])),
            Some('?') => !n.is_empty() && go(&p[1..], &n[1..]),
            Some(c) => n.first() == Some(c) && go(&p[1..], &n[1..]),
        }
    }
    let pattern = if pattern == "*.*" { "*" } else { pattern };
    go(&fold(pattern), &fold(name))
}

fn sorted_subdirectories(directory: &Path) -> Vec<String> {
    let mut names: Vec<String> = std::fs::read_dir(directory)
        .map(|entries| entries.flatten().filter(|e| e.path().is_dir()).map(|e| e.file_name().to_string_lossy().into_owned()).collect())
        .unwrap_or_default();
    names.sort();
    names
}

/// The fixed folders, then every other top-level folder of `local`. (emulator_assets::folders_in)
fn folders_in(local: &Path) -> Vec<EmulatorAssetFolder> {
    let mut folders: Vec<EmulatorAssetFolder> = emulator_assets::folders().into_iter().take(3).collect();
    if !local.is_dir() {
        return folders;
    }
    let taken: HashSet<String> = folders.iter().map(|f| f.source.split('/').next().unwrap_or("").to_string()).collect();
    for name in sorted_subdirectories(local) {
        if taken.contains(&name) || name.starts_with('.') {
            continue;
        }
        let parts = sorted_subdirectories(&local.join(&name));
        let purpose = if parts.is_empty() { format!("Assets/{name} → gamedata/{name}") } else { parts.join(", ") };
        let title = if name == "c_images" { "Game images (c_images)".to_string() } else { name.clone() };
        folders.push(EmulatorAssetFolder { name: title, source: name.clone(), target: name, pattern: "*".into(), recursive: true, purpose });
    }
    folders
}

fn server_source(f: &EmulatorAssetFolder) -> String {
    format!("{}/{}", emulator_assets::server_assets(), f.source)
}

fn gamedata_target(f: &EmulatorAssetFolder) -> String {
    format!("{}/{}", octane::gamedata(), f.target)
}

fn local_source(local: &Path, f: &EmulatorAssetFolder) -> PathBuf {
    let mut path = local.to_path_buf();
    for part in f.source.split('/') {
        path.push(part);
    }
    path
}

fn find(directory: &str, f: &EmulatorAssetFolder, source_filter: bool) -> String {
    let q = shell_quote(directory);
    format!(
        "[ -d {q} ] && (cd {q} && find . {}-type f {}-printf '%P\\n')",
        if f.recursive { "" } else { "-maxdepth 1 " },
        if source_filter { format!("-name {} ! -iname 'README*' ", shell_quote(&f.pattern)) } else { String::new() }
    )
}

fn usable(name: &str) -> bool {
    !name.is_empty() && !name.split('/').any(|p| p.starts_with('.')) && !name.chars().any(char::is_control)
}

fn local_files(local: &Path, f: &EmulatorAssetFolder) -> Vec<String> {
    let root = local_source(local, f);
    let mut files = Vec::new();
    let mut pending = std::collections::VecDeque::from([root.clone()]);
    while let Some(directory) = pending.pop_front() {
        let Ok(entries) = std::fs::read_dir(&directory) else { continue };
        for entry in entries.flatten() {
            let path = entry.path();
            if path.is_dir() {
                if f.recursive {
                    pending.push_back(path);
                }
                continue;
            }
            let file_name = entry.file_name().to_string_lossy().into_owned();
            if !matches_pattern(&f.pattern, &file_name) || file_name.to_ascii_uppercase().starts_with("README") {
                continue;
            }
            let Ok(relative) = path.strip_prefix(&root) else { continue };
            let name = relative.to_string_lossy().replace('\\', "/");
            if usable(&name) {
                files.push(name);
            }
        }
    }
    files
}

fn parse_lists(output: &str) -> HashMap<String, HashSet<String>> {
    let mut lists: HashMap<String, HashSet<String>> = HashMap::new();
    let mut current: Option<String> = None;
    for line in output.split('\n') {
        let text = line.trim_end_matches('\r');
        if let Some(key) = text.strip_prefix("== ") {
            lists.insert(key.to_string(), HashSet::new());
            current = Some(key.to_string());
        } else if let Some(key) = &current {
            if usable(text) {
                lists.get_mut(key).unwrap().insert(text.to_string());
            }
        }
    }
    lists
}

/// What each folder misses in gamedata. (emulator_assets::inspect, for `local`)
async fn inspect_assets(server: &dyn Server, local: &Path) -> anyhow::Result<Vec<EmulatorAssetState>> {
    let folders = folders_in(local);
    let mut script = folders
        .iter()
        .enumerate()
        .map(|(i, f)| format!("echo '== S{i}'; {}\necho '== T{i}'; {}", find(&server_source(f), f, true), find(&gamedata_target(f), f, false)))
        .collect::<Vec<_>>()
        .join("\n");
    script.push_str("\ntrue");
    let result = server.run_as_root(&script).await?;
    let lists = parse_lists(&result.output);

    let empty = HashSet::new();
    let mut states = Vec::new();
    for (i, f) in folders.into_iter().enumerate() {
        let server_files = lists.get(&format!("S{i}")).unwrap_or(&empty);
        let target_files = lists.get(&format!("T{i}")).unwrap_or(&empty);
        let local_set: HashSet<String> = local_files(local, &f).into_iter().collect();
        let total = server_files.union(&local_set).count();
        let from_server: BTreeSet<&String> = server_files.iter().filter(|n| !target_files.contains(*n)).collect();
        let from_installer: BTreeSet<&String> = local_set.iter().filter(|n| !server_files.contains(*n) && !target_files.contains(*n)).collect();
        states.push(EmulatorAssetState {
            folder: f,
            total,
            from_server: from_server.into_iter().cloned().collect(),
            from_installer: from_installer.into_iter().cloned().collect(),
        });
    }
    Ok(states)
}

const COPY: &str = r#"src={{src}}
dst={{dst}}
install -d -m 2775 -o www-data -g www-data "$dst"
n=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  if [ ! -e "$dst/$f" ]; then
    install -d -m 2775 -o www-data -g www-data "$(dirname "$dst/$f")"
    install -m 664 -o www-data -g www-data "$src/$f" "$dst/$f"
    n=$((n + 1))
  fi
done <<'ALL_IN_1_FILES'
{{files}}
ALL_IN_1_FILES
echo "  {{name}}: $n copied from $src to $dst"
echo "ADDED=$n""#;

const UPLOAD: &str = r#"set -e
install -d -m 2775 -o www-data -g www-data {{target}}
# --skip-old-files keeps files that are already there.
tar -x -f - -C {{target}} --no-same-owner --no-same-permissions --skip-old-files
find {{target}} -user root -exec chown www-data:www-data {} +
echo OK"#;

/// A tar header field in octal with a closing NUL.
fn octal(field: &mut [u8], value: u64) {
    let digits = field.len() - 1;
    let text = format!("{value:0digits$o}");
    let bytes = text.as_bytes();
    field[..digits].copy_from_slice(&bytes[bytes.len().saturating_sub(digits)..]);
    field[digits] = 0;
}

fn tar_header(name: &[u8], size: u64, mtime: u64, kind: u8) -> [u8; 512] {
    let mut h = [0u8; 512];
    let n = name.len().min(100);
    h[..n].copy_from_slice(&name[..n]);
    octal(&mut h[100..108], 0o644);
    octal(&mut h[108..116], 0);
    octal(&mut h[116..124], 0);
    octal(&mut h[124..136], size);
    octal(&mut h[136..148], mtime);
    h[148..156].fill(b' ');
    h[156] = kind;
    h[257..263].copy_from_slice(b"ustar\0");
    h[263..265].copy_from_slice(b"00");
    let sum: u64 = h.iter().map(|&b| u64::from(b)).sum();
    let text = format!("{sum:06o}");
    h[148..154].copy_from_slice(&text.as_bytes()[text.len() - 6..]);
    h[154] = 0;
    h[155] = b' ';
    h
}

/// "<length> key=value\n", the length counting itself.
fn pax_record(key: &str, value: &str) -> String {
    let rest = format!(" {key}={value}\n");
    let mut length = rest.len() + 1;
    while length.to_string().len() + rest.len() != length {
        length = length.to_string().len() + rest.len();
    }
    format!("{length}{rest}")
}

fn pad(out: &mut Vec<u8>) {
    let rest = (512 - out.len() % 512) % 512;
    out.extend(std::iter::repeat_n(0u8, rest));
}

/// A ustar archive of `(file, name in the archive)`, with a PAX header for names longer than 100 bytes.
fn tar_archive(items: &[(PathBuf, String)]) -> std::io::Result<Vec<u8>> {
    let mut out = Vec::new();
    for (path, name) in items {
        let data = std::fs::read(path)?;
        let mtime = std::fs::metadata(path)
            .and_then(|m| m.modified())
            .ok()
            .and_then(|m| m.duration_since(std::time::UNIX_EPOCH).ok())
            .map(|d| d.as_secs())
            .unwrap_or(0);
        if name.len() > 100 {
            let record = pax_record("path", name);
            out.extend_from_slice(&tar_header(b"././@PaxHeader", record.len() as u64, 0, b'x'));
            out.extend_from_slice(record.as_bytes());
            pad(&mut out);
        }
        out.extend_from_slice(&tar_header(name.as_bytes(), data.len() as u64, mtime, b'0'));
        out.extend_from_slice(&data);
        pad(&mut out);
    }
    out.extend_from_slice(&[0u8; 1024]);
    Ok(out)
}

/// Adds the missing files without replacing any and returns how many were added. (emulator_assets::install, for `local`)
async fn install_assets(server: &dyn Server, local: &Path, log: Log<'_>) -> anyhow::Result<i64> {
    let states = inspect_assets(server, local).await?;
    let mut added = 0i64;

    // Copy what the server's Polaris clone has, so those files need no upload.
    let copies = states
        .iter()
        .filter(|s| !s.from_server.is_empty())
        .map(|s| {
            fill(
                COPY,
                &[
                    ("src", &shell_quote(&server_source(&s.folder))),
                    ("dst", &shell_quote(&gamedata_target(&s.folder))),
                    ("name", &s.folder.name),
                    ("files", &s.from_server.join("\n")),
                ],
            )
        })
        .collect::<Vec<_>>()
        .join("\n");
    if !copies.is_empty() {
        let copied = std::sync::atomic::AtomicI64::new(0);
        let on_line = |line: &str| {
            if let Some(n) = line.trim().strip_prefix("ADDED=") {
                copied.fetch_add(n.parse::<i64>().unwrap_or(0), std::sync::atomic::Ordering::Relaxed);
            } else {
                log(line);
            }
        };
        script::run(server, &script::with_prelude(&copies), &on_line, "Copying the emulator's assets failed", None).await?;
        added += copied.load(std::sync::atomic::Ordering::Relaxed);
    }

    // Upload the rest, including everything when there is no clone, from the installer's Assets.
    for state in states.iter().filter(|s| !s.from_installer.is_empty()) {
        let target_path = gamedata_target(&state.folder);
        let script = fill(UPLOAD, &[("target", &shell_quote(&target_path))]);
        let root = local_source(local, &state.folder);
        let items: Vec<(PathBuf, String)> = state
            .from_installer
            .iter()
            .map(|name| {
                let mut path = root.clone();
                for part in name.split('/') {
                    path.push(part);
                }
                (path, name.clone())
            })
            .collect();
        let archive = tokio::task::spawn_blocking(move || tar_archive(&items)).await??;
        let result = server.run_as_root_with_input(&script, Box::new(std::io::Cursor::new(archive))).await?;
        if !server.is_dry_run() && (!result.succeeded() || !result.output.contains("OK")) {
            fail!("Uploading {} failed: {}", state.folder.name.to_lowercase(), format!("{}{}", result.output, result.error).trim());
        }
        log(&format!("    {}: {} uploaded from the installer to {target_path}", state.folder.name, state.from_installer.len()));
        added += state.from_installer.len() as i64;
    }

    if added > 0 {
        let p = octane::PERMISSIONS_SCRIPT;
        script::run(server, &format!("if [ -x {p} ]; then {p} >/dev/null; fi"), log, "Setting the permissions failed", None).await?;
    }
    if added == 0 {
        log("    Nothing missing.");
    }
    Ok(added)
}

#[derive(Clone)]
struct AssetRow {
    name: String,
    purpose: String,
    target: String,
    missing: String,
    has_missing: bool,
}

fn asset_rows(states: &[EmulatorAssetState]) -> Vec<AssetRow> {
    states
        .iter()
        .map(|state| {
            let missing = if state.total == 0 {
                t("no source").to_string()
            } else if state.missing() == 0 {
                tf("none ({total} files)", &[("total", &state.total.to_string())])
            } else {
                let mut text = tf("{missing} of {total}", &[("missing", &state.missing().to_string()), ("total", &state.total.to_string())]);
                if !state.from_installer.is_empty() {
                    text.push_str(&tf(" ({count} from the installer)", &[("count", &state.from_installer.len().to_string())]));
                }
                text
            };
            AssetRow {
                name: state.folder.name.clone(),
                purpose: state.folder.purpose.clone(),
                target: format!("gamedata/{}/", state.folder.target),
                missing,
                has_missing: state.missing() > 0,
            }
        })
        .collect()
}

#[derive(Default)]
pub struct EmulatorAssetsStep {
    rows: Mutex<Vec<AssetRow>>,
    summary: Mutex<Option<String>>,
}

impl EmulatorAssetsStep {
    fn summary(&self) -> Option<String> {
        self.summary.lock().unwrap().clone()
    }

    fn show_states(&self, states: &[EmulatorAssetState]) {
        *self.rows.lock().unwrap() = asset_rows(states);
    }
}

const PRELOAD_IMAGES: [&str; 3] = ["loading_icon.png", "clear_icon.png", "big_arrow.png"];

#[async_trait]
impl InstallStep for EmulatorAssetsStep {
    fn title(&self) -> String {
        t("Images").into()
    }

    fn caption(&self) -> String {
        t("From Polaris").into()
    }

    fn install_label(&self) -> String {
        t("Copy the missing files").into()
    }

    fn rerun_label(&self) -> String {
        t("Check again").into()
    }

    fn intro(&self) -> Intro {
        let gamedata = octane::gamedata();
        let g = [("gamedata", gamedata.as_str())];
        Intro {
            icon: Icon::Server,
            heading: t("The emulator's images and group badge parts").into(),
            description: format!(
                "{}{}",
                tf(
                    "The client images and group badge parts that are not in the Habbo downloads: Polaris ships them in {assets}, and the installer has them in Assets/ next to its executable. ",
                    &[("assets", &emulator_assets::server_assets())],
                ),
                t("Octane needs them: when its preload images (loading_icon, clear_icon, big_arrow) are missing the client does not start, and the emulator logs \"invalid SSO ticket\" for every login.")
            ),
            bullets: vec![
                tf("Assets/images → {gamedata}/images (the preload images, wallet, navigator and catalogue extras).", &g),
                tf("Assets/images/reception → {gamedata}/c_images/reception (the hotel view in Octane's ui-config).", &g),
                tf("Assets/badgeparts → {gamedata}/c_images/groupbadge/badgeparts (group badges, for Octane and the emulator's badge imager).", &g),
                tf("Every other folder in the installer's Assets goes to the same path in gamedata: Assets/c_images (talent, Habbo Way, SnowStorm ...) → {gamedata}/c_images.", &g),
                t("Copies from the emulator's clone on the server first; what it does not have is uploaded from the installer's Assets folder (put your own extra images there too).").into(),
                t("Only adds missing files: images you changed or added yourself stay as they are.").into(),
            ],
            footnote: Some(t("Install New Server does this in the Octane step and Tools → Update emulator after every pull; run it here for a server installed before that.").into()),
        }
    }

    fn checks(&self) -> Vec<String> {
        vec![t("Compare with gamedata").into(), t("Copy the missing files").into()]
    }

    fn options(&self, ui: &mut egui::Ui, palette: &Palette) {
        let rows = self.rows.lock().unwrap().clone();
        if rows.is_empty() {
            return;
        }
        ui.add_space(2.0);
        egui::Grid::new("emulator_assets_states").num_columns(3).spacing(egui::vec2(16.0, 6.0)).show(ui, |ui| {
            for heading in ["Folder", "Server", "Missing"] {
                small(ui, t(heading), 11.5, palette.muted, true);
            }
            ui.end_row();
            for row in &rows {
                ui.vertical(|ui| {
                    ui.spacing_mut().item_spacing.y = 1.0;
                    ui.label(RichText::new(t(&row.name)).size(13.0).strong());
                    ui.set_max_width(380.0);
                    ui.add(egui::Label::new(RichText::new(t(&row.purpose)).size(11.5).color(palette.subtle)).wrap());
                });
                small(ui, &row.target, 12.0, palette.muted, false);
                small(ui, &row.missing, 12.0, if row.has_missing { palette.text } else { palette.subtle }, row.has_missing);
                ui.end_row();
            }
        });
    }

    async fn detect(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<bool> {
        let local = local_assets().await?;
        let states = inspect_assets(server.as_ref(), &local).await?;
        self.show_states(&states);
        if states.iter().all(|s| s.total == 0) {
            ui.detected(
                ResultKind::Warning,
                t("No assets to copy"),
                &tf(
                    "Neither the emulator's clone ({server}) nor the installer ({local}) has an Assets folder.",
                    &[("server", &emulator_assets::server_assets()), ("local", &emulator_assets::local_assets().display().to_string())],
                ),
            );
            return Ok(false);
        }
        let missing: usize = states.iter().map(|s| s.missing()).sum();
        if missing == 0 {
            let total: usize = states.iter().map(|s| s.total).sum();
            ui.detected(
                ResultKind::Success,
                t("gamedata has all of the emulator's assets"),
                &tf("{count} files, all in place.", &[("count", &total.to_string())]),
            );
            return Ok(true);
        }
        let preload = states.first().is_some_and(|s| s.from_server.iter().chain(&s.from_installer).any(|f| PRELOAD_IMAGES.contains(&f.as_str())));
        ui.detected(
            if preload { ResultKind::Warning } else { ResultKind::Info },
            &tf("{count} files are missing in gamedata", &[("count", &missing.to_string())]),
            if preload {
                t("Among them are Octane's preload images: without them the client does not start (\"invalid SSO ticket\" in the emulator's log). Copy them now.")
            } else {
                t("Copy them now; nothing in gamedata is replaced.")
            },
        );
        Ok(false)
    }

    async fn run(&self, server: Arc<dyn Server>, ui: StepUi) -> anyhow::Result<StepOutcome> {
        let log = ui.logger();
        let server = server.as_ref();
        log(&format!("> Comparing {} and the installer's Assets with {} ...", emulator_assets::server_assets(), octane::gamedata()));
        let local = local_assets().await?;
        let states = inspect_assets(server, &local).await?;
        for state in &states {
            log(&format!(
                "  {}: {} of {} missing ({} in the emulator's clone, {} only in the installer)",
                state.folder.name,
                state.missing(),
                state.total,
                state.from_server.len(),
                state.from_installer.len()
            ));
        }
        let missing: usize = states.iter().map(|s| s.missing()).sum();
        ui.done(&tf("{count} missing", &[("count", &missing.to_string())]));

        let added = install_assets(server, &local, &log).await?;
        ui.done(&tf("{count} added", &[("count", &added.to_string())]));
        let after = inspect_assets(server, &local).await?;
        self.show_states(&after);
        let summary = if added == 0 {
            t("gamedata already had the emulator's images and badge parts.").to_string()
        } else {
            tf("{count} of the emulator's images and badge parts copied into gamedata.", &[("count", &added.to_string())])
        };
        *self.summary.lock().unwrap() = Some(summary.clone());
        Ok(ui.succeeded(
            if added == 0 { t("Nothing was missing") } else { t("The emulator's assets are in gamedata") },
            &format!("{summary} {}", t("Reload the hotel (Ctrl+F5) to start the client with them.")),
        ))
    }
}

pub fn wizard(shared: &Shared) -> Wizard {
    let sync = Arc::new(AssetSyncStep::default());
    let texts = Arc::new(UiTextsStep::default());
    let images = Arc::new(EmulatorAssetsStep::default());
    let (s, x, i) = (sync.clone(), texts.clone(), images.clone());
    Wizard::new(
        shared.runtime.clone(),
        &shared.ctx,
        &shared.dialogs,
        vec![sync, texts, images],
        false,
        Arc::new(move |info| super::finished::deploy(info.site, vec![s.summary(), x.summary(), i.summary()], info.steps)),
        t("Assets on the server"),
        t("Completed"),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sizes_and_rates_as_the_status_page() {
        assert_eq!(bytes(300 * 1024), "300 KB");
        assert_eq!(bytes(15 << 20), "15 MB");
        assert_eq!(bytes(3 << 29), "1.5 GB");
        assert_eq!(rate(500.0), "500 B/s");
        assert_eq!(rate(2048.0), "2.0 KB/s");
        assert_eq!(rate(2.5 * 1024.0 * 1024.0), "2.50 MB/s");
    }

    #[test]
    fn languages_keep_the_choice() {
        let mut form = TextsForm::default();
        assert_eq!(form.codes[form.selected.unwrap()], "en");
        form.set_languages(vec!["de".into(), "en".into(), "nl".into()], Some("nl"));
        assert_eq!(form.codes[form.selected.unwrap()], "nl");
        form.set_languages(vec!["de".into(), "nl".into()], None);
        assert_eq!(form.codes[form.selected.unwrap()], "nl");
        form.set_languages(vec!["fr".into()], Some("xx"));
        assert_eq!(form.selected, Some(0));
    }

    #[test]
    fn tar_headers_are_valid() {
        let h = tar_header(b"images/loading_icon.png", 1234, 0, b'0');
        let mut copy = h;
        copy[148..156].fill(b' ');
        let sum: u64 = copy.iter().map(|&b| u64::from(b)).sum();
        let stored = std::str::from_utf8(&h[148..154]).unwrap();
        assert_eq!(u64::from_str_radix(stored, 8).unwrap(), sum);
        assert_eq!(std::str::from_utf8(&h[124..135]).unwrap(), format!("{:011o}", 1234));
        assert_eq!(&h[257..263], b"ustar\0");
        assert_eq!(pax_record("path", "a"), "9 path=a\n");
    }

    #[test]
    fn archives_hold_the_files() {
        let dir = tempfile::tempdir().unwrap();
        let file = dir.path().join("a.png");
        std::fs::write(&file, b"hello").unwrap();
        let long = format!("{}/b.png", "x".repeat(120));
        let archive = tar_archive(&[(file.clone(), "a.png".into()), (file, long)]).unwrap();
        assert_eq!(archive.len() % 512, 0);
        assert_eq!(&archive[..5], b"a.png");
        assert_eq!(&archive[512..517], b"hello");
        assert_eq!(archive[1024 + 156], b'x');
    }

    #[test]
    fn packed_images_and_your_own_are_merged() {
        let dir = tempfile::tempdir().unwrap();
        let beside = dir.path().join("beside");
        std::fs::create_dir_all(beside.join("extra")).unwrap();
        std::fs::write(beside.join("extra").join("mine.png"), b"x").unwrap();
        let mut zip = zip::ZipWriter::new(std::io::Cursor::new(Vec::new()));
        zip.start_file("images/loading_icon.png", zip::write::SimpleFileOptions::default()).unwrap();
        std::io::Write::write_all(&mut zip, b"png").unwrap();
        let packed = zip.finish().unwrap().into_inner();
        let into = dir.path().join("Assets");
        prepare_assets(&packed, &beside, &into).unwrap();
        assert!(into.join("images").join("loading_icon.png").is_file());
        assert!(into.join("extra").join("mine.png").is_file());
        let folders = folders_in(&into);
        assert_eq!(folders.len(), 4);
        assert_eq!(folders[3].source, "extra");
        assert_eq!(local_files(&into, &folders[0]), vec!["loading_icon.png".to_string()]);
    }

    #[test]
    fn the_binary_carries_the_client_images() {
        let archive = zip::ZipArchive::new(std::io::Cursor::new(CLIENT_ASSETS)).unwrap();
        assert!(archive.file_names().any(|n| n.starts_with("images/")));
    }

    #[test]
    fn server_lists_are_parsed() {
        let lists = parse_lists("== S0\na.png\n.hidden\n== T0\r\nb.png\r\n");
        assert_eq!(lists["S0"].len(), 1);
        assert!(lists["T0"].contains("b.png"));
    }
}
