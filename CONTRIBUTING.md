# Contributing

IISDeploy Studio is an internal enterprise tool.

## Development Setup

1. Install .NET 10 SDK
2. Clone the repository
3. Open `IISDeployStudio.slnx` in Visual Studio 2022+ or VS Code
4. Build: `dotnet build`
5. Run: `dotnet run --project src/UI`

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

1. Create feature branch from `main`
2. Write implementation following the code standards
3. Ensure `dotnet build` passes with 0 warnings
4. Add/update unit tests
5. Create PR with description
