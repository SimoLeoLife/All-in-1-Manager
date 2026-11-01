// A stand-in for SourceCode/Remote/ServerConnection.cs with the same public surface. It records every command the
// Remote/*.cs installers send and answers from Recorder.Respond, so the parity harness can write the exact scripts the
// C# installer runs to golden files, which the Rust port's tests compare with.
using System.Text;

namespace AllInOneInstaller.Remote;

public sealed record ServerLogin(string Host, int Port, string User, string Password, IReadOnlyList<string>? PrivateKeys = null,
    string KeyPassphrase = "")
{
    public bool UsesKey => PrivateKeys is { Count: > 0 };

    public static (string Host, int Port) ParseHost(string text)
    {
        text = text.Trim();
        if (text.StartsWith('[') && text.IndexOf(']') is var close and > 0)
        {
            string host = text[1..close];
            string rest = text[(close + 1)..];
            return rest.StartsWith(':') && int.TryParse(rest[1..], out int p) ? (host, p) : (host, 22);
        }
        int colon = text.LastIndexOf(':');
        if (colon > 0 && text.IndexOf(':') == colon && int.TryParse(text[(colon + 1)..], out int port))
            return (text[..colon], port);
        return (text, 22);
    }
}

public sealed record OsInfo(string Id, string VersionId, string PrettyName)
{
    public bool IsSupported => Id is "ubuntu" or "debian";
}

public enum ConnectStage { Login, OperatingSystem, Root }

public sealed record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class ServerConnectionException(string message, Exception? inner = null) : Exception(message, inner);

public static class Recorder
{
    public static readonly StringBuilder Log = new();
    /// <summary>Answers a command (kind, command, stdin). Default: exit 0, no output.</summary>
    public static Func<string, string, string?, CommandResult> Respond = (_, _, _) => new CommandResult(0, "", "");

    public static void Reset()
    {
        Log.Clear();
        Respond = (_, _, _) => new CommandResult(0, "", "");
    }

    public static CommandResult Record(string kind, string command, string? stdin)
    {
        Log.Append("=== ").Append(kind).Append('\n');
        Log.Append(command.ReplaceLineEndings("\n"));
        if (!command.EndsWith('\n')) Log.Append('\n');
        if (stdin is not null)
        {
            Log.Append("--- stdin\n").Append(stdin.ReplaceLineEndings("\n"));
            if (!stdin.EndsWith('\n')) Log.Append('\n');
        }
        return Respond(kind, command, stdin);
    }
}

public sealed class ServerConnection : IAsyncDisposable
{
    public ServerConnection(ServerLogin login) { Login = login; }

    public string LoginUser { get; set; } = "root";
    public string HostKeyFingerprint { get; set; } = "ssh-ed25519 SHA256:test";
    public OsInfo? Os { get; set; } = new("ubuntu", "24.04", "Ubuntu 24.04 LTS");
    public bool IsRoot => true;
    public bool IsConnected => true;
    public ServerLogin Login { get; }

    public static Task<ServerConnection> ConnectAsync(ServerLogin login, Action<string> log, Action<ConnectStage> stage,
        CancellationToken token = default)
    {
        Recorder.Record("connect", $"{login.User}@{login.Host}:{login.Port} key={login.UsesKey}", null);
        return Task.FromResult(new ServerConnection(login));
    }

    public Task<CommandResult> RunAsync(string command, CancellationToken token = default, string? input = null) =>
        Task.FromResult(Recorder.Record("run", command, input));

    public Task<CommandResult> RunAsRootAsync(string command, CancellationToken token = default) =>
        Task.FromResult(Recorder.Record("root", command, null));

    public Task<int> RunAsRootStreamingAsync(string script, Action<string> onLine, CancellationToken token = default, string? stdin = null)
    {
        CommandResult result = Recorder.Record("stream", script, stdin);
        foreach (string line in result.Output.Split('\n')) if (line.Length > 0) onLine(line);
        return Task.FromResult(result.ExitCode);
    }

    public Task FollowAsRootAsync(string script, Action<string> onLine, CancellationToken token)
    {
        Recorder.Record("follow", script, null);
        return Task.CompletedTask;
    }

    public static string FollowScript(string command) => $"""
        setsid bash -c {ShellQuote(command)} &
        follower=$!
        wait $ALL_IN_1_WATCH
        kill -- -$follower 2>/dev/null
        true
        """;

    public async Task<CommandResult> RunAsRootWithInputAsync(string script, Func<Stream, CancellationToken, Task> writeInput,
        CancellationToken token = default)
    {
        using var input = new MemoryStream();
        await writeInput(input, token);
        CommandResult result = Recorder.Record("input", script, $"<{input.Length} bytes>");
        return result;
    }

    public Task DownloadAsync(string remotePath, string localPath, Action<ulong>? progress = null, CancellationToken token = default)
    {
        Recorder.Record("download", remotePath + " -> " + localPath, null);
        return Task.CompletedTask;
    }

    public Task DownloadFilesAsync(IReadOnlyList<(string Remote, string Local)> files, Action<long>? progress = null, CancellationToken token = default)
    {
        Recorder.Record("download-files", string.Join("\n", files.Select(f => f.Remote + " -> " + f.Local)), null);
        return Task.CompletedTask;
    }

    public Task<byte[]?> ReadFileAsync(string remotePath, long maxBytes, CancellationToken token = default)
    {
        Recorder.Record("read-file", remotePath, null);
        return Task.FromResult<byte[]?>([]);
    }

    public Task UploadAsync(string localPath, string remotePath, Action<ulong>? progress = null, CancellationToken token = default)
    {
        Recorder.Record("upload", localPath + " -> " + remotePath, null);
        return Task.CompletedTask;
    }

    public PortForward ForwardLocalPort(string host, int port)
    {
        Recorder.Record("forward", $"{host}:{port}", null);
        return new PortForward();
    }

    public sealed class PortForward : IDisposable
    {
        public int LocalPort => 13306;
        public void Dispose() { }
    }

    public static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
