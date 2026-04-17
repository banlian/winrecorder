using System.Text.Json;

namespace WinRecorder.Config;

public static class SettingsLoader
{
    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WinRecorder", "settings.json");
    }

    public static AppSettings LoadOrCreateDefault()
    {
        var settingsPath = GetSettingsPath();
        var settingsDir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(settingsDir))
            Directory.CreateDirectory(settingsDir);

        if (!File.Exists(settingsPath))
        {
            var defaults = CreateDefaultSettings();
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
            EnsureLogDirExists(defaults.LogDir);
            return defaults;
        }

        var text = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? CreateDefaultSettings();

        EnsureLogDirExists(settings.LogDir);
        return settings;
    }

    private static void EnsureLogDirExists(string logDir)
    {
        if (string.IsNullOrWhiteSpace(logDir))
            return;
        Directory.CreateDirectory(logDir);
    }

    private static AppSettings CreateDefaultSettings()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new AppSettings
        {
            // Default (you can edit settings.json to point to your preferred directory).
            LogDir = Path.Combine(docs, "winrecorder", "logs"),
            CaptureKeysText = true,
            ExcludedProcessNames = new List<string>(),
            ExcludedWindowTitleSubstrings = new List<string>(),
            MaxEventsPerSecond = 200
        };
    }
}

