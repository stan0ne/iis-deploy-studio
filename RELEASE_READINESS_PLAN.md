# IISDeploy Studio — Release Readiness Plan

## Amaç
Bu plan, mevcut uygulamayı "release-ready" seviyeye taşımak için yapılacak işleri açıklar. Plan şu odaklara göre hazırlanmıştır:

- Sertifika desteği yok; sertifika export/import işlevi dahil edilmez.
- Büyük IIS siteleri için dosya kopyalama sınırları kaldırılır.
- Import akışının "tam üretim" seviyesine çıkarılması için net risk ve hardening adımları tanımlanır.
- PowerShell servisleri ana akışa entegre edilir.
- Yayın standardı netleştirilir ve release pipeline'a uygun hale getirilir.

---

## 1. Mevcut Durum ve Doğrulanan Temel

Şu anki kod tabanı ve publish çıktısı aşağıdaki şekilde doğrulanmıştır:

- `dotnet build IISDeployStudio.slnx` → başarılı
- `dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj` → 18 test başarılı, 0 başarısız
- `dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release` → başarılı

Bu da şunu gösterir:

- Mevcut proje, çalışan ve yayınlanabilir bir temel yapıya sahiptir.
- `release/` klasörü, release candidate olarak kullanılabilir bir çıktı üretmektedir.
- Ancak release standardı için ek güvenlik, kalite ve operasyonel hardening gerekir.

---

## 2. "Import tarafı tam üretim değil" ifadesinin anlamı

Bu ifade, teknik olarak şu anki import akışının tamamen "production-grade" olmadığı anlamına gelir. Yani:

### Şu an ne var?
- Site ve app pool import mantığı çalışıyor.
- Conflict detection ve basit overwrite / skip / rename akışları var.
- Testler bu akışı doğruluyor.

### Ama neden hâlâ "tam üretim" demiyoruz?
Çünkü production hardening için şu ek güvenlik ve operasyon özellikleri eksik kalıyor:

1. Rollback / transaction safety
   - Bir import sırasında bir site veya pool oluşturulup sonra hata alınırsa, sistemin önceki durumuna geri dönme garantisi yok.
   - Şu anki akış "başarılı/başarısız kayıt" seviyesinde kalıyor.

2. Idempotent import davranışı
   - Aynı paketin tekrar tekrar import edilmesi durumunda aynı sonucu vermesi gerekir.
   - Şu an mevcut akışta bu iyice test edilmemiştir.

3. Gerçek ortam senaryoları için daha kapsamlı validation
   - Port çakışması, fiziksel yol erişimi, izin sorunları, mevcut site isimleri, app pool çakışmaları gibi senaryolar testte kapsanmış olsa da, gerçek sunucu ortamında daha geniş smoke test gerekir.

4. Import sırasında detaylı diagnostic logging
   - Hangi dosya kopyalandı, hangi binding eklendi, hangi adım neden başarısız oldu gibi bilgiler daha görünür olmalıdır.

5. Partial failure yönetimi
   - Bir site import edilirken bir diğeri başarısız olabilir.
   - Bu durumda kullanıcıya net rapor ve tekrar çalıştırılabilir status verilmelidir.

Sonuç olarak:
- "Testler geçiyor" demek, üretim güvenliği için yeterli değildir.
- Bu yüzden import tarafı için hardening planı oluşturulmalıdır.

---

## 3. PowerShell entegrasyon eksiklikleri

PowerShell servislerinin bulunduğu doğru; ancak ana akışa tam entegre olmadığı için şu eksiklikler vardır:

### 3.1. Export / import akışında kullanılmıyor
- Mevcut `PowerShellExecutionService` var fakat UI veya orchestrator tarafında aktif olarak çağrılmıyor.
- Yani otomatik remediation, feature validation, module check veya environment repair akışı çalışmıyor.

### 3.2. Environment validation çok yüzeysel
- IIS kurulumu, yetki ve temel site kontrolü var.
- Ancak sistem düzeyinde:
  - IIS modülleri
  - ASP.NET runtime
  - VC++/dependency kontrolü
  - Windows feature kontrolü
  - PowerShell tabanlı health check
  gibi derin kontroller yok.

### 3.3. Otomatik düzeltme akışı yok
- Import sırasında "otomatik olarak eksik olan dependency veya IIS yapılandırmasını düzelt" mantığı yok.
- Bu da özellikle enterprise ortamında önemli bir eksikliktir.

### 3.4. PowerShell çıktıları raporlanmıyor
- PowerShell komutları çalışsa bile sonuçları kullanıcıya net şekilde gösteren pipeline yok.

### 3.5. Güvenlik ve yetki kontrolü sınırlı
- PowerShell komutlarını yönetmek için parametre doğrulama ve execution policy kontrolü daha güçlü hale getirilmelidir.

---

## 4. Büyük sitelerde dosya kopyalama sınırını kaldırma planı

Bu madde doğrudan uygulanmalıdır.

### Hedef
- Export sırasında fiziksel dosya kopyalama için sabit sınırları kaldırmak.
- Herhangi bir `Take(10000)` / `Take(1000)` limitine bağlı kalmamak.

### Uygulanacak yaklaşım

