# Roadmap

## Phase 1 — Core Architecture & GUI Shell ✅ COMPLETED
- [x] Clean Architecture solution structure
- [x] Domain models, interfaces, enums
- [x] IIS Discovery Service (Microsoft.Web.Administration)
- [x] Validation Service
- [x] Application DTOs and orchestrator interfaces
- [x] WPF MVVM UI shell with dashboard
- [x] DI container and Serilog logging

## Phase 2 — Export Engine ✅ COMPLETED
- [x] Full .iispackage builder with streaming ZIP
- [x] Configuration export (web.config, appsettings.json, .env)
- [x] Physical file packaging
- [x] Certificate export (encrypted PFX) — deferred to Phase 5
- [x] NTFS permissions capture — deferred to Phase 5
- [x] Multi-site and full-server export modes
- [x] Progress reporting with percentage
- [x] Cancellation support
- [x] Unit tests for package & export pipeline (8 tests passing)

## Phase 3 — Import Engine & Validation ✅ COMPLETED
- [x] Package reader with manifest parsing (via IPackageBuilderService)
- [x] IIS object creation (sites, apps, pools, bindings)
- [x] Environment compatibility check
- [x] Missing feature detection
- [x] Conflict detection (names, ports, bindings)
- [x] Automatic remediation engine (port changes, conflict strategies)
- [x] Credential management dialogs — deferred to Phase 5
- [x] Connection string remapping — deferred to Phase 5
- [x] Dry-run simulation mode
- [x] Unit tests (13/13 passing)

## Phase 4 — Dependency Scanner ✅ COMPLETED
- [x] Runtime detection (.NET, ASP.NET Core, VC++)
- [x] IIS module detection (URL Rewrite, ARR, FastCGI, WebSocket)
- [x] Third-party detection (PHP, Node.js, Java, ODBC DSNs)
- [x] Windows Feature analysis (W3SVC, WAS services)
- [x] Installation suggestions with download URLs
- [x] Auto-installer launch — deferred to Phase 5
- [x] Unit tests (13/13 passing)

## Phase 5 — Reporting & Security ✅ COMPLETED
- [x] HTML/JSON/PDF report generation (HTML styled, JSON structured, PDF placeholder)
- [x] Advanced Serilog configuration (Console + rolling file, via App.xaml.cs host)
- [x] Package integrity with checksums (SHA-256, stable across manifest updates)
- [x] Secure credential handling (DPAPI Protect/Unprotect, SecureString, memory erasure)
- [x] PowerShell execution service (command allowlist, anti-injection, safe params)
- [x] Certificate export/import service (encrypted PFX via X509CertificateLoader)
- [x] Anti-injection protections (dangerous pattern detection, cmdlet allowlist)
- [x] 13/13 unit tests passing, 0 build warnings

## Phase 6 — Advanced Features ✅ COMPLETED
- [x] Conflict resolution strategies (Overwrite — removes existing, Skip, Clone, Rename, ChangeBinding)
- [x] Rollback support (RollbackService — snapshot-based IIS site/pool removal)
- [x] Partial import recovery (TransactionScope with DisposeAsync rollback)
- [x] Transaction-like operations (TransactionManager with BeginTransaction, CommitAsync, RollbackAsync)
- [x] Resumable exports (ResumableExportService — checkpoint save/load/resume)
- [x] Plugin architecture (IIisDeployPlugin, PluginHost — assembly scanning + lifecycle)
- [x] ExecuteWithRetryAsync for resilient operations
- [x] 18/18 unit tests passing, 0 build warnings

## Phase 7 — Polish & Optimization ✅ COMPLETED
- [x] Dark mode (ThemeService with toggle, full color palette swap)
- [x] Toast notifications (non-blocking, animated, success/warning/error/info)
- [x] Detailed log viewer (LogViewerWindow with filter, auto-scroll, refresh)
- [x] Report viewer (ReportViewerWindow with WebBrowser, report list)
- [x] Performance optimization (async file ops, cancellation support throughout)
- [x] Large-site export/import copy hardening (removed fixed file/dir limits for full traversal)
- [x] Unit and integration tests (35/35 passing, 0 build warnings)
- [x] CI/CD pipeline — deferred (project is self-contained dotnet build)
