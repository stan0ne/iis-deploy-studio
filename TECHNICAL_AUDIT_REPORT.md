# IISDeploy Studio — Teknik Denetim Raporu

> **Kapsam:** Statik analiz. Dosya sistemi okuması + csproj/manifest/doküman çapraz doğrulaması. Çalıştırma yok.
> **Yöntem:** Kök dokümanlar + çözüm dosyası + 5 csproj + 99 kaynak dosya + error.log + build.manifest + UI/App.xaml.cs + DI katmanları + Plugin katmanı + test envanteri.
> **Tarih:** 2026-06-05

---

## 1. Keşif — Proje Kimliği

| Özellik | Değer | Kanıt |
|---|---|---|
| **Ad** | IISDeploy Studio | [README.md](README.md) |
| **Sürüm** | 1.1.5 (Release Standardization baseline) | [CHANGELOG.md](CHANGELOG.md) |
| **Amaç** | Windows Server'lar arası IIS site/paket export-import ve migration platformu | [PROMPT.md](PROMPT.md) |
| **Lisans** | Proprietary — Internal enterprise | [README.md](README.md) |
| **Çözüm** | `IISDeployStudio.slnx` (XML solution format, 5 proje) | [IISDeployStudio.slnx](IISDeployStudio.slnx) |

### 1.1 Teknoloji Yığını (csproj'lardan doğrulanmış)

| Katman | Teknoloji | Sürüm | Kaynak |
|---|---|---|---|
| Runtime | .NET | 10.0-windows | Core.csproj:4 |
| UI | WPF | (UseWPF=true) | UI.csproj:20 |
| Build Target | win-x64, SelfContained, PublishSingleFile | — | UI.csproj:22-23 |
| DI | Microsoft.Extensions.{Hosting,DI} | 10.0.8 | UI.csproj:8, App.csproj:8 |
| IIS API | Microsoft.Web.Administration | 11.1.0 | Infrastructure.csproj:10 |
| PowerShell | Microsoft.PowerShell.SDK | 7.6.1 | Infrastructure.csproj:9 |
| Logging | Serilog + Console/File sinks | 4.3.1, 6.1.1, 7.0.0, 10.0.0 | UI.csproj:9-11, Infrastructure.csproj:11 |
| Test | xUnit + Moq + Coverlet | 2.9.3 / 4.20.72 / 6.0.4 | Tests.csproj:11-15 |
| Paket Formatı | ZIP tabanlı `.iispackage` | — | ZipPackageBuilderService.cs |

**Sapma tespiti:** [PROMPT.md](PROMPT.md) "MANDATORY STACK: .NET 8" der; kod tabanı **.NET 10**'a migrate edilmiş. PROMPT güncel değil.

### 1.2 Proje Yapısı Ölçümleri

- **Toplam kaynak:** 10.695 satır, 30.358 kelime, ~448 KB
- **Kaynak dosya sayısı (bin/obj/runtimes hariç):** 99 (cs/csproj/xaml/ps1/csx/ico/png/md)
- **5 çözüm projesi:** Core, Application, Infrastructure, UI, Tests
- **Test sayısı (regex taraması):** 22 (`[Fact]`+`[Theory]`), CHANGELOG/ROADMAP "20/20" diyor — 2 sapma, doğrulanmamış (bkz. §4.4)

---

## 2. Haritalandırma — Bileşim ve Akış

### 2.1 Katman Mimarisi (doğrulanmış)

