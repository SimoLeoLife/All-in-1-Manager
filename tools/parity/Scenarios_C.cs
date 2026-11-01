#if GROUP_C
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group C: WebAccessInstaller, HotelSite, OctaneInstaller, LocalPorts, CmsSite.
internal static partial class Program
{
    private static readonly WebAccessSettings HttpIp = new("192.0.2.10", false, false);
    private static readonly WebAccessSettings HttpIpv6 = new("2001:db8::1", false, false);
    private static readonly WebAccessSettings HttpsDomain = new("hotel.example.com", true, false);
    private static readonly WebAccessSettings HttpsWwwCloudflare = new("www.hotel.example.com", true, true);
    private static readonly WebAccessSettings AtomLegacy = new("hotel.example.com", true, false, AtomCms: true);
    private static readonly WebAccessSettings AtomOwnPort = new("hotel.example.com", true, true, AtomCms: true, CmsPort: 86);
    private static readonly WebAccessSettings CustomOwnPort = new("hotel.example.com", false, false, CustomCms: "mycms", CmsPort: 87);
    private static readonly WebAccessSettings CustomNoPort = new("hotel.example.com", true, false, CustomCms: "mycms");
    private static readonly WebAccessSettings EpicNext = new("www.hotel.example.com", true, true, EpicNext: true, CmsPort: 88);

    private static readonly (string Name, WebAccessSettings Site)[] Sites =
    [
        ("http_ip", HttpIp), ("http_ipv6", HttpIpv6), ("https_domain", HttpsDomain), ("https_www_cloudflare", HttpsWwwCloudflare),
        ("atom_legacy", AtomLegacy), ("atom_own_port", AtomOwnPort), ("custom_own_port", CustomOwnPort),
        ("custom_no_port", CustomNoPort), ("epicnext", EpicNext)
    ];

    private static CommandResult Answer(string command, params (string Needle, string Output)[] pairs)
    {
        foreach (var (needle, output) in pairs)
            if (command.Contains(needle)) return new CommandResult(0, output, "");
        return new CommandResult(0, "", "");
    }

