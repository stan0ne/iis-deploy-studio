# IISDeploy Studio

Enterprise IIS Export / Import & Migration Platform

A production-grade Windows desktop application for fully exporting and importing Microsoft IIS websites and their dependencies between Windows servers through a modern GUI.

**Version:** 1.1.6 (Installer Pipeline + Scan Fix + Icon Redesign)  
**Platform:** .NET 10 (Windows x64)  
**Release Output:** `release/` (canonical publish folder)

---

## Overview

IISDeploy Studio is a complete IIS migration/orchestration platform designed to migrate IIS websites from one Windows Server to another with minimal manual intervention. It handles sites, applications, virtual directories, application pools, bindings, configuration settings, physical files, dependency scans, and package-based transfer workflows.

The current repository baseline has been verified with:
- `dotnet build IISDeployStudio.slnx`
- `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj`
- `pwsh -File .\scripts\publish-release.ps1`

These commands are the reference validation path for release preparation.

### Supported Operating Systems

- Windows Server 2012 / 2016 / 2019 / 2022 / 2025

---

## Architecture

```
IISDeployStudio.slnx
│
├── src/Core/           IISDeploy.Core         — Domain models, interfaces, enums
├── src/Application/    IISDeploy.Application  — DTOs, service interfaces, orchestrators
├── src/Infrastructure/ IISDeploy.Infrastructure — IIS integration, logging, external APIs
└── src/UI/             IISDeploy.UI           — WPF desktop application (MVVM)
```

**Clean Architecture — Dependency Flow:**
```
  UI → Infrastructure → Application → Core
```

- **Core** has no external dependencies — pure domain logic
- **Application** defines use cases and DTOs
- **Infrastructure** implements interfaces from Core using Microsoft.Web.Administration
- **UI** is a WPF MVVM application with DI via Microsoft.Extensions.Hosting

### Key Design Patterns

- **MVVM** — Model-View-ViewModel for the UI layer
- **Dependency Injection** — Microsoft.Extensions.DependencyInjection
- **Async/Await** — All I/O operations are async
- **Repository/Service Pattern** — Clean separation of concerns
- **SOLID Principles** — Single responsibility, interface segregation, dependency inversion

---

## Phase 1 — Completed Features

### Core Layer
- **Models:** IisSite, IisApplication, IisVirtualDirectory, IisApplicationPool, BindingInfo, CertificateInfo, PackageManifest, ValidationResult, OperationProgress, MigrationReport, DatabaseConfig
- **Enums:** ExportMode, ConflictResolutionStrategy, AppPoolIdentityType, PipelineMode, ValidationSeverity, PackageStatus, OperationStatus
- **Interfaces:** IIisDiscoveryService, IIisExportService, IIisImportService, IDependencyScannerService, IPackageBuilderService, IValidationService, IReportGeneratorService, ICertificateExportService, IBindingManagerService, IPowerShellExecutionService, ILoggingService

### Application Layer
- **DTOs:** ExportRequest/Result, ImportRequest/Result, SiteTreeNode, AppPoolSummary, ServerSummary, DependencyScanRequest/Result, CredentialEntry
- **Orchestrator Interfaces:** IExportOrchestrator, IImportOrchestrator, IDashboardService, IDependencyAnalysisService
- **Stub Implementations:** DashboardService, ExportOrchestrator, ImportOrchestrator, DependencyAnalysisService

### Infrastructure Layer
- **IisDiscoveryService** — Full IIS server discovery using Microsoft.Web.Administration:
  - Read all sites, applications, virtual directories
  - Read all application pools with detailed configuration
  - Read all bindings including SSL/SNI detection
  - Map native IIS objects to domain models
- **ValidationService** — Environment validation:
  - Admin privilege check
  - IIS accessibility check
  - Site existence and physical path validation
  - Package file validation (ZIP header, extension)
- **ConsoleLoggingService** — Structured console logging
- **DI Registration** — `AddInfrastructure()` extension method

### UI Layer (WPF)
- **MVVM Framework:** ObservableObject base, RelayCommand with async support
- **MainWindow** — Enterprise dashboard:
  - Title bar with primary actions (Refresh, Export, Import, Scan Dependencies)
  - Search/filter bar with server summary metrics
  - Left panel: IIS site tree with status indicators
  - Right panel: Server information dashboard
  - Status bar with progress indicator