1. Rekürsif kopyalama fonksiyonunu tamamen stream / full traversal yapısına geçir.
2. Sınırsız dosya ve klasör sayısını destekleyen bir utility oluştur.
3. Kopyalama sırasında ilerleme bilgisi ve hata raporu sağla.
4. Kopyalama sırasında dosya/klasör sayısı ve toplam byte bilgisi toplasın.
5. Büyük siteler için performans ve bellek optimizasyonu ekle.

### Sonuç hedefi
- Export işlemi, 10.000 dosya veya 1000 klasör limitine değil; gerçek site boyutuna göre çalışır hale gelir.

---

## 5. Release Standardı

Bu proje için release standardı şu şekilde tanımlanmalıdır:

### 5.1. Build standardı
- `dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- Çıktı klasörü: `release/`

### 5.2. Test standardı
- Tüm unit testler geçer olmalı.
- Export/import smoke testleri manuel olarak doğrulanmalı.
- Release öncesi minimum kabul kriteri:
  - build başarılı
  - test başarılı
  - publish başarılı
  - manuel smoke test tamamlandı

### 5.3. Packaging standardı
- Tek dosya executable
- Self-contained runtime
- `release/` release artifact olarak saklanır
- `build.manifest` / `build.manifest.sig` gibi release metadata tutulur

### 5.4. Operational standardı
- Log dosyaları üretmeli
- Hata durumunda kullanıcıya anlamlı mesaj verilmeli
- Import/export adımları progress bar ile izlenmeli
- Exception detayları loglanmalı

### 5.5. Quality gate standardı
Release öncesi şu kontrol listesi zorunlu olmalı:

- Build başarılı
- Test başarılı
- Publish başarılı
- Smoke test tamamlandı
- Büyük site export test edildi
- Import test edildi
- PowerShell path / environment test edildi
- Hata senaryoları test edildi

---

## 6. Uygulanacak Ana Çalışma Paketleri

### Paket A — Release Baseline Stabilizasyonu
1. `release/` çıktısını release standardı ile yeniden doğrula.
2. Build / publish / test komutlarını tek dokümanda standardize et.
3. Version bilgisi ve manifest standardını netleştir.
4. Release note ve changelog akışını oluştur.

### Paket B — Büyük Dosya Kopyalama Sınırlarını Kaldırma
1. `IisExportService` içinde dosya/klasör traversal mantığını revize et.
2. `CopyDirectoryAsync` mantığını sınırsız traversal yapısına geçir.
3. Kopyalama loglama ve ilerleme raporlamasını artır.
4. Büyük site export test senaryosu ekle.

### Paket C — Import Hardening
1. Import işlemi için rollback planı ekle.
2. Partial failure senaryoları için report ve retry akışı tamamla.
3. Idempotent import davranışı için testler ekle.
4. Import sonrası validation sonucu kullanıcıya görünür hale getir.

### Paket D — PowerShell Entegrasyonu
1. `PowerShellExecutionService` kullanımını export/import akışına bağla.
2. Dependency ve IIS feature validation akışını PowerShell üzerinden çalıştır.
3. PowerShell çıktılarını report ve log sistemine bağla.
4. Execution error handling ve timeout policy ekle.

### Paket E — Release Quality Gates
1. Smoke test checklist oluştur.
2. Manuel validation scripti hazırla.
3. Release öncesi kontrol listesi ekle.
4. `release/` ve installer pipeline için hazırlık yap.

---

## 7. Önerilen Yol Haritası

### Aşama 1 — Stabilizasyon (1–2 gün)
- Build / publish / test pipeline doğrulama
- Release standardı belgelendirme
- Baseline artifact doğrulama

### Aşama 2 — Performans ve Kopyalama Güçlendirme (2–3 gün)
- Sınırsız traversal
- Büyük site test senaryoları
- İlerleme ve log iyileştirmeleri

### Aşama 3 — Import Production Hardening (2–4 gün)
- Rollback / retry / partial failure
- Idempotency testleri
- Validation report iyileştirmeleri

### Aşama 4 — PowerShell Entegrasyonu (2 gün)
- Ana akışa bağlama
- Validation ve remediation wiring

### Aşama 5 — Release Candidate (1 gün)
- Final build
- Testler
- Smoke test
- Release artifact onay

---

## 8. Kabul Kriterleri

Plan tamamlandığında şu kriterler sağlanmalıdır:

- Büyük site export işlemi herhangi bir sabit dosya/klasör sınırı olmadan çalışır.
- Import akışı için rollback ve partial failure senaryoları desteklenir.
- PowerShell servisleri ana akışa bağlanır ve hata durumları raporlanır.
- `release/` release artifact standardına uygun hale gelir.
- Build, test ve publish adımları tekrar tekrar doğrulanabilir.

---

## 9. Son Değerlendirme

Bu plan, mevcut uygulamayı sadece "çalışır" seviyeden çıkarıp "release-ready" seviyeye taşımayı hedefler. Özellikle:

- sertifika desteği hariç tutulur,
- büyük sitelerde sınır kaldırılır,
- import akışı production hardening'e taşınır,
- PowerShell entegrasyonu tamamlanır,
- release standardı belge ve pipeline seviyesinde netleşir.

Bu planı onayladıktan sonra, bir sonraki adımda gerçek kod değişikliklerine geçebiliriz.
