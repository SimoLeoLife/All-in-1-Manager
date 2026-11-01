//! (port in progress) The completed pages (FinishedView.cs): the summary of the steps and what to do next.
//!
//! PORTING: these signatures are what the wizards call; keep them and fill in the pages.

use aio_core::DatabaseCredentials;
use aio_remote::crowdsec::CrowdSecResult;
use aio_remote::fail2ban::Fail2BanSettings;
use aio_remote::gitea::{GiteaResult, GiteaSettings};
use aio_remote::web_access::WebAccessSettings;

use crate::gui::wizard::{FinishInfo, FinishPage, StepSummary};

pub type Steps = Vec<(String, StepSummary)>;

fn pending() -> Box<dyn FinishPage> {
    Box::new(super::pending::PendingFinish)
}

/// Install New Server: the summary, the WinSCP upload and where the assets go. (Build)
pub fn server(info: FinishInfo) -> Box<dyn FinishPage> {
    let _ = info;
    pending()
}

/// The AtomCMS addon: the web installer key, the paths it already has, and admin rights. (BuildAtomCms)
pub fn atom_cms(site: Option<WebAccessSettings>, installation_key: Option<String>, installation_completed: bool, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (site, installation_key, installation_completed, steps);
    pending()
}

/// (BuildCustomCms)
pub fn custom_cms(
    site: Option<WebAccessSettings>,
    name: Option<String>,
    root: Option<String>,
    php: bool,
    credentials: Option<DatabaseCredentials>,
    host: String,
    steps: Steps,
) -> Box<dyn FinishPage> {
    let _ = (site, name, root, php, credentials, host, steps);
    pending()
}

/// (BuildEpicNext)
pub fn epicnext(site: Option<WebAccessSettings>, atom_tables: bool, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (site, atom_tables, steps);
    pending()
}

/// (BuildGitea)
pub fn gitea(settings: Option<GiteaSettings>, result: Option<GiteaResult>, login_file: String, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (settings, result, login_file, steps);
    pending()
}

/// (BuildRemoveGitea)
pub fn remove_gitea(domain: Option<String>, data_deleted: bool, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (domain, data_deleted, steps);
    pending()
}

/// (BuildFail2Ban)
pub fn fail2ban(settings: Option<Fail2BanSettings>, ssh_ports: Vec<i64>, client_ip: Option<String>, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (settings, ssh_ports, client_ip, steps);
    pending()
}

/// (BuildRemoveFail2Ban)
pub fn remove_fail2ban(steps: Steps) -> Box<dyn FinishPage> {
    let _ = steps;
    pending()
}

/// (BuildCrowdSec)
pub fn crowdsec(site: Option<WebAccessSettings>, name: Option<String>, enrolled_now: bool, result: Option<CrowdSecResult>, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (site, name, enrolled_now, result, steps);
    pending()
}

/// (BuildRemoveCrowdSec)
pub fn remove_crowdsec(data_deleted: bool, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (data_deleted, steps);
    pending()
}

/// (BuildRemoveCms)
pub fn remove_cms(site: Option<WebAccessSettings>, removed: Option<String>, files_deleted: bool, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (site, removed, files_deleted, steps);
    pending()
}

/// Deploy Assets: what each of its steps did. (BuildDeploy)
pub fn deploy(site: Option<WebAccessSettings>, summaries: Vec<Option<String>>, steps: Steps) -> Box<dyn FinishPage> {
    let _ = (site, summaries, steps);
    pending()
}
