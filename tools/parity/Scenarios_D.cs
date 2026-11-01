#if GROUP_D
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group D: HardenInstaller, SshLoginInstaller, RootAuthorizedKeys (without TestKeyLoginAsync, which opens a real
// connection), Fail2BanInstaller, CrowdSecInstaller.
internal static partial class Program
{
    private static Func<string, string, string?, CommandResult> AnswerD(params (string Needle, string Output)[] pairs) =>
        (_, command, _) => pairs.Where(p => command.Contains(p.Needle)).Select(p => new CommandResult(0, p.Output, "")).FirstOrDefault()
                           ?? new CommandResult(0, "", "");

    private static void ResultD(string text) => Recorder.Log.Append("result: ").Append(text).Append('\n');

    private static string YesNo(bool value) => value ? "yes" : "no";
    private static string OrNull(string? value) => value ?? "null";
    private static string ListD<T>(IEnumerable<T> items) => string.Join(",", items);

    private static string Describe(Fail2BanState s) =>
        $"installed={YesNo(s.Installed)} version={OrNull(s.Version)} running={YesNo(s.Running)} configured={YesNo(s.Configured)} " +
        $"settings={(s.Settings is { } c ? $"{c.MaxRetry}/{c.FindTimeMinutes}/{c.BanTimeMinutes}/{YesNo(c.Increment)}/[{ListD(c.Whitelist)}]" : "null")} " +
        $"ssh=[{ListD(s.SshPorts)}] banned=[{ListD(s.Banned)}] total={s.TotalBanned} client={OrNull(s.ClientIp)} complete={YesNo(s.IsComplete)}";

    private static string Describe(CrowdSecState s) =>
        $"site={OrNull(s.Site?.Address)} docker={YesNo(s.DockerRunning)} traefik={YesNo(s.TraefikRunning)} installed={YesNo(s.Installed)} " +
        $"running={YesNo(s.Running)} bouncer={YesNo(s.BouncerConfigured)} enrolled={YesNo(s.Enrolled)} version={OrNull(s.Version)} " +
        $"instance={OrNull(s.InstanceName)} port={s.Port} whitelist=[{ListD(s.Whitelist)}] banned=[{ListD(s.Banned)}] dns={OrNull(s.DnsAddresses)} " +
        $"dnscf={(s.DnsBehindCloudflare is { } cf ? YesNo(cf) : "null")} client={OrNull(s.ClientIp)} complete={YesNo(s.IsComplete)} " +
        $"problem={OrNull(CrowdSecInstaller.Problem(s))}";

    private static string Describe(HardenState s) =>
        $"site={OrNull(s.Site?.Address)} active={YesNo(s.UfwActive)} deny={YesNo(s.DenyIncoming)} ssh=[{ListD(s.SshPorts)}] " +
        $"mariadb={YesNo(s.MariaDbOpen)} sysctl={YesNo(s.SysctlConfigured)} whitelist={OrNull(s.Whitelist)} ipheader={OrNull(s.IpHeader)} " +
        $"ufwlog={YesNo(s.FirewallLogSeparate)} trusted={OrNull(s.TrustedProxies)} realipcf={YesNo(s.NginxRealIpCloudflare)} " +
        $"expected={OrNull(s.ExpectedWhitelist)} readable={YesNo(s.VisitorIpsReadable)} complete={YesNo(s.IsComplete)}";

    private static string Describe(SshLoginState s) =>
        $"password={YesNo(s.PasswordLogin)} keyboard={YesNo(s.KeyboardInteractive)} permit={OrNull(s.PermitRootLogin)} ours={YesNo(s.OurFile)} " +
        $"saved={YesNo(s.RootHasSavedKey)} keysonly={YesNo(s.KeysOnly)}";

    private static async Task GroupD()
    {
        var plain = new WebAccessSettings("www.hotel.example.com", true, false);
        var cloudflare = new WebAccessSettings("hotel.example.com", false, true);

        // HardenInstaller
        const string hardenOutput = "conf_ADDRESS=www.hotel.example.com\nconf_SCHEME=https\nconf_CLOUDFLARE=yes\nactive=yes\ndeny=yes\n" +
                                    "ssh=22 2222 abc 0 \nmariadb=\nsysctl=yes\nufwlog=yes\nrealipcf=yes\nwhitelist=hotel.example.com\n" +
                                    "ipheader=X-Forwarded-For\ntrusted=173.245.48.0/20\n";
        await Capture("harden_inspect", async () => ResultD(Describe(await HardenInstaller.InspectAsync(Server()))), AnswerD(("sysctl=", hardenOutput)));
        await Capture("harden_inspect_empty", async () => ResultD(Describe(await HardenInstaller.InspectAsync(Server()))));
        await Capture("harden_inspect_open", async () => ResultD(Describe(await HardenInstaller.InspectAsync(Server()))),
            AnswerD(("sysctl=", "conf_ADDRESS=hotel.example.com\nconf_CLOUDFLARE=no\nactive=yes\ndeny=yes\nmariadb=open\nsysctl=yes\nufwlog=\nwhitelist=hotel.example.com\nipheader=X-Forwarded-For\n")));
        await Capture("harden_configure_firewall", () => HardenInstaller.ConfigureFirewallAsync(Server(), plain, [2222, 22, 2222], Log));
        await Capture("harden_configure_firewall_cloudflare", () => HardenInstaller.ConfigureFirewallAsync(Server(), cloudflare, [], Log));
        await Capture("harden_enable_firewall_fresh", async () => ResultD(YesNo(await HardenInstaller.EnableFirewallAsync(Server(), Log))),
            AnswerD(("sysctl=", "conf_ADDRESS=hotel.example.com\nconf_CLOUDFLARE=yes\nssh=22 2222\n")));
        await Capture("harden_enable_firewall_no_site", async () => ResultD(YesNo(await HardenInstaller.EnableFirewallAsync(Server(), Log))),
            AnswerD(("marker=yes", "ufw=yes\nmarker=yes\nrules=0\n")));
        await Capture("harden_enable_firewall_existing", async () => ResultD(YesNo(await HardenInstaller.EnableFirewallAsync(Server(), Log))),
            AnswerD(("marker=yes", "ufw=yes\nmarker=yes\nrules=4\n"), ("sysctl=", "ssh=2222 22\n")));
        await Capture("harden_disable_firewall", () => HardenInstaller.DisableFirewallAsync(Server(), Log));
        await Capture("harden_configure_sysctl", () => HardenInstaller.ConfigureSysctlAsync(Server(), Log));
        await Capture("harden_configure_polaris", async () => ResultD(YesNo(await HardenInstaller.ConfigurePolarisAsync(Server(), plain, Log))));
        await Capture("harden_configure_polaris_cloudflare", async () => ResultD(YesNo(await HardenInstaller.ConfigurePolarisAsync(Server(), cloudflare, Log))));

        // SshLoginInstaller
        AuthorizedKey[] saved = [new("ssh-ed25519", "AAAAC3NzaC1lZDI1NTE5AAAAIB", "hotel"), new("ssh-rsa", "AAAAB3NzaC1yc2EAAAADAQAB", "")];
        await Capture("ssh_login_inspect", async () => ResultD(Describe(await SshLoginInstaller.InspectAsync(Server(), saved))),
            AnswerD(("sshd -T", "passwordauthentication=no\nkbdinteractiveauthentication=no\npermitrootlogin=prohibit-password\nours=yes\nkeys=AAAAX AAAAB3NzaC1yc2EAAAADAQAB\n")));
        await Capture("ssh_login_inspect_empty", async () => ResultD(Describe(await SshLoginInstaller.InspectAsync(Server(), saved))));
        await Capture("ssh_login_disable", () => SshLoginInstaller.DisablePasswordLoginAsync(Server(), Log));
        await Capture("ssh_login_allow", () => SshLoginInstaller.AllowPasswordLoginAsync(Server(), Log));

        // RootAuthorizedKeys
        var key = new AuthorizedKey("ssh-ed25519", "AAAAC3NzaC1lZDI1NTE5AAAAIB", "it's me");
        await Capture("root_authorized_keys_read", async () => ResultD(ListD((await RootAuthorizedKeys.ReadAsync(Server())).Select(k => k.Line))),
            AnswerD(("cat", "# comment\nssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIB hotel\n\nssh-rsa AAAAB3NzaC1yc2EAAAADAQAB\n")));
        await Capture("root_authorized_keys_read_failed", () => RootAuthorizedKeys.ReadAsync(Server()),
            (_, _, _) => new CommandResult(1, "", " cat: permission denied \n"));
        await Capture("root_authorized_keys_install_replace", async () => ResultD(OrNull(await RootAuthorizedKeys.InstallAsync(Server(), key, AuthorizedKeysMode.Replace))),
            AnswerD(("set -e", " /root/.ssh/authorized_keys.bak-20260101-000000-abcd \n")));
        await Capture("root_authorized_keys_install_add", async () => ResultD(OrNull(await RootAuthorizedKeys.InstallAsync(Server(), key, AuthorizedKeysMode.Add))));
        await Capture("root_authorized_keys_install_failed", () => RootAuthorizedKeys.InstallAsync(Server(), key, AuthorizedKeysMode.Add),
            (_, _, _) => new CommandResult(1, "", "\n  install: cannot create /root/.ssh \nmore\n"));
        await Capture("root_authorized_keys_install_failed_silent", () => RootAuthorizedKeys.InstallAsync(Server(), key, AuthorizedKeysMode.Replace),
            (_, _, _) => new CommandResult(1, "", " \n"));
        await Capture("root_authorized_keys_permit_root_login", async () => ResultD(OrNull(await RootAuthorizedKeys.PermitRootLoginAsync(Server()))),
            AnswerD(("permitrootlogin", "without-password\n")));
        await Capture("root_authorized_keys_permit_root_login_unknown", async () => ResultD(OrNull(await RootAuthorizedKeys.PermitRootLoginAsync(Server()))));

        // Fail2BanInstaller
        string[] entries =
        [
            "1.2.3.4", "192.168.1.0/24", "0.0.0.0/0", "1.2.3.4/32", "1.2.3.4/33", "1.2.3.4/", "1.2.3.4/24/1", "1.2.3.4/+24", "1.2.3.4/-0",
            "1.2.3.4/-1", "1.2.3.4/024", "1.2.3.4/ 24", "1.2.3.4/24 ", "1.2.3.4/2147483648", "1.2.3.4/x", "1.2.3", "1.2.3.4.5", "10.1", "1..3.4",
            "1.2.3.", ".1.2.3", "", "a.b.c.d", "1.2.3.256", "256.1.1.1", "010.1.1.1", "08.1.1.1", "0x10.1.1.1", "0X1F.0.0.1", "0x.1.1.1",
            "00.0.0.0", "4294967295.1.1.1", "1.2.3.4 ", " 1.2.3.4", "1.2.3.4:80", "1.2.3.4%1", "localhost", "::1", "::1/128", "::1/129",
            "::1/0", "::", "2001:db8::/32", "2001:DB8::1", "2001:db8::1/64", "1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:9", "1::2::3", "gggg::1",
            "12345::1", "::ffff:1.2.3.4", "::1.2.3.4", "::ffff:010.1.1.1", "::ffff:1.2.3", "::ffff:1.2.3.4/96", "[::1]", "[::1]:80", "[::1]/64",
            "[::1]:", "[::1]:99999", "[::1]x", "[::1", "::1]", "fe80::1%1", "::1%eth0", "fe80::1%1/64", ":", ":::", "1:2:3:4:5:6:7::",
            "::1:2:3:4:5:6:7", "1:2:3:4:5:6:1.2.3.4", "0000:0000::00001", "1.2.3.4/00032", "[::1]:abc", "[::1]:0x1F", "[::1]:0xZZ", "[::1]:0X1F",
            "[::1]:0x", "[::1]:0", "[1.2.3.4]:80", "[1.2.3.4]", "1:2:3:4:5:6:7:1.2.3.4", "1:2:3:4:5:6:7:8::", "::1:2:3:4:5:6:7:8", "1::",
            "[::1]:80:90", "[[::1]]", "[::1]]", "1:2:3:4:5:6:7:8/64", "::1.2.3.4.5", "::1.2.3", "::FFFF:1.2.3.4", "fe80::", "1:2:3:4::5:6:7:8",
            "::1.2.3.04", "::256.1.1.1", "1:2:3:4:5::1.2.3.4", "::ffff:1.2.3.4:1", "-1.2.3.4", "1.2.3.4/+", "1.2.3.4/-", "::1/1e2", "[ ::1]",
            "::1 ", "0:0:0:0:0:0:0:0", "1:2:3:4:5:6:7:8:", ":1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:", "[]", "[]:80", "1.2.3.4/32\u0000"
        ];
        Text("fail2ban_entry_problems", string.Join("\n", entries.Select(e => $"{e} => {OrNull(Fail2BanInstaller.EntryProblem(e))}")) + "\n");
        string[] whitelists =
        [
            "203.0.113.5, 198.51.100.0/24 # office\r\n# a comment\n2001:db8::1\t203.0.113.5\n2001:DB8::1",
            "1.2.3.4 nope 5.6.7.8",
            "",
            "#1.2.3.4\n,,, \t",
            string.Concat(Enumerable.Range(0, 101).Select(i => $"10.0.{i / 256}.{i % 256}\n")),
            "1.2.3.4/33 5.6.7.8"
        ];
        Text("fail2ban_parse_whitelist", string.Join("\n", whitelists.Select(w => Fail2BanInstaller.ParseWhitelist(w) is var (list, problem)
            ? $"[{ListD(list)}] {OrNull(problem)}" : "")) + "\n");
        Fail2BanSettings[] settingsList =
        [
            Fail2BanInstaller.Defaults, new(0, 10, 60, true, []), new(101, 10, 60, true, []), new(5, 0, 60, true, []), new(5, 10081, 60, true, []),
            new(5, 10080, 0, true, []), new(5, 10, 525601, true, []), new(100, 1, 525600, false, [])
        ];
        Text("fail2ban_settings_problems", string.Join("\n", settingsList.Select(s => OrNull(Fail2BanInstaller.SettingsProblem(s)))) + "\n");

        Func<string, string, string?, CommandResult> F2b(string root, string client) =>
            (kind, _, _) => new CommandResult(0, kind == "run" ? client : root, "");
        await Capture("fail2ban_inspect", async () => ResultD(Describe(await Fail2BanInstaller.InspectAsync(Server()))),
            F2b("version=1.0.2\ninstalled=yes\nrunning=yes\nconfigured=yes\nignoreip=127.0.0.1/8 ::1 203.0.113.5 198.51.100.0/24\nmaxretry=3\n" +
                "findtime=600\nbantime=2h\nincrement=false\nssh=22 2222 \nbanned=198.51.100.1 198.51.100.2\ntotal=7\n", "client=203.0.113.9\n"));
        await Capture("fail2ban_inspect_units", async () => ResultD(Describe(await Fail2BanInstaller.InspectAsync(Server()))),
            F2b("installed=yes\nconfigured=yes\nmaxretry=abc\nfindtime=1w\nbantime=30\nincrement=true\n", "client=localhost\n"));
        await Capture("fail2ban_inspect_bad_units", async () => ResultD(Describe(await Fail2BanInstaller.InspectAsync(Server()))),
            F2b("installed=yes\nconfigured=yes\nmaxretry=-2\nfindtime=1y\nbantime=m\n", "client=\n"));
        await Capture("fail2ban_inspect_days", async () => ResultD(Describe(await Fail2BanInstaller.InspectAsync(Server()))),
            F2b("configured=yes\nfindtime=2D\nbantime= 10m \n", "client=2001:db8::5\n"));
        await Capture("fail2ban_inspect_empty", async () => ResultD(Describe(await Fail2BanInstaller.InspectAsync(Server()))));
        var f2bSettings = new Fail2BanSettings(3, 15, 120, true, ["203.0.113.5", "198.51.100.0/24", "2001:db8::1"]);
        await Capture("fail2ban_install", () => Fail2BanInstaller.InstallAsync(Server(), f2bSettings, [22, 2222], Log));
        await Capture("fail2ban_install_defaults", () => Fail2BanInstaller.InstallAsync(Server(), Fail2BanInstaller.Defaults with { Increment = false }, [], Log));
        await Capture("fail2ban_save", () => Fail2BanInstaller.SaveAsync(Server(), f2bSettings, [2222], Log));
        await Capture("fail2ban_save_empty", () => Fail2BanInstaller.SaveAsync(Server(), Fail2BanInstaller.Defaults, [22], Log));
        await Capture("fail2ban_unban", () => Fail2BanInstaller.UnbanAsync(Server(), "203.0.113.5", Log));
        await Capture("fail2ban_unban_network", () => Fail2BanInstaller.UnbanAsync(Server(), "198.51.100.0/24", Log));
        await Capture("fail2ban_unban_invalid", () => Fail2BanInstaller.UnbanAsync(Server(), "1.2.3", Log));
        await Capture("fail2ban_remove", () => Fail2BanInstaller.RemoveAsync(Server(), Log));

        // CrowdSecInstaller
        string[] keyTexts =
        [
            "sudo cscli console enroll -e context cm1abcdefghijklmnop", "  'cm1abcdefghijklmnop'\r\n", "\"cm1abcdefghijklmnop\"", "short", "-e", "",
            "  ", "cm1abcdefghijklmnop --name x", "cm1abc-defghijklmnop", new string('a', 64), new string('a', 65), "cm1abcdefghijklmnop -e"
        ];
        Text("crowdsec_keys", string.Join("\n", keyTexts.Select(t =>
            $"[{t}] key={OrNull(CrowdSecInstaller.KeyFrom(t))} enrolled={OrNull(CrowdSecInstaller.KeyProblem(t, true))} new={OrNull(CrowdSecInstaller.KeyProblem(t, false))}")) + "\n");
        string[] names = ["my-hotel_1.eu", "hotel\n", "", "my hotel", new string('a', 64), new string('a', 65), "hötel", "a\n\n"];
        Text("crowdsec_names", string.Join("\n", names.Select(n => $"[{n}] {OrNull(CrowdSecInstaller.NameProblem(n))}")) + "\n");

        const string crowdsecOutput = "conf_ADDRESS=hotel.example.com\nconf_SCHEME=https\ndocker=yes\ntraefik=true\ncs_LAPI_PORT=8081\ncs_ENROLLED=yes\n" +
                                      "cs_INSTANCE=hotel\ncs_WHITELIST=203.0.113.5 198.51.100.0/24\ninstalled=yes\nbouncer=yes\nrunning=true\nversion=v1.6.4\n" +
                                      "banned=192.0.2.1 192.0.2.2\ndns=104.21.1.1\ndnscf=yes\n";
        Func<string, string, string?, CommandResult> Cs(string root, string client) =>
            (kind, _, _) => new CommandResult(0, kind == "run" ? client : root, "");
        await Capture("crowdsec_inspect", async () => ResultD(Describe(await CrowdSecInstaller.InspectAsync(Server()))), Cs(crowdsecOutput, "client=203.0.113.9\n"));
        await Capture("crowdsec_inspect_cloudflare", async () => ResultD(Describe(await CrowdSecInstaller.InspectAsync(Server()))),
            Cs(crowdsecOutput.Replace("conf_SCHEME=https", "conf_CLOUDFLARE=yes"), "client=nope\n"));
        await Capture("crowdsec_inspect_no_docker", async () => ResultD(Describe(await CrowdSecInstaller.InspectAsync(Server()))),
            Cs("conf_ADDRESS=hotel.example.com\ntraefik=false\n", ""));
        await Capture("crowdsec_inspect_no_traefik", async () => ResultD(Describe(await CrowdSecInstaller.InspectAsync(Server()))),
            Cs("conf_ADDRESS=hotel.example.com\ndocker=yes\ntraefik=false\ndnscf=no\n", ""));
        await Capture("crowdsec_inspect_empty", async () => ResultD(Describe(await CrowdSecInstaller.InspectAsync(Server()))));
        var csSettings = new CrowdSecSettings("cm1abcdefghijklmnop", "my-hotel", ["203.0.113.5", "198.51.100.0/24"]);
        await Capture("crowdsec_install_engine", () => CrowdSecInstaller.InstallEngineAsync(Server(), csSettings, Log));
        await Capture("crowdsec_install_engine_empty", () => CrowdSecInstaller.InstallEngineAsync(Server(), csSettings with { Whitelist = [] }, Log));
        await Capture("crowdsec_enroll", () => CrowdSecInstaller.EnrollAsync(Server(), "sudo cscli console enroll -e context cm1abcdefghijklmnop", "my-hotel", Log));
        await Capture("crowdsec_enroll_invalid", () => CrowdSecInstaller.EnrollAsync(Server(), "nope", "my-hotel", Log));
        await Capture("crowdsec_configure_traefik", () => CrowdSecInstaller.ConfigureTraefikAsync(Server(), plain, csSettings.Whitelist, Log));
        await Capture("crowdsec_configure_traefik_cloudflare", () => CrowdSecInstaller.ConfigureTraefikAsync(Server(), cloudflare, [], Log));
        await Capture("crowdsec_test", async () =>
        {
            CrowdSecResult result = await CrowdSecInstaller.TestAsync(Server(), plain, Log);
            ResultD($"{YesNo(result.BouncerConnected)} {YesNo(result.LogRead)}");
        }, AnswerD(("probe", "  fetched\nBOUNCER=yes\nLOGREAD=no\n")));
        await Capture("crowdsec_test_http", async () =>
        {
            CrowdSecResult result = await CrowdSecInstaller.TestAsync(Server(), cloudflare, Log);
            ResultD($"{YesNo(result.BouncerConnected)} {YesNo(result.LogRead)}");
        }, AnswerD(("probe", "LOGREAD=yes\n")));
        await Capture("crowdsec_save_whitelist", () => CrowdSecInstaller.SaveWhitelistAsync(Server(), plain, 8081, csSettings.Whitelist, Log));
        await Capture("crowdsec_save_whitelist_cloudflare", () => CrowdSecInstaller.SaveWhitelistAsync(Server(), cloudflare, 8080, [], Log));
        await Capture("crowdsec_unban", () => CrowdSecInstaller.UnbanAsync(Server(), "192.0.2.1", Log));
        await Capture("crowdsec_unban_range", () => CrowdSecInstaller.UnbanAsync(Server(), "192.0.2.0/24", Log));
        await Capture("crowdsec_unban_invalid", () => CrowdSecInstaller.UnbanAsync(Server(), "1.2.3", Log));
        await Capture("crowdsec_update", () => CrowdSecInstaller.UpdateAsync(Server(), Log));
        await Capture("crowdsec_remove", () => CrowdSecInstaller.RemoveAsync(Server(), plain, false, Log));
        await Capture("crowdsec_remove_without_site", () => CrowdSecInstaller.RemoveAsync(Server(), null, true, Log));
    }
}
#endif