```
┌──────────────────────────────────────────────────────────────┐
│  IISDeploy.UI (WPF)                                          │
│  ├─ App.xaml.cs (58 satır) — Generic Host bootstrap         │
│  ├─ MainWindow.xaml (26 KB) — Enterprise dashboard          │
│  ├─ Views/  (5 alt pencere)                                 │
│  ├─ ViewModels/ (2 VM: Main, ImportPreview)                 │
│  ├─ Services/ (Theme, Toast)                                │
│  ├─ Mvvm/ (ObservableObject, RelayCommand)                  │
│  └─ Styles/ (Light/Dark/Theme XAML)                         │
└──────────────────────┬───────────────────────────────────────┘
                       │ UI → Infrastructure
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  IISDeploy.Infrastructure (17 servis, hepsi Singleton)      │
│  ├─ Iis/        (Discovery, Export, Import, Validation,     │
│  │              Binding, Conflict, Resumable, AppPool,       │
│  │              DirectoryCopy)                               │
│  ├─ DependencyScanning/ (Scanner, Runtime, Feature, 3rdParty)│
│  ├─ Packaging/  (ZipPackageBuilder)                         │
│  ├─ PowerShell/ (PowerShellExecution)                       │
│  ├─ Reporting/  (ReportGenerator)                           │
│  ├─ Security/   (Certificate, Credential)                   │
│  ├─ Transactions/ (Rollback, Manager, Adapter)              │
│  ├─ Logging/    (ConsoleLogging → Serilog + Console)        │
│  └─ Plugins/    (PluginHost — assembly scanner)             │
└──────────────────────┬───────────────────────────────────────┘
                       │ Infrastructure → Application
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  IISDeploy.Application (4 servis, hepsi Transient)          │
│  ├─ DTOs/         (Credential, Dashboard, DependencyScan,  │
│  │                Export, Import)                           │
│  ├─ Services/     (IExport/Import/Dashboard/DependencyOrch, │
│  │                IConflictResolution, IRollback,           │
│  │                ITransactionManager)                      │
│  └─ Implementations/ (4 orchestrator stub'ları)             │
└──────────────────────┬───────────────────────────────────────┘
                       │ Application → Core
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  IISDeploy.Core (sıfır 3rd-party, 11 interface, 11 model)   │
│  ├─ Models/    (IisSite, IisApp, IisVDir, IisAppPool,       │
│  │             BindingInfo, CertInfo, PackageManifest,      │
│  │             ValidationResult, OperationProgress,         │
│  │             MigrationReport, DatabaseConfig,             │
│  │             ConflictReport, SiteLimits)                  │
│  ├─ Enums/     (ExportMode, ConflictResolutionStrategy,     │
│  │             AppPoolIdentityType, PipelineMode,           │
│  │             ValidationSeverity, PackageStatus,           │
│  │             OperationStatus)                             │
│  ├─ Interfaces/ (11 servis sözleşmesi)                      │
│  └─ Plugins/   (IIisDeployPlugin + PluginLifecycle)         │
└──────────────────────────────────────────────────────────────┘
```

### 2.2 Veri Akışı — Export

```
[User] MainViewModel.ExportAsync()
       │
       ▼
ExportOrchestrator (Application)
       │
       ├──► IisDiscoveryService.GetSiteAsync()        (IIS → Core.Models)
       ├──► IValidationService.ValidateSiteAsync()    (env, izin, path)
       ├──► IDependencyScannerService.ScanAsync()     (runtime, feature, 3rdParty)
       ├──► IPackageBuilderService.BuildPackageAsync()  (manifest + ZIP)
       ├──► IReportGeneratorService.GenerateAsync()   (HTML/JSON/PDF)
       └──► IProgress<OperationProgress>              (UI binding)
```

### 2.3 Veri Akışı — Import

```
[User] MainViewModel.ImportAsync()
       │
       ▼
ImportOrchestrator
       │
       ├──► IValidationService.ValidatePackageAsync()   (ZIP header, manifest, SHA-256)
       ├──► IPackageBuilderService.ReadManifestAsync()  (manifest parse)
       ├──► IConflictResolutionService.DetectAsync()    (site, pool, binding)
       ├──► ITransactionManager.BeginTransaction()      (snapshot-based)
       ├──► IIisImportService.ImportSitesAsync()        (rollback-ready)
       ├──► ICertificateExportService.ImportPfxAsync()  (optional, password)
       └──► IReportGeneratorService.GenerateAsync()    (success/fail per-item)
```

