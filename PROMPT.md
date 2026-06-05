# MASTER PROMPT — Enterprise IIS Export / Import & Migration Platform

> **⚠️ [DEPRECATED — HISTORICAL REFERENCE ONLY]**
>
> Bu dosya, projenin ilk tasarım brief'ini içerir. Birçok bölümü güncel kod tabanıyla uyuşmaz (özellikle "TECHNOLOGY STACK" — aşağıdaki düzeltmeler uygulanmıştır).
>
> **Güncel doğrular için bak:**
> - Mimari ve build: [README.md](README.md), [ARCHITECTURE.md](ARCHITECTURE.md)
> - Sürüm geçmişi ve doğrulanmış durum: [CHANGELOG.md](CHANGELOG.md)
> - Phasing ve bilinen açıklar: [ROADMAP.md](ROADMAP.md), [RELEASE_READINESS_PLAN.md](RELEASE_READINESS_PLAN.md)
>
> **Güncel teknoloji yığını:** .NET 10 (Windows x64) + WPF + Microsoft.Web.Administration 11.1.0 + Serilog + Microsoft.PowerShell.SDK 7.6.1. `WinUI 3` seçeneği iptal edilmiştir.

You are a senior software architect and principal .NET engineer.

Your task is to design and build a production-grade Windows desktop application that can fully export and import Microsoft IIS websites and their dependencies between Windows servers through a modern GUI.

The application is NOT a simple backup utility.

It must function as a complete IIS migration/orchestration platform capable of migrating IIS websites from one Windows Server to another with minimal manual intervention.

The product is intended primarily for internal enterprise use.

The project must be designed with clean architecture, scalability, modularity, maintainability, reliability, and future extensibility in mind.

---

# PRIMARY GOAL

---

The application should allow users to:

* Export one IIS website
* Export multiple selected IIS websites
* Export all IIS websites
* Import exported packages into another IIS server
* Restore all IIS settings and dependencies automatically
* Analyze missing dependencies before import
* Validate environment compatibility
* Generate detailed audit reports
* Resolve conflicts interactively

The import result should recreate the IIS environment as accurately as possible.

SSL certificates must be excluded by default but optionally exportable/importable.

---

# TARGET OPERATING SYSTEMS

---

Supported operating systems:

* Windows Server 2012
* Windows Server 2016
* Windows Server 2019
* Windows Server 2022
* Windows Server 2025

Older systems are NOT required.

---

# TECHNOLOGY STACK

---

MANDATORY STACK:

Backend/Core:

* .NET 10 (Windows x64)

GUI:

* WPF

Architecture:

* Clean Architecture
* MVVM Pattern
* Dependency Injection
* Async/Await everywhere possible
* Modular service-based structure

IIS Interaction:

* Microsoft.Web.Administration
* appcmd.exe
* msdeploy (Web Deploy)
* PowerShell integration

Packaging:

* Self-contained single EXE
* x64 only

Storage:

* JSON metadata
* ZIP-based custom package format

Logging:

* Serilog

Reports:

* HTML reports
* JSON reports
* Optional PDF reports

---

# APPLICATION NAME IDEAS

---

Possible names:

* IISDeploy Studio
* IIS Migration Manager
* IIS Sync Studio
* IIS Transfer Tool
* IIS Package Manager
* IIS Relocation Manager

Use a professional enterprise visual style.

---

# CORE FEATURES

---

Implement ALL of the following:

============================================================

1. IIS SITE EXPORT
   ============================================================

The application must export:

* IIS Sites
* Applications
* Virtual Directories
* Application Pools
* Bindings
* MIME Types
* URL Rewrite Rules
* Handler Mappings
* ISAPI Filters
* FastCGI settings
* HTTP Redirect settings
* Authentication settings
* Authorization rules
* Compression settings
* Logging settings
* Custom Error Pages
* Request Filtering
* IP Restrictions
* ARR / Reverse Proxy settings
* Environment Variables
* IIS Modules
* GAC dependency references
* Web.config files
* appsettings.json files
* .env files
* Connection strings
* Site physical files
* Folder structures
* NTFS permissions (optional)
* IIS feature requirements
* Installed runtimes
* Installed hosting bundles
* Runtime dependency analysis

