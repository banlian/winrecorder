using System.Collections.Concurrent;
using WinRecorder.Models;

namespace WinRecorder.Logging;

public sealed class MarkdownDailyWriter
{
    private readonly string _logDir;
    private readonly ConcurrentDictionary<string, object> _fileLocks = new();

    public MarkdownDailyWriter(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
    }

    public Task AppendAsync(UiEvent ev, CancellationToken cancellationToken = default)
    {
        return AppendBatchAsync(new[] { ev }, cancellationToken);
    }

    public Task AppendBatchAsync(IReadOnlyList<UiEvent> events, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (events.Count == 0)
            return Task.CompletedTask;

        // Keep it simple: writer-strategy will group by date and call this per date.
        var date = events[0].Timestamp.ToString("yyyy-MM-dd");
        var path = Path.Combine(_logDir, $"{date}.md");
        var fileLock = _fileLocks.GetOrAdd(path, _ => new object());

        lock (fileLock)
        {
            var isNewFile = !File.Exists(path);
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);

            if (isNewFile)
            {
                writer.WriteLine($"# {date} WinRecorder Log");
            }

            for (var i = 0; i < events.Count; i++)
                writer.WriteLine(EntryFormatter.Format(events[i]));

            writer.Flush();
        }

        return Task.CompletedTask;
    }
}