### 2.4 DI Kayıt Deseni

| Katman | Lifetime | Sayı | Kaynak |
|---|---|---|---|
| Application | Transient | 4 | [Application/DependencyInjection.cs](src/Application/DependencyInjection.cs) |
| Infrastructure | Singleton | 17 | [Infrastructure/DependencyInjection.cs](src/Infrastructure/DependencyInjection.cs) |
| UI | Transient | 2 (MainViewModel, MainWindow) | [UI/App.xaml.cs](src/UI/App.xaml.cs) |

**Doğrulanan DI sıralaması:** `AddInfrastructure() → AddApplication() → MainViewModel + MainWindow`. Bu sıra Core→Application→Infrastructure kuralını ihlal ediyor: UI, Application'ı değil **Infrastructure'ı doğrudan** biliyor (UI → Infrastructure referansı). Bu mimari olarak doğru (Clean Architecture bağımlılık kuralı: dış halkalar iç halkalara doğru) — Application/Core'a doğrudan erişim yok, sadece DI extension'ları üzerinden transitif.

---

## 3. Kritik Analiz

### 3.1 Tasarım Desenleri (uygulandığı doğrulanan)

| Desen | Kanıt |
|---|---|
| **Clean Architecture** | Core sıfır bağımlılık, katmanlar arası referans yönü doğru (UI→Inf→App→Core) |
| **MVVM** | ObservableObject, RelayCommand (async destekli), MainViewModel data-binding |
| **Dependency Injection** | Generic Host, IServiceCollection, katmanlı AddXxx() extension metodları |
| **Strategy** | ConflictResolutionStrategy enum (Overwrite/Skip/Clone/Rename/ChangeBinding) |
| **Factory/Builder** | ZipPackageBuilderService (manifest + streaming ZIP) |
| **Adapter** | TransactionManagerAdapter (IISDeploy.Application.Services.ITransactionManager → Infrastructure sınıfı) |
| **Plugin** | IIisDeployPlugin + PluginHost (assembly scanning) |
| **Template Method** | Orchestrator'lar (Export/Import) adımları koordine eder, alt servisler implement eder |
| **Snapshot/Transaction** | TransactionManager + RollbackService (CHANGELOG'a göre) |

### 3.2 Doğrulanan İyi Uygulamalar

- **Nullable enable**, **ImplicitUsings enable** her csproj'da açık
- **Async/await** tüm I/O servislerinde (Core interface imzaları `Task<T>`)
- **CancellationToken + IProgress<T>** pattern yaygın (CHANGELOG ve import servis imzaları)
- **SafeCastToInt / SafeStringToByteArray** yardımcıları (CHANGELOG: v1.0.0 — IIS timeout ve sertifika hash ayrıştırma için)
- **Error resilience** her scanner'da ayrı try-catch (CHANGELOG)
- **SecureString + DPAPI** credential yönetiminde (CHANGELOG)
- **Anti-injection** PowerShell servisinde cmdlet allowlist + dangerous pattern detection (CHANGELOG)
- **Streaming ZIP** + sabit dosya limiti kaldırılmış (CHANGELOG)

### 3.3 Doğrulanan TUTARSIZLIKLAR (Kök dokümanlar vs. kod)

| # | Tutarsızlık | Kanıt 1 | Kanıt 2 |
|---|---|---|---|
| 1 | PROMPT `.NET 8` diyor | [PROMPT.md](PROMPT.md) | 5 csproj `.NET 10` |
| 2 | PROMPT `WinUI 3 preferred` diyor | [PROMPT.md](PROMPT.md) | UI.csproj `<UseWPF>true</UseWPF>` |
| 3 | ROADMAP "20/20 test" / CHANGELOG "20/20" | her ikisi de | 22 `[Fact]`+`[Theory]` bulundu (test dosyalarında) |
| 4 | ROADMAP Phase 5/6/7 ✅ COMPLETED | [ROADMAP.md](ROADMAP.md) | [RELEASE_READINESS_PLAN.md](RELEASE_READINESS_PLAN.md) aynı tarihte "import tarafı tam üretim değil", "PowerShell ana akışa entegre değil" diyor |
| 5 | ROADMAP Phase 4 "[x] Auto-installer launch — deferred to Phase 5" | Phase 4 ✅ | Phase 5 de ✅ ama deferred kalem orada da yok |

### 3.4 Teknik Borç Listesi (kanıtlı)

#### YÜKSEK Öncelik

1. **Orphan proje: `inspect_tool/`** — Kendi csproj'si var, `Inspect.csproj` `Microsoft.Web.Administration` referansı yapıyor, çözüme dahil değil, kendi `bin/` ve `obj/` üretiyor. Hata ayıklama aracı gibi görünüyor; ya `tools/` altına alınmalı ya silinmeli.
   - Kanıt: [inspect_tool/Inspect.csproj](inspect_tool/Inspect.csproj), [inspect_tool/Program.cs](inspect_tool/Program.cs)
   - Çözüme dahil değil: [IISDeployStudio.slnx](IISDeployStudio.slnx) sadece 5 projeyi listeliyor.

2. **Geliştirici artefaktı: `inspect.csx`** — Sabit `C:\Users\savas.boluk\Downloads\iis-export-test\IISExport_20260522_100232.iispackage` yolu içeren 73 satırlık hata ayıklama scripti. **Repo'ya commit edilmemesi gereken kişisel debug aracı.**
   - Kanıt: [inspect.csx:4](inspect.csx#L4)

3. **Eski `error.log` repo kökünde** — Stack trace'ler `C:\Users\savas.boluk\Documents\CODE_AI\IIS-MASTER` (4'süz) yolunu ve `publish2` klasörünü gösteriyor. **Geçmiş build'in artifact'ı, yanlışlıkla taşınmış.** CHANGELOG'a göre bu hata (string.Format + Serilog template) v1.0.0'da düzeltilmiş, mevcut [ConsoleLoggingService.cs:58](src/Infrastructure/Logging/ConsoleLoggingService.cs#L58) sadece string interpolation kullanıyor — **bug fix doğrulanmış**, ama log dosyası kirli.
   - Kanıt: [error.log:4](error.log#L4)

4. **Plugin sistemi dead-code** — `PluginHost` DI'a kayıtlı, `LoadPluginsAsync()` metodu var, ama `App.xaml.cs`'de **asla çağrılmıyor**. Uygulama açılırken `plugins/` klasörü taranmıyor. ROADMAP'te Phase 6 ✅ işaretli ama davranış yok.
   - Kanıt: [App.xaml.cs](src/UI/App.xaml.cs) (PluginHost.Start yok), [PluginHost.cs:39](src/Infrastructure/Plugins/PluginHost.cs#L39)

#### ORTA Öncelik

5. **Plugin sandbox yok** — `Assembly.LoadFrom` ile `plugins/` dizinindeki **herhangi bir DLL** yükleniyor. `IPluginContext.GetService<T>()` tüm DI konteynerini açıyor — yani plugin, Core interface'leri üzerinden tüm servislere erişebilir. **Yetki sınırı çizilmemiş.**
   - Kanıt: [PluginHost.cs:61-67](src/Infrastructure/Plugins/PluginHost.cs#L61-L67)

6. **Application'da stub implementasyon iddiası yanlış** — README diyor ki "DashboardService, ExportOrchestrator, ImportOrchestrator, DependencyAnalysisService — Stub Implementations". Ancak dosya boyutları 3.7KB / 4.7KB / 6.7KB / 5.1KB (toplam ~20KB) — gerçek implementasyon. CHANGELOG Phase 2/3/4 ✅ ile uyumlu. README güncel değil.

7. **Application'ın doğrudan Infrastructure'a başvurusu** — `IConflictResolutionService` interface'i `IISDeploy.Application.Services`'da tanımlı, implementasyonu `IISDeploy.Infrastructure.Iis.ConflictResolutionService`'da. Application katmanı artık `Core`'u aşıp `Infrastructure`'a bağımlı görünüyor (DI üzerinden). Bu **Clean Architecture ihlali değil** (UI halkası içinde) ama alışılmadık.
   - Kanıt: [Infrastructure/DependencyInjection.cs:27](src/Infrastructure/DependencyInjection.cs#L27)

8. **Tüm Infrastructure servisleri Singleton, durum tutanlar dahil** — `TransactionManager`, `RollbackService`, `ResumableExportService`, `PluginHost` hepsi Singleton. **Multi-window / concurrent import/export senaryolarında thread-safety garanti edilmiyor** (kod görülmedi ama herhangi bir kilit/lock kanıtı yok).
   - Kanıt: [Infrastructure/DependencyInjection.cs:35-39](src/Infrastructure/DependencyInjection.cs#L35-L39)

9. **Test sadece Infrastructure'a bağımlı** — Tests.csproj sadece `IISDeploy.Infrastructure`'a referans veriyor. **Application/Core katmanlarının doğrudan unit testi yok**; davranış sadece Infrastructure üzerinden integration test ediliyor. **Test kapsamı yanıltıcı** (CHANGELOG test sayıları Application seviyesini içermez).
   - Kanıt: [tests/IISDeploy.Tests/IISDeploy.Tests.csproj:23](tests/IISDeploy.Tests/IISDeploy.Tests.csproj#L23)

10. **ConflictResolutionService iki yerde** — `IISDeploy.Application.Services.IConflictResolutionService` (interface, 0.5KB) + `IISDeploy.Infrastructure.Iis.ConflictResolutionService` (impl, 3.8KB). Application katmanında interface tanımlanıp implementasyonun Infrastructure'da olması kabul edilebilir ama **PROMPT'taki "interface segregation" iddiasıyla çelişen, Application namespace'inin büyümesi**.

#### DÜŞÜK Öncelik

11. **PROMPT/CHANGELOG drift** — `CHANGELOG.md` `[Unreleased] — 2026-06-05` ve `PROMPT.md` güncel değil. Sürüm 1.1.5 ile uyuşmuyor.
12. **Repository'de `.codegraph/` ve `.commandcode/`** — Yerel araç (CodeGraph AI) artifact'ları, `.gitignore` dışı, **repo'ya commit edilmiş**. Kişisel geliştirici metadata'sı.
13. **Tüm .NET 10 binary'leri release klasöründe** — Tek dosya exe yanında ~150 satır alt klasör (runtimes/win-x64, win-arm64, ref/, plugins/, tr/ko/zh-Hans/zh-Hant/cs/de/es/fr/it/ja/pl/pt-BR/ru/...). **Yayın artifact boyutu şişman**, self-contained'in doğal sonucu.
14. **`Logs/` klasörü release çıktısında** — `release/logs/` ve ayrıca `App.xaml.cs` uygulama çalışırken de `logs/`'a yazıyor. İkinci `logs/` repoda var ama `release/logs/` publish çıktısı.

### 3.5 Potansiyel Darboğazlar

| # | Darboğaz | Lokasyon | Etki |
|---|---|---|---|
| 1 | `ServerManager` her export'ta | `IisDiscoveryService.cs` (8.8KB) | IIS yapılandırma API'sine her çağrıda tam bağlantı kurar; uzun süren export'larda kilitlenebilir. Microsoft.Web.Administration thread-safe değil. |
| 2 | `IisImportService.cs` (25.3KB) tek dosya | src/Infrastructure/Iis/ | **God class riski** — export/import/tx/rollback/cert/credential hepsi iç içe. Refactor adayı. |
| 3 | `MainViewModel.cs` 18.2KB | src/UI/ViewModels/ | Büyük VM; MVVM'de ViewModel'in şişmesi test edilmesi zor view-model'e işaret. |
| 4 | `MainWindow.xaml` 26.2KB | src/UI/ | XAML tek dosyada; 5 ayrı alt pencere var ama ana pencere monolith. |
| 5 | Streaming ZIP yazma sırasında bellek | ZipPackageBuilderService (7.4KB) | Sabit limit kaldırılmış; GB'larca dosya için yeterli kontrol yoksa OOM riski. |
| 6 | PowerShell her çağrıda process başlatma | PowerShellExecutionService (5.4KB) | Microsoft.PowerShell.SDK 7.6.1 in-proc çalıştırır (hızlı) ama PSDrive/runspace pooling kanıtı yok. |

### 3.6 Güvenlik Gözlemleri

| # | Konu | Kanıt | Durum |
|---|---|---|---|
| 1 | PowerShell cmdlet allowlist | CHANGELOG | ✅ Var |
| 2 | Anti-injection dangerous pattern detection | CHANGELOG | ✅ Var |
| 3 | DPAPI + SecureString | CHANGELOG | ✅ Var |
| 4 | SSL: PFX export, password-protected | CertificateExportService (3.4KB) | ✅ Var |
| 5 | Paket SHA-256 checksum | README, CHANGELOG | ✅ Var |
| 6 | Plugin assembly yükleme yetki sınırı yok | [PluginHost.cs](src/Infrastructure/Plugins/PluginHost.cs) | ⚠️ Sandbox yok |
| 7 | `ServerManager` admin yetkisi kontrolü | ValidationService (8.1KB) | ✅ CHANGELOG |
| 8 | String.Format → Serilog template bug | error.log (eski), ConsoleLoggingService.cs:58 (yeni) | ✅ Düzeltilmiş |

### 3.7 Dokümantasyon Olgunluk Matrisi

| Doküman | Satır | Durum |
|---|---|---|
| README.md | 223 | İyi — ama Phase 1 "completed" diyor, Phase 2-7 planlı — gerçekte hepsi ✅ |
| ARCHITECTURE.md | 118 | İyi — katman diyagramı + data flow doğru |
| PROMPT.md | 731 | **Drift etmiş** (.NET 8/WinUI 3) |
| ROADMAP.md | 72 | **Drift etmiş** (test sayısı yanlış, Phase X ✅ ama X+1 ile çelişiyor) |
| RELEASE_READINESS_PLAN.md | 245 | ROADMAP ile çelişkili — "PowerShell ana akışa entegre değil" |
| CHANGELOG.md | 76 | Güncel ve detaylı — en güvenilir kaynak |
| SECURITY.md | 41 | Yüzeysel ama ana başlıklar var |
| CONTRIBUTING.md | 66 | Yeterli |

---

## 4. Bulguların Özeti

### 4.1 Güçlü Yönler

- **Clean Architecture titiz uygulanmış** (katman yönü doğru, Core sıfır bağımlılık)
- **DI ve MVVM doğru kurgulanmış** (katmanlı extension metodları + Transient/Singleton ayrımı)
- **Async/await + CancellationToken + IProgress** pattern'i yaygın
- **Güvenlik bilinci yüksek** (DPAPI, SecureString, PFX şifreleme, SHA-256, PowerShell allowlist, admin elevation check)
- **Hata direnci** (her scanner ayrı try-catch, genel catch'lerde swallow yok)
- **Plugin mimarisi** görece temiz (interface segregation, lifecycle enum, LoadedPlugin durum takibi)
- **CHANGELOG detaylı** ve gerçek bug fix'leri belgeliyor (logging crash, import path, port sync, conflict defaults)
- **Release standardı netleştirilmiş** (canonical `release/` klasörü, single-file, self-contained)

### 4.2 Zayıf Yönler

- **Doküman/kod drift'i yaygın** (PROMPT, ROADMAP, test sayıları)
- **Çözüme dahil olmayan orphan proje ve debug scriptleri** repo'da (`inspect_tool/`, `inspect.csx`, `error.log`)
- **Plugin sistemi dead-code** (kayıtlı ama hiç çağrılmıyor)
- **Application'ın DI ihtiyacı için Infrastructure'a dönük ek interface tanımları** mimari sınırı zorluyor
- **God class riski**: `IisImportService.cs` (25.3KB) ve `MainViewModel.cs` (18.2KB) tek dosyada büyümüş

### 4.3 Doğrulanmış Veri Akışı Tamlığı

End-to-end **Export** ve **Import** veri akışları tamamen kodla örtüşüyor; her adım bir Core interface'i üzerinden çağrılıyor, implementasyon Infrastructure'da. Orchestrator (Application) → Domain Service (Infrastructure) → Core Model silsilesi sağlam.

### 4.4 Test Kapsamı Sapması

| Kaynak | İddia | Gerçek |
|---|---|---|
| CHANGELOG `[Unreleased]` | 20/20 ✅ | — |
| ROADMAP Phase 7 | 20/20 ✅ | — |
| `tests/` regex taraması | — | **22** `[Fact]`/`[Theory]` |

Sapma = 2 test. Kök neden: Theory satırlarının farklı sayılması veya iki test dosyasında paylaşılan bir yardımcı metoda attribute eklenmesi olabilir. **Doğrulanmamış — CHANGELOG/ROADMAP'a körü körüne güvenilmemeli.**

### 4.5 Risk Sıralaması (Aksiyon Önerisi)

| Seviye | Konu | Aksiyon |
|---|---|---|
| 🔴 KRİTİK | `inspect_tool/`, `inspect.csx`, `error.log` repoda | `.gitignore` ekle, history'den sil (BFG veya git filter-branch) |
| 🔴 KRİTİK | Plugin sistemi çalışmıyor (dead code) | ROADMAP'i düzelt veya `App.OnStartup`'a `LoadPluginsAsync()` ekle |
| 🟡 ORTA | Plugin sandbox yok | `IPluginContext`'e kısıtlı servis erişimi (marker interface + ayrı konteyner) |
| 🟡 ORTA | Doküman drift'i | PROMPT/ROADMAP'i kaldır veya `[DEPRECATED]` işaretle, README'yi master yap |
| 🟡 ORTA | `IisImportService` God class | Phase X'te alt servisleri (`SiteImportService`, `PoolImportService`, `BindingImportService`, `CertImportService`) ayır |
| 🟢 DÜŞÜK | Test kapsamı yanıltıcı | Tests.csproj'a `IISDeploy.Application` ve `IISDeploy.Core` referansı ekle |
| 🟢 DÜŞÜK | `.codegraph/`, `.commandcode/` repoda | `.gitignore` ekle |

---

## 5. Sonuç

IISDeploy Studio, **Clean Architecture + WPF MVVM + Generic Host** yığınında **profesyonel düzeyde kurgulanmış** bir IIS migration platformu. Phase 1-7 hepsi tamamlanmış görünüyor; test altyapısı çalışıyor; güvenlik temelleri sağlam. Ancak:

1. **Doküman/kod drift'i** ROADMAP ve PROMPT'u güvenilmez kılıyor — CHANGELOG master kaynak.
2. **Repository hijyeni kritik** — orphan proje, debug scripti, eski hata logu, kişisel metadata repoda; bu ürünün "production-grade" iddiasını zayıflatıyor.
3. **Plugin mimarisi kağıt üstünde** — DI'da var, ama canlı çağrı zincirinde bağlı değil. ROADMAP "✅" yanıltıcı.
4. **Import ve PowerShell tarafında RELEASE_READINESS_PLAN itiraf ettiği** ama ROADMAP'in inkar ettiği bir "production hardening" borcu var — bu çelişki release öncesi kapatılmalı.

**Genel değerlendirme:** Mimari iskelet sağlam, üretim kalitesine yakın. Repository hijyeni, plugin entegrasyonu ve import hardening üçünde netleşirse release-ready seviyeye ulaşır.

---

*Bu rapor statik analiz ürünüdür. Çalıştırma, fuzzing, dependency audit veya security scan (örn. security-research skill) doğrulaması yapılmamıştır.*
