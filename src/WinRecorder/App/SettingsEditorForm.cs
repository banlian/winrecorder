using System.Drawing;
using System.Windows.Forms;
using WinRecorder.Config;

namespace WinRecorder.App;

internal sealed class SettingsEditorForm : Form
{
    private readonly TextBox _logDirTextBox;
    private readonly CheckBox _captureKeysCheckBox;
    private readonly NumericUpDown _maxEventsNumeric;
    private readonly TextBox _excludedProcessesTextBox;
    private readonly TextBox _excludedTitlesTextBox;

    public SettingsEditorForm()
    {
        Text = "WinRecorder 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 520);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // LogDir label
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // LogDir input
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Capture keys
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Max events
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); // Excluded process list
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); // Excluded title list
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons
        Controls.Add(root);

        var logDirLabel = new Label
        {
            Text = "日志目录 (LogDir)",
            AutoSize = true
        };
        root.Controls.Add(logDirLabel, 0, 0);

        var logDirLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        logDirLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logDirLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _logDirTextBox = new TextBox { Dock = DockStyle.Fill };
        var browseButton = new Button { Text = "浏览...", AutoSize = true };
        browseButton.Click += (_, _) => BrowseLogDir();
        logDirLayout.Controls.Add(_logDirTextBox, 0, 0);
        logDirLayout.Controls.Add(browseButton, 1, 0);
        root.Controls.Add(logDirLayout, 0, 1);

        _captureKeysCheckBox = new CheckBox
        {
            Text = "记录键盘输入文本 (CaptureKeysText)",
            AutoSize = true
        };
        root.Controls.Add(_captureKeysCheckBox, 0, 2);

        var maxEventsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        maxEventsLayout.Controls.Add(new Label
        {
            Text = "每秒最大事件数 (MaxEventsPerSecond)",
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        });
        _maxEventsNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10000,
            Value = 200,
            Width = 120
        };
        maxEventsLayout.Controls.Add(_maxEventsNumeric);
        root.Controls.Add(maxEventsLayout, 0, 3);

        _excludedProcessesTextBox = CreateMultiLineEditor(
            "排除进程名 (ExcludedProcessNames，每行一个，如 chrome)");
        root.Controls.Add(WrapWithGroup("进程排除", _excludedProcessesTextBox), 0, 4);

        _excludedTitlesTextBox = CreateMultiLineEditor(
            "排除窗口标题关键字 (ExcludedWindowTitleSubstrings，每行一个)");
        root.Controls.Add(WrapWithGroup("窗口标题排除", _excludedTitlesTextBox), 0, 5);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += (_, _) => SaveSettings();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);
        root.Controls.Add(buttonPanel, 0, 6);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = SettingsLoader.LoadOrCreateDefault();
        _logDirTextBox.Text = settings.LogDir;
        _captureKeysCheckBox.Checked = settings.CaptureKeysText;
        _maxEventsNumeric.Value = Math.Clamp(settings.MaxEventsPerSecond, (int)_maxEventsNumeric.Minimum, (int)_maxEventsNumeric.Maximum);
        _excludedProcessesTextBox.Text = string.Join(Environment.NewLine, settings.ExcludedProcessNames);
        _excludedTitlesTextBox.Text = string.Join(Environment.NewLine, settings.ExcludedWindowTitleSubstrings);
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                LogDir = _logDirTextBox.Text.Trim(),
                CaptureKeysText = _captureKeysCheckBox.Checked,
                MaxEventsPerSecond = (int)_maxEventsNumeric.Value,
                ExcludedProcessNames = SplitLines(_excludedProcessesTextBox.Text),
                ExcludedWindowTitleSubstrings = SplitLines(_excludedTitlesTextBox.Text)
            };

            if (string.IsNullOrWhiteSpace(settings.LogDir))
            {
                MessageBox.Show(this, "日志目录不能为空。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SettingsLoader.Save(settings);
            MessageBox.Show(this, "设置已保存。重启程序后生效。", "WinRecorder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"保存设置失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseLogDir()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择日志目录",
            SelectedPath = _logDirTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _logDirTextBox.Text = dialog.SelectedPath;
    }

    private static List<string> SplitLines(string value)
    {
        return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GroupBox WrapWithGroup(string title, TextBox textBox)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        group.Controls.Add(textBox);
        return group;
    }

    private static TextBox CreateMultiLineEditor(string placeholder)
    {
        return new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            PlaceholderText = placeholder
        };
    }
}
