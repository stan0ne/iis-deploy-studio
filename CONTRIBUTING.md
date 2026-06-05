# Contributing

IISDeploy Studio is an internal enterprise tool. This document describes the expected development and release workflow for contributors.

## Development Setup

1. Install .NET 10 SDK on a Windows machine.
2. Clone the repository and open `IISDeployStudio.slnx` in Visual Studio 2022+ or VS Code.
3. Restore/build the solution with:
   - `dotnet build IISDeployStudio.slnx`
4. Run the tests with:
   - `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj`
5. Publish the release artifact with:
   - `pwsh -File .\scripts\publish-release.ps1`
6. Run locally with:
   - `dotnet run --project src/UI`

## Release Workflow

The canonical release output path is `release/`.

Use this standard validation flow before opening a release candidate:
1. `dotnet build IISDeployStudio.slnx`
2. `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj`
3. `pwsh -File .\scripts\publish-release.ps1`
4. Verify the generated `release/` folder contains the executable and runtime dependencies.

## Code Standards

- **Async/Await** — All I/O operations must be async
- **Dependency Injection** — Services registered via `IServiceCollection`
- **SOLID** — Single responsibility per class, interface segregation
- **Interfaces** — All services have a corresponding interface in `Core/Interfaces/`
- **DTOs** — Data transfer between layers uses dedicated DTOs in `Application/DTOs/`
- **No God Classes** — Keep services focused and small
- **Cancellation** — Long operations accept `CancellationToken`
- **Progress** — Report progress via `IProgress<T>`

## Project Structure

```
src/
├── Core/          Domain models + interfaces (no dependencies)
├── Application/   DTOs + orchestrators (depends on Core)
├── Infrastructure/ IIS integration + external services (depends on Application)
└── UI/            WPF views + ViewModels (depends on Infrastructure)
```

## Pull Request Process

1. Create a feature branch from `main`.
2. Implement changes using the established architecture and naming conventions.
3. Verify the release gates:
   - `dotnet build IISDeployStudio.slnx`
   - `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj`
   - `pwsh -File .\scripts\publish-release.ps1`
4. Add or update tests whenever behavior changes.
5. Document the change in `CHANGELOG.md` when relevant.
6. Open a PR with a summary of the change, validation steps, and any known limitations.

## Notes for Contributors

- Prefer small, testable changes.
- Keep the release output path (`release/`) consistent across docs and scripts.
- Do not rely on old `publish*` folders for release validation.
- If a change affects export/import, include a smoke-test note in the PR description.
