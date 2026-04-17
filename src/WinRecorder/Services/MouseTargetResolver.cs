using System.Text;
using WinRecorder.Win32;

namespace WinRecorder.Services;

public sealed class MouseTargetResolver
{
    public MouseTargetInfo ResolveAt(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        var hwnd = User32.WindowFromPoint(point);
        if (hwnd == IntPtr.Zero)
            return new MouseTargetInfo(null, null);

        var className = TryGetClassName(hwnd);
        var text = TryGetWindowText(hwnd);

        if (string.IsNullOrWhiteSpace(text))
            text = null;

        return new MouseTargetInfo(className, text);
    }

    private static string? TryGetClassName(IntPtr hwnd)
    {
        try
        {
            var sb = new StringBuilder(256);
            var n = User32.GetClassName(hwnd, sb, sb.Capacity);
            return n > 0 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetWindowText(IntPtr hwnd)
    {
        try
        {
            var sb = new StringBuilder(512);
            var n = User32.GetWindowText(hwnd, sb, sb.Capacity);
            return n > 0 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MouseTargetInfo(string? ClassName, string? Text);

