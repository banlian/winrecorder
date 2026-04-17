# WinRecorder

A lightweight Windows activity recorder built with .NET 8 + WinForms.

WinRecorder runs in the system tray, captures UI activity events, and writes them to local markdown logs for later review.

## Highlights

- Tray app with pause/resume and exit controls
- Event capture pipeline with bounded buffering
- Configurable filtering by process and window title
- Optional keyboard text capture
- Local markdown log output by day

## Screenshot

Add screenshots in `docs/assets/` and link them here, for example:

```markdown
![Tray Icon](docs/assets/tray.png)
```

## Quick Start

### Requirements

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

- Settings file: `%APPDATA%\WinRecorder\settings.json`
- Default log directory: `%USERPROFILE%\Documents\winrecorder\logs`

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

## Project Layout

- `src/WinRecorder`: application code
- `src/WinRecorder.Tests`: tests
- `docs/`: documentation and screenshots

## Roadmap

- Improve event schema and metadata
- Add richer filtering rules
- Increase automated test coverage
- Add packaged releases for easier installation

## FAQ

### Where are logs stored?

By default under `%USERPROFILE%\Documents\winrecorder\logs` unless overridden in `settings.json`.

### Does this capture sensitive data?

Potentially yes, especially when `CaptureKeysText` is enabled. Review your config and redact data before sharing logs.

### Is this tool intended for unauthorized monitoring?

No. Use only in environments where you have explicit permission.

## Community

- Contribution guide: `CONTRIBUTING.md`
- Security policy: `SECURITY.md`
- Code of conduct: `CODE_OF_CONDUCT.md`
- Changelog: `CHANGELOG.md`

## License

MIT. See `LICENSE`.
