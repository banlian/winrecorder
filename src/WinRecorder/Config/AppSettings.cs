namespace WinRecorder.Config;

public sealed class AppSettings
{
    // Example: "D:\\Work\\winrecorder\\logs"
    public string LogDir { get; set; } = "";

    public bool CaptureKeysText { get; set; } = true;

    // Process name matching (e.g. "chrome", "Code", "devenv")
    public List<string> ExcludedProcessNames { get; set; } = new();

    // Case-insensitive substring match on window title.
    public List<string> ExcludedWindowTitleSubstrings { get; set; } = new();

    public int MaxEventsPerSecond { get; set; } = 200;

    public bool ShouldExclude(string? processName, string? windowTitle)
    {
        if (!string.IsNullOrWhiteSpace(processName))
        {
            foreach (var excluded in ExcludedProcessNames)
            {
                if (string.IsNullOrWhiteSpace(excluded))
                    continue;
                if (string.Equals(processName, excluded.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            var title = windowTitle.Trim();
            foreach (var excluded in ExcludedWindowTitleSubstrings)
            {
                if (string.IsNullOrWhiteSpace(excluded))
                    continue;
                if (title.Contains(excluded.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}

