#if GROUP_E
using AllInOneInstaller.Configuration;
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group E: AtomCmsInstaller, CustomCmsInstaller, EpicNextInstaller, GiteaInstaller.
internal static partial class Program
{
    private static void NoteE(string line) => Recorder.Log.Append(line).Append('\n');

    private static string YesE(bool value) => value ? "yes" : "no";

    private static string OptE(string? value) => value ?? "<none>";

    private static string OptE(bool? value) => value is null ? "<none>" : YesE(value.Value);

    private static Func<string, string, string?, CommandResult> AnswerE(params (string Needle, string Output)[] pairs) =>
        (_, command, _) => pairs.FirstOrDefault(p => command.Contains(p.Needle)) is { Needle: not null } p
            ? new CommandResult(0, p.Output, "")
            : new CommandResult(0, "", "");

    private const string AtomInspectOutputE =
        "conf_ADDRESS=hotel.example.com\nconf_SCHEME=https\nconf_CLOUDFLARE=yes\nconf_CMS=atom\nconf_CMS_PORT=86\nphp=8.5.1\ncomposer=yes\n" +
        "node=v26.12.1\noctane=yes\nemulator=yes\nname=My Hotel\nvendor=yes\ntheme=yes\nmigrated=yes\nkey=abc-123\ncompleted=1\nsite=yes\n";

    private static void NoteAtomE(AtomCmsState s) =>
        NoteE($"site={OptE(s.Site?.Url)} atom={YesE(s.Site?.AtomCms == true)} port={s.Site?.CmsPort ?? -1} php={OptE(s.PhpVersion)} " +
              $"composer={YesE(s.ComposerPresent)} node={OptE(s.NodeVersion)} octane={YesE(s.OctaneBuilt)} emulator={YesE(s.EmulatorConfigured)} " +
              $"name={OptE(s.HotelName)} vendor={YesE(s.VendorPresent)} theme={YesE(s.ThemeBuilt)} migrated={YesE(s.Migrated)} " +
              $"serves={YesE(s.SiteServesAtom)} key={OptE(s.InstallationKey)} completed={YesE(s.InstallationCompleted)} complete={YesE(s.IsComplete)}");

    private static void ProblemsE(string name, IEnumerable<(string Input, string? Result)> results) =>
        Text(name, string.Concat(results.Select(r => $"[{r.Input}] -> {r.Result ?? "<ok>"}\n")));

