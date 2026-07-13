# IISDeploy Studio

> Enterprise IIS Export / Import & Migration Platform

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Windows](https://img.shields.io/badge/Windows-x64-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Build](https://img.shields.io/badge/Build-passing-brightgreen)](https://github.com/stan0ne/iis-deploy-studio/actions)

A production-grade Windows desktop application for exporting and importing Microsoft IIS websites and their dependencies between Windows servers — through a modern WPF GUI, with full package-based transfer, dependency scanning, and rollback support.

---

## Quick Start

```bash
# Build
dotnet build IISDeployStudio.slnx

# Test
dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj

# Run
dotnet run --project src/UI

# Publish portable release
dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -o release
```

**Requirements:** .NET 10 SDK, Windows x64 with IIS, Administrator privileges.

---

## Features

| Area | Capabilities |
|------|-------------|
| **Export** | Full IIS site export to `.iispackage` (ZIP) — sites, apps, vdirs, app pools, bindings, configs, physical files. Multi-site and full-server modes. |
| **Import** | Package reader, IIS object creation, conflict detection (names/ports/bindings), automatic remediation (port changes, rename, clone), transaction-scoped rollback, idempotent re-import. |
| **Dependency Scan** | Runtime detection (.NET, ASP.NET Core, VC++), IIS modules (URL Rewrite, ARR, FastCGI), third-party (PHP, Node.js, Java, ODBC), Windows Features. |
| **Security** | DPAPI credential encryption, SecureString handling, encrypted PFX export, PowerShell execution with parameter validation, SHA-256 package integrity. |
| **Reporting** | HTML / JSON / PDF reports, structured Serilog logging (console + rolling file). |
| **UI** | Dark mode, toast notifications, log viewer, report viewer, progress reporting, cancellation support. |
| **Resilience** | Retry with backoff, transactional rollback, plugin architecture (`IIisDeployPlugin`), conflict resolution strategies. |

---

## Architecture

Clean Architecture with strict dependency flow:

```
┌──────────────────────────────────────────────────────────────┐
│  IISDeploy.UI           WPF MVVM (ObservableObject, RelayCommand) │
├──────────────────────────────────────────────────────────────┤
│  IISDeploy.Infrastructure  IIS, PowerShell, Packaging, Plugins  │
├──────────────────────────────────────────────────────────────┤
│  IISDeploy.Application     DTOs, Orchestrators, Use Cases       │
├──────────────────────────────────────────────────────────────┤
│  IISDeploy.Core            Domain Models, Interfaces, Enums     │
└──────────────────────────────────────────────────────────────┘
```

- **Core** — pure domain, zero external dependencies
- **Application** — DTOs, service interfaces, orchestrators (`DashboardService`, `ExportOrchestrator`, `ImportOrchestrator`)
- **Infrastructure** — IIS integration (`Microsoft.Web.Administration`), PowerShell, packaging, plugins, reporting, security
- **UI** — WPF with DI via `Microsoft.Extensions.Hosting`, Serilog

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| UI | WPF (MVVM) |
| DI | Microsoft.Extensions.Hosting |
| IIS API | Microsoft.Web.Administration |
| Logging | Serilog (Console + File) |
| Package | `.iispackage` (ZIP-based) |
| Serialization | System.Text.Json |
| Installer | WiX 7 MSI |
| Testing | xUnit (68+ tests) |

---

## Installation

**Option 1 — MSI Installer (recommended):**
```cmd
IISDeployStudio-Setup.msi
:: Silent: msiexec /i IISDeployStudio-Setup.msi /qn /l*v install.log
```

**Option 2 — Portable ZIP:**
1. Download `IISDeployStudio-v1.1.6.zip` from [Releases](https://github.com/stan0ne/iis-deploy-studio/releases)
2. Extract and run `IISDeployStudio.exe`

See [docs/INSTALL.md](docs/INSTALL.md) for full details.

---

## Project Structure

```
IISDeployStudio.slnx
├── src/Core/           IISDeploy.Core
├── src/Application/    IISDeploy.Application
├── src/Infrastructure/ IISDeploy.Infrastructure
├── src/UI/             IISDeploy.UI
├── tests/              IISDeploy.Tests
├── installer/          WiX MSI project
├── scripts/            publish-release.ps1, validate-release.ps1
├── docs/               INSTALL.md, RELEASE_SMOKE_TEST.md
└── release/            Canonical publish output
```

---

## Security

- Administrator privilege enforcement
- DPAPI encryption for sensitive data in transit
- Encrypted PFX export with password
- App pool passwords never stored — requested at import
- SHA-256 package integrity verification
- PowerShell command allowlist with anti-injection

---

## License

GNU General Public License v3.0 — see [LICENSE](LICENSE).

---

*IISDeploy Studio — Reliable IIS migration, minimal manual work.*
