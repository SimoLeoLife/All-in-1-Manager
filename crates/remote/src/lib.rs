//! All-in-1 Installer remote side: the SSH connection and, per install step, the scripts it runs on the server as
//! root. Every module mirrors one file of the C# installer's `SourceCode/Remote`, with the same commands in the same
//! order; `tests/golden` holds what the C# code sends, and the parity tests compare with it.

pub mod recorder;
pub mod script;
pub mod server;

pub mod nginx;

pub use server::{CommandResult, Log, OsInfo, Result, Server, ServerError, ServerLogin};
