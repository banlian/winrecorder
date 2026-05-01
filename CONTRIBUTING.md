# Contributing to WinRecorder

Thanks for your interest in improving WinRecorder.

## How to Contribute

1. Fork the repository
2. Create a feature branch from `main`
3. Implement your change with focused commits
4. Add or update tests when behavior changes
5. Open a pull request

## Development Setup

```powershell
dotnet restore .\src\WinRecorder\WinRecorder.csproj
dotnet build .\src\WinRecorder\WinRecorder.csproj
dotnet run --project .\src\WinRecorder.Tests\WinRecorder.Tests.csproj
```

The `WinRecorder.Tests` project is a self-test **console** host (not a VSTest project). Use `dotnet run`, not `dotnet test`.
`WinRecorder.Tests` 为自测控制台工程，请使用 `dotnet run` 而非 `dotnet test`。

## Pull Request Guidelines

- Keep PRs small and focused
- Include clear description of the problem and solution
- Link related issues when applicable
- Ensure build and tests pass locally

## Coding Style

- Follow existing C# style in the project
- Prefer clear naming and small, testable methods
- Avoid unrelated refactors in the same PR

## Reporting Bugs

When filing an issue, please include:

- Reproduction steps
- Expected behavior
- Actual behavior
- Windows version and .NET SDK version
- Relevant logs (remove sensitive data)
