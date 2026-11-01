//! Fail2Ban: sshd and NGINX jails, whitelist and bans. (Fail2BanInstaller.cs)
//!
//! PORTING: the items above the line are used by other modules; keep their names and signatures.

/// What is wrong with a whitelist entry (an IP address or range), or None. (EntryProblem)
pub fn entry_problem(entry: &str) -> Option<String> {
    let _ = entry;
    todo!("Fail2BanInstaller.EntryProblem")
}

// ---------------------------------------------------------------------------------------------------------------
