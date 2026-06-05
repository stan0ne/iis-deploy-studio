# Changelog

All notable changes to IISDeploy Studio will be documented in this file.

## [Unreleased] — 2026-06-06 — Release Standardization + Import Hardening (Paket C completed)

### Added
- **Notification sound on export/import completion** — new `INotificationSoundService` interface in `Core/Interfaces` with a WPF implementation (`WpfNotificationSoundService` in `UI/Services`) that uses `System.Media.SoundPlayer` to play `Windows Background.wav` (success) / `Windows Exclamation.wav` (failure) from `%WINDIR%\Media`. `ExportOrchestrator` plays the success sound after the success log line and the failure sound inside the `catch (Exception)` block; `ImportOrchestrator` does the same. `OperationCanceledException` is silent (user-initiated). Missing/corrupt sound files are logged as a warning and never throw. DI-registered as a singleton in `App.xaml.cs`. `NotificationSoundServiceTests` (5 tests) covers default constants + graceful missing-file handling for both methods.
- **Transaction-scoped rollback for IIS import** — `IisImportService.ImportSite` now runs inside an `ITransactionManager` scope. Every site, application, app pool, and binding created during the import registers a cleanup action with the manager; on any failure `Commit()` is skipped and `RollbackAsync` reverts every change in reverse order. `IisImportRollbackTests` (4 tests) covers site, app, pool, and binding rollback paths.
- **Idempotent re-import via manifest checksum** — `OperationStatus.Skipped` enum value; `IReportGeneratorService.FindPreviousImportAsync(checksum, machine, directory?)` queries the report archive for a matching checksum on the same machine. `IisImportService` returns `ImportResult.Status = Skipped` (without touching IIS) when the same package is re-imported. `IisImportIdempotencyTests` (7 tests) covers checksum match, machine mismatch, and missing-report edge cases.
- **Post-import state validation** — `BuildPostValidationEntries` enumerates sites/pools that were just imported and re-queries `IIisDiscoveryService` to confirm each exists with the expected configuration; missing/mismatched entries are emitted as `ValidationResult` items. `IisImportPostValidationTests` (5 tests) verifies site presence, app pool presence, and missing-entry reporting.
- **Diagnostic `Information` logs for import milestones** — `IisImportService` emits `ILoggingService.Information` at scope start, before/after each major phase (site apply, binding apply, post-validation), and at scope end. `IisImportLoggingTests` (5 tests) asserts log presence/absence at each milestone.
- **`AppPoolNameResolver` static utility** — `src/Infrastructure/Iis/AppPoolNameResolver.cs`: `ApplyResolvedPoolNames(site, map)` updates `site.AppPoolName` and every `Application.ApplicationPoolName` consistently per an `OrdinalIgnoreCase` name map. `IisImportService` calls it whenever a rename/clone strategy resolves a new pool name. `AppPoolNameResolverTests` (1 test).
- **Pool conflict detection from site app-pool references** — `ConflictResolutionService.AnalyzeImportConflictsAsync` now inspects `site.AppPoolName` and every `Application.ApplicationPoolName` in addition to `poolsToImport`, closing the gap where sites whose `poolsToImport` was empty but referenced a live pool were treated as conflict-free. `ConflictResolutionPoolReferenceTests` (1 test).
- **28 new unit tests** across 8 classes: 4 rollback + 7 idempotency + 5 post-validation + 5 logging + 6 change-binding + 1 skip + 1 pool-reference + 1 app-pool-resolver. Baseline 35 → **63 total** (verified PASS).
- Standard release publish workflow via `scripts/publish-release.ps1`.
- Canonical release output folder: `release/`.
- `TECHNICAL_AUDIT_REPORT.md` — static audit covering architecture, data flow, design patterns, bottlenecks, and tech debt.
- Plugin lifecycle wiring: `App.OnStartup` now invokes `PluginHost.LoadPluginsAsync()` and `App.OnExit` invokes `PluginHost.ShutdownAsync()` so the registered plugin host actually runs.
- `tests/IISDeploy.Tests/IISDeploy.Tests.csproj` now references `IISDeploy.Core` and `IISDeploy.Application` in addition to `IISDeploy.Infrastructure`, enabling direct unit tests against domain models and orchestrators.
- **PowerShell integration via `IisFeatureScanner`** — `Get-WindowsFeature` cmdlet now runs through `IPowerShellExecutionService` during dependency scans, emitting a `PowerShellFeatureScan` `DependencyInfo` entry. Logged via `ILoggingService.Information` on success and `Warning` on graceful failure.
- **PowerShell execution policy check at scan start** — `Get-ExecutionPolicy` added to PS service allowlist; new `IisFeatureScanner.CheckPowerShellExecutionPolicyAsync` emits a `PowerShellPolicy` `DependencyInfo` (`Version` = policy name, `Required=false` when policy is `Restricted` or `AllSigned`).
- **`Install-WindowsFeature` remediation** — new `IisFeatureScanner.RemediateIisFeaturesAsync(List<string> featureNames, CancellationToken)` method invokes `Install-WindowsFeature -Name X` per feature through the allowlisted PS pipeline; returns `Dictionary<string,bool>` mapping each feature to success/failure with graceful per-feature degradation.
- **`release/build.manifest.json` auto-generation** — `scripts/publish-release.ps1` now writes a release metadata manifest (version, gitHash, gitBranch, buildTime, dotnetVersion, runtime, selfContained, singleFile, exePath, exeSize, publishedBy) after every publish. `scripts/validate-release.ps1` includes a validation gate that verifies the file is present, valid JSON, and has all required fields.
- **`scripts/validate-release.ps1`** — 8-gate release validation script (SDK presence, required files, repo hygiene, build, test, publish artifact, build manifest, PROMPT.md baseline). Exits 0 only when every gate passes.
- **`docs/RELEASE_SMOKE_TEST.md`** — 7-section manual smoke test checklist (preflight, app startup, dependency scan, export, import, plugins, logging, shutdown) covering items the automated gates cannot verify.
- **10 new unit tests** — 8 in `IisFeatureScannerPowerShellTests` (3 base + 3 execution policy + 2 remediation + 1 empty-input guard) and 2 in `PackageBuilderTests` (stable checksum for identical content, different checksum for different content).

