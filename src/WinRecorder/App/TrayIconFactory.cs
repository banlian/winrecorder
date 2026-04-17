using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WinRecorder.App;

internal static class TrayIconFactory
{
    public static Icon CreateActiveIcon()
        => CreateRecorderIcon(Color.FromArgb(34, 197, 94), Color.White);

    public static Icon CreatePausedIcon()
        => CreateRecorderIcon(Color.FromArgb(245, 158, 11), Color.FromArgb(36, 36, 36));

    private static Icon CreateRecorderIcon(Color background, Color glyph)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Outer rounded square background.
        using (var bgBrush = new SolidBrush(background))
        {
            var rect = new RectangleF(3.5f, 3.5f, 25f, 25f);
            using var path = RoundedRect(rect, 7f);
            g.FillPath(bgBrush, path);
        }

        // Inner "record" glyph circle.
        using (var glyphBrush = new SolidBrush(glyph))
        {
            g.FillEllipse(glyphBrush, 10.2f, 10.2f, 11.6f, 11.6f);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            // Clone to detach from unmanaged handle lifetime.
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

