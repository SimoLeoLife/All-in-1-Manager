#if GROUP_F
using AllInOneInstaller.Configuration;
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group F: ServerTools, PlusImport, ServerStatus, EmulatorMonitor, ServerUsers (+ the Debug page's follow scripts).
internal static partial class Program
{
    /// <summary>Answers commands containing a needle with that output (exit 0); others with exit 0 and nothing.</summary>
    private static Func<string, string, string?, CommandResult> AnswerF(params (string Needle, string Output)[] pairs) =>
        (_, command, _) => pairs.FirstOrDefault(p => command.Contains(p.Needle)) is { Needle: not null } hit
            ? new CommandResult(0, hit.Output, "")
            : new CommandResult(0, "", "");

    private static Func<string, string, string?, CommandResult> ReplyF(int exit, string output, string error = "") =>
        (_, _, _) => new CommandResult(exit, output, error);

    private static void ResultF(string text) => Recorder.Log.Append("result: ").Append(text).Append('\n');

    private static string N(string? value) => value ?? "-";
    private static string B(bool value) => value ? "yes" : "no";

    // DebugView.Sources, copied verbatim from SourceCode/App/Professional/Views/DebugView.cs (the App is not part of
    // this harness).
    private sealed record DebugSourceF(string Name, string Description, string Follows, string Command);

    private static readonly DebugSourceF[] DebugSourcesF =
    [
        new("Emulator", $"{EmulatorInstaller.LogDirectory}/emulator.log: what Polaris prints (startup, errors, wired, players)",
            $"{EmulatorInstaller.LogDirectory}/emulator.log",
            $"tail -n 200 -F {EmulatorInstaller.LogDirectory}/emulator.log"),
        new("NGINX errors", $"{HotelSite.LogDirectory}/error.log and /var/log/nginx/error.log",
            $"{HotelSite.LogDirectory}/error.log, /var/log/nginx/error.log",
            $"tail -n 100 -F {HotelSite.LogDirectory}/error.log /var/log/nginx/error.log"),
        new("NGINX access", $"{HotelSite.LogDirectory}/access.log: every request that reaches the hotel site",
            $"{HotelSite.LogDirectory}/access.log",
            $"tail -n 100 -F {HotelSite.LogDirectory}/access.log"),
        new("Traefik", "docker logs of the traefik container: certificates, routing and connection errors",
            "docker logs traefik",
            "if command -v docker >/dev/null 2>&1 && docker inspect traefik >/dev/null 2>&1; then docker logs -f --tail 200 traefik; " +
            "else echo 'There is no traefik container on this server (Web access step).'; sleep infinity; fi"),
        new("Firewall", $"{HardenInstaller.UfwLog}: connections ufw blocked (BLOCK) or let through and logged",
            $"{HardenInstaller.UfwLog} (or the journal without rsyslog)",
            $"if [ -f {HardenInstaller.UfwLog} ]; then tail -n 200 -F {HardenInstaller.UfwLog}; " +
            "else echo 'No ufw.log (no rsyslog): following the journal instead.'; journalctl -k -f -n 200 -o short-iso | grep --line-buffered UFW; fi"),
        new("Fail2Ban", $"{Fail2BanInstaller.LogFile}: failed SSH logins it counted (Found), bans (Ban) and unbans (Unban)",
            $"{Fail2BanInstaller.LogFile} (or the journal)",
            "if ! command -v fail2ban-client >/dev/null 2>&1; then echo 'Fail2ban is not installed on this server (Addons -> Fail2Ban).'; sleep infinity; " +
            $"elif [ -f {Fail2BanInstaller.LogFile} ]; then tail -n 200 -F {Fail2BanInstaller.LogFile}; " +
            "else echo 'No fail2ban.log: following the journal instead.'; journalctl -u fail2ban -f -n 200 -o short-iso; fi"),
        new("CrowdSec", "docker logs of the crowdsec container: bans it decides, the hub, the console and the bouncer's connections",
            "docker logs crowdsec",
            $"if docker inspect {CrowdSecInstaller.Container} >/dev/null 2>&1; then docker logs -f --tail 200 {CrowdSecInstaller.Container}; " +
            "else echo 'CrowdSec is not installed on this server (Addons -> CrowdSec).'; sleep infinity; fi")
    ];

    private const string StatusOutputF = "cpu cpu  4705 150 1120 16250 520 0 25 0 0 0\ncores 4\nmem MemTotal: 8148108\nmem MemAvailable: 5023412\n" +
        "mem SwapTotal: 2097148\nmem SwapFree: 1048576\nnet 123456789 98765432\nload 0.42 0.31 0.25\nuptime 864123.57\n" +
        "disk / /dev/sda1 81032015872 21474836480 55985274880\ndisk /boot/efi /dev/sda15 109422592 6291456 103131136\n" +
        "user root pts/0 2026-10-31_09:12 203.0.113.7\nuser simone tty1 2026-10-30_18:00\nplayers 12\nemulator yes\n";

    private const string MonitorOutputF = "clk 100\ncores 4\nmachine 2345678\npid 4242\nproc 81234 1500000\nrss 1610612736\nxmx 2 g\nboot 20000\n" +
        "t 4242 S 12 java\nt 4250 R 5400 WiredTickCoordi\nt 4251 S 2100 WiredTickShard-\nt 4260 S 700 C2 CompilerThre\n";

    private const string WiredOutputF = "room 12 3 85 190 2026-10-31T09:12:01 0,2\nroom 7 5 40 160 2026-10-31T09:10:44 1\n" +
        "room 99 1 300 300 2026-10-31T09:13:00\ntotals 9 2\n" +
        "recent 2026-10-31T09:13:00 WARN Slow wired task: shard=1, room=99, took=300ms\n" +
        "recent 2026-10-31T09:13:05 WARN Room 99 has 64 wired tasks waiting\nname 12\tThe Casino\nname 7\tWired lab\n";

    private static async Task GroupF()
    {
        // ServerTools
        await Capture("server_tools_restart_emulator", () => ServerTools.RestartEmulatorAsync(Server(), Log));
        await Capture("server_tools_restart_mariadb", () => ServerTools.RestartMariaDbAsync(Server(), Log));
        await Capture("server_tools_restart_mariadb_failed", () => ServerTools.RestartMariaDbAsync(Server(), Log),
            ReplyF(1, "> Restarting MariaDB\nERROR: MariaDB does not answer after the restart (journalctl -u mariadb).\n"));
        await Capture("server_tools_restart_nginx", () => ServerTools.RestartNginxAsync(Server(), Log));
        await Capture("server_tools_restart_traefik", () => ServerTools.RestartTraefikAsync(Server(), Log));
        await Capture("server_tools_update_traefik", () => ServerTools.UpdateTraefikAsync(Server(), Log));
        await Capture("server_tools_backup_database", async () =>
            {
                DatabaseBackup b = await ServerTools.BackupDatabaseAsync(Server(), Log);
                ResultF($"{b.RemotePath}|{b.Database}|{b.Bytes}");
            },
            AnswerF(("mariadb-dump", "> mariadb-dump habbo (120 tables)\n  4.2M in 3 seconds\nFILE=/var/backups/all-in-1/habbo-20261031-120000.sql.gz\nDB=habbo\nBYTES=4404019\n")));
        await Capture("server_tools_backup_database_failed", () => ServerTools.BackupDatabaseAsync(Server(), Log),
            ReplyF(1, "ERROR: Database habbo does not exist or MariaDB does not run.\n"));
        await Capture("server_tools_import_database", () => ServerTools.ImportDatabaseAsync(Server(), "/var/backups/all-in-1/import-20261031-120000.sql.gz", Log));
        await Capture("server_tools_import_database_quote", () => ServerTools.ImportDatabaseAsync(Server(), "/tmp/it's a dump.sql", Log));
        await Capture("server_tools_update_system", async () => ResultF(B(await ServerTools.UpdateSystemAsync(Server(), Log))));
        await Capture("server_tools_update_system_reboot", async () => ResultF(B(await ServerTools.UpdateSystemAsync(Server(), Log))),
            ReplyF(0, "> apt-get update\n  A reboot is needed\nREBOOT=yes\n"));
        await Capture("server_tools_emulator_version", async () =>
            {
                (string? commit, string? version) = await ServerTools.EmulatorVersionAsync(Server());
                ResultF($"{N(commit)}|{N(version)}");
            },
            ReplyF(0, "commit=a1b2c3d\nversion=Polaris-4.2.0\njava=/usr/lib/jvm/java-25-openjdk-amd64\n"));
        await Capture("server_tools_emulator_version_none", async () =>
            {
                (string? commit, string? version) = await ServerTools.EmulatorVersionAsync(Server());
                ResultF($"{N(commit)}|{N(version)}");
            });
        await Capture("server_tools_java_home", async () => ResultF(await ServerTools.JavaHomeAsync(Server())),
            ReplyF(0, "/usr/lib/jvm/java-25-openjdk-amd64\n"));
        await Capture("server_tools_java_home_missing", async () => ResultF(await ServerTools.JavaHomeAsync(Server())));
        await Capture("server_tools_fix_emulator_owner", () => ServerTools.FixEmulatorOwnerAsync(Server(), Log));
        Text("server_tools_debug_sources", string.Join("", DebugSourcesF.Select(s =>
            $"### {s.Name}\n{s.Description}\n{s.Follows}\n{ServerConnection.FollowScript(s.Command)}\n")));

        // PlusImport
        const string plusOutput = "> Safety backup of habbo first\nSAFETY=/var/backups/all-in-1/habbo-before-plus-import-1.sql.gz\n" +
            "FURNI_EXISTING=5120\nFURNI_NEW=312\nCRACKABLE_WITHOUT_PRIZES=4\nPAGES=180\nCATALOG_ITEMS=7400\n";
        await Capture("plus_import_furniture_only", async () =>
            {
                PlusImportResult r = await PlusImport.ImportAsync(Server(), "/var/backups/all-in-1/plus-import-1.sql.gz", PlusImportMode.FurnitureOnly, Log);
                ResultF($"{r.FurnitureExisting}|{r.FurnitureNew}|{r.CrackablesWithoutPrizes}|{r.Pages}|{r.CatalogItems}|{r.SafetyBackup}");
            },
            ReplyF(0, plusOutput));
        await Capture("plus_import_furniture_and_catalog", async () =>
            {
                PlusImportResult r = await PlusImport.ImportAsync(Server(), "/var/backups/all-in-1/plus-import-1.sql", PlusImportMode.FurnitureAndCatalog, Log);
                ResultF($"{r.FurnitureExisting}|{r.FurnitureNew}|{r.CrackablesWithoutPrizes}|{r.Pages}|{r.CatalogItems}|{r.SafetyBackup}");
            });
        await Capture("plus_import_failed", () => PlusImport.ImportAsync(Server(), "/tmp/x.sql", PlusImportMode.FurnitureOnly, Log),
            ReplyF(1, "ERROR: x.sql is not a PlusEMU dump: it has no furniture table (Polaris and Arcturus call it items_base).\n"));

        // ServerStatus
        await Capture("server_status_sample", async () =>
            {
                StatusSample s = await ServerStatus.SampleAsync(Server());
                ResultF($"{s.CpuTotal}|{s.CpuIdle}|{s.Cores}|{s.MemTotal}|{s.MemAvailable}|{s.SwapTotal}|{s.SwapFree}|{s.ReceivedBytes}|{s.SentBytes}|{s.Load}|{s.UptimeSeconds}|{N(s.PlayersOnline?.ToString())}|{B(s.EmulatorRunning)}");
                foreach (DiskUsage d in s.Disks) ResultF($"disk {d.Mount}|{d.Device}|{d.Total}|{d.Used}|{d.Available}");
                foreach (LoggedInUser u in s.Users) ResultF($"user {u.User}|{u.Terminal}|{u.Since}|{u.From}");
            },
            ReplyF(0, StatusOutputF));
        await Capture("server_status_versions", async () =>
            {
                foreach (RepositoryVersion v in await ServerStatus.VersionsAsync(Server()))
                    ResultF($"{v.Name}|{v.Directory}|{N(v.Branch)}|{N(v.Commit)}|{N(v.Date)}|{N(v.Subject)}|{N(v.Build)}");
            },
            ReplyF(0, "repo0 main|a1b2c3d|2026-10-30 21:15|Fix wired|with a pipe\nrepo1 dev|e4f5a6b|2026-10-29 10:00|Octane update\nrepo3 \njar Polaris-4.2.0\noctane 1.6.3\n"));
        await Capture("server_status_updates", async () =>
            {
                foreach (RepositoryUpdate u in await ServerStatus.UpdatesAsync(Server()))
                    ResultF($"{u.Name}|{u.Directory}|{N(u.Branch)}|{N(u.Target)}|{u.Behind}|{N(u.Newest)}|{B(u.SwitchesBranch)}|{N(u.Counted)}");
            },
            ReplyF(0, "repo0 main|Main|3|9f8e7d6|2026-10-31 08:00|Newest commit\nrepo1 dev||0|\nrepo2 HEAD|main|x|\n"));
        await Capture("server_status_images", async () =>
            {
                foreach (ImageStatus i in await ServerStatus.ImagesAsync(Server()))
                    ResultF($"{i.Name}|{i.Container}|{i.Image}|{N(i.Version)}|{B(i.Running)}|{B(i.NewerOnHub)}|{B(i.NotRestarted)}|{N(i.NewestVersion)}|{N(i.Problem)}");
            },
            ReplyF(0, "image0 true|3.5.3|yes||3.5.4|\nimage2 false|1.6.3||||Docker Hub did not answer\n"));

        // EmulatorMonitor
        await Capture("emulator_monitor_sample", async () =>
            {
                EmulatorSample s = await EmulatorMonitor.SampleAsync(Server());
                ResultF($"{s.Pid}|{s.TicksPerSecond}|{s.Cores}|{s.MachineTicks}|{s.ProcessTicks}|{s.RssBytes}|{s.MaxHeapBytes}|{s.StartedSecondsAgo}");
                foreach (JvmThread t in s.Threads) ResultF($"thread {t.Id}|{t.Name}|{t.State}|{t.Ticks}");
            },
            ReplyF(0, MonitorOutputF));
        await Capture("emulator_monitor_thread_names", async () =>
            {
                foreach (var pair in (await EmulatorMonitor.ThreadNamesAsync(Server(), 4242)).OrderBy(p => p.Key))
                    ResultF($"{pair.Key}|{pair.Value}");
            },
            ReplyF(0, "4250 WiredTickCoordinator\n4251 WiredTickShard-0 \nx bad\n"));
        await Capture("emulator_monitor_wired_log", async () =>
            {
                WiredLogSummary w = await EmulatorMonitor.WiredLogAsync(Server());
                ResultF($"{w.SlowTasks}|{w.DroppedWarnings}");
                foreach (SlowWiredRoom r in w.Rooms) ResultF($"room {r.RoomId}|{N(r.Name)}|{r.Count}|{r.SlowestMs}|{r.TotalMs}|{r.LastSeen}|{r.Shards}");
                foreach (string line in w.Recent) ResultF($"recent {line}");
            },
            ReplyF(0, WiredOutputF));

        // ServerUsers
        await Capture("server_users_list", async () =>
            {
                foreach (ServerUser u in await ServerUsers.ListAsync(Server()))
                    ResultF($"{u.Name}|{u.Uid}|{u.Home}|{u.Shell}|{B(u.InWwwData)}|{B(u.IsSystem)}|{B(u.CanLogIn)}");
            },
            ReplyF(0, "www=emulator,simone\nuser=root:0:/root:/bin/bash\nuser=daemon:1:/usr/sbin:/usr/sbin/nologin\n" +
                      "user=www-data:33:/var/www:/usr/sbin/nologin\nuser=emulator:998:/var/lib/emulator:/usr/sbin/nologin\n" +
                      "user=zoe:1001:/home/zoe:/bin/bash\nuser=simone:1000:/home/simone:/bin/bash\nuser=nobody:65534:/nonexistent:/usr/sbin/nologin\n"));
        await Capture("server_users_list_failed", () => ServerUsers.ListAsync(Server()), ReplyF(1, "", " getent: permission denied \n"));
        await Capture("server_users_create", () => ServerUsers.CreateAsync(Server(), "hotel"));
        await Capture("server_users_create_root", () => ServerUsers.CreateAsync(Server(), "root"));
        await Capture("server_users_create_invalid", () => ServerUsers.CreateAsync(Server(), "Bad Name"));
        await Capture("server_users_create_failed", () => ServerUsers.CreateAsync(Server(), "hotel"), ReplyF(1, "useradd: cannot lock /etc/passwd\n"));
    }
}
#endif
