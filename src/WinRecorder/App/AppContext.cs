using System.Threading.Channels;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Windows.Forms;
using WinRecorder.Config;
using WinRecorder.Hooks;
using WinRecorder.Logging;
using WinRecorder.Models;
using WinRecorder.Services;

namespace WinRecorder.App;

public sealed class AppContext : ApplicationContext, IDisposable
{
    private readonly AppSettings _settings;
    private readonly MarkdownDailyWriter _writer;
    private readonly Channel<UiEvent> _channel;
    private readonly CancellationTokenSource _cts = new();

    private readonly Task _writerTask;
    private int _cleanupDone;
    private int _startupLogged;
    private int _shutdownLogged;
    private readonly object _lastWindowGate = new();
    private WindowInfo? _lastWindowInfo;

    private readonly ForegroundWindowHook _foregroundHook;
    private readonly MouseHook _mouseHook;
    private readonly KeyboardHook _keyboardHook;

    private readonly KeyboardTextTranslator _keyboardTextTranslator;
    private readonly MouseTargetResolver _mouseTargetResolver;
    private readonly EventDeduplicator _eventDeduplicator = new();

    private volatile bool _paused;
    private readonly TrayIconController _tray;

    /// <summary>
    /// WinForms expects a MainForm for a stable message loop. Tray-only contexts without a form
    /// can exit unexpectedly on some systems / after long runs.
    /// </summary>
    private readonly Form _messagePumpForm;

    // Very simple limiter: drop when more than N events occur within the same second.
    private readonly object _rateGate = new();
    private long _rateSecond = DateTimeOffset.Now.ToUnixTimeSeconds();
    private int _rateCount = 0;
    private DateTimeOffset _lastForegroundEmitTs = DateTimeOffset.MinValue;
    private string? _lastForegroundProcess;
    private string? _lastForegroundTitle;

    // Cross-batch keyboard input session state:
    // keep accumulating chars until delimiter/context switch/long idle.
    private readonly object _keyboardSessionGate = new();
    private readonly StringBuilder _keyboardSessionBuffer = new();
    private DateTimeOffset _keyboardSessionStartTs = DateTimeOffset.MinValue;
    private DateTimeOffset _keyboardSessionLastTs = DateTimeOffset.MinValue;
    private string? _keyboardSessionProcess;
    private string? _keyboardSessionTitle;