    private static async Task GroupE()
    {
        var https = new WebAccessSettings("www.example.com", true, true, AtomCms: true);
        var ip = new WebAccessSettings("192.0.2.10", false, false);
        var custom = new WebAccessSettings("hotel.example.com", true, false, CustomCms: "mycms", CmsPort: 87);
        var credentials = new DatabaseCredentials("127.0.0.1", 3306, "hotel", "root", "rootpw", "cmsuser", "p'w$\"x\\y");

        // ---- AtomCMS
        await Capture("atom_cms_inspect_empty", async () => NoteAtomE(await AtomCmsInstaller.InspectAsync(Server())));
        await Capture("atom_cms_inspect_installed", async () => NoteAtomE(await AtomCmsInstaller.InspectAsync(Server())),
            AnswerE(("php8.5", AtomInspectOutputE)));
        await Capture("atom_cms_configure_env_https", () =>
            AtomCmsInstaller.ConfigureEnvAsync(Server(), https, "My \"Hotel\" $1 it's", credentials, Log));
        await Capture("atom_cms_configure_env_ip", () => AtomCmsInstaller.ConfigureEnvAsync(Server(), ip, "Hotel", null, Log));
        await Capture("atom_cms_composer_install", () => AtomCmsInstaller.ComposerInstallAsync(Server(), Log));
        await Capture("atom_cms_migrate_first", async () => NoteE("first=" + YesE(await AtomCmsInstaller.MigrateAsync(Server(), "It's \\ Hotel", Log))),
            AnswerE(("FIRST=", "  migrated\nFIRST=yes\n")));
        await Capture("atom_cms_migrate_again", async () => NoteE("first=" + YesE(await AtomCmsInstaller.MigrateAsync(Server(), "Hotel", Log))));
        await Capture("atom_cms_migrate_fails", () => AtomCmsInstaller.MigrateAsync(Server(), "Hotel", Log),
            (_, _, _) => new CommandResult(1, "something\nERROR: boom\nlast line\n", ""));
        await Capture("atom_cms_installation_key_https", async () =>
        {
            var (key, completed) = await AtomCmsInstaller.InstallationKeyAsync(Server(), https, Log);
            NoteE($"key={OptE(key)} completed={YesE(completed)}");
        }, AnswerE(("KEY=", "KEY=abc-123\nCOMPLETED=1\n")));
        await Capture("atom_cms_installation_key_ip", async () =>
        {
            var (key, completed) = await AtomCmsInstaller.InstallationKeyAsync(Server(), ip, Log);
            NoteE($"key={OptE(key)} completed={YesE(completed)}");
        });
        await Capture("atom_cms_test_https", () => AtomCmsInstaller.TestAsync(Server(), https, Log));
        await Capture("atom_cms_test_ip", () => AtomCmsInstaller.TestAsync(Server(), ip, Log));

        // ---- Your own CMS
        static void NoteCustom(CustomCmsState s) =>
            NoteE($"site={OptE(s.Site?.Url)} name={OptE(s.CurrentName)} root={OptE(s.CurrentRoot)} php={OptE(s.CurrentPhp)} content={YesE(s.FolderHasContent)}");
        const string customEnv = "conf_ADDRESS=hotel.example.com\nconf_SCHEME=https\nconf_CMS=custom\nconf_CMS_NAME=oldcms\nconf_CMS_PORT=87\n";
        await Capture("custom_cms_inspect_none", async () => NoteCustom(await CustomCmsInstaller.InspectAsync(Server(), null)));
        await Capture("custom_cms_inspect_invalid_name", async () => NoteCustom(await CustomCmsInstaller.InspectAsync(Server(), "Bad Name")));
        await Capture("custom_cms_inspect_name", async () => NoteCustom(await CustomCmsInstaller.InspectAsync(Server(), "mycms")),
            AnswerE(("php8.5", customEnv), ("fastcgi_pass", "root=/var/www/mycms/public\nphp=yes\ncontent=yes\n")));
        await Capture("custom_cms_inspect_current", async () => NoteCustom(await CustomCmsInstaller.InspectAsync(Server(), "")),
            AnswerE(("php8.5", customEnv), ("fastcgi_pass", "root=/var/www/oldcms\nphp=no\n")));
        await Capture("custom_cms_inspect_other_root", async () => NoteCustom(await CustomCmsInstaller.InspectAsync(Server(), "mycms")),
            AnswerE(("fastcgi_pass", "root=/srv/elsewhere\nphp=maybe\n")));
        await Capture("custom_cms_check_git_access", () => CustomCmsInstaller.CheckGitAccessAsync(Server(), "https://github.com/owner/it's.git", Log));
        await Capture("custom_cms_extract_zip", () => CustomCmsInstaller.ExtractZipAsync(Server(), "/tmp/my cms.zip", "mycms", Log));
        await Capture("custom_cms_prepare_php", () => CustomCmsInstaller.PrepareAsync(Server(), "mycms", " /public/ ", true, Log));
        await Capture("custom_cms_prepare_static", () => CustomCmsInstaller.PrepareAsync(Server(), "mycms", "/", false, Log));
        await Capture("custom_cms_test_https", () => CustomCmsInstaller.TestAsync(Server(), custom, Log));
        await Capture("custom_cms_test_ip", () => CustomCmsInstaller.TestAsync(Server(), ip, Log));
        ProblemsE("custom_cms_problems",
            new[] { "", "mycms", "My CMS", "abcdefghijklm", "hotel", "gamedata", "cms2" }.Select(n => ($"name {n}", CustomCmsInstaller.NameProblem(n)))
                .Concat(new[] { "/", "/public", "/public/", "public", "/../etc", "/a/./b", "/a b", "//" }.Select(r =>
                    ($"root {r}", CustomCmsInstaller.RootProblem(r))))
                .Concat(new[] { "https://github.com/owner/cms.git", "https://git.example.com:3000/o/c", "http://github.com/o/c", "git@github.com:o/c.git" }
                    .Select(u => ($"git {u}", CustomCmsInstaller.GitUrlProblem(u))))
                .Concat(new[] { "https://github.com/owner/My-CMS.git", "https://github.com/owner/Very_Long-Name-Of-CMS.GIT/", "x" }
                    .Select(u => ($"name-from-url {u}", (string?)CustomCmsInstaller.NameFromUrl(u))))
                .Concat(new[] { " /public/ ", "", "/", "a", "/web/dist//" }
                    .Select(r => ($"normalize {r}", (string?)(CustomCmsInstaller.NormalizeRoot(r) + " " + CustomCmsInstaller.RootPath("mycms", r))))));

        // ---- EpicNext
        await Capture("epicnext_inspect", async () =>
        {
            EpicNextState s = await EpicNextInstaller.InspectAsync(Server());
            NoteE($"site={OptE(s.Site?.Url)} epicnext={YesE(s.Site?.EpicNext == true)} name={OptE(s.HotelName)} origin={OptE(s.Origin)} built={YesE(s.Built)} " +
                  $"service={YesE(s.ServiceInstalled)} running={YesE(s.ServiceRunning)} atom={YesE(s.AtomTables)} branch={OptE(s.SourceBranch)} " +
                  $"folder={OptE(s.SourceFolder)} installed={YesE(s.Installed)}");
        }, AnswerE(("php8.5", "conf_ADDRESS=hotel.example.com\nconf_SCHEME=https\nconf_CMS=epicnext\nconf_CMS_PORT=88\n"),
            ("HOTEL_NAME", "name=Epic\norigin=https://example.com/repo.git\nbranch=main\nfolder=NextCMS/epicnext-cms\nbuilt=yes\nservice=yes\nrunning=yes\natom=\n")));
        await Capture("epicnext_get_from_folder", async () =>
                NoteE("commit=" + await EpicNextInstaller.GetFromFolderAsync(Server(), "https://github.com/owner/it's.git", "main", "NextCMS/epicnext-cms", Log)),
            AnswerE(("COMMIT=", "COMMIT=main abc1234 2026-01-01 First\n")));
        await Capture("epicnext_get_from_folder_default_branch", async () =>
            NoteE("commit=" + await EpicNextInstaller.GetFromFolderAsync(Server(), EpicNextInstaller.Repository, "", "", Log)));
        await Capture("epicnext_prepare_tools", () => EpicNextInstaller.PrepareToolsAsync(Server(), Log));
        await Capture("epicnext_configure_env_credentials", () =>
            EpicNextInstaller.ConfigureEnvAsync(Server(), https, "My \"Hotel\" $1 it's", credentials, Log));
        await Capture("epicnext_configure_env_ip", () => EpicNextInstaller.ConfigureEnvAsync(Server(), ip, "Hotel", null, Log));
        await Capture("epicnext_build", () => EpicNextInstaller.BuildAsync(Server(), Log));
        await Capture("epicnext_start_service", () => EpicNextInstaller.StartServiceAsync(Server(), Log));
        await Capture("epicnext_remove_service_keep", () => EpicNextInstaller.RemoveServiceAsync(Server(), false, Log));
        await Capture("epicnext_remove_service_delete", () => EpicNextInstaller.RemoveServiceAsync(Server(), true, Log));
        await Capture("epicnext_test_https", () => EpicNextInstaller.TestAsync(Server(), https, Log));
        await Capture("epicnext_test_ip", () => EpicNextInstaller.TestAsync(Server(), ip, Log));
        ProblemsE("epicnext_problems",
            new[] { "", "NextCMS/epicnext-cms", "/NextCMS", "a/../b", "a/", "a b" }.Select(f => ($"folder {f}", EpicNextInstaller.FolderProblem(f)))
                .Concat(new[] { "", "main", "feature/x", "-x", "a..b", "x/", "/x", "a b", new string('b', 101) }
                    .Select(b => ($"branch {b}", EpicNextInstaller.BranchProblem(b)))));

        // ---- Gitea
        var giteaHttps = new GiteaSettings("git.example.com", true, true, "gitadmin", "admin@example.com");
        var giteaHttp = new GiteaSettings("git.example.org", false, false, "o'neil", "o'neil@example.org");
        await Capture("gitea_inspect_empty", async () =>
        {
            GiteaState s = await GiteaInstaller.InspectAsync(Server());
            NoteE($"site={OptE(s.Site?.Url)} docker={YesE(s.DockerRunning)} traefik={YesE(s.TraefikRunning)} domain={OptE(s.Domain)} cloudflare={OptE(s.Cloudflare)} " +
                  $"port={s.Port} running={YesE(s.Running)} version={OptE(s.Version)} installed={YesE(s.Installed)}");
        });
        await Capture("gitea_inspect_installed", async () =>
        {
            GiteaState s = await GiteaInstaller.InspectAsync(Server());
            NoteE($"site={OptE(s.Site?.Url)} docker={YesE(s.DockerRunning)} traefik={YesE(s.TraefikRunning)} domain={OptE(s.Domain)} cloudflare={OptE(s.Cloudflare)} " +
                  $"port={s.Port} running={YesE(s.Running)} version={OptE(s.Version)} installed={YesE(s.Installed)}");
        }, AnswerE(("gitea_", "conf_ADDRESS=example.com\nconf_SCHEME=https\ndocker=yes\ntraefik=true\ngitea_DOMAIN=git.example.com\ngitea_CLOUDFLARE=no\n" +
                              "gitea_PORT=89\nrunning=true\nversion=1.25.1\n")));
        await Capture("gitea_check_dns_cloudflare", () => GiteaInstaller.CheckDnsAsync(Server(), giteaHttps, Log));
        await Capture("gitea_check_dns_plain", () => GiteaInstaller.CheckDnsAsync(Server(), giteaHttp, Log));
        await Capture("gitea_install_https_cloudflare", async () =>
        {
            GiteaResult r = await GiteaInstaller.InstallAsync(Server(), giteaHttps, Log);
            NoteE($"port={r.Port} password={OptE(r.AdminPassword)} existed={YesE(r.AdminExisted)} cert={YesE(r.CertificateValid)}");
        }, AnswerE(("PORT=", "PORT=86\nPASSWORD=Secr3t\n")));
        await Capture("gitea_install_http", async () =>
        {
            GiteaResult r = await GiteaInstaller.InstallAsync(Server(), giteaHttp, Log);
            NoteE($"port={r.Port} password={OptE(r.AdminPassword)} existed={YesE(r.AdminExisted)} cert={YesE(r.CertificateValid)}");
        }, AnswerE(("PORT=", "ADMIN=existed\nPORT=87\n")));
        await Capture("gitea_update", () => GiteaInstaller.UpdateAsync(Server(), Log));
        await Capture("gitea_remove_domain_delete", () => GiteaInstaller.RemoveAsync(Server(), "git.example.com", true, true, Log));
        await Capture("gitea_remove_no_domain_keep", () => GiteaInstaller.RemoveAsync(Server(), null, false, false, Log));
        await Capture("gitea_test_https", async () => NoteE("cert=" + YesE(await GiteaInstaller.TestAsync(Server(), giteaHttps, Log))),
            AnswerE(("CERT=", "CERT=yes\n")));
        await Capture("gitea_test_http", async () => NoteE("cert=" + YesE(await GiteaInstaller.TestAsync(Server(), giteaHttp, Log))));
        var hotel = new WebAccessSettings("Example.com", true, false);
        ProblemsE("gitea_problems",
            new[] { "git.example.com", "example", "-a.example.com", "a.b", "example.com", "x.y.example.co.uk", string.Join(".", Enumerable.Repeat(new string('a', 60), 5)) + ".com" }
                .Select(d => ($"domain {d}", GiteaInstaller.DomainProblem(d, hotel)))
                .Concat(new[] { "gitadmin", "Admin", ".x", "a", new string('u', 41), "user.name" }.Select(u => ($"user {u}", GiteaInstaller.UserProblem(u))))
                .Concat(new[] { "a@b.cd", "a@b", "a b@c.de" }.Select(e => ($"email {e}", GiteaInstaller.EmailProblem(e))))
                .Concat(new WebAccessSettings?[] { null, new("www.example.com", true, false), new("example.com", true, false), ip }
                    .Select(s => ($"suggest {s?.Address}", (string?)GiteaInstaller.SuggestedDomain(s)))));
    }
}
#endif