- **Themes:** Professional styling with resource dictionary (Colors.xaml-style)
- **Serilog Integration:** Console + rolling file logging
- **DI Host:** `IHost` with full service registration

---

## Planned Phases

### Phase 2 — Export Engine
- Full export engine with streaming ZIP packaging
- Custom `.iispackage` format
- Certificate export (encrypted PFX)
- Configuration export (web.config, appsettings.json, .env)
- Physical file packaging
- NTFS permission capture (optional)
- Schema generation
- Checksum verification

### Phase 3 — Import Engine & Validation
- Package reading and manifest parsing
- Environment validation (IIS features, runtimes, modules)
- Conflict detection (site names, bindings, ports, app pools)
- Automatic remediation
- Credential management (secure entry dialogs)
- Connection string remapping
- Dry-run simulation mode

### Phase 4 — Dependency Scanner
- Runtime detection (.NET, ASP.NET Core, VC++ Redist)
- IIS module detection (URL Rewrite, ARR, FastCGI)
- Third-party dependency detection (PHP, Node.js, Java)
- ODBC DSN detection
- COM registration checks
- Windows Feature analysis
- GAC assembly scanning
- Installation package suggestions

### Phase 5 — Reporting & Security
- HTML/JSON/PDF report generation
- Serilog advanced configuration
- Package integrity validation
- Secure credential handling
- PowerShell execution service
- Role-based checks

### Phase 6 — Advanced Features
- Conflict resolution strategies (Overwrite, Clone, Rename, Port change)
- Rollback support
- Partial import recovery
- Transaction-like operations
- Resumable exports

### Phase 7 — Polish & Optimization
- Dark mode
- Progress bars with ETA
- Cancellation support
- Toast notifications
- Detailed log viewer
- Report viewer
- Performance optimization

---

## Build, Test, and Release

```bash
# 1. Build the solution
 dotnet build IISDeployStudio.slnx

# 2. Run the automated tests
 dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj

# 3. Publish the canonical release artifact
 pwsh -File .\scripts\publish-release.ps1

# 4. Direct publish (equivalent command)
 dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release

# 5. Run the application locally
 dotnet run --project src/UI

# 6. Build the Windows installer (MSI)
 dotnet build installer/IISDeployStudio.Installer.csproj -c Release
# → installer/bin/Release/IISDeployStudio-Setup.msi
```

### Release standard
- Canonical output folder: `release/`
- Single-file self-contained publish
- Windows x64 runtime target
- Build + test + publish are the minimum release gates
- MSI installer produced via WiX 4 (`installer/IISDeployStudio.Installer.csproj`)

**Requirements:**
- .NET 10 SDK
- Windows x64 with IIS installed
- Administrator privileges
- WiX 4 SDK (auto-restored by `dotnet build` for the installer project)

---

## Installation (End Users)

End users receive `IISDeployStudio-Setup.msi` (~60 MB) and install it on a target Windows Server.

```cmd
:: Interactive
IISDeployStudio-Setup.msi

:: Silent (fleet deployment)
msiexec /i IISDeployStudio-Setup.msi /qn /l*v install.log
```

See **[docs/INSTALL.md](docs/INSTALL.md)** for the full installation guide — system requirements, silent deployment flags, upgrade/uninstall, troubleshooting, and enterprise deployment notes.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| UI Framework | WPF (.NET) |
| DI Container | Microsoft.Extensions.Hosting |
| IIS API | Microsoft.Web.Administration |
| Logging | Serilog (Console + File sinks) |
| Package Format | ZIP-based `.iispackage` |
| Data Serialization | System.Text.Json |
| Build Target | win-x64, Single-file EXE |

---

## Security

- Administrator privilege enforcement
- SSL certificate private keys never stored in plain text
- Encrypted PFX export with password protection
- App pool credentials: username only, password requested at import
- No hardcoded paths or credentials
- Package integrity verification via checksums
- PowerShell execution with parameter validation (coming in Phase 5)

---

## License

Proprietary — Internal enterprise use.

---

*IISDeploy Studio — Reliable IIS migration, minimal manual work.*