    public AppContext()
    {
        _messagePumpForm = CreateMessagePumpForm();
        MainForm = _messagePumpForm;

        _settings = SettingsLoader.LoadOrCreateDefault();
        _writer = new MarkdownDailyWriter(_settings.LogDir);

        _channel = Channel.CreateBounded<UiEvent>(new BoundedChannelOptions(capacity: 5000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

        _writerTask = Task.Run(() => WriterLoopAsync(_cts.Token));
        LogLifecycleEventOnce(ref _startupLogged, "app:start", "reason=startup");

        _keyboardTextTranslator = new KeyboardTextTranslator();
        _mouseTargetResolver = new MouseTargetResolver();

        _foregroundHook = new ForegroundWindowHook(new WindowInfoProvider(), onEvent: HandleHookEvent);
        _mouseHook = new MouseHook(onEvent: HandleHookEvent, targetResolver: _mouseTargetResolver);
        _keyboardHook = new KeyboardHook(onEvent: HandleHookEvent, translator: _keyboardTextTranslator, captureKeysText: _settings.CaptureKeysText);

        _tray = new TrayIconController(_settings);
        _tray.PauseToggled += () => TogglePause();
        _tray.ExitRequested += () => ExitThreadSafe();

        _tray.SetPaused(_paused);
        RegisterSystemSessionEvents();

        // Start hooks after the UI message loop exists.
        // In WinForms, Application.Run creates the loop, so hooking immediately is usually fine.
        // If you find early events missing, move Start() to Shown event later.
        StartHooks();
    }

    private static Form CreateMessagePumpForm()
    {
        var f = new Form
        {
            Text = "WinRecorder",
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
            Opacity = 0,
            MinimizeBox = false,
            MaximizeBox = false,
            ControlBox = false
        };
        f.Load += (_, _) => f.Hide();
        return f;
    }

    protected override void OnMainFormClosed(object? sender, EventArgs e)
    {
        try
        {
            Cleanup();
        }
        catch
        {
            // ignore
        }
        base.OnMainFormClosed(sender, e);
    }

    private void StartHooks()
    {
        try
        {
            _foregroundHook.Start();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("ForegroundWindowHook.Start", ex);
        }

        try
        {
            _mouseHook.Start();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("MouseHook.Start", ex);
        }

        try
        {
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("KeyboardHook.Start", ex);
        }
    }

    private void RegisterSystemSessionEvents()
    {
        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("SystemEvents.SessionSwitch subscribe", ex);
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        try
        {
            string? eventCode = e.Reason switch
            {
                SessionSwitchReason.SessionLogon => "system:login",
                SessionSwitchReason.SessionLogoff => "system:logout",
                _ => null
            };

            if (eventCode == null)
                return;

            var ev = new UiEvent(
                timestamp: DateTimeOffset.Now,
                type: UiEventType.ForegroundChanged,
                processName: "System",
                windowTitle: "Session",
                eventCode: eventCode,
                details: $"reason={e.Reason}");

            _channel.Writer.TryWrite(ev);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("SystemEvents.SessionSwitch handler", ex);
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _tray.SetPaused(_paused);
    }

    private void ExitThreadSafe()
    {
        try
        {
            var mf = MainForm ?? _messagePumpForm;
            if (mf is { IsHandleCreated: true })
            {
                mf.BeginInvoke(() =>
                {
                    try
                    {
                        mf.Close();
                    }
                    catch
                    {
                        try { Application.Exit(); } catch { }
                    }
                });
            }
            else
            {
                mf?.Close();
            }
        }
        catch
        {
            try { Application.Exit(); } catch { }
        }
    }

    private void HandleHookEvent(UiEvent raw)
    {
        try
        {
            if (_paused)
                return;

            if (!TryAllowEventByRateLimit())
                return;

            UiEvent? dwellEvent = null;
            if (raw.Type == UiEventType.ForegroundChanged)
                dwellEvent = TryBuildWindowDwellEvent(raw);

            var enriched = Enrich(raw);
            if (enriched == null)
                return;

            if (!_eventDeduplicator.ShouldEmit(enriched))
                return;

            if (dwellEvent != null && _eventDeduplicator.ShouldEmit(dwellEvent))
                _channel.Writer.TryWrite(dwellEvent);

            // Best-effort enqueue; bounded channel may drop.
            _channel.Writer.TryWrite(enriched);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("HandleHookEvent", ex);
        }
    }

    private UiEvent? Enrich(UiEvent ev)
    {
        if (ev.Type == UiEventType.ForegroundChanged)
        {
            if (string.IsNullOrWhiteSpace(ev.WindowTitle))
                return null;

            lock (_lastWindowGate)
            {
                _lastWindowInfo = new WindowInfo(ev.ProcessName, ev.WindowTitle);
            }

            // Suppress short-interval duplicates from app transitions.
            var now = ev.Timestamp;
            if (string.Equals(_lastForegroundProcess, ev.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_lastForegroundTitle, ev.WindowTitle, StringComparison.Ordinal) &&
                (now - _lastForegroundEmitTs).TotalMilliseconds < 800)
            {
                return null;
            }

            _lastForegroundEmitTs = now;
            _lastForegroundProcess = ev.ProcessName;
            _lastForegroundTitle = ev.WindowTitle;

            if (_settings.ShouldExclude(ev.ProcessName, ev.WindowTitle))
                return null;

            return ev;
        }

        WindowInfo? last;
        lock (_lastWindowGate)
        {
            last = _lastWindowInfo;
        }

        var proc = last?.ProcessName;
        var title = last?.WindowTitle;

        if (_settings.ShouldExclude(proc, title))
            return null;

        // Re-create UiEvent to attach window/process context.
        return new UiEvent(
            timestamp: ev.Timestamp,
            type: ev.Type,
            processName: proc,
            windowTitle: title,
            eventCode: ev.EventCode,
            details: ev.Details);
    }

    private bool TryAllowEventByRateLimit()
    {
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        lock (_rateGate)
        {
            if (now != _rateSecond)
            {
                _rateSecond = now;
                _rateCount = 0;
            }

            if (_rateCount >= _settings.MaxEventsPerSecond)
                return false;

            _rateCount++;
            return true;
        }
    }

    private async Task WriterLoopAsync(CancellationToken ct)
    {
        var reader = _channel.Reader;
        var batch = new List<UiEvent>(capacity: 128);
        var lastFlush = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                try
                {
                    if (!await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                        break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (reader.TryRead(out var ev))
                {
                    batch.Add(ev);
                    if (batch.Count >= 100 || lastFlush.ElapsedMilliseconds >= 1000)
                    {
                        await SafeFlushBatchAsync(batch, ct).ConfigureAwait(false);
                        batch.Clear();
                        lastFlush.Restart();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("WriterLoopAsync outer", ex);
        }

        // Drain remaining (ignore cancellation for final flush).
        if (batch.Count > 0)
        {
            await SafeFlushBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }

        // Ensure final keyboard session text is flushed when loop ends.
        await FlushPendingKeyboardSessionAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SafeFlushBatchAsync(List<UiEvent> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        try
        {
            await FlushBatchAsync(batch, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            ErrorLog.Write("FlushBatchAsync", ex);
        }
    }

    private async Task FlushBatchAsync(List<UiEvent> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        var optimized = OptimizeKeyboardInput(batch, forceFlush: false);
        if (optimized.Count == 0)
            return;

        // Partition by date string to avoid mixing across midnight.
        var byDate = new Dictionary<string, List<UiEvent>>(StringComparer.Ordinal);
        foreach (var ev in optimized)
        {
            var date = ev.Timestamp.ToString("yyyy-MM-dd");
            if (!byDate.TryGetValue(date, out var list))
            {
                list = new List<UiEvent>();
                byDate[date] = list;
            }
            list.Add(ev);
        }

        foreach (var kv in byDate)
        {
            await _writer.AppendBatchAsync(kv.Value, ct).ConfigureAwait(false);
        }
    }

    private async Task FlushPendingKeyboardSessionAsync(CancellationToken ct)
    {
        var pending = OptimizeKeyboardInput(Array.Empty<UiEvent>(), forceFlush: true);
        if (pending.Count == 0)
            return;

        var byDate = new Dictionary<string, List<UiEvent>>(StringComparer.Ordinal);
        foreach (var ev in pending)
        {
            var date = ev.Timestamp.ToString("yyyy-MM-dd");
            if (!byDate.TryGetValue(date, out var list))
            {
                list = new List<UiEvent>();
                byDate[date] = list;
            }
            list.Add(ev);
        }

        foreach (var kv in byDate)
        {
            await _writer.AppendBatchAsync(kv.Value, ct).ConfigureAwait(false);
        }
    }

    private List<UiEvent> OptimizeKeyboardInput(IReadOnlyList<UiEvent> source, bool forceFlush)
    {
        var result = new List<UiEvent>(source.Count);

        lock (_keyboardSessionGate)
        {
            void FlushBufferToResult()
            {
                if (_keyboardSessionBuffer.Length == 0)
                    return;

                var text = EscapeTextForDetails(_keyboardSessionBuffer.ToString());
                result.Add(new UiEvent(
                    timestamp: _keyboardSessionStartTs,
                    type: UiEventType.Keyboard,
                    processName: _keyboardSessionProcess,
                    windowTitle: _keyboardSessionTitle,
                    eventCode: "input:text",
                    details: $"text=\"{text}\""));

                _keyboardSessionBuffer.Clear();
                _keyboardSessionProcess = null;
                _keyboardSessionTitle = null;
                _keyboardSessionStartTs = DateTimeOffset.MinValue;
                _keyboardSessionLastTs = DateTimeOffset.MinValue;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var ev = source[i];

                // Any non-keyboard event acts as delimiter and flushes pending text.
                if (ev.Type != UiEventType.Keyboard)
                {
                    FlushBufferToResult();
                    result.Add(ev);
                    continue;
                }

                // Delimiter keys: flush pending text first, then keep this event.
                if (ev.EventCode is "key:Enter" or "key:Tab" or "key:Esc" or "key:Delete" ||
                    ev.EventCode.StartsWith("shortcut:", StringComparison.Ordinal))
                {
                    FlushBufferToResult();
                    result.Add(ev);
                    continue;
                }

                if (ev.EventCode == "input:char")
                {
                    var ch = TryExtractTextFromDetails(ev.Details);
                    if (!string.IsNullOrEmpty(ch))
                    {
                        var sameContext =
                            string.Equals(_keyboardSessionProcess, ev.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(_keyboardSessionTitle, ev.WindowTitle, StringComparison.Ordinal);

                        var closeEnough = _keyboardSessionBuffer.Length == 0 ||
                                          (ev.Timestamp - _keyboardSessionLastTs).TotalMilliseconds <= 3500;

                        if (_keyboardSessionBuffer.Length > 0 && (!sameContext || !closeEnough))
                            FlushBufferToResult();

                        if (_keyboardSessionBuffer.Length == 0)
                        {
                            _keyboardSessionStartTs = ev.Timestamp;
                            _keyboardSessionProcess = ev.ProcessName;
                            _keyboardSessionTitle = ev.WindowTitle;
                        }

                        _keyboardSessionBuffer.Append(ch);
                        _keyboardSessionLastTs = ev.Timestamp;
                        continue;
                    }
                }

                // Consume Backspace by editing current buffered text where possible.
                if (ev.EventCode == "key:Backspace")
                {
                    var sameContext =
                        _keyboardSessionBuffer.Length > 0 &&
                        string.Equals(_keyboardSessionProcess, ev.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(_keyboardSessionTitle, ev.WindowTitle, StringComparison.Ordinal) &&
                        (ev.Timestamp - _keyboardSessionLastTs).TotalMilliseconds <= 3500;

                    if (sameContext)
                    {
                        if (_keyboardSessionBuffer.Length > 0)
                            _keyboardSessionBuffer.Length -= 1;
                        _keyboardSessionLastTs = ev.Timestamp;
                    }
                    // Do not log raw backspace entries; they are reflected in input:text result.
                    continue;
                }

                // For other keyboard keys, flush text and keep key event.
                FlushBufferToResult();
                result.Add(ev);
            }

            if (forceFlush)
                FlushBufferToResult();
        }

        return result;
    }

    private static string? TryExtractTextFromDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return null;

        const string prefix = "text=\"";
        if (!details.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var end = details.LastIndexOf('"');
        if (end <= prefix.Length)
            return null;

        var encoded = details.Substring(prefix.Length, end - prefix.Length);
        if (encoded.Length == 0)
            return null;

        return encoded.Replace("\\\"", "\"");
    }

    private static string EscapeTextForDetails(string text)
    {
        return text.Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "\\\"");
    }

    private UiEvent? TryBuildWindowDwellEvent(UiEvent nextForegroundEvent)
    {
        var previousProcess = _lastForegroundProcess;
        var previousTitle = _lastForegroundTitle;
        var previousTimestamp = _lastForegroundEmitTs;
        if (string.IsNullOrWhiteSpace(previousTitle) || previousTimestamp == DateTimeOffset.MinValue)
            return null;

        // Only emit dwell when an actual window switch occurs.
        if (string.Equals(previousProcess, nextForegroundEvent.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previousTitle, nextForegroundEvent.WindowTitle, StringComparison.Ordinal))
            return null;

        var duration = nextForegroundEvent.Timestamp - previousTimestamp;
        if (duration.TotalMilliseconds < 0)
            return null;

        var durationText = $"{duration:hh\\:mm\\:ss\\.fff}";
        return new UiEvent(
            timestamp: nextForegroundEvent.Timestamp,
            type: UiEventType.ForegroundChanged,
            processName: previousProcess,
            windowTitle: previousTitle,
            eventCode: "window:dwell",
            details: $"duration={durationText}");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
                Cleanup();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public new void Dispose()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (Interlocked.Exchange(ref _cleanupDone, 1) != 0)
            return;

        LogLifecycleEventOnce(ref _shutdownLogged, "app:stop", "reason=shutdown");
        try { _paused = true; } catch { }

        try { _tray.Dispose(); } catch { }

        try { _foregroundHook.Stop(); } catch { }
        try { _mouseHook.Stop(); } catch { }
        try { _keyboardHook.Stop(); } catch { }
        try { SystemEvents.SessionSwitch -= OnSessionSwitch; } catch { }

        try { _channel.Writer.TryComplete(); } catch { }

        try { _cts.Cancel(); } catch { }

        try { _writerTask.Wait(TimeSpan.FromSeconds(5)); } catch { }
    }

    private void LogLifecycleEventOnce(ref int flag, string eventCode, string details)
    {
        if (Interlocked.Exchange(ref flag, 1) != 0)
            return;

        try
        {
            var ev = new UiEvent(
                timestamp: DateTimeOffset.Now,
                type: UiEventType.ForegroundChanged,
                processName: "WinRecorder",
                windowTitle: "Lifecycle",
                eventCode: eventCode,
                details: details);
            _channel.Writer.TryWrite(ev);
        }
        catch
        {
            // Best-effort.
        }
    }
}