    private static async Task GroupC()
    {
        // LocalPorts
        Text("local_ports_functions", LocalPorts.Functions);

        // HotelSite
        Text("hotel_site_php_locations", HotelSite.PhpLocations);
        Text("hotel_site_real_ip_plain", HotelSite.RealIpScript(HttpsDomain));
        Text("hotel_site_real_ip_cloudflare", HotelSite.RealIpScript(HttpsWwwCloudflare));
        foreach (var (name, site) in Sites)
        {
            Text($"hotel_site_write_{name}", HotelSite.WriteScript(site, null));
            Text($"hotel_site_config_{name}", HotelSite.Config(site));
        }
        Text("hotel_site_write_root", HotelSite.WriteScript(HttpsDomain, "/var/www/site"));

        // CmsSite
        Text("cms_site_write_static", CmsSite.WriteScript(HttpsDomain, "mysite", "/var/www/mysite", false));
        Text("cms_site_write_php", CmsSite.WriteScript(HttpIp, "atomcms", "/var/www/atomcms/public", true));
        Text("cms_site_write_proxy", CmsSite.WriteScript(EpicNext, "epicnext", "/var/www/epicnext", false, "http://127.0.0.1:3000"));
        await Capture("cms_site_apply", () => CmsSite.ApplyAsync(Server(), CustomOwnPort, "mycms", "/var/www/mycms", true, Log),
            (_, _, _) => new CommandResult(0, "PORT=87\n", ""));
        await Capture("cms_site_apply_no_port", () => CmsSite.ApplyAsync(Server(), CustomOwnPort, "mycms", "/var/www/mycms", true, Log));
        await Capture("cms_site_remove", () => CmsSite.RemoveAsync(Server(), null, null, Log));
        await Capture("cms_site_remove_delete", () => CmsSite.RemoveAsync(Server(), "mycms", "/var/www/my cms", Log));

        // WebAccessInstaller
        Text("web_access_wait_for_traefik", WebAccessInstaller.WaitForTraefikScript);
        Text("web_access_compose", WebAccessInstaller.ComposeScript());
        foreach (var (name, site) in Sites) Text($"web_access_static_config_{name}", WebAccessInstaller.StaticConfigScript(site));
        await Capture("web_access_inspect", () => WebAccessInstaller.InspectAsync(Server()));
        await Capture("web_access_install_docker", () => WebAccessInstaller.InstallDockerAsync(Server(), Log));
        await Capture("web_access_configure_nginx", () => WebAccessInstaller.ConfigureNginxAsync(Server(), HttpsDomain, Log));
        foreach (var (name, site) in Sites)
        {
            await Capture($"web_access_configure_traefik_{name}", () => WebAccessInstaller.ConfigureTraefikAsync(Server(), site, Log));
            await Capture($"web_access_configure_routes_{name}", () => WebAccessInstaller.ConfigureRoutesAsync(Server(), site, Log));
            await Capture($"web_access_test_{name}", () => WebAccessInstaller.TestAsync(Server(), site, Log));
        }
        await Capture("web_access_configure_nginx_real_ip_plain", () => WebAccessInstaller.ConfigureNginxRealIpAsync(Server(), HttpIp, Log));
        await Capture("web_access_configure_nginx_real_ip_cloudflare",
            () => WebAccessInstaller.ConfigureNginxRealIpAsync(Server(), HttpsWwwCloudflare, Log));
        await Capture("web_access_cloudflare_refresh_on", () => WebAccessInstaller.InstallCloudflareRefreshAsync(Server(), HttpsWwwCloudflare, Log));
        await Capture("web_access_cloudflare_refresh_off", () => WebAccessInstaller.InstallCloudflareRefreshAsync(Server(), HttpsDomain, Log));
        await Capture("web_access_cloudflare_ranges", () => WebAccessInstaller.CloudflareRangesAsync(Server()),
            (_, c, _) => Answer(c, ("RANGES=", "RANGES=173.245.48.0/20,2400:cb00::/32\n")));
        await Capture("web_access_polaris_trusted_proxies_plain", () => WebAccessInstaller.PolarisTrustedProxiesAsync(Server(), HttpsDomain));
        await Capture("web_access_polaris_trusted_proxies_cloudflare",
            () => WebAccessInstaller.PolarisTrustedProxiesAsync(Server(), HttpsWwwCloudflare));
        await Capture("web_access_configure_polaris", () => WebAccessInstaller.ConfigurePolarisAsync(Server(), HttpsWwwCloudflare, Log),
            (_, c, _) => Answer(c, ("RANGES=", "RANGES=173.245.48.0/20,2400:cb00::/32\n"), ("= 'ws.whitelist'", "other.example.com\n")));
        await Capture("web_access_configure_polaris_star", () => WebAccessInstaller.ConfigurePolarisAsync(Server(), HttpIp, Log),
            (_, c, _) => Answer(c, ("= 'ws.whitelist'", "*\n")));

        // OctaneInstaller
        await Capture("octane_inspect", () => OctaneInstaller.InspectAsync(Server()));
        await Capture("octane_yarn_install", () => OctaneInstaller.YarnInstallAsync(Server(), OctaneInstaller.Renderer, Log));
        await Capture("octane_yarn_install_fresh", () => OctaneInstaller.YarnInstallAsync(Server(), OctaneInstaller.Octane, Log, fresh: true));
        await Capture("octane_configure_plain", () => OctaneInstaller.ConfigureAsync(Server(), HttpIp, Log));
        await Capture("octane_configure_cms", () => OctaneInstaller.ConfigureAsync(Server(), AtomOwnPort, Log));
        await Capture("octane_configure_restore_login", () => OctaneInstaller.ConfigureAsync(Server(), HttpsDomain, Log, restoreLoginScreen: true));
        await Capture("octane_configure_cms_restore_login",
            () => OctaneInstaller.ConfigureAsync(Server(), CustomNoPort, Log, restoreLoginScreen: true));
        await Capture("octane_build_plain", () => OctaneInstaller.BuildAsync(Server(), HttpsDomain, Log));
        await Capture("octane_build_cms", () => OctaneInstaller.BuildAsync(Server(), EpicNext, Log));
        foreach (var (name, site) in Sites)
            await Capture($"octane_publish_{name}", () => OctaneInstaller.PublishAsync(Server(), site, Log));
    }
}
#endif
