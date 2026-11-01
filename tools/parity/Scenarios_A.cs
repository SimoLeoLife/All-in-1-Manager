#if GROUP_A
using AllInOneInstaller.Configuration;
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group A: PhpInstaller, MariaDbInstaller, DevToolsInstaller, GitCloneInstaller.
internal static partial class Program
{
    private static Func<string, string, string?, CommandResult> Answer(int exit, string output, string error = "") =>
        (_, _, _) => new CommandResult(exit, output, error);

    private static void Result(string text) => Recorder.Log.Append("result: ").Append(text).Append('\n');

    private static async Task GroupA()
    {
        // PhpInstaller
        await Capture("php_inspect", () => PhpInstaller.InspectAsync(Server()));
        await Capture("php_inspect_failed", () => PhpInstaller.InspectAsync(Server()), Answer(1, "", " permission denied \n"));
        await Capture("php_add_repository", () => PhpInstaller.AddRepositoryAsync(Server(), Log));
        await Capture("php_install_packages", async () => Result(await PhpInstaller.InstallPackagesAsync(Server(), Log)),
            Answer(0, "  installing\nVERSION=8.5.13\n"));
        await Capture("php_check_extensions", () => PhpInstaller.CheckExtensionsAsync(Server(), Log));
        await Capture("php_check_extensions_failed", () => PhpInstaller.CheckExtensionsAsync(Server(), Log),
            Answer(1, "  mysqli ok\nERROR: These PHP extensions are not loaded: gd\n"));
        await Capture("php_start_and_connect", async () => Result((await PhpInstaller.StartAndConnectAsync(Server(), Log)).ToString()),
            Answer(0, "NGINX=yes\n"));
        string[] versions = ["8.5.12", "8.5.13-1+ubuntu24.04.1+deb.sury.org+1", "8.6.0", "8.5.12.1", "8.5.11", "8.5", "8", "", "8.5.", "8.5.1.2.3", "9.0"];
        Text("php_is_at_least_minimum", string.Join("\n", versions.Select(v => $"{v}={PhpInstaller.IsAtLeastMinimum(v)}")) + "\n");

        // MariaDbInstaller
        var credentials = new DatabaseCredentials("192.0.2.10", 3306, "habbo", "root", "R00t'pass\\1", "cms", "Cms\"pass2");
        var odd = new DatabaseCredentials("192.0.2.10", 3306, "hab`bo", "ad'min", "pw1", "c\"ms", "pw2");
        await Capture("mariadb_inspect", () => MariaDbInstaller.InspectAsync(Server()));
        await Capture("mariadb_inspect_failed", () => MariaDbInstaller.InspectAsync(Server()), Answer(1, "", "boom"));
        await Capture("mariadb_add_repository", () => MariaDbInstaller.AddRepositoryAsync(Server(), Log));
        await Capture("mariadb_backup", async () => Result(await MariaDbInstaller.BackupAsync(Server(), Log)),
            Answer(0, "BACKUP=/root/mariadb-backup-1.sql.gz\n"));
        await Capture("mariadb_install", async () => Result(await MariaDbInstaller.InstallAsync(Server(), Log)),
            Answer(0, "VERSION=12.3.2+maria~ubu2404\n"));
        await Capture("mariadb_secure_and_listen", () => MariaDbInstaller.SecureAndListenAsync(Server(), Log));
        await Capture("mariadb_allow_tunnel_root_login", () => MariaDbInstaller.AllowTunnelRootLoginAsync(Server(), credentials, Log));
        await Capture("mariadb_reset_root_password", () => MariaDbInstaller.ResetRootPasswordAsync(Server(), credentials, Log));
        await Capture("mariadb_read_accounts", async () =>
        {
            var a = await MariaDbInstaller.ReadAccountsAsync(Server());
            Result($"{a.Database}|{a.CmsUser}|{a.CmsPassword}|{a.EmulatorUser}|{a.EmulatorPassword}");
        }, Answer(0, "DB=habbo\nCMSUSER=cms\nCMSPASS=secret\nEMUUSER=\nEMUPASS=pw\n"));
        await Capture("mariadb_create_users", () => MariaDbInstaller.CreateUsersAsync(Server(), credentials, Log));
        await Capture("mariadb_create_users_quoted", () => MariaDbInstaller.CreateUsersAsync(Server(), odd, Log));
        await Capture("mariadb_test_login_root", async () =>
            Result(await MariaDbInstaller.TestLoginAsync(Server(), "root", "R00t'pass\\1", null, local: false, Log) ?? "null"));
        await Capture("mariadb_test_login_cms_local", async () =>
            Result(await MariaDbInstaller.TestLoginAsync(Server(), "cms", "Cms\"pass2", "habbo", local: true, Log) ?? "null"));
        await Capture("mariadb_test_login_local_only", async () =>
            Result(await MariaDbInstaller.TestLoginAsync(Server(), "cms", "pw", "hab`bo", local: true, Log, remote: false) ?? "null"));
        await Capture("mariadb_test_login_failed", async () =>
            Result(await MariaDbInstaller.TestLoginAsync(Server(), "cms", "pw", "habbo", local: true, Log) ?? "null"),
            Answer(1, "ERROR: Login as cms (--protocol=socket) failed: denied\n"));
        await Capture("mariadb_test_local_root", async () =>
            Result(await MariaDbInstaller.TestLocalRootAsync(Server(), Log) ?? "null"));
        await Capture("mariadb_test_local_root_failed", async () =>
            Result(await MariaDbInstaller.TestLocalRootAsync(Server(), Log) ?? "null"), Answer(2, "some output\n"));
        Text("mariadb_local_root_password_sql", MariaDbInstaller.LocalRootPasswordSql(credentials) + "\n");

        // DevToolsInstaller
        await Capture("dev_tools_inspect", () => DevToolsInstaller.InspectAsync(Server()));
        await Capture("dev_tools_add_node_source", () => DevToolsInstaller.AddNodeSourceAsync(Server(), Log));
        await Capture("dev_tools_install_node", async () => Result(await DevToolsInstaller.InstallNodeAsync(Server(), Log)),
            Answer(0, "VERSION=v26.1.0\n"));
        await Capture("dev_tools_install_composer", () => DevToolsInstaller.InstallComposerAsync(Server(), Log));
        await Capture("dev_tools_install_yarn", () => DevToolsInstaller.InstallYarnAsync(Server(), Log));
        await Capture("dev_tools_versions", async () =>
        {
            var v = await DevToolsInstaller.VersionsAsync(Server(), Log);
            Result($"{v.Node}|{v.Npm}|{v.Yarn}|{v.Composer}|{string.Join(",", v.Shadowing)}");
        }, Answer(0, "NODE=v26.1.0\nNPM=11.0.0\nYARN=4.9.1\nCOMPOSER=2.8.10\nSHADOWING=/usr/local/bin/node /usr/local/bin/npm \n"));
        await Capture("dev_tools_versions_missing", () => DevToolsInstaller.VersionsAsync(Server(), Log),
            Answer(0, "NODE=v26.1.0\nNPM=\n"));

        // GitCloneInstaller
        const string url = "https://github.com/ObjectRetros/atomcms.git";
        const string target = "/var/www/it's here";
        await Capture("git_clone_inspect", async () =>
        {
            CloneState s = await GitCloneInstaller.InspectAsync(Server(), url, target);
            Result($"{s.Kind}|{s.Origin}|{s.Branch}|{s.Commit}|{s.ChangedFiles}|{s.Behind}");
        }, Answer(0, "origin=git@github.com:objectretros/atomcms\nkind=repo\nbranch=main\ncommit=abc1234 2026-01-01 Fix\nchanged=      3\nbehind=2\n"));
        await Capture("git_clone_inspect_other", async () =>
        {
            CloneState s = await GitCloneInstaller.InspectAsync(Server(), url, target);
            Result($"{s.Kind}|{s.Origin}|{s.Branch}|{s.Commit}|{s.ChangedFiles}|{s.Behind}");
        }, Answer(0, "origin=https://github.com/someone/else\nkind=repo\nchanged=x\n"));
        await Capture("git_clone_move_aside", async () => Result(await GitCloneInstaller.MoveAsideAsync(Server(), target, Log)),
            Answer(0, "BACKUP=/var/www/x.bak-1\n"));
        await Capture("git_clone_clone_default", async () => Result(await GitCloneInstaller.CloneAsync(Server(), url, target, null, Log)),
            Answer(0, "COMMIT=main abc1234 2026-01-01 Fix\n"));
        await Capture("git_clone_clone_branch", () => GitCloneInstaller.CloneAsync(Server(), url, target, "De'v", Log));
        await Capture("git_clone_update_default", () => GitCloneInstaller.UpdateAsync(Server(), target, null, Log));
        await Capture("git_clone_update_branch", () => GitCloneInstaller.UpdateAsync(Server(), target, "dev", Log));
        await Capture("git_clone_update_failed", () => GitCloneInstaller.UpdateAsync(Server(), target, null, Log),
            Answer(1, " M README.md\nERROR: Files tracked by git were changed in /x. Commit or undo those changes first, so they are not lost.\n"));
    }
}
#endif
