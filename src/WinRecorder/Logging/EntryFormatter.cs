using System.Text;
using WinRecorder.Models;

namespace WinRecorder.Logging;

public static class EntryFormatter
{
    public static string Format(UiEvent ev)
    {
        // Keep output stable for AI parsing: single-line markdown list item.
        var ts = ev.Timestamp.ToString("HH:mm:ss.fff");
        var processName = string.IsNullOrWhiteSpace(ev.ProcessName) ? "" : ev.ProcessName.Trim();
        var windowTitle = ev.WindowTitle ?? "";
        windowTitle = windowTitle.Replace("\"", "\\\"");
        windowTitle = windowTitle.Replace("\r", " ").Replace("\n", " ");

        var details = ev.Details ?? "";
        details = details.Replace("\r", " ").Replace("\n", " ");

        // Always render quoted title to keep the column layout predictable.
        // Example:
        // - 14:23:12.004 | vscode.exe | "main.ts" | key:Ctrl+K; text="..."
        return $"- {ts} | {processName} | \"{windowTitle}\" | {RenderEventDetails(ev.EventCode, details)}";
    }

    private static string RenderEventDetails(string eventCode, string details)
    {
        var sb = new StringBuilder();
        sb.Append(eventCode ?? "");
        if (!string.IsNullOrWhiteSpace(details))
        {
            sb.Append("; ");
            sb.Append(details.Trim());
        }
        return sb.ToString();
    }
}