Export modes:

* Single site
* Multi-select
* Entire IIS server

---

# OPTIONAL FULL DEPENDENCY SCAN

---

Provide an optional advanced dependency scanner capable of detecting:

* ASP.NET Hosting Bundles
* .NET runtimes
* ASP.NET Core runtimes
* VC++ Redistributables
* URL Rewrite module
* ARR module
* FastCGI dependencies
* PHP installations
* Node.js installations
* Java runtimes
* ODBC DSNs
* COM registrations
* Windows Features
* IIS Role Services
* GAC assemblies
* PowerShell modules
* Windows Services related to the application

---

# EXPORT PACKAGE FORMAT

---

Create a custom package format:

.iispackage

Internally ZIP-based.

Example structure:

/manifest.json
/sites/
/applications/
/apppools/
/config/
/bindings/
/files/
/dependencies/
/reports/
/logs/
/runtime/
/metadata/

Manifest should include:

* package version
* export timestamp
* machine info
* IIS version
* OS version
* dependency list
* conflicts
* warnings
* exported objects

---

# SSL CERTIFICATE HANDLING

---

Default behavior:

* SSL certificates EXCLUDED

Optional behavior:

* Allow export/import of certificates
* Export as encrypted PFX
* Require password during export
* Require password during import

Private keys must never be stored insecurely.

---

# APPLICATION POOL IDENTITY HANDLING

---

Detect:

* LocalSystem
* LocalService
* NetworkService
* ApplicationPoolIdentity
* Custom identities

If custom identity:

* Export username only
* NEVER export passwords
* Request password again during import

Provide secure credential entry dialog.

---

# DATABASE CONFIGURATION DETECTION

---

Detect and analyze:

* web.config connectionStrings
* appsettings.json
* .env files
* ODBC DSNs
* SQL aliases

Import wizard should optionally allow:

* SQL server remapping
* Connection string replacement
* Environment variable replacement

---

# IMPORT ENGINE

---

The import engine must:

* Validate environment
* Check IIS compatibility
* Detect missing features
* Detect missing runtimes
* Detect missing modules
* Detect conflicting site names
* Detect binding conflicts
* Detect occupied ports
* Detect app pool conflicts
* Detect missing directories

Provide automatic remediation where possible.

---

# AUTO SERVER PREPARATION

---

The tool should optionally:

* Enable missing IIS features
* Install required Windows Features
* Detect missing runtimes
* Detect missing Hosting Bundles
* Detect missing URL Rewrite module
* Detect missing ARR module
* Suggest installation packages
* Launch installers automatically if approved

---

# IMPORT CONFLICT RESOLUTION

---

Implement import conflict strategies:

* Overwrite existing
* Clone as new site
* Rename imported site
* Change bindings
* Change ports
* Skip conflicting items
* Dry-run simulation mode

---

# GUI REQUIREMENTS

---

Modern enterprise-grade GUI.

MANDATORY UI FEATURES:

Main Dashboard:

* IIS tree view
* Site list
* Search/filter
* Status indicators
* Dependency indicators
* Runtime indicators

Left Panel:

* IIS hierarchy
* Sites
* Applications
* App pools

Right Panel:

* Detailed configuration
* Bindings
* Runtime requirements
* Dependency results
* Validation status

Wizards:

* Export Wizard
* Import Wizard
* Validation Wizard
* Dependency Scan Wizard

Additional:

* Dark mode
* Responsive layout
* Progress bars
* Background operations
* Cancellation support
* Toast notifications
* Detailed logs viewer
* Report viewer

---

# REPORTING SYSTEM

---

Generate detailed reports:

Export reports:

* Exported items
* Missing dependencies
* Warnings
* Runtime analysis

Import reports:

* Successfully imported items
* Failed items
* Missing dependencies
* Remediation suggestions
* Conflict resolutions
* Runtime mismatches

Output formats:

* HTML
* JSON
* Optional PDF

---

# LOGGING

---

Implement enterprise logging using Serilog.

Log:

