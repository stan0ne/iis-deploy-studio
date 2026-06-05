# Changelog

All notable changes to IISDeploy Studio will be documented in this file.

## [Unreleased] — 2026-06-05 — Release Standardization

### Added
- Standard release publish workflow via `scripts/publish-release.ps1`.
- Canonical release output folder: `release/`.

### Changed
- Removed fixed file/dir copy limits from export/import packaging to support large IIS sites without truncation.

### Verified
- `dotnet build IISDeployStudio.slnx` ✅
- `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj` ✅ (20/20 passed)
- `pwsh -File .\scripts\publish-release.ps1` ✅ (release artifact generated)

## [1.1.5] — 2026-05-26 — UI Overhaul (publish5)

### Changed
- **Complete UI redesign:** Revamped the entire WPF interface using Gemini 3.1 Pro (via Antigravity), reaching publish5 milestone after iterative builds (publish → publish2 → publish3 → publish4 → publish5). The new design features a Windows-native aesthetic with modern dark/light themes, refined color palettes, improved spacing and typography, a redesigned sidebar with site tree, server info cards in dashboard layout, summary metrics bar (Sites/Running/Stopped/App Pools), animated status indicators, and a polished status bar. This replaces the original v1.0.0 UI shipped in the initial `publish/` folder.
- **MainWindow starts maximized:** Added `WindowState="Maximized"` so the application opens in full-screen mode on first launch.
- **Toast notifications refined:** Disabled toast popups for export success/failure and startup "ready" message. Only theme toggle ("Dark mode enabled" / "Light mode enabled") toasts remain active.
- **Scan results layout:** Missing dependencies section now uses `Auto` height instead of equal `*` split, preventing empty wasted space when few dependencies are missing.

## [1.0.0] — Phase 1 — Core Architecture & GUI Shell

### Added
- .NET 10 solution with Clean Architecture (Core, Application, Infrastructure, UI)
- WPF desktop application with MVVM pattern
- Microsoft.Web.Administration integration for IIS discovery
- Domain models: IisSite, IisApplication, IisVirtualDirectory, IisApplicationPool, BindingInfo, CertificateInfo, PackageManifest, ValidationResult, OperationProgress, MigrationReport
- Enum definitions: ExportMode, ConflictResolutionStrategy, AppPoolIdentityType, PipelineMode, ValidationSeverity, PackageStatus, OperationStatus
- Service interfaces: IIisDiscoveryService, IIisExportService, IIisImportService, IDependencyScannerService, IPackageBuilderService, IValidationService, IReportGeneratorService, ICertificateExportService, IBindingManagerService, IPowerShellExecutionService, ILoggingService
- IIS Discovery Service — full site/application/pool/binding enumeration
- Validation Service — admin privilege check, IIS accessibility, package validation
- Application layer DTOs and orchestrator interfaces
- Dashboard service for server summary and site tree view
- WPF MainWindow with title bar, search, site tree, server info, status bar
- MVVM framework (ObservableObject, RelayCommand with async)
- Professional theme with color palette and styles
- DI container with Microsoft.Extensions.Hosting
- Serilog logging (Console + rolling file)
- Stub implementations for Export, Import, and Dependency orchestrators
- **Import Preview Window:** Full preview dialog before import showing source machine info, site/pool lists, physical path configuration with per-site editable target path and "Create if missing" checkbox, and interactive conflict resolution with per-conflict strategy dropdowns.
- **Custom name input:** Rename/Clone strategies now show a "New name" textbox for user-specified names.
- **Custom port input:** Change port strategy now shows a "New port" textbox for user-specified port numbers.
- **Export preview:** Confirmation dialog showing selected sites and output path before export.
- **Smart export filename:** Single site: `IISExport_<sitename>_<timestamp>.iispackage`, multiple: `IISExport_<N>sites_<timestamp>.iispackage`.
- **Serilog reference** added to Infrastructure project for unified file logging from `ConsoleLoggingService`.
- **BoolToVisibilityConverter** added for XAML data binding.
- **Detailed import result messages:** Success/failure per-item breakdown shown in MessageBox after import.
- **Scanner error resilience:** Each scanner (Runtime, IIS Feature, Third-Party) runs in separate try-catch blocks; failures logged as `ScannerError` entries instead of crashing the entire scan.

### Fixed
- **Logging crash:** `ConsoleLoggingService` was using `string.Format()` on Serilog structured logging templates (e.g., `{Count}`), causing `FormatException`. Changed to direct message output without format parsing.
- **Startup site loading:** `MainWindow` `Loaded` event now properly fires with `IsLoaded` fallback check for race conditions.
- **Import path:** Physical path now defaults to `C:\inetpub\<sitename>` instead of broken `C:\Windows\inetpub\<sitename>`.
- **Import path override:** Target path entered by user in preview dialog is now correctly applied (fixed `originalName` key mismatch after rename).
- **Port override:** Custom port from preview dialog is now applied **regardless of site conflict strategy** — extracted to standalone `ApplyCustomPorts()` method. Previously only worked inside `ChangeBinding` strategy case.
- **BindingInformation sync:** `UpdateBindingInformation()` helper added to keep `BindingInformation` string (`IP:Port:Host`) in sync with `Port` property changes.
- **Reports sorting:** Report list now sorted by `LastWriteTime` descending (newest first) instead of filename.
- **Log viewer:** Now uses `FileShare.ReadWrite` to read log files while Serilog is actively writing.
- **Conflict defaults changed:** Site conflicts now default to "Rename", Pool conflicts to "Rename", Binding conflicts to "Change port" instead of all defaulting to "Overwrite".
- **Binding strategy options filtered:** Binding conflicts now only show "Change port" and "Skip" options (removed irrelevant Overwrite/Rename/Clone).

### Changed
- **Dark mode redesign:** GitHub-dark inspired palette (`#0D1117` background, `#161B22` surface, `#30363D` borders, `#4CC2FF` accent). New styles: `IconButton`, `SearchBoxStyle`, `CardStyle`, `DataGridStyle`. Title bar now uses surface color instead of primary blue.
- **SafeCastToInt:** `IisDiscoveryService.MapAppPool` now uses safe int casting to prevent overflow on large IIS timeout/process values.
- **SafeStringToByteArray:** Certificate hash parsing now strips non-hex characters (dashes, colons, spaces) and returns null on failure instead of throwing.
- **LoadJsonFiles:** Now catches `JsonException`, `FormatException`, and generic exceptions separately with descriptive log messages.
- **DependencyAnalysisService:** Removed `throw;` in scan catch block — scan failures now return results instead of propagating exceptions.
- **ConflictResolutionService:** Default strategy changed from `Skip` to `Overwrite` for site/pool conflicts.
- **ResolveConflicts:** Removed automatic `FindAlternativePort` logic — port changes now driven entirely by user input from preview dialog.
- **Import preview window:** Height increased from 620 to 700 to accommodate conflict entries without scrolling.
