using System.Diagnostics;
using System.Text;
using WinRecorder.Win32;

namespace WinRecorder.Services;

public sealed class WindowInfoProvider
{
    private const int MaxTitleLength = 1024;

    public WindowInfo GetWindowInfo(IntPtr hwnd)
    {
        string? windowTitle = null;
        string? processName = null;

        try
        {
            var sb = new StringBuilder(MaxTitleLength);
            var copied = User32.GetWindowText(hwnd, sb, sb.Capacity);
            if (copied > 0)
            {
                windowTitle = sb.ToString();
            }
        }
        catch
        {
            // Best-effort: window title can fail for some protected windows.
        }

        try
        {
            if (User32.GetWindowThreadProcessId(hwnd, out var pid) == 0)
                return new WindowInfo(processName, windowTitle);

            processName = pid == 0 ? null : Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            // Access denied / process exited -> ignore.
        }

        return new WindowInfo(processName, windowTitle);
    }
}

public sealed record WindowInfo(string? ProcessName, string? WindowTitle);