* UI actions
* IIS operations
* Errors
* Warnings
* Dependency checks
* Import/export steps
* PowerShell execution
* msdeploy execution

Support:

* Rolling logs
* Log retention
* Debug mode
* Verbose mode

---

# SECURITY REQUIREMENTS

---

The application must:

* Require administrator privileges
* Validate elevation
* Avoid insecure credential storage
* Securely handle passwords
* Encrypt sensitive temporary data
* Prevent arbitrary PowerShell injection
* Validate package integrity
* Verify checksums
* Detect corrupted packages

---

# PERFORMANCE REQUIREMENTS

---

The application must:

* Use async operations
* Avoid UI freezing
* Handle very large IIS sites
* Support multi-GB exports
* Support resumable operations where possible
* Support cancellation tokens
* Use streaming ZIP operations

---

# PLUGIN / EXTENSION SYSTEM

---

Design future plugin architecture.

Possible future plugins:

* FTP migration
* Azure App Service migration
* Docker export
* Kubernetes export
* CI/CD integration
* Scheduled backups
* Remote deployment

---

# INTERNAL ARCHITECTURE

---

Recommended architecture:

/Core
/Application
/Infrastructure
/UI
/Services
/Contracts
/Models
/Logging
/Reports
/Packaging
/ImportEngine
/ExportEngine
/DependencyScanner
/Validators
/PowerShell
/MsDeploy
/Security
/Plugins

---

# REQUIRED COMPONENTS

---

Build dedicated services for:

* IISDiscoveryService
* IISExportService
* IISImportService
* DependencyScannerService
* RuntimeValidationService
* PackageBuilderService
* PackageReaderService
* ReportGeneratorService
* PowerShellExecutionService
* MsDeployService
* ConflictResolutionService
* CertificateExportService
* BindingManagerService
* AppPoolManagerService
* ValidationService
* LoggingService

---

# THREADING REQUIREMENTS

---

Long-running operations must:

* Run in background tasks
* Support cancellation
* Report progress
* Report estimated remaining time
* Prevent duplicate execution

---

# USER EXPERIENCE

---

The software should feel like:

* A professional Microsoft management tool
* Enterprise-grade deployment software
* Modern Windows administration software

The UX should prioritize:

* Reliability
* Clarity
* Visibility
* Troubleshooting
* Safe recovery

---

# FAILURE RECOVERY

---

Implement:

* Rollback support
* Partial import recovery
* Safe retry
* Transaction-like operations
* Temporary staging areas

---

# TESTING REQUIREMENTS

---

Include:

* Unit tests
* Integration tests
* Mock IIS environments
* Import/export validation tests
* Large package tests
* Failure simulation tests

---

# DOCUMENTATION REQUIREMENTS

---

Generate:

* README.md
* CHANGELOG.md
* ARCHITECTURE.md
* SECURITY.md
* ROADMAP.md
* CONTRIBUTING.md

Include:

* Diagrams
* Flowcharts
* Sequence diagrams
* Import/export lifecycle diagrams

---

# AI DEVELOPMENT RULES

---

While generating code:

* Never generate placeholder architecture
* Never create fake implementations
* Build production-grade implementations
* Use proper async patterns
* Use dependency injection everywhere
* Use interfaces properly
* Use SOLID principles
* Keep services modular
* Avoid God classes
* Avoid hardcoded paths
* Avoid insecure credential handling
* Include proper exception handling
* Include structured logging
* Include XML documentation
* Include meaningful comments

---

# DEVELOPMENT STRATEGY

---

Implement in phases:

PHASE 1

* Core architecture
* IIS discovery
* GUI shell

PHASE 2

* Export engine
* Package system

PHASE 3

* Import engine
* Validation engine

PHASE 4

* Dependency scanner
* Runtime analyzer

PHASE 5

* Reporting/logging/security

PHASE 6

* Advanced conflict resolution
* Rollback support

PHASE 7

* UI polishing
* Optimization
* Final testing

---

# FINAL EXPECTATION

---

The final product should behave like a real enterprise IIS migration platform and should be capable of reliably moving IIS workloads between Windows servers with minimal manual work.

The generated codebase must be maintainable, extensible, production-ready, and architecturally clean.
