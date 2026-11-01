using System.Reflection;
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

        // Every group's scenarios: a file Scenarios_<Group>.cs per group, wrapped in #if GROUP_<NAME> so a group can be
        // built on its own (dotnet run -p:DefineConstants=GROUP_<NAME>), or all together.
        foreach (MethodInfo method in typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                     .Where(m => m.Name.StartsWith("Group", StringComparison.Ordinal) && m.ReturnType == typeof(Task))
                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            await (Task)method.Invoke(null, null)!;
        }
    }
}
