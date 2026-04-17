using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WinRecorder.Config;

namespace WinRecorder.App;

public sealed class TrayIconController : IDisposable
{
    public event Action? PauseToggled;
    public event Action? ExitRequested;

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _activeIcon;
    private readonly Icon _pausedIcon;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly string _logDir;

    public TrayIconController(AppSettings settings)
    {
        _logDir = settings.LogDir;
        _activeIcon = TrayIconFactory.CreateActiveIcon();
        _pausedIcon = TrayIconFactory.CreatePausedIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _activeIcon,
            Visible = true,
            Text = "WinRecorder"
        };

        var menu = new ContextMenuStrip();

        _pauseItem = new ToolStripMenuItem("暂停记录");
        _pauseItem.Click += (_, __) => PauseToggled?.Invoke();
        menu.Items.Add(_pauseItem);

        var openLogsItem = new ToolStripMenuItem("打开日志目录");
        openLogsItem.Click += (_, __) => OpenLogDir();
        menu.Items.Add(openLogsItem);

        var openSettingsItem = new ToolStripMenuItem("打开 settings.json");
        openSettingsItem.Click += (_, __) => OpenSettingsFile();
        menu.Items.Add(openSettingsItem);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, __) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void SetPaused(bool paused)
    {
        _pauseItem.Text = paused ? "继续记录" : "暂停记录";
        _notifyIcon.Icon = paused ? _pausedIcon : _activeIcon;
        _notifyIcon.Text = paused ? "WinRecorder (Paused)" : "WinRecorder";
    }

    private void OpenLogDir()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_logDir))
                return;
            Process.Start(new ProcessStartInfo
            {
                FileName = _logDir,
                UseShellExecute = true
            });
        }
        catch
        {
            // Best-effort.
        }
    }

    private static void OpenSettingsFile()
    {
        try
        {
            var settingsPath = SettingsLoader.GetSettingsPath();
            Process.Start(new ProcessStartInfo
            {
                FileName = settingsPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Best-effort.
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _activeIcon.Dispose();
        _pausedIcon.Dispose();
    }
}

