using System.Text.Json;

namespace WinRecorder.Config;

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions JsonReadOptions = new() { PropertyNameCaseInsensitive = true };

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
            Save(defaults);
            return defaults;
        }

        var text = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(text, JsonReadOptions) ?? CreateDefaultSettings();

        EnsureLogDirExists(settings.LogDir);
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var settingsPath = GetSettingsPath();
        var settingsDir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(settingsDir))
            Directory.CreateDirectory(settingsDir);

        var json = JsonSerializer.Serialize(settings, JsonWriteOptions);
        File.WriteAllText(settingsPath, json);
        EnsureLogDirExists(settings.LogDir);
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

