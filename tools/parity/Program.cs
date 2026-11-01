using AllInOneInstaller.Remote;

namespace AllInOneInstaller.Parity;

internal static partial class Program
{
    private static string _out = "";

    private static async Task<int> Main(string[] args)
    {
        _out = args.Length > 0 ? args[0] : Path.Combine("..", "..", "crates", "remote", "tests", "golden");
        Directory.CreateDirectory(_out);
        await Scenarios();
        Console.WriteLine($"golden files written to {Path.GetFullPath(_out)}");
        return 0;
    }

    /// <summary>Runs one scenario and writes what it sent to &lt;name&gt;.txt. Exceptions are written too.</summary>
    private static async Task Capture(string name, Func<Task> scenario, Func<string, string, string?, CommandResult>? respond = null)
    {
        Recorder.Reset();
        if (respond is not null) Recorder.Respond = respond;
        try { await scenario(); }
        catch (Exception ex) { Recorder.Log.Append("!!! ").Append(ex.Message).Append('\n'); }
        File.WriteAllText(Path.Combine(_out, name + ".txt"), Recorder.Log.ToString());
    }

    private static void Text(string name, string text) =>
        File.WriteAllText(Path.Combine(_out, name + ".txt"), text.ReplaceLineEndings("\n"));

    private static ServerConnection Server() => new(new ServerLogin("192.0.2.10", 22, "root", "secret"));

    private static readonly Action<string> Log = _ => { };
}