### Changed
- Removed fixed file/dir copy limits from export/import packaging to support large IIS sites without truncation.
- `PROMPT.md` marked `[DEPRECATED — HISTORICAL REFERENCE ONLY]` with banner pointing to README/CHANGELOG/ARCHITECTURE for current state. `TECHNOLOGY STACK` section corrected to `.NET 10 + WPF` (was `WinUI 3`/`.NET 8`).
- `ROADMAP.md` Phase 7 test count corrected from `20/20 passing` to `35/35 passing` (current verified count).
- `IisFeatureScanner` constructor signature: now requires `IPowerShellExecutionService` and `ILoggingService` in addition to `IIisDiscoveryService`. DI wiring updated accordingly; existing concrete `AddSingleton<IisFeatureScanner>()` registration continues to resolve transparently.
- `IPowerShellExecutionService` interface now includes `GetExecutionPolicyAsync` (new); `PowerShellExecutionService` allowlist extended with `Get-ExecutionPolicy`.
- `scripts/publish-release.ps1` now also writes `release/build.manifest.json` after the publish step.
- `scripts/validate-release.ps1` now generates `release/build.manifest.json` inline after its own publish step, and adds a gate to verify the manifest is present, valid JSON, and contains required fields.
- `RELEASE_READINESS_PLAN.md` rewritten as a live, evidence-based status: §0 paket durum tablosu (A/B/D/E ✅, C ⚠️), §9 güncel kabul kriterleri tablosu, §8 backlog, §10 release onay süreci. After this commit: §0 shows 5/6 paketler ✅, §8 backlog reduced to 2 items (rollback, installer).
- `IisImportService` constructor: 6 → 7 dependencies (added `ITransactionManager` for the rollback scope); DI wiring in `App.xaml.cs` updated to match.
- `IisImportService.ImportSite` signature: `Task<ImportResult>` → `Task<bool>`; the skip outcome is now communicated through the migration report (consistent with the pool path) instead of the result.
- `OperationStatus` enum: added `Skipped = 3` value.
- `IReportGeneratorService` interface: added `FindPreviousImportAsync(string checksum, string machine, string? directory = null)` to support idempotency.
- `Theme.xaml` ComboBox style: removed the custom 47-line `ControlTemplate` (custom `Border` + `ContentPresenter` + drop-down arrow `TextBlock` + full `PART_Popup` with shadowed `SurfaceElevatedBrush` border + `IsMouseOver`/`IsDropDownOpen` triggers). Replaced with the default WPF ComboBox template + alignment/padding setters. The `ItemContainerStyle` (ComboBoxItem) is preserved.
- `ImportPreviewWindow.xaml`: default window size 800×700 → 980×900 (`MinHeight=900`); conflicts `ScrollViewer` capped at `MaxHeight=520` with `VerticalScrollBarVisibility=Auto` so the bottom action bar stays reachable without manual resizing.
- `RELEASE_READINESS_PLAN.md` §0 updated again: Paket C moved ⚠️ PARTIAL → ✅ DONE, test count 35 → 63, §8 backlog reduced to 1 (installer); §3 hardening status refreshed; §7.3 Paket C sub-bullets filled in; §9 kabul kriterleri table updated (rollback / idempotency / post-validation / diagnostics all ✅); §11 final summary refreshed.
- `docs/RELEASE_SMOKE_TEST.md` §4.3 rollback "bilinen açık" note removed; added §4.4 (idempotent re-import) and §4.5 (post-import validation) smoke tests.

