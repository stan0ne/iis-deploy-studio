# IISDeploy Studio — Release Smoke Test Checklist

Bu doküman, `scripts/validate-release.ps1` tarafından otomatik kontrol edilemeyen
**manuel** release öncesi doğrulama adımlarını listeler. Otomatik gate'ler
(`validate-release.ps1`) yeşil olduğunda bu checklist sırayla uygulanmalıdır.

**Release engelleme kuralı:** Bu listedeki herhangi bir madde FAIL olursa,
release engellenir; önce kök neden düzeltilir, sonra tekrar doğrulanır.

---

## 0. Hazırlık (Pre-flight)

- [ ] `scripts/validate-release.ps1` exit code = 0 döndü
- [ ] `release/IISDeployStudio.exe` mevcut, boyut > 1 MB
- [ ] Test ortamı: Windows Server 2019+ veya Windows 11 + IIS rolü yüklü
- [ ] Test makinesinde Administrator hesabıyla oturum açıldı
- [ ] Hedef sunucuda IIS kurulu ve en az 1 örnek site mevcut
- [ ] Test paketinin çıktısı: `%TEMP%\iisdeploy-smoke\`

---

## 1. Uygulama Başlatma

- [ ] `release/IISDeployStudio.exe` çift tıklayarak çalıştır
- [ ] Ana pencere tam ekran açılıyor (`WindowState="Maximized"`)
- [ ] Tema yüklendi (Light veya Dark — sistem temasına göre)
- [ ] Sol panel site ağacı hedef sunucudaki siteleri listeliyor
- [ ] Sağ panel sunucu özet kartlarını gösteriyor (Site/AppPool/Binding sayıları)
- [ ] Status bar'da "Ready" yazıyor
- [ ] Uygulama crash olmadan en az 30 saniye çalışıyor

**FAIL kriteri:** Pencere açılmıyor, site listesi boş, crash log.

---

## 2. Bağımlılık Taraması

- [ ] "Scan Dependencies" butonu tıklanabilir
- [ ] Tarama tamamlandığında:
  - [ ] Runtime bölümünde .NET, ASP.NET Core sürümleri görünüyor
  - [ ] IIS Module bölümünde mevcut modüller "Installed" işaretli
  - [ ] **IIS OS Features (PowerShell) satırı mevcut** (Paket D doğrulaması)
  - [ ] Bu satır `OK` veya `Empty` durumunda (sunucuda PS yoksa `Failed:` kabul edilebilir)
  - [ ] Eksik bağımlılıklar kırmızı işaretli
- [ ] Tarama hata vermeden tamamlanıyor (UI donmuyor)
- [ ] Sonuçlar filtrelenebiliyor / aranabiliyor (varsa)

**FAIL kriteri:** `IIS OS Features (PowerShell)` satırı hiç yoksa
PowerShell entegrasyonu kopmuş demektir → release engellenir.

---

## 3. Export Akışı

### 3.1 Küçük site export
- [ ] Bir test sitesi seç (ör. `Default Web Site`)
- [ ] "Export" tıkla
- [ ] Export onay diyaloğu açılıyor
- [ ] Çıktı yolu seçilebilir (varsayılan: `Documents\IISDeployStudio\exports`)
- [ ] Export tamamlanıyor
- [ ] `.iispackage` dosyası oluştu
- [ ] Paket içinde: `manifest.json`, `sites/`, `applicationpools/`, `bindings/`, `web.config` dosyaları mevcut

### 3.2 Büyük site export (Paket B doğrulaması)
- [ ] 10.000+ dosya içeren bir test sitesi hazırla (örn. `C:\inetpub\bigsite\`)
- [ ] Bu siteyi export et
- [ ] İşlem tamamlanana kadar bekle (timeout yok)
- [ ] Paket içindeki dosya sayısı kaynaktaki dosya sayısına eşit
- [ ] **Hiçbir `Take()` limiti tetiklenmedi** — log dosyasında "truncated" kelimesi yok

**FAIL kriteri:** Paket içindeki dosya sayısı kaynaktan az, veya
logda "limit/10000/1000" gibi sınır referansı varsa → Paket B bozuk.

---

## 4. Import Akışı

### 4.1 Temiz import
- [ ] Bölüm 3'teki paketi seç
- [ ] "Import" tıkla
- [ ] Preview penceresi açılıyor
- [ ] Hedef fiziksel yol görünüyor ve düzenlenebilir
- [ ] Conflict yoksa direkt "Import" tıklanabilir
- [ ] Import tamamlandığında MessageBox detaylı sonuç gösteriyor
- [ ] Site IIS Manager'da görünüyor
- [ ] Site bindings doğru port/protokol ile eklendi
- [ ] Uygulama havuzu oluşturuldu ve doğru identity ile başlatıldı
- [ ] `C:\inetpub\<sitename>` altında dosyalar kopyalandı

### 4.2 Conflict senaryosu
- [ ] Aynı paketi tekrar import etmeyi dene
- [ ] Preview'da site/binding çakışmaları gösteriliyor
- [ ] Her çakışma için strateji seçilebilir (Skip / Overwrite / Rename / Clone / Change port)
- [ ] "Rename" seçildiğinde custom name input görünüyor
- [ ] "Change port" seçildiğinde custom port input görünüyor
- [ ] Import sonrası çakışma stratejisi uygulanmış (yeni isim / yeni port)

### 4.3 Retry + Rollback senaryosu (Paket C doğrulaması)
- [ ] Import sırasında ağ kesintisi simüle et (paket eksik bırak)
- [ ] Hata sonrası "Retry" butonu çalışıyor
- [ ] Aynı paket ile tekrar import denemesi başarılı oluyor
- [ ] **Rollback akışı doğrulanır** — hata senaryosunda `ITransactionManager.RollbackAsync` çağrılır; logda `rollback` / `TransactionManager` referansı beklenir
- [ ] **Hata sonrası IIS durumu import öncesi haline döner** — yarım kalan site / app pool / binding kalmadığı `Get-WebSite`, `Get-IISAppPool`, `Get-WebBinding` ile doğrulanır

**FAIL kriteri:** Rollback tetiklenmedi, hata sonrası IIS'te yarım kalan artifact kaldı, veya retry başarısız.

### 4.4 Idempotent re-import (Paket C doğrulaması)
- [ ] Başarılı bir import sonrası aynı paket (aynı manifest checksum) ile tekrar import denenir
- [ ] Preview penceresi açılır veya `ImportResult.Status = Skipped` mesajı görünür
- [ ] **IIS'e dokunulmadığı doğrulanır** — `Get-WebSite` / `Get-IISAppPool` çıktısı import öncesi ile birebir aynı (sadece `Last Modified` timestamp değişmemeli)
- [ ] Report archive'da yeni bir rapor eklenmediği doğrulanır (skip path rapor üretmez)

**FAIL kriteri:** Aynı paket için IIS'e yazma yapıldı veya yeni rapor dosyası oluştu.

### 4.5 Post-import validation (Paket C doğrulaması)
- [ ] Import tamamlandıktan sonra `IisImportService` son `ValidationResult` UI'da gösteriliyor
- [ ] Her imported site için "site exists" entry `OK` durumunda
- [ ] Her imported app pool için "pool exists" entry `OK` durumunda
- [ ] **Senaryo: import sırasında site elle silinirse** post-validation `Missing`/`Failed` severity ile raporlar
- [ ] Diagnostic log dosyasında scope start, her phase ve scope end için `Information` satırları mevcut

**FAIL kriteri:** Site/pool eksikse validation sessizce geçti, veya log dosyasında Information seviyesinde milestone kaydı yok.

---

## 5. Plugin Sistemi

- [ ] Boş `plugins/` klasörü uygulamayı crash etmiyor
- [ ] Bir test `.dll` plugin'i `plugins/` altına kopyalanıp uygulama yeniden başlatıldığında
      plugin yükleniyor
- [ ] Uygulama kapatıldığında plugin Dispose çağrılıyor (logda `Shutdown` mesajı)

---

## 6. Loglama

- [ ] `logs/` klasöründe Serilog rolling file oluştu
- [ ] Her önemli aksiyon (Export/Import/Scan) log satırına yazılıyor
- [ ] Hata durumunda exception detayı (stack trace) logda görünüyor
- [ ] **PowerShell hata logları `PowerShell Error:` öneki ile yazılıyor** (Paket D doğrulaması)
- [ ] Log viewer (UI) dosyayı kilitlemeden okuyabiliyor

---

## 7. Kapanış

- [ ] Tüm pencereleri kapat
- [ ] Uygulama düzgün şekilde exit (process kalmadı)
- [ ] Plugin Dispose log mesajı oluştu
- [ ] `release/` klasörü sonraki release adayı için arşivlenebilir

---

## Kabul Kriteri

Yukarıdaki tüm maddeler PASS ise **release adayı onaylanabilir**.

Herhangi bir madde FAIL ise:

1. FAIL maddesini kök neden analizi ile log'a kaydet
2. Düzeltmeyi yap
3. Tüm checklist'i baştan sona tekrar çalıştır (regression)
4. `scripts/validate-release.ps1` tekrar çalıştır
5. Yalnızca her iki geçiş de yeşil ise release onayı ver
