# Installer + Auto-Update Plan for IISDeploy Studio

## Summary
Inno Setup ile Windows installer (.exe setup), Velopack ile auto-update sistemi. İç ağda IIS üzerinden güncelleme dağıtımı.

---

## 1. Version Tracking Altyapısı

**Dosya:** `Directory.Build.props` (solution root)

```xml
<Project>
  <PropertyGroup>
    <Version>1.1.5</Version>
  </PropertyGroup>
</Project>
```

Bu .props dosyası otomatik olarak tüm csproj'lara uygulanır. Tek noktadan versiyon yönetimi sağlar.

---

## 2. Velopack Entegrasyonu (Auto-Update)

### 2.1. NuGet Paketleri — `src/UI/IISDeploy.UI.csproj`:
- `Velopack.App` (build-time) — publish sonrası `releases/` klasörü oluşturur
- `Velopack` (runtime) — güncelleme kontrolü ve uygulaması

### 2.2. Updater Yardımcı Projesi — `src/Updater`:
Velopack admin yetkisiyle dosya değiştirebilmek için ayrı bir executable ister.

**Dosya:** `src/Updater/IISDeploy.Updater.csproj`
```
net10.0-windows, win-x64, OutputType=WinExe
```

**Dosya:** `src/Updater/Program.cs`
```csharp
Velopack
  .UpdateExe
  .FromArgs(args)
  .Run();
```

### 2.3. Runtime Kod Değişiklikleri:

**Dosya:** `src/UI/App.xaml.cs` — `OnStartup`'a ekle:
```csharp
// Arka planda güncelleme kontrolü
Task.Run(async () => {
    var updateUrl = "https://updates.sirket.local/iisdeploy";
    var mgr = new UpdateManager(updateUrl);
    var update = await mgr.CheckForUpdatesAsync();
    if (update != null) {
        await Dispatcher.InvokeAsync(() => {
            // Toast ile bildir veya status bar'da göster
            ShowUpdateNotification(update.TargetFullRelease.Version);
        });
    }
});
```

**Arayüz:** Status bar'a "Update Available (vX.Y.Z)" göstergesi + tıklanınca update başlatma.

---

## 3. Inno Setup Installer

**Dosya:** `installer/setup.iss`

Özellikler:
- `PrivilegesRequired=admin` — IIS yönetimi için gerekli
- `ArchitecturesInstallIn64BitMode=x64compatible`
- `DefaultDirName={autopf}\IISDeploy Studio` → `C:\Program Files\IISDeploy Studio`
- Start Menu + Desktop kısayolu
- Add/Remove Programs kaydı
- `#define MyAppVersion GetFileVersion("release\IISDeployStudio.exe")` ile otomatik versiyon

**CI/CD build komutu:**
```
iscc.exe installer/setup.iss
```
Çıktı: `dist/installer/IISDeployStudio-Setup-1.1.5.exe`

---

## 4. Güncelleme Dağıtımı

### Önerilen: Şirket içi IIS sunucusu

```
https://updates.sirket.local/iisdeploy/
├── RELEASES          (Velopack appcast JSON)
└── *.nupkg           (delta güncelleme paketleri)
```

### Build Pipeline (manuel veya script):
```
1. dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release
2. velopack pack release --packId IISDeployStudio --packVersion 1.1.5
3. iscc.exe installer/setup.iss
4. releases/* → IIS update sunucusuna kopyala
5. dist/installer/*.exe → paylaşımlı klasöre kopyala
```

### Güncelleme Akışı:
```
Uygulama başlar → GET {updateUrl}/RELEASES
  ↓
Yeni sürüm var mı? → Evet: Toast bildirimi + "Update" butonu
  ↓
Kullanıcı tıklar → Delta .nupkg indirilir
  ↓
ApplyUpdatesAndRestart() → Uygulama kapanır
  ↓
update.exe (admin) → Program Files'taki dosyaları günceller
  ↓
Uygulama yeni sürümle yeniden başlar
```

---

## 5. Dosya Değişiklikleri Özeti

| Dosya | İşlem |
|---|---|
| `Directory.Build.props` | Yeni — versiyon tek noktası |
| `installer/setup.iss` | Yeni — Inno Setup scripti |
| `src/Updater/IISDeploy.Updater.csproj` | Yeni — Velopack helper |
| `src/Updater/Program.cs` | Yeni — 3 satırlık bootstrap |
| `src/UI/IISDeploy.UI.csproj` | Değişiklik — 2 NuGet paketi + Version prop'u |
| `src/UI/App.xaml.cs` | Değişiklik — update check kodu |
| `src/UI/MainWindow.xaml` | Değişiklik — status bar'a update göstergesi |
| `src/UI/MainWindow.xaml.cs` | Değişiklik — update başlatma işleyicisi |
| `.gitignore` | Değişiklik — `dist/`, `releases/` ekle |

---

## 6. Cevaplanması Gereken Sorular

1. **Güncelleme feed'i nerede?** Şirket içi IIS sunucusu mu, ağ paylaşımı mı?
2. **Güncelleme kontrol sıklığı:** Her başlangıçta mı, timer ile mi, manuel butonla mı?
3. **Güncelleme politikası:** Kullanıcı haber verilip isteğe bağlı mı, zorunlu mu?
4. **Kurulum türü:** Sadece makine başına (Program Files) mı, kullanıcı başına da olsun mu?

---

## 7. Doğrulama

- `dotnet build` ile çözüm derlenir
- `dotnet publish` ile release çıktısı alınır
- `velopack pack` ile releases/ oluşur
- Inno Setup ile installer exe üretilir
- Kurulum sonrası IISDeploy Studio başlatılır, admin yetkisi kontrol edilir
- Güncelleme URL'sine yeni bir RELEASES bırakılır, uygulama tekrar başlatılır → update bildirimi görünür
