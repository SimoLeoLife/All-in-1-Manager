#if GROUP_B
using System.Text.RegularExpressions;
using AllInOneInstaller.Configuration;
using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

// Group B: EmulatorInstaller, EmulatorAssetsInstaller, UiTextsInstaller, AssetSync, GamedataFiles.
internal static partial class Program
{
    private static readonly DateTime FixedTime = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static Func<string, string, string?, CommandResult> ByKind(string root, string stream, string input, int exit = 0) =>
        (kind, _, _) => kind switch
        {
            "root" => new CommandResult(exit, root, ""),
            "stream" => new CommandResult(exit, stream, ""),
            "input" => new CommandResult(exit, input, ""),
            _ => new CommandResult(0, "", "")
        };

    private static void WriteFile(string path, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)'x', size).ToArray());
        File.SetLastWriteTimeUtc(path, FixedTime);
    }

    // The random secrets of ConfigureAsync's stdin are replaced, the same way the Rust test does it.
    private static void Unrandom(string name)
    {
        string path = Path.Combine(_out, name + ".txt");
        File.WriteAllText(path, Regex.Replace(File.ReadAllText(path), "(master_key|jwt\\.secret)=[A-Za-z0-9]{64}", "$1=<random>"));
    }

    private static async Task GroupB()
    {
        // EmulatorInstaller
        Text("emulator_control_functions", EmulatorInstaller.ControlFunctions);
        Text("emulator_imager_settings_line", EmulatorInstaller.ImagerSettingsLine("https://hotel.example.com"));
        Text("emulator_imager_keys_sql", EmulatorInstaller.ImagerKeysSql);
        await Capture("emulator_inspect", () => EmulatorInstaller.InspectAsync(Server()),
            (_, _, _) => new CommandResult(0, "repo=yes\nmemory=2048\nzones=Europe/Rome Etc/UTC\n", ""));
        await Capture("emulator_install_java", () => EmulatorInstaller.InstallJavaAsync(Server(), Log));
        await Capture("emulator_create_account", () => EmulatorInstaller.CreateAccountAsync(Server(), Log));
        await Capture("emulator_build", () => EmulatorInstaller.BuildAsync(Server(), "/usr/lib/jvm/java-25-openjdk-amd64", Log));
        await Capture("emulator_build_failed", () => EmulatorInstaller.BuildAsync(Server(), "/usr/lib/jvm/java-25-openjdk-amd64", Log),
            (_, _, _) => new CommandResult(1, "  compiling\nERROR: build.sh stopped with exit code 1 (see the Maven output above).", ""));
        await Capture("emulator_configure", () => EmulatorInstaller.ConfigureAsync(Server(),
            new DatabaseCredentials("127.0.0.1", 3306, "habbo", "root", "rootpw", "cms", "cmspw", "emulator", "emu'pw"),
            "Europe/Rome", 2048, "/usr/lib/jvm/java-25-openjdk-amd64", Log));
        Unrandom("emulator_configure");
        await Capture("emulator_configure_imager", () => EmulatorInstaller.ConfigureImagerAsync(Server(), "https://hotel.example.com", Log));
        await Capture("emulator_set_settings", () => EmulatorInstaller.SetSettingsAsync(Server(),
            [("ws.whitelist", "*.example.com"), ("a.key", "it's")], "test", Log),
            (_, _, _) => new CommandResult(0, "  done\nSETTINGS=yes", ""));
        await Capture("emulator_read_setting", () => EmulatorInstaller.ReadSettingAsync(Server(), "ws.whitelist"),
            (_, _, _) => new CommandResult(0, "*.example.com\n", ""));
        await Capture("emulator_start", () => EmulatorInstaller.StartAsync(Server(), "*.example.com", Log),
            (_, _, _) => new CommandResult(0, "ERRORS=2\nSYSTEMD=yes\nJAR=/x.jar", ""));
        await Capture("emulator_start_failed", () => EmulatorInstaller.StartAsync(Server(), "*.example.com", Log),
            (_, _, _) => new CommandResult(1, "  loading\n", ""));

        // EmulatorAssetsInstaller (no Assets/ next to the harness, so only the fixed folders and nothing to upload)
        const string assets = "== S0\nloading_icon.png\nwallet/coins.png\n.git/x\n== T0\nwallet/coins.png\n== S1\nbg.png\n== T1\nbg.png\n== S2\n== T2\n";
        await Capture("emulator_assets_inspect", () => EmulatorAssetsInstaller.InspectAsync(Server()), ByKind(assets, "", ""));
        await Capture("emulator_assets_install", () => EmulatorAssetsInstaller.InstallAsync(Server(), Log), ByKind(assets, "  x\nADDED=1", ""));
        await Capture("emulator_assets_install_nothing", () => EmulatorAssetsInstaller.InstallAsync(Server(), Log));

        // UiTextsInstaller
        await Capture("ui_texts_inspect", () => UiTextsInstaller.InspectAsync(Server()),
            (_, _, _) => new CommandResult(0, "LANGS= de en\nCURRENT=de\n", ""));
        await Capture("ui_texts_install", () => UiTextsInstaller.InstallAsync(Server(), "de", Log),
            (_, _, _) => new CommandResult(0, "BACKUP=/var/www/gamedata/config/UITexts.jsonc.bak-1", ""));
        await Capture("ui_texts_install_bad_code", () => UiTextsInstaller.InstallAsync(Server(), "x;y", Log));

        // AssetSync: a downloader folder with fixed files and dates.
        string downloader = Path.Combine(Path.GetTempPath(), "aio-parity-b", "downloader");
        if (Directory.Exists(downloader)) Directory.Delete(downloader, true);
        WriteFile(Path.Combine(downloader, "Habbo_Default", "hof_furni", "hab", "chair.hab"), 10);
        WriteFile(Path.Combine(downloader, "Habbo_Default", "hof_furni", "hab", "table.hab"), 600);
        WriteFile(Path.Combine(downloader, "Habbo_Default", "hof_furni", "hab", "notes.txt"), 3);
        WriteFile(Path.Combine(downloader, "Habbo_Default", "files", "json", "FurnitureData.json"), 50);
        WriteFile(Path.Combine(downloader, "Habbo_Default", "files", "json", "Other.json"), 5);
        const string listing = "== 0\nchair.hab\t10\t1704164645.0\n== 12\nFurnitureData.json\t5\t1.0\n";
        foreach (bool overwrite in new[] { false, true })
        {
            await Capture(overwrite ? "asset_sync_plan_upload_overwrite" : "asset_sync_plan_upload", async () =>
            {
                List<AssetPlan> plans = await AssetSync.PlanAsync(Server(), downloader, overwrite);
                foreach (AssetPlan plan in plans.Where(p => p.UploadCount > 0))
                    await AssetSync.UploadAsync(Server(), plan, _ => { });
            }, ByKind(listing, "", "OK 2"));
        }
        await Capture("asset_sync_upload_failed", async () =>
        {
            List<AssetPlan> plans = await AssetSync.PlanAsync(Server(), downloader, false);
            await AssetSync.UploadAsync(Server(), plans[0], _ => { });
        });
        await Capture("asset_sync_free_space", () => AssetSync.FreeSpaceAsync(Server()), ByKind("123456\n", "", ""));
        await Capture("asset_sync_fix_permissions", () => AssetSync.FixPermissionsAsync(Server(), Log));

        // GamedataFiles
        string local = Path.Combine(Path.GetTempPath(), "aio-parity-b", "explorer");
        if (Directory.Exists(local)) Directory.Delete(local, true);
        WriteFile(Path.Combine(local, "pack", "a.png"), 700);
        WriteFile(Path.Combine(local, "pack", "sub", "b.png"), 20);
        WriteFile(Path.Combine(local, "single.gif"), 1);
        await Capture("gamedata_list", () => GamedataFiles.ListAsync(Server(), "c_images/album1584"),
            ByKind("d\t4096\t1700000000.5\tsub\nf\t12\t1700000000\ta.gif\n", "", ""));
        await Capture("gamedata_list_failed", () => GamedataFiles.ListAsync(Server(), "x"),
            ByKind("ERROR: not a folder\n", "", "", 3));
        await Capture("gamedata_outside", () => GamedataFiles.ListAsync(Server(), "a/../.."));
        await Capture("gamedata_create_folder", () => GamedataFiles.CreateFolderAsync(Server(), "c_images", "new folder"));
        await Capture("gamedata_create_folder_bad_name", () => GamedataFiles.CreateFolderAsync(Server(), "c_images", "a/b"));
        await Capture("gamedata_rename", () => GamedataFiles.RenameAsync(Server(), "", "old's", "new"));
        await Capture("gamedata_delete", () => GamedataFiles.DeleteAsync(Server(), "icons", ["a.png", "b dir"]));
        await Capture("gamedata_files_under", () => GamedataFiles.FilesUnderAsync(Server(), "icons", ["a.png", "sub"]),
            ByKind("5\t./a.png\n7\t./sub/b.png\n", "", ""));
        await Capture("gamedata_upload", () => GamedataFiles.UploadAsync(Server(),
            [Path.Combine(local, "pack") + Path.DirectorySeparatorChar, Path.Combine(local, "single.gif")], "c_images", false, _ => { }),
            ByKind("", "", "OK"));
        await Capture("gamedata_upload_skip", () => GamedataFiles.UploadAsync(Server(),
            [Path.Combine(local, "single.gif")], "", true, _ => { }), ByKind("", "", "ERROR: the target is not a folder", 3));
    }
}
#endif
