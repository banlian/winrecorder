using WinRecorder.Logging;
using WinRecorder.Models;

namespace WinRecorder.Tests;

public static class EntryFormatterTests
{
    public static void RunAll()
    {
        Format_ReplacesQuotesAndNewlines();
        Format_EmptyColumnsWhenOptionalValuesAreNull();
        Writer_WritesHeaderAndLines();
    }

    private static void Format_ReplacesQuotesAndNewlines()
    {
        var ts2 = new DateTimeOffset(2026, 3, 25, 14, 23, 12, 4, TimeSpan.Zero);
        var ev2 = new UiEvent(
            timestamp: ts2,
            type: UiEventType.Mouse,
            processName: "chrome.exe",
            windowTitle: "main\"ts",
            eventCode: "mouse:leftClick",
            details: "x=1\ny=2");

        var actual = EntryFormatter.Format(ev2);
        var expected = "- 14:23:12.004 | chrome.exe | \"main\\\"ts\" | mouse:leftClick; x=1 y=2";
        AssertEqual(expected, actual, "Format_ReplacesQuotesAndNewlines");
    }

    private static void Format_EmptyColumnsWhenOptionalValuesAreNull()
    {
        var ts = new DateTimeOffset(2026, 3, 25, 8, 1, 2, 3, TimeSpan.Zero);

        var ev = new UiEvent(
            timestamp: ts,
            type: UiEventType.ForegroundChanged,
            processName: null,
            windowTitle: null,
            eventCode: "foreground",
            details: null);

        var actual = EntryFormatter.Format(ev);
        var expected = "- 08:01:02.003 |  | \"\" | foreground";
        AssertEqual(expected, actual, "Format_EmptyColumnsWhenOptionalValuesAreNull");
    }

    private static void Writer_WritesHeaderAndLines()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "WinRecorderSelfTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);

        try
        {
            var writer = new MarkdownDailyWriter(logDir);

            var ts1 = new DateTimeOffset(2026, 3, 25, 9, 10, 11, 123, TimeSpan.Zero);
            var ts2 = new DateTimeOffset(2026, 3, 25, 9, 10, 12, 4, TimeSpan.Zero);

            var ev1 = new UiEvent(
                timestamp: ts1,
                type: UiEventType.Mouse,
                processName: "app.exe",
                windowTitle: "title",
                eventCode: "mouse:leftClick",
                details: "x=1 y=2");

            var ev2 = new UiEvent(
                timestamp: ts2,
                type: UiEventType.Keyboard,
                processName: "app.exe",
                windowTitle: "title",
                eventCode: "key:Ctrl+K",
                details: "text=\"a\"");

            writer.AppendBatchAsync(new[] { ev1, ev2 }).GetAwaiter().GetResult();

            var date = ts1.ToString("yyyy-MM-dd");
            var path = Path.Combine(logDir, $"{date}.md");
            if (!File.Exists(path))
                throw new InvalidOperationException("Writer didn't create md file.");

            var lines = File.ReadAllLines(path);
            if (lines.Length != 3)
                throw new InvalidOperationException($"Expected 3 lines (header + 2 events), got {lines.Length}.");

            var expectedHeader = $"# {date} WinRecorder Log";
            AssertEqual(expectedHeader, lines[0], "Writer header");
            AssertEqual(EntryFormatter.Format(ev1), lines[1], "Writer line1");
            AssertEqual(EntryFormatter.Format(ev2), lines[2], "Writer line2");
        }
        finally
        {
            try { Directory.Delete(logDir, recursive: true); } catch { }
        }
    }

    private static void AssertEqual(string expected, string actual, string testName)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{testName} failed.\nExpected: {expected}\nActual:   {actual}");
        }
    }
}

