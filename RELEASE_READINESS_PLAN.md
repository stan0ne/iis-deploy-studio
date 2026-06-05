# IISDeploy Studio — Release Readiness Plan

> **Durum:** Plan aktive edildi — paketler tamamlandıkça doğrulanmış kanıtlarla güncelleniyor.
> **Son doğrulama:** 2026-06-05 — `scripts/validate-release.ps1` exit 0 (tüm gate'ler PASS).

---

## 0. Paket Durumları (Verified)

| Paket | Ad | Durum | Kanıt |
|---|---|---|---|
| **A** | Release Baseline Stabilizasyonu | ✅ DONE | `validate-release.ps1` exit 0; build/test/publish gates PASS |
| **B** | Büyük Dosya Kopyalama Sınırlarını Kaldırma | ✅ DONE | `DirectoryCopyHelper.cs` tam rekürsif traversal; `Take()` limiti yok; 10.000+ dosya testi (DirectoryCopyTests) geçiyor |
| **C** | Import Hardening | ⚠️ PARTIAL | `ExecuteWithRetryAsync` `ImportOrchestrator`'da kullanılıyor; **rollback henüz `IisImportService`'e bağlanmadı** (farklı agent'ın aktif çalışması) |
| **D** | PowerShell Entegrasyonu | ✅ DONE | `IisFeatureScanner` artık `Get-WindowsFeature` çağırıyor + execution policy check + `Install-WindowsFeature` remediation; 11 PS testi yeşil |
| **E** | Release Quality Gates | ✅ DONE | `scripts/validate-release.ps1` (8 otomatik gate) + `docs/RELEASE_SMOKE_TEST.md` (manuel checklist) + `release/build.manifest.json` üretimi |

**Test sayısı:** 18 → 22 → 25 → **35** (şu an doğrulanan: 35/35 PASS)

---

## 1. Amaç

Bu plan, mevcut uygulamayı "release-ready" seviyeye taşımak için yapılacak işleri açıklar. Plan şu odaklara göre hazırlanmıştır:

- Sertifika desteği yok; sertifika export/import işlevi dahil edilmez.
- Büyük IIS siteleri için dosya kopyalama sınırları kaldırılır.
- Import akışının "tam üretim" seviyesine çıkarılması için net risk ve hardening adımları tanımlanır.
- PowerShell servisleri ana akışa entegre edilir.
- Yayın standardı netleştirilir ve release pipeline'a uygun hale getirilir.

---

## 2. Mevcut Durum ve Doğrulanan Temel

Şu anki kod tabanı ve publish çıktısı aşağıdaki şekilde **doğrulanmıştır** (2026-06-05):

- `dotnet build IISDeployStudio.slnx` → **başarılı, 0 warnings, 0 errors**
- `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj` → **25 test başarılı, 0 başarısız**
- `dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release` → **başarılı, IISDeployStudio.exe ~150 MB**
- `pwsh -File .\scripts\validate-release.ps1` → **exit 0, tüm 7 gate PASS**

Bu da şunu gösterir:

- Mevcut proje, çalışan ve yayınlanabilir bir temel yapıya sahiptir.
- `release/` klasörü, release candidate olarak kullanılabilir bir çıktı üretmektedir.
- Otomatik gate'ler artık release pipeline'ında non-negotiable kabul edilebilir.

---

## 3. "Import tarafı tam üretim değil" ifadesinin anlamı

Bu ifade, teknik olarak şu anki import akışının tamamen "production-grade" olmadığı anlamına gelir. Yani:

### 3.1 Şu an ne var?
- Site ve app pool import mantığı çalışıyor.
- Conflict detection ve basit overwrite / skip / rename akışları var.
- Retry desteği `ImportOrchestrator.ExecuteWithRetryAsync` üzerinden sağlanıyor.
- Testler bu akışı doğruluyor.

### 3.2 Ama neden hâlâ "tam üretim" demiyoruz?
Çünkü production hardening için şu ek güvenlik ve operasyon özellikleri eksik kalıyor:

1. **Rollback / transaction safety** — ⚠️ KISMEN
   - `RollbackService` ve `TransactionManager` mevcut ve DI'a kayıtlı.
   - **Ama henüz `IisImportService` tarafından çağrılmıyor** — sitede/pool'da yarıda kalındığında geri alma garantisi yok.
   - `ImportOrchestrator.ExecuteWithRetryAsync` retry sağlıyor ama rollback değil.
2. **Idempotent import davranışı** — ⚠️ KISMEN
   - Conflict resolution stratejileri (Skip/Overwrite/Rename/Clone/ChangePort) preview diyaloğunda seçilebiliyor.
   - Aynı paketin tekrar import edilmesinde davranış stratejiye bağlı; test ile sınırlı kapsam.
3. **Gerçek ortam senaryoları için smoke test** — ✅ YAPILDI
   - `docs/RELEASE_SMOKE_TEST.md` 7 bölüm halinde manuel doğrulama adımlarını içeriyor.
4. **Diagnostic logging** — ✅ YAPILDI
   - `ConsoleLoggingService` düzeltildi (Serilog template crash yok).
   - PowerShell hata logları `PowerShell Error:` öneki ile yazılıyor.
5. **Partial failure yönetimi** — ⚠️ KISMEN
   - Scanner hataları `DependencyScannerService` içinde try/catch ile izole edildi.
   - Import kısmi hata senaryoları için `ApplyCustomPorts`, custom rename, vs. mevcut ama toplu "rollback to checkpoint" yok.

---

## 4. PowerShell entegrasyonu — Durum

### 4.1 Tamamlanan işler (Paket D)

- ✅ `IisFeatureScanner` artık `IPowerShellExecutionService` ve `ILoggingService` ile DI alıyor.
- ✅ Yeni `CheckIisFeaturesViaPowerShellAsync` metodu `Get-WindowsFeature` cmdlet'ini
  allowlisted script olarak çalıştırıyor.
- ✅ PowerShell çıktısı `DependencyInfo` (`Type = "PowerShellFeatureScan"`) olarak
  scan sonucuna ekleniyor.
- ✅ Çıktı `ILoggingService.Information` ile loglanıyor.
- ✅ Hata durumunda `ILoggingService.Warning` + "Failed:" DependencyInfo (graceful degrade).
- ✅ 3 yeni unit test (`IisFeatureScannerPowerShellTests`) eklendi — başarı, boş çıktı, hata senaryoları.

### 4.2 Hâlâ açık (backlog)

- `IisImportService` PowerShell remediation adımı (`Install-WindowsFeature`) henüz tetiklenmiyor — farklı agent'ın aktif çalışma alanı.
- PowerShell timeout policy net tanımlı değil (interface'te `CancellationToken` geçiriliyor ama policy dökümante edilmemiş).
- Execution policy kontrolü şu an cmdlet bazlı allowlist'e bırakıldı; makine-bazlı `Set-ExecutionPolicy` kontrolü eklenmedi.

---

## 5. Büyük sitelerde dosya kopyalama sınırı (Paket B — DONE)

- ✅ `DirectoryCopyHelper.cs` tam rekürsif traversal yapıyor.
- ✅ `Take(10000)` / `Take(1000)` limiti yok.
- ✅ 10.001 dosya ve 1.001 klasör senaryoları `DirectoryCopyTests` ile doğrulanıyor.

---

## 6. Release Standardı

### 6.1 Build standardı
- `dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- Çıktı klasörü: `release/`
- Validate: `scripts/validate-release.ps1` "Publish single-file artifact" gate'i exe boyutunu kontrol ediyor.

### 6.2 Test standardı
- Tüm unit testler geçer olmalı (şu an 25/25).
- Export/import smoke testleri `docs/RELEASE_SMOKE_TEST.md` ile manuel olarak doğrulanır.
- Release öncesi minimum kabul kriteri:
  - `validate-release.ps1` exit 0
  - 7 gate'in tamamı PASS
  - Smoke test checklist tüm maddeler PASS

### 6.3 Packaging standardı
- Tek dosya executable (`IISDeployStudio.exe`)
- Self-contained runtime
- `release/` release artifact olarak saklanır
- `build.manifest` / `build.manifest.sig` gibi release metadata **henüz yok** (backlog)

### 6.4 Operational standardı
- ✅ Log dosyaları üretiliyor (Serilog rolling file)
- ✅ Hata durumunda kullanıcıya anlamlı mesaj veriliyor
- ✅ Import/export adımları progress bar ile izleniyor
- ✅ Exception detayları loglanıyor

### 6.5 Quality gate standardı (Paket E — DONE)
- ✅ `scripts/validate-release.ps1` — 7 otomatik gate
- ✅ `docs/RELEASE_SMOKE_TEST.md` — 7 bölümlük manuel checklist

---

## 7. Tamamlanan Ana Çalışma Paketleri (Detay)

### 7.1 Paket A — Release Baseline Stabilizasyonu ✅
1. ✅ `release/` çıktısı release standardı ile yeniden doğrulandı.
2. ✅ Build / publish / test komutları `validate-release.ps1` içinde standardize edildi.
3. ✅ Version bilgisi README'de netleştirildi (`1.1.5`).
4. ✅ `CHANGELOG.md` ile release note akışı kuruldu.

### 7.2 Paket B — Büyük Dosya Kopyalama Sınırlarını Kaldırma ✅
1. ✅ `IisExportService` içinde traversal mantığı gözden geçirildi.
2. ✅ `CopyDirectoryAsync` tam rekürsif yapıda.
3. ✅ 10.001 dosya + 1.001 klasör unit testleri (`DirectoryCopyTests`).

### 7.3 Paket C — Import Hardening ⚠️
1. ⚠️ Rollback planı kısmen mevcut — `RollbackService` sınıfı var ama `IisImportService` henüz çağırmıyor.
2. ✅ Retry akışı `ImportOrchestrator.ExecuteWithRetryAsync` ile sağlanıyor.
3. ⚠️ Idempotent import testleri smoke test checklist'ine eklendi ama otomatik test setinde genişletilebilir.
4. ✅ Import sonrası validation sonucu MessageBox'ta detaylı gösteriliyor.

### 7.4 Paket D — PowerShell Entegrasyonu ✅
1. ✅ `IisFeatureScanner` üzerinden ana akışa bağlandı.
2. ✅ Dependency ve IIS feature validation `Get-WindowsFeature` ile çalışıyor.
3. ✅ PS çıktıları `ILoggingService` ve `DependencyInfo` üzerinden raporlanıyor.
4. ✅ Hata durumunda `SecurityException` + graceful degrade.

### 7.5 Paket E — Release Quality Gates ✅
1. ✅ `scripts/validate-release.ps1` — 7 otomatik gate (SDK, dosya, hijyen, build, test, publish, PROMPT tutarlılığı).
2. ✅ `docs/RELEASE_SMOKE_TEST.md` — 7 bölümlük manuel checklist.
3. ✅ Release öncesi kontrol listesi her iki dokümandan birlikte çalıştırılır.
4. ⚠️ `installer` pipeline'ı henüz yok (backlog).

---

## 8. Açık İşler (Backlog — Sonraki Release)

1. **Paket C tamamlanması** — `IisImportService` içinde `RollbackService` entegrasyonu (şu an farklı bir agent çalışıyor).
2. **Installer pipeline** — MSI/EXE installer üretimi.

---

## 9. Kabul Kriterleri (Güncel)

| Kriter | Durum | Kanıt |
|---|---|---|
| Büyük site export işlemi herhangi bir sabit dosya/klasör sınırı olmadan çalışır | ✅ | `DirectoryCopyTests` 10.001 dosya + 1.001 klasör |
| Import akışı retry senaryolarını destekler | ✅ | `ImportOrchestrator.ExecuteWithRetryAsync` + `TransactionTests` |
| Import akışı rollback senaryolarını destekler | ❌ | `RollbackService` sınıfı var, henüz `IisImportService`'e bağlı değil |
| PowerShell servisleri ana akışa bağlanır ve hata durumları raporlanır | ✅ | `IisFeatureScanner` (3 yöntem: scan + policy + remediation) + 11 PS unit test + `PowerShell Error:` log |
| PS execution policy restrictive ise kullanıcı uyarılır | ✅ | `IisFeatureScanner.CheckPowerShellExecutionPolicyAsync` + `Restricted/AllSigned → Required=false` |
| PS üzerinden `Install-WindowsFeature` ile feature remediation çağrılabilir | ✅ | `IisFeatureScanner.RemediateIisFeaturesAsync` + 3 unit test |
| Paket checksum içerik-deterministic (aynı içerik → aynı checksum) | ✅ | `PackageBuilderTests.BuildPackage_Should_Produce_Stable_Checksum_For_Identical_Content` |
| `release/build.manifest.json` her publish sonrası üretilir | ✅ | `scripts/publish-release.ps1` post-publish step |
| `release/` release artifact standardına uygun | ✅ | `validate-release.ps1` Publish + Build manifest gate PASS |
| Build, test ve publish adımları tekrar tekrar doğrulanabilir | ✅ | `validate-release.ps1` 8/8 gates PASS |

---

## 10. Release Onay Süreci

1. `pwsh -File .\scripts\validate-release.ps1` → exit 0
2. `docs/RELEASE_SMOKE_TEST.md` tüm maddeleri → PASS
3. `CHANGELOG.md` `[Unreleased]` bölümü sürüm numarası ile değiştirildi
4. `release/` klasörü arşivlendi / dağıtım kanalına yüklendi
5. PR veya release tag oluşturuldu

**Yalnızca 1–5 adımların tamamı PASS ise release onaylanır.**

---

## 11. Son Değerlendirme

Bu plan, mevcut uygulamayı "çalışır" seviyeden **release-ready** seviyeye taşımayı
hedeflemiş ve **6 paketten 5'i tamamlanmış, 1'i (Paket C — rollback) kısmen
tamamlanmıştır.** Kalan işler §8 Backlog'ta listelenmiştir ve sonraki release
döngüsünde ele alınacaktır.

Özellikle:

- ✅ sertifika desteği hariç tutuldu,
- ✅ büyük sitelerde sınır kaldırıldı,
- ⚠️ import akışı kısmen production-hardening'e taşındı (retry DONE, rollback BACKLOG),
- ✅ PowerShell entegrasyonu tamamlandı (scan + execution policy + remediation),
- ✅ release standardı otomatik gate (8/8) + manuel checklist + build.manifest seviyesinde netleşti,
- ✅ paket içerik-determinism ve build traceability sağlandı.
