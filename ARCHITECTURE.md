# Architecture — IISDeploy Studio

## High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        IISDeploy.UI (WPF)                        │
│  ┌─────────────┐  ┌───────────────┐  ┌────────────────────────┐ │
│  │   Views      │  │  ViewModels   │  │  Mvvm (Observable,     │ │
│  │  MainWindow  │  │  MainVM       │  │  RelayCommand)         │ │
│  │  App.xaml    │  │               │  │                        │ │
│  └─────────────┘  └───────────────┘  └────────────────────────┘ │
└──────────────────────────────┬───────────────────────────────────┘
                               │ IExportOrchestrator, IDashboard...
┌──────────────────────────────▼───────────────────────────────────┐
│                   IISDeploy.Application                           │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────────┐  │
│  │  DTOs           │  │  Services         │  │  Implementations│ │
│  │  ExportRequest  │  │  IExportOrch      │  │  DashboardSvc  │  │
│  │  ImportResult   │  │  IDashboardSvc    │  │  ExportOrch    │  │
│  │  SiteTreeNode   │  │  IDependencySvc   │  │  DependencySvc │  │
│  └─────────────────┘  └──────────────────┘  └────────────────┘  │
└──────────────────────────────┬───────────────────────────────────┘
                               │ IIisDiscoveryService...
┌──────────────────────────────▼───────────────────────────────────┐
│                   IISDeploy.Infrastructure                        │
│  ┌───────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │  Iis/         │  │  Logging/         │  │  DI.cs           │  │
│  │  DiscoverySvc │  │  ConsoleLogger    │  │  AddInfrastructure│ │
│  │  ValidationSvc│  │                   │  │                   │  │
│  └───────────────┘  └──────────────────┘  └──────────────────┘  │
└──────────────────────────────┬───────────────────────────────────┘
                               │ Domain interfaces
┌──────────────────────────────▼───────────────────────────────────┐
│                      IISDeploy.Core                               │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────────┐  │
│  │  Models/        │  │  Interfaces/      │  │  Enums/        │  │
│  │  IisSite        │  │  IIisDiscovery    │  │  ExportMode    │  │
│  │  BindingInfo    │  │  IIisExport       │  │  ConflictRes   │  │
│  │  AppPool        │  │  IValidation      │  │  OpStatus      │  │
│  │  PackageManifest│  │  IPackageBuilder  │  │  ValidSeverity │  │
│  └─────────────────┘  └──────────────────┘  └────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

## Layer Responsibilities

### Core (IISDeploy.Core)
- Pure domain models with no external dependencies
- All interfaces that services must implement
- Enums defining domain terminology
- Zero infrastructure or UI awareness

### Application (IISDeploy.Application)
- DTOs for data transfer between layers
- Orchestrator interfaces defining business use cases
- Orchestrator implementations coordinating domain services
- No direct IIS or database dependencies — works through Core interfaces

### Infrastructure (IISDeploy.Infrastructure)
- Microsoft.Web.Administration integration for IIS access
- PowerShell execution (planned)
- File/system operations
- Logging implementation
- External dependency scanning (planned)

### UI (IISDeploy.UI — WPF)
- XAML views with data binding
- ViewModels implementing MVVM pattern
- DI container setup via IHost
- Serilog logging configuration
- Theme resource dictionaries

## Key Design Decisions

1. **WPF over WinUI 3** — WPF has mature tooling, broader Windows Server compatibility, and stable .NET support
2. **Microsoft.Web.Administration** — Native IIS management API, no external IIS dependencies
3. **Generic Host** — `Microsoft.Extensions.Hosting` provides industry-standard DI, configuration, and logging
4. **Async everywhere** — All service methods return Task for non-blocking UI
5. **CancellationToken** — Every long-running operation accepts cancellation
6. **IProgress\<T\>** — Standard progress reporting pattern

## Data Flow (Export)

```
User clicks Export
       │
       ▼
MainViewModel.ExportAsync()
       │
       ▼
ExportOrchestrator.ExportAsync(ExportRequest)
       │
       ├──► IisDiscoveryService.GetSiteAsync()     — Read site config
       ├──► IValidationService.ValidateSiteAsync()  — Check prerequisites
       ├──► DependencyScannerService.ScanAsync()    — Analyze dependencies
       ├──► PackageBuilderService.BuildPackageAsync() — Create .iispackage
       └──► ReportGeneratorService.GenerateHtmlAsync() — Generate report
```

## Data Flow (Import)

```
User selects .iispackage
       │
       ▼
MainViewModel.ImportAsync()
       │
       ▼
ImportOrchestrator.ImportAsync(ImportRequest)
       │
       ├──► PackageReaderService.ReadManifestAsync() — Parse manifest
       ├──► ValidationService.ValidatePackageAsync()  — Check integrity
       ├──► ValidationService.ValidateEnvironment()   — Check target server
       ├──► ConflictResolutionService.DetectConflicts() — Find issues
       ├──► IISImportService.ImportSitesAsync()       — Create IIS objects
       └──► ReportGeneratorService.GenerateHtmlAsync() — Generate report
```
