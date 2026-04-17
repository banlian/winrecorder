# WinRecorder

A lightweight Windows activity recorder built with .NET 8 + WinForms.
一个基于 .NET 8 + WinForms 的轻量级 Windows 活动记录器。

WinRecorder runs in the system tray, captures UI activity events, and writes them to local markdown logs for later review.
WinRecorder 运行在系统托盘中，捕获 UI 活动事件，并将其写入本地 Markdown 日志，便于后续回溯和分析。

## Highlights

- Tray app with pause/resume and exit controls
- Event capture pipeline with bounded buffering
- Configurable filtering by process and window title
- Optional keyboard text capture
- Local markdown log output by day
- 托盘应用，支持暂停/恢复与退出控制
- 事件采集管线，带有有界缓冲
- 支持按进程名和窗口标题进行过滤
- 可选键盘文本采集
- 按天输出本地 Markdown 日志

## Screenshot

Add screenshots in `docs/assets/` and link them here, for example:
将截图放入 `docs/assets/` 并在此处引用，例如：

```markdown
![Tray Icon](docs/assets/tray.png)
```

## Quick Start

### Requirements

- Windows 10/11
- .NET SDK 8.0+
- Windows 10/11
- .NET SDK 8.0+

### Build

```powershell
dotnet build .\src\WinRecorder\WinRecorder.csproj
```

### Run

```powershell
dotnet run --project .\src\WinRecorder\WinRecorder.csproj
```

### Test

```powershell
dotnet test .\src\WinRecorder.Tests\WinRecorder.Tests.csproj
```

## Configuration

On first launch, WinRecorder creates:
首次启动时，WinRecorder 会创建：

- Settings file: `%APPDATA%\WinRecorder\settings.json`
- Default log directory: `%USERPROFILE%\Documents\winrecorder\logs`
- 配置文件：`%APPDATA%\WinRecorder\settings.json`
- 默认日志目录：`%USERPROFILE%\Documents\winrecorder\logs`

### Example `settings.json`

```json
{
  "LogDir": "D:\\Work\\winrecorder\\logs",
  "CaptureKeysText": true,
  "ExcludedProcessNames": [],
  "ExcludedWindowTitleSubstrings": [],
  "MaxEventsPerSecond": 200
}
```

### Config Fields

- `LogDir`: local path for markdown log files
- `CaptureKeysText`: enable/disable keyboard text capture
- `ExcludedProcessNames`: exact process-name blacklist (case-insensitive)
- `ExcludedWindowTitleSubstrings`: window-title substring blacklist (case-insensitive)
- `MaxEventsPerSecond`: simple rate limiter for incoming events
- `LogDir`：Markdown 日志文件的本地目录
- `CaptureKeysText`：是否启用键盘文本采集
- `ExcludedProcessNames`：按进程名精确匹配的黑名单（不区分大小写）
- `ExcludedWindowTitleSubstrings`：按窗口标题子串匹配的黑名单（不区分大小写）
- `MaxEventsPerSecond`：对输入事件进行简单限流

## Project Layout

- `src/WinRecorder`: application code
- `src/WinRecorder.Tests`: tests
- `docs/`: documentation and screenshots
- `src/WinRecorder`：应用代码
- `src/WinRecorder.Tests`：测试代码
- `docs/`：文档与截图资源

## Roadmap

- Improve event schema and metadata
- Add richer filtering rules
- Increase automated test coverage
- Add packaged releases for easier installation
- 优化事件模型与元数据
- 增强过滤规则能力
- 提升自动化测试覆盖率
- 提供打包发布版本，简化安装流程

## FAQ

### Where are logs stored?
### 日志存储在哪里？

By default under `%USERPROFILE%\Documents\winrecorder\logs` unless overridden in `settings.json`.
默认位于 `%USERPROFILE%\Documents\winrecorder\logs`，可通过 `settings.json` 覆盖。

### Does this capture sensitive data?
### 会采集敏感数据吗？

Potentially yes, especially when `CaptureKeysText` is enabled. Review your config and redact data before sharing logs.
有可能，尤其是在启用 `CaptureKeysText` 时。请在分享日志前检查配置并做好脱敏处理。

### Is this tool intended for unauthorized monitoring?
### 该工具是否可用于未授权监控？

No. Use only in environments where you have explicit permission.
不可以。仅可在已获得明确授权的环境中使用。

## Community

- Contribution guide: `CONTRIBUTING.md`
- Security policy: `SECURITY.md`
- Code of conduct: `CODE_OF_CONDUCT.md`
- Changelog: `CHANGELOG.md`
- 贡献指南：`CONTRIBUTING.md`
- 安全策略：`SECURITY.md`
- 行为准则：`CODE_OF_CONDUCT.md`
- 变更日志：`CHANGELOG.md`

## License

MIT. See `LICENSE`.
MIT 许可证，详见 `LICENSE`。
