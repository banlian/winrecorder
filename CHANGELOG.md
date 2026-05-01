# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog.

## [Unreleased]

### Added

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

- `README` / `CONTRIBUTING`: document mouse deduplication; fix test instructions to `dotnet run` the self-test project (it is not a VSTest project).
- `.gitignore`: ignore top-level `scripts/` and `marketing/`.
