using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRecorder.Logging;
using WinRecorder.Models;
using WinRecorder.Services;
using WinRecorder.Win32;

namespace WinRecorder.Hooks;

public sealed class MouseHook : IDisposable
{
    private readonly Action<UiEvent> _onEvent;
    private readonly MouseTargetResolver _targetResolver;
    private readonly object _gate = new();

    private IntPtr _hookHandle;
    private LowLevelMouseProc? _callback;

    // Hook procedure must stay alive (delegate shouldn't be GC'ed).
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    public MouseHook(Action<UiEvent> onEvent, MouseTargetResolver targetResolver)
    {
        _onEvent = onEvent;
        _targetResolver = targetResolver;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            _callback = HookCallback;

            var procModule = Process.GetCurrentProcess().MainModule;
            var moduleName = procModule?.ModuleName;
            var hMod = string.IsNullOrWhiteSpace(moduleName) ? IntPtr.Zero : GetModuleHandle(moduleName);

            _hookHandle = SetWindowsHookEx(MouseMessages.WH_MOUSE_LL, _callback, hMod, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("SetWindowsHookEx(WH_MOUSE_LL) failed.");
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _callback = null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hhk;
        lock (_gate)
            hhk = _hookHandle;

        try
        {
            if (nCode >= 0)
            {
                var msg = (uint)wParam.ToInt64();

                // Only emit a small set of events to keep logs readable.
                if (msg == MouseMessages.WM_LBUTTONDOWN ||
                    msg == MouseMessages.WM_RBUTTONDOWN ||
                    msg == MouseMessages.WM_MBUTTONDOWN)
                {
                    var hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var ts = DateTimeOffset.Now;
                    var target = _targetResolver.ResolveAt(hs.pt.X, hs.pt.Y);

                    // Only keep meaningful click records: must have visible target text.
                    // Drop coordinate-only / shell-noise clicks.
                    var targetText = NormalizeTargetText(target.Text);
                    if (string.IsNullOrWhiteSpace(targetText))
                        return CallNextHookEx(hhk, nCode, wParam, lParam);

                    string eventCode;
                    string details;

                    if (msg == MouseMessages.WM_LBUTTONDOWN)
                    {
                        eventCode = "mouse:leftClick";
                        details = BuildDetails(targetText, target.ClassName);
                    }
                    else if (msg == MouseMessages.WM_RBUTTONDOWN)
                    {
                        eventCode = "mouse:rightClick";
                        details = BuildDetails(targetText, target.ClassName);
                    }
                    else
                    {
                        eventCode = "mouse:middleClick";
                        details = BuildDetails(targetText, target.ClassName);
                    }

                    _onEvent(new UiEvent(
                        timestamp: ts,
                        type: UiEventType.Mouse,
                        processName: null,
                        windowTitle: null,
                        eventCode: eventCode,
                        details: details));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("MouseHook", ex);
        }

        return CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private static string BuildDetails(string targetText, string? className)
    {
        if (!string.IsNullOrWhiteSpace(className))
            return $"target=\"{targetText}\" class={className}";

        return $"target=\"{targetText}\"";
    }

    private static string? NormalizeTargetText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        var text = rawText.Trim()
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "\\\"");

        // Collapse repeated spaces.
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);

        if (text.Length == 0)
            return null;

        if (text.Length > 120)
            text = text[..120] + "...";

        return text;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}

