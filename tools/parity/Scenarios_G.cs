#if GROUP_G
using System.Reflection;
using System.Text;
using AllInOneInstaller.Remote;
using MySqlConnector;

namespace AllInOneInstaller.Parity;

// Group G: SqlScript (statement splitting) and the parts of SqlSession reachable without a connection (Quote,
// Display, SqlColumn.IsBinary, KeyCondition). SqlSession.OpenAsync and every query need a real MariaDB: the SQL text
// those build inline is replicated in Rust and unit-tested there.
internal static partial class Program
{
    private static readonly (string Name, string Script)[] SqlScripts =
    [
        ("simple", "SELECT 1;\nSELECT 2;\n"),
        ("semicolon_in_strings", "SELECT 'a;b', \"c;d\";\nINSERT INTO t VALUES ('x;y');"),
        ("escaped_quotes", "SELECT 'it\\'s; ok', 'double '' quote;', \"say \\\"hi;\\\"\";\nSELECT 2"),
        ("backticks", "SELECT `we;ird``name` FROM `t;1`;\nSELECT `a\\`;\nSELECT 3;"),
        ("line_comments", "-- first comment; with semicolon\nSELECT 1; -- trailing; comment\n# hash; comment\nSELECT 2;\nSELECT 3 --not a comment\n;"),
        ("block_comments", "/* header; comment\n spanning lines */\nSELECT 1;\nSELECT /* inline; */ 2;\n/*!40101 SET NAMES utf8 */;\n/* only comment */;\nSELECT 3;"),
        ("delimiter_procedure", "DELIMITER $$\nCREATE PROCEDURE p()\nBEGIN\n  SELECT 1;\n  SELECT 'x$$y';\nEND$$\nDELIMITER ;\nCALL p();\n"),
        ("delimiter_trigger", "delimiter //\nCREATE TRIGGER tr BEFORE INSERT ON t FOR EACH ROW\nBEGIN\n  SET NEW.a = 1;\nEND //\ndelimiter ;\nINSERT INTO t VALUES (1);"),
        ("delimiter_tab_crlf", "DELIMITER\t;;\r\nSELECT 1;;\r\nSELECT 2;;\r\nDELIMITER ;\r\nSELECT 3;\r\n"),
        ("delimiter_not_at_start", "SELECT 1 DELIMITER $$;\nSELECT 2;"),
        ("delimiter_empty_value", "DELIMITER \nSELECT 1;"),
        ("delimiter_word_last", "DELIMITER $$\nSELECT 1$$ SELECT 2 $$\nDELIMITER ;\nSELECT 3; DELIMITER"),
        ("empty_statements", ";;;\n  ;\nSELECT 1;;;SELECT 2;\n\n;"),
        ("crlf", "SELECT 1;\r\nSELECT\r\n  2;\r\n\r\nSELECT 3"),
        ("multiline", "INSERT INTO t (a, b)\nVALUES\n  (1, 'one\ntwo'),\n  (2, 'three');\n\nUPDATE t\nSET b = 'x'\nWHERE a = 1;"),
        ("backslash_newline_in_string", "SELECT 'a\\\nb';\nSELECT 2;"),
        ("unterminated_string", "SELECT 1;\nSELECT 'never closed;\nSELECT 2;"),
        ("unterminated_comment", "SELECT 1;\n/* never closed;\nSELECT 2;"),
        ("dashes", "SELECT 5--3;\nSELECT 5 - -3;\nSELECT 1 --\nSELECT 2;\n--"),
        ("unicode_whitespace", " SELECT 'é;ü'; \nSELECT 2;　"),
        ("comment_then_statement", "/* c */SELECT 1;\n# x\n  SELECT 2;"),
        ("trailing_comment", "SELECT 1\n-- done"),
        ("doubled_double_quote", "SELECT \"a\"\"b;\";SELECT 'end'"),
        ("only_comments", "-- a\n# b\n/* c */\n"),
    ];

    private static string ShowSql(string text) => text.Replace("\r", "<CR>").Replace("\t", "<TAB>");

    private static string RenderSplit(string script)
    {
        var text = new StringBuilder();
        text.Append("=== input\n").Append(ShowSql(script)).Append("\n=== statements\n");
        foreach (SqlStatement statement in SqlScript.Split(script))
            text.Append("--- line ").Append(statement.Line).Append('\n').Append(ShowSql(statement.Text)).Append('\n');
        return text.ToString();
    }