### Fixed
- **`ChangeBinding` strategy now actually finds an alternative port** — `IisImportService.ImportSite` is now `async Task<bool>` and awaits `FindAlternativePortAsync` from `IBindingManagerService` whenever a binding conflict has `ConflictResolutionStrategy.ChangeBinding` and no user-supplied port. Previously the strategy was a silent no-op and the import collided with the existing binding (item E bug). `IisImportChangeBindingTests` (6 tests) covers port-discovered, user-supplied, and exhausted-port scenarios.
- **Site `Skip` strategy now adds a `ReportEntry` consistent with pool `Skip`** — Caller's else branch now adds a `BuildSkipReportEntry` to the migration report when a site is skipped. Previously a skipped site was silently filtered out and the report only saw the pool-side skip entries. `IisImportSkipTests` (1 test) verifies the report entry shape.

### Removed
- `error.log` at repo root (residual log from a previous `IIS-MASTER` build path, `publish2` artifact).
- `inspect.csx` (developer-specific debug script with hardcoded `C:\Users\savas.boluk\Downloads\...` path).
- `inspect_tool/` orphan console project (not part of `IISDeployStudio.slnx`, no incoming references).

### Verified
- `dotnet build IISDeployStudio.slnx` ✅ (0 warnings, 0 errors)
- `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj` ✅ (**63/63** passed — 35 baseline + 4 rollback + 7 idempotency + 5 post-validation + 5 logging + 6 change-binding + 1 skip + 1 pool-reference + 1 app-pool-resolver)
- `pwsh -File .\scripts\publish-release.ps1` ✅ — `release/IISDeployStudio.exe` (182,348,265 B / ~174 MB) + `release/build.manifest.json` (git=`ce4910b`, buildTime=2026-06-05T21:59:06Z UTC) re-built 2026-06-06
- `pwsh -File .\scripts\validate-release.ps1` ✅ — 8/8 gates PASS, exit 0, git=`ce4910b`, exeSize=182,348,265 B

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
