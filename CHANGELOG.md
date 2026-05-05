# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog.

## [Unreleased]

### Added

- Embedded **log statistics** HTTP server: parse daily markdown logs, serve `/api/stats` (JSON) and a bundled `log-stats.html` UI; tray/menu wiring; ship HTML with the app (`LogStatsServer`, `Server/log-stats.html`).
- Optional **LaunchAfterBuild** MSBuild switch and `run-after-build.ps1` to start WinRecorder after a successful build (`-p:LaunchAfterBuild=true`).
- **WiX 5** MSI packaging: `installer/WinRecorder.Installer.wixproj`, `installer/Package.wxs`, and `installer/build-msi.ps1` (`dotnet publish` for `win-x64`, then harvest published files into an MSI). `WinRecorder.Installer` is listed in the solution but excluded from the default solution build so routine builds do not require a publish output folder.
- GitHub Actions: on `v*` tag pushes (Release configuration), build and upload `WinRecorder-<tag>.msi` alongside the existing zip; attach both to the GitHub Release.
- Deduplication of repeated identical **mouse** log lines: same fingerprint after enrichment (type, process, window title, `eventCode`, `details`) until the fingerprint changes. Non-mouse events do not reset the last mouse fingerprint. `EventDeduplicator` in `AppContext` before enqueue.
- Self-tests in `EventDeduplicatorTests`, invoked from the `WinRecorder.Tests` console host.
- Application icon (`src/WinRecorder/Assets/app.ico`) referenced from `WinRecorder.csproj`.
- Initial open-source project documentation and governance files:
  - `LICENSE` (MIT)
  - `README.md`
  - `CONTRIBUTING.md`
  - `CODE_OF_CONDUCT.md`
  - `SECURITY.md`
  - `.gitignore`

### Changed

- `README`: MSI build instructions; `CONTRIBUTING` / `README` document mouse deduplication; test instructions use `dotnet run` on the self-test project (not VSTest).
- `.github/workflows/dotnet-desktop.yml`: release packaging publishes `src/WinRecorder/WinRecorder.csproj` with `-r win-x64` (framework-dependent), zips that output, then builds the WiX installer from the same folder.
- `.gitignore`: ignore top-level `scripts/` and `marketing/`.

### Fixed

- Log statistics **HTML** date controls (previous / next / today): format and parse dates in the **local** calendar instead of `toISOString()` (UTC), so day navigation matches the user’s timezone.
- Log statistics **API** JSON: serialize with **camelCase** names so the dashboard script can render summary, hourly, and top-app tables.
