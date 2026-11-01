using System.Globalization;
using System.Text;
using AllInOneInstaller.Terminal;

namespace AllInOneInstaller.Parity;

// Reads crates/terminal/tests/scenarios.txt, feeds every scenario into TerminalEmulator and writes one dump per
// scenario to crates/terminal/tests/golden. Also writes zero_width.txt, the code point ranges RuneWidth treats as
// zero width (from .NET's Unicode tables), which the Rust port's table must equal.
internal static class Program
{
    private static int Main(string[] args)
    {
        string tests = args.Length > 0 ? args[0] : Path.Combine("..", "..", "crates", "terminal", "tests");
        string golden = Path.Combine(tests, "golden");
        Directory.CreateDirectory(golden);

        string[] lines = File.ReadAllLines(Path.Combine(tests, "scenarios.txt"), Encoding.UTF8);
        int count = 0;
        TerminalEmulator? term = null;
        StringBuilder? output = null;
        string name = "";
        var replies = new StringBuilder();
        int bells = 0, dumps = 0;
        foreach (string raw in lines)
        {
            if (raw.Length == 0 || raw.StartsWith('#')) continue;
            int space = raw.IndexOf(' ');
            string keyword = space < 0 ? raw : raw[..space];
            string rest = space < 0 ? "" : raw[(space + 1)..];
            switch (keyword)
            {
                case "scenario":
                {
                    string[] parts = rest.Split(' ');
                    name = parts[0];
                    term = new TerminalEmulator(int.Parse(parts[1]), int.Parse(parts[2]));
                    replies.Clear();
                    bells = dumps = 0;
                    term.Reply += r => replies.Append(r);
                    term.Bell += () => bells++;
                    output = new StringBuilder();
                    break;
                }
                case "feed":
                    term!.Feed(Unescape(rest));
                    break;
                case "repeat":
                {
                    int sp = rest.IndexOf(' ');
                    int n = int.Parse(rest[..sp]);
                    string body = rest[(sp + 1)..];
                    for (int i = 0; i < n; i++) term!.Feed(Unescape(body.Replace("%d", i.ToString(CultureInfo.InvariantCulture))));
                    break;
                }
                case "resize":
                {
                    string[] parts = rest.Split(' ');
                    term!.Resize(int.Parse(parts[0]), int.Parse(parts[1]));
                    break;
                }
                case "select":
                {
                    int[] v = rest.Split(' ').Select(int.Parse).ToArray();
                    output!.Append($"select {rest} {Escape(term!.Text((v[0], v[1]), (v[2], v[3])))}\n");
                    break;
                }
                case "dump":
                    Dump(term!, output!, ++dumps, replies.ToString(), bells);
                    break;
                case "end":
                    Dump(term!, output!, ++dumps, replies.ToString(), bells);
                    File.WriteAllText(Path.Combine(golden, name + ".txt"), output!.ToString(), new UTF8Encoding(false));
                    count++;
                    break;
                default:
                    throw new InvalidDataException("unknown line: " + raw);
            }
        }

        var ranges = new StringBuilder();
        int start = -1;
        for (int c = 0x300; c <= 0x110000; c++)
        {
            bool zero = c <= 0x10ffff && (c < 0xd800 || c > 0xdfff) && TerminalEmulator.RuneWidth(c) == 0;
            if (zero && start < 0) start = c;
            else if (!zero && start >= 0) { ranges.Append($"{start:X4} {c - 1:X4}\n"); start = -1; }
        }
        File.WriteAllText(Path.Combine(golden, "zero_width.txt"), ranges.ToString(), new UTF8Encoding(false));

        Console.WriteLine($"{count} scenarios written to {Path.GetFullPath(golden)}");
        return 0;
    }

    private static byte[] Unescape(string text)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '\\')
            {
                int end = char.IsHighSurrogate(c) ? i + 2 : i + 1;
                bytes.AddRange(Encoding.UTF8.GetBytes(text[i..end]));
                i = end - 1;
                continue;
            }
            char e = text[++i];
            switch (e)
            {
                case 'e': bytes.Add(0x1b); break;
                case 'r': bytes.Add(0x0d); break;
                case 'n': bytes.Add(0x0a); break;
                case 't': bytes.Add(0x09); break;
                case 's': bytes.Add(0x20); break;
                case '\\': bytes.Add((byte)'\\'); break;
                case 'x': bytes.Add(byte.Parse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber)); i += 2; break;
                default: throw new InvalidDataException("unknown escape \\" + e);
            }
        }
        return bytes.ToArray();
    }

    private static string EscapeRune(int r) =>
        r == '\\' ? "\\\\" : r < 0x20 || r is >= 0x7f and <= 0x9f ? $"\\u{{{r:X}}}" : char.ConvertFromUtf32(r);

    private static string Escape(string text)
    {
        var b = new StringBuilder();
        foreach (Rune r in text.EnumerateRunes()) b.Append(EscapeRune(r.Value));
        return b.ToString();
    }

    private static void DumpLine(StringBuilder o, string label, TerminalLine line)
    {
        o.Append(label).Append(line.Wrapped ? " w |" : " - |");
        foreach (TerminalCell c in line.Cells) o.Append(EscapeRune(c.Rune));
        o.Append("|\n");
        var runs = new List<string>();
        int x = 0;
        while (x < line.Cells.Length)
        {
            TerminalCell first = line.Cells[x];
            int start = x;
            while (x < line.Cells.Length && line.Cells[x].Foreground == first.Foreground && line.Cells[x].Background == first.Background
                   && line.Cells[x].Flags == first.Flags) x++;
            if (first.Foreground != TerminalCell.DefaultColor || first.Background != TerminalCell.DefaultColor || first.Flags != 0)
                runs.Add($"{start}-{x - 1}:{first.Foreground}/{first.Background}/{(int)first.Flags}");
        }
        if (runs.Count > 0) o.Append("     ").Append(string.Join(' ', runs)).Append('\n');
    }

    private static void Dump(TerminalEmulator t, StringBuilder o, int number, string replies, int bells)
    {
        o.Append($"# dump {number}\n");
        o.Append($"size {t.Columns}x{t.Rows}\n");
        o.Append($"cursor {t.CursorX},{t.CursorY} visible={(t.CursorVisible ? 1 : 0)}\n");
        o.Append($"modes app_cursor={(t.ApplicationCursorKeys ? 1 : 0)} bracketed={(t.BracketedPaste ? 1 : 0)} alternate={(t.AlternateScreen ? 1 : 0)}\n");
        o.Append($"title {Escape(t.Title)}\n");
        o.Append($"scrollback {t.Scrollback.Count} dropped {t.ScrollbackDropped}\n");
        o.Append($"replies {Escape(replies)}\n");
        o.Append($"bells {bells}\n");
        o.Append("screen\n");
        for (int y = 0; y < t.Rows; y++) DumpLine(o, y.ToString("D2", CultureInfo.InvariantCulture), t.Line(y));
        o.Append("scrollback-lines\n");
        int n = t.Scrollback.Count;
        for (int i = 0; i < n; i++)
        {
            if (n > 13 && i >= 3 && i < n - 10) continue;
            DumpLine(o, i.ToString(CultureInfo.InvariantCulture), t.Scrollback[i]);
        }
        if (t.AbsoluteCount <= 300) o.Append($"text {Escape(t.Text((0, 0), (t.AbsoluteCount - 1, t.Columns - 1)))}\n");
    }
}
