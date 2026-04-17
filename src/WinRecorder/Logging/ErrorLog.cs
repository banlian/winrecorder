namespace WinRecorder.Logging;

/// <summary>
/// Best-effort file log for diagnostics when hooks or writer fail; avoids silent process exit.
/// </summary>
internal static class ErrorLog
{
    private static readonly object Gate = new();

    public static void Write(string category, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinRecorder");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "errors.log");
            var msg = ex == null ? "" : ex.ToString();
            if (msg.Length > 8000)
                msg = msg[..8000] + "\n...(truncated)";
            var line = $"{DateTimeOffset.Now:O}\t{category}\t{msg.Replace('\r', ' ').Replace('\n', ' ')}";
            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // ignore
        }
    }
}
