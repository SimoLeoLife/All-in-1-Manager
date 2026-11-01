using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

internal static partial class Program
{
    private static async Task Scenarios()
    {
        Text("prelude", RemoteScript.Prelude);
        await Capture("nginx_inspect", () => NginxInstaller.InspectAsync(Server()));
        await Capture("nginx_add_repository", () => NginxInstaller.AddRepositoryAsync(Server(), Log));
        await Capture("nginx_remove_distro", () => NginxInstaller.RemoveDistroNginxAsync(Server(), Log));
        await Capture("nginx_install_package", () => NginxInstaller.InstallPackageAsync(Server(), Log));
        await Capture("nginx_debian_layout", () => NginxInstaller.SetUpDebianLayoutAsync(Server(), Log));
        await Capture("nginx_start_and_test", () => NginxInstaller.StartAndTestAsync(Server(), Log));
    }
}
