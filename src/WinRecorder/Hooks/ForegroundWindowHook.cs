using WinRecorder.Logging;
using WinRecorder.Models;
using WinRecorder.Services;
using WinRecorder.Win32;

namespace WinRecorder.Hooks;

public sealed class ForegroundWindowHook : IDisposable
{
    private readonly WindowInfoProvider _windowInfoProvider;
    private readonly Action<UiEvent> _onEvent;

    private readonly object _gate = new();
    private IntPtr _hookHandle;
    private User32.WinEventDelegate? _callback;
    private IntPtr _lastHwnd;

    public ForegroundWindowHook(WindowInfoProvider windowInfoProvider, Action<UiEvent> onEvent)
    {
        _windowInfoProvider = windowInfoProvider;
        _onEvent = onEvent;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            _callback = WinEventProc;
            _hookHandle = User32.SetWinEventHook(
                User32.EVENT_SYSTEM_FOREGROUND,
                User32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _callback,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed.");
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            if (!User32.UnhookWinEvent(_hookHandle))
            {
                // Best-effort; do not crash.
            }

            _hookHandle = IntPtr.Zero;
            _callback = null;
        }
    }

    private void WinEventProc(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
                return;

            // Reduce noise: only emit when hwnd changes.
            lock (_gate)
            {
                if (hwnd == _lastHwnd)
                    return;
                _lastHwnd = hwnd;
            }

            var info = _windowInfoProvider.GetWindowInfo(hwnd);
            _onEvent(new UiEvent(
                timestamp: DateTimeOffset.Now,
                type: UiEventType.ForegroundChanged,
                processName: info.ProcessName,
                windowTitle: info.WindowTitle,
                eventCode: "foreground",
                details: null));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("ForegroundWindowHook", ex);
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

