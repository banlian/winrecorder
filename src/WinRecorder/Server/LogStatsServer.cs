using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WinRecorder.Logging;

namespace WinRecorder.Server;

public sealed class LogStatsServer : IDisposable
{
    private static readonly JsonSerializerOptions StatsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _logDir;
    private readonly int _port;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private int _started;
    private readonly string _htmlPath = Path.Combine(AppContext.BaseDirectory, "Server", "log-stats.html");

    public LogStatsServer(string logDir, int port = 8099)
    {
        _logDir = logDir;
        _port = port;
    }

    public string BaseUrl => $"http://localhost:{_port}/";

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        try
        {
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _serverTask = Task.Run(ServerLoopAsync);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LogStatsServer.Start", ex);
            Interlocked.Exchange(ref _started, 0);
        }
    }

    public void OpenBrowser()
    {
        try
        {
            var url = $"{BaseUrl}?file={Uri.EscapeDataString(GetDefaultLogFilePath())}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LogStatsServer.OpenBrowser", ex);
        }
    }

    public void OpenServicePage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = BaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LogStatsServer.OpenServicePage", ex);
        }
    }

    private async Task ServerLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("LogStatsServer.ServerLoopAsync", ex);
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            if (request.Url?.AbsolutePath == "/api/stats")
            {
                HandleApi(context);
                return;
            }

            var html = LoadHtmlPage();
            WriteResponse(context.Response, "text/html; charset=utf-8", html);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LogStatsServer.HandleRequest", ex);
            context.Response.StatusCode = 500;
            WriteResponse(context.Response, "text/plain; charset=utf-8", ex.Message);
        }
        finally
        {
            try { context.Response.OutputStream.Close(); } catch { }
        }
    }

    private void HandleApi(HttpListenerContext context)
    {
        try
        {
            var queryDate = context.Request.QueryString["date"];
            var queryFile = context.Request.QueryString["file"];
            var targetFile = ResolveTargetLogFilePath(queryDate, queryFile);
            var stats = BuildStats(targetFile, queryDate);
            var json = JsonSerializer.Serialize(stats, StatsJsonOptions);
            WriteResponse(context.Response, "application/json; charset=utf-8", json);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            WriteResponse(context.Response, "text/plain; charset=utf-8", ex.Message);
        }
    }

    private static void WriteResponse(HttpListenerResponse response, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private string GetDefaultLogFilePath()
    {
        return Path.Combine(_logDir, $"{DateTime.Today:yyyy-MM-dd}.md");
    }

    private string ResolveTargetLogFilePath(string? queryDate, string? queryFile)
    {
        if (!string.IsNullOrWhiteSpace(queryDate))
        {
            if (!DateTime.TryParse(queryDate, out var parsedDate))
                throw new ArgumentException($"Invalid date format: {queryDate}. Expected yyyy-MM-dd.");

            return Path.Combine(_logDir, $"{parsedDate:yyyy-MM-dd}.md");
        }

        return string.IsNullOrWhiteSpace(queryFile) ? GetDefaultLogFilePath() : queryFile;
    }

    private static LogStatsResult BuildStats(string path, string? queryDate)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Log file not found: {path}");

        var lineRegex = new Regex(
            "^- (?<time>\\d{2}:\\d{2}:\\d{2}\\.\\d{3}) \\| (?<app>[^|]+?) \\| \"(?<title>.*?)\" \\| (?<detail>.+)$",
            RegexOptions.Compiled);

        var appCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var hourly = new int[24];
        var eventTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["foreground"] = 0,
            ["mouse"] = 0,
            ["key"] = 0,
            ["dwell"] = 0,
            ["lifecycle"] = 0,
            ["input"] = 0,
            ["other"] = 0
        };

        var total = 0;
        foreach (var line in File.ReadLines(path))
        {
            var m = lineRegex.Match(line);
            if (!m.Success)
                continue;

            total++;
            var app = m.Groups["app"].Value.Trim();
            var detail = m.Groups["detail"].Value.Trim();
            var hourText = m.Groups["time"].Value.AsSpan(0, 2);
            if (int.TryParse(hourText, out var h) && h >= 0 && h <= 23)
                hourly[h]++;

            appCount.TryGetValue(app, out var current);
            appCount[app] = current + 1;

            if (detail.StartsWith("foreground", StringComparison.Ordinal)) eventTypes["foreground"]++;
            else if (detail.StartsWith("mouse:", StringComparison.Ordinal)) eventTypes["mouse"]++;
            else if (detail.StartsWith("key:", StringComparison.Ordinal)) eventTypes["key"]++;
            else if (detail.StartsWith("window:dwell", StringComparison.Ordinal)) eventTypes["dwell"]++;
            else if (detail.StartsWith("app:", StringComparison.Ordinal)) eventTypes["lifecycle"]++;
            else if (detail.StartsWith("input:text", StringComparison.Ordinal)) eventTypes["input"]++;
            else eventTypes["other"]++;
        }

        var topApps = appCount
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(x => new NameCount(x.Key, x.Value))
            .ToList();

        var hourlyRows = Enumerable.Range(0, 24)
            .Select(i => new HourCount(i.ToString("00"), hourly[i]))
            .ToList();

        return new LogStatsResult(
            path,
            ResolveSelectedDate(path, queryDate),
            DateTime.Now.ToString("s"),
            total,
            eventTypes,
            topApps,
            hourlyRows);
    }

    private static string ResolveSelectedDate(string path, string? queryDate)
    {
        if (!string.IsNullOrWhiteSpace(queryDate))
            return queryDate;

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (DateTime.TryParse(fileNameWithoutExtension, out var date))
            return date.ToString("yyyy-MM-dd");

        return DateTime.Today.ToString("yyyy-MM-dd");
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        try { _serverTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }

    private string LoadHtmlPage()
    {
        try
        {
            if (File.Exists(_htmlPath))
                return File.ReadAllText(_htmlPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LogStatsServer.LoadHtmlPage", ex);
        }

        return """
<!doctype html>
<html><body><h1>log-stats.html not found</h1><p>Please verify Server/log-stats.html exists in app output.</p></body></html>
""";
    }
}

public sealed record NameCount(string Name, int Count);
public sealed record HourCount(string Hour, int Count);
public sealed record LogStatsResult(
    string File,
    string SelectedDate,
    string GeneratedAt,
    int TotalEvents,
    Dictionary<string, int> EventTypes,
    List<NameCount> TopApps,
    List<HourCount> Hourly);