    private static async Task GroupG()
    {
        foreach ((string name, string script) in SqlScripts) Text("sql_script_" + name, RenderSplit(script));

        var display = new StringBuilder();
        void D(string name, object? value, string? type = null, int maxBytes = 64) =>
            display.Append(name).Append(": ").Append(SqlSession.Display(value, type is null ? null : new SqlColumn("c", type), maxBytes) ?? "<null>").Append('\n');
        byte[] hundred = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        D("null", null);
        D("bytes_short", new byte[] { 0x00, 0xAB, 0xFF }, "VARBINARY");
        D("bytes_cut", hundred, "BLOB");
        D("bytes_full", hundred, "BLOB", int.MaxValue);
        D("bytes_empty", Array.Empty<byte>(), "BLOB");
        D("datetime", new DateTime(2024, 1, 2, 3, 4, 5), "DATETIME");
        D("datetime_fraction", new DateTime(2024, 1, 2, 3, 4, 5).AddTicks(1_200_000), "DATETIME");
        D("datetime_zero_fraction", new DateTime(2024, 1, 2, 3, 4, 5), "DATETIME");
        D("datetime_micro", new DateTime(2024, 1, 2, 3, 4, 5).AddTicks(10), "TIMESTAMP");
        D("date_column", new DateTime(2024, 1, 2), "DATE");
        D("zero_datetime", new MySqlDateTime(0, 0, 0, 0, 0, 0, 0), "DATETIME");
        D("zero_date", new MySqlDateTime(0, 0, 0, 0, 0, 0, 0), "DATE");
        D("mysql_datetime", new MySqlDateTime(2024, 5, 6, 7, 8, 9, 123456), "DATETIME");
        D("mysql_date", new MySqlDateTime(2024, 5, 6, 0, 0, 0, 0), "DATE");
        D("time_long", new TimeSpan(34, 22, 59, 59), "TIME");
        D("time_negative", -new TimeSpan(1, 2, 3), "TIME");
        D("time_fraction", new TimeSpan(0, 12, 0, 0, 500), "TIME");
        double[] doubles = [1.5, 0.1 + 0.2, 1e15, 1e16, 1e20, 12345678901234567890.0, 0.0001, 0.00001, -2.5e-7, 123456789012345.0, 0.0, 100.0, 1e-300, double.MaxValue, -1234.5678];
        for (int i = 0; i < doubles.Length; i++) D($"double_{i}", doubles[i], "DOUBLE");
        float[] floats = [3.14159f, 1.1f, 1e10f, 1e7f, 1e8f, 1234567f, 12345678f, 0.5f, 1e-5f, 0.0001f, -7.25f];
        for (int i = 0; i < floats.Length; i++) D($"float_{i}", floats[i], "FLOAT");
        D("decimal", 1.50m, "DECIMAL");
        D("decimal_negative", -0.001m, "DECIMAL");
        D("long", -42L, "BIGINT");
        D("ulong_max", ulong.MaxValue, "BIGINT");
        D("sbyte", (sbyte)-1, "TINYINT");
        D("int", 2147483647, "INT");
        D("year", 2024, "YEAR");
        D("string", "héllo, wörld", "VARCHAR");
        D("bit", 258UL, "BIT");
        Text("sql_session_display", display.ToString());

        var misc = new StringBuilder();
        foreach (string type in new[] { "BLOB", "VARBINARY", "binary", "VARCHAR", "GEOMETRY", "LONGBLOB", "TEXT", "INT" })
            misc.Append("is_binary ").Append(type).Append(": ").Append(new SqlColumn("c", type).IsBinary).Append('\n');
        foreach (string name in new[] { "users", "we`ird", "", "a``b" })
            misc.Append("quote ").Append(name).Append(": ").Append(SqlSession.Quote(name)).Append('\n');
        MethodInfo keyCondition = typeof(SqlSession).GetMethod("KeyCondition", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (string[] columns in new[] { new[] { "id" }, new[] { "user_id", "room`id", "slot" } })
        {
            IReadOnlyList<(string Column, object? Value)> key = columns.Select(c => (c, (object?)1)).ToList();
            misc.Append("key_condition ").Append(string.Join(",", columns)).Append(": ").Append(keyCondition.Invoke(null, [key])).Append('\n');
        }
        Text("sql_session_misc", misc.ToString());
        await Task.CompletedTask;
    }
}
#endif
