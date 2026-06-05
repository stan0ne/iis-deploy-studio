# IISDeploy Studio — Installation Guide

This guide covers installing IISDeploy Studio on a production Windows Server using the official MSI installer.

## 1. System Requirements

### Minimum
- **OS:** Windows Server 2012 (build 17763) or later — x64
- **IIS:** IIS 7.5 or later with the IIS Management Console role
- **.NET:** .NET 10 Desktop Runtime (x64) — the MSI bundles the self-contained app, but a recent Desktop Runtime is recommended for system-wide tools
- **Privileges:** Administrator (IIS site/application pool management requires elevation)
- **Disk:** ~250 MB free (60 MB MSI + ~190 MB installed payload)

### Supported Editions
- Windows Server 2012 R2
- Windows Server 2016
- Windows Server 2019
- Windows Server 2022
- Windows Server 2025

The MSI is **x64-only**. There is no 32-bit build.

---

## 2. Downloading the Installer

Official MSI builds are published as GitHub Release artifacts:

1. Navigate to the repository's [Releases](https://github.com/) page.
2. Download `IISDeployStudio-Setup.msi` from the latest release.
3. Verify the file size is ~60 MB (a partial download will fail MSI validation).

> **Security tip:** Always download from the official Releases page. The MSI is signed by the release pipeline; check the digital signature in the file properties before running.

---

## 3. Installation

### 3.1 Standard (Interactive)

1. Double-click `IISDeployStudio-Setup.msi`.
2. If prompted by UAC, click **Yes** to allow elevation.
3. Accept the license terms.
4. Choose the install location (default: `C:\Program Files\IISDeployStudio`).
5. Click **Install**.
6. Click **Finish** when complete.

The installer creates:
- A Start Menu shortcut under **IISDeploy Studio**
- The application folder at `%ProgramFiles%\IISDeployStudio`
- A `plugins\` subfolder for runtime plugin discovery
- An uninstall entry in **Settings → Apps → Installed apps**

### 3.2 Silent (Unattended Deployment)

For fleet deployment via Group Policy, SCCM, Intune, or a deployment script:

```cmd
msiexec /i IISDeployStudio-Setup.msi /qn ALLUSERS=1 MSIINSTALLPERUSER=0
```

Common flags:
- `/qn` — completely silent (no UI)
- `/qb` — basic UI with progress bar
- `/l*v install.log` — verbose log
- `INSTALLFOLDER="D:\Tools\IISDeployStudio"` — custom install path

Full example with logging and custom path:

```cmd
msiexec /i IISDeployStudio-Setup.msi /qn ^
    INSTALLFOLDER="D:\Tools\IISDeployStudio" ^
    /l*v C:\Windows\Temp\iisdeploy-install.log
```

Exit code `0` = success, `3010` = reboot required (not expected for this MSI), anything else = failure — check the log.

### 3.3 PowerShell (One-liner)

```powershell
Start-Process msiexec.exe -ArgumentList '/i', "$PSScriptRoot\IISDeployStudio-Setup.msi", '/qn', '/l*v', "$env:TEMP\iisdeploy-install.log" -Wait -NoNewWindow
```

---

## 4. First-Run Verification

1. Launch **IISDeploy Studio** from the Start Menu.
2. The main window opens in maximized state with the site tree on the left.
3. The right panel should populate with site/app pool/binding counts within a few seconds.
4. If the site tree is empty, the service account running IISDeploy Studio does not have access to the IIS configuration. Re-launch **as Administrator** (right-click → Run as administrator).

A successful discovery populates the status bar with site count, application pool count, and binding count.

---

## 5. Uninstallation

### 5.1 Settings UI
**Settings → Apps → Installed apps → IISDeploy Studio → Uninstall**

### 5.2 Control Panel
**Control Panel → Programs and Features → IISDeploy Studio → Uninstall/Change**

### 5.3 Silent
```cmd
msiexec /x IISDeployStudio-Setup.msi /qn /l*v uninstall.log
```

> The uninstaller removes the application folder, Start Menu shortcuts, and the `SOFTWARE\IISDeploy\IISDeployStudio` registry key. **Exported `.iispackage` files in your Documents folder are not touched** — back them up separately if needed.

---

## 6. Upgrading

To upgrade an existing installation:

1. Download the new `IISDeployStudio-Setup.msi`.
2. Run it. The installer detects the existing version and performs an in-place upgrade.
3. Your existing data (exported packages, plugin folder contents) is preserved.

> **Breaking-change releases** will be flagged in the release notes and may require uninstall-then-install. Always read the release notes before upgrading production.

---

## 7. Plugin Folder

The installer creates `%ProgramFiles%\IISDeployStudio\plugins\` for runtime plugin discovery. The application watches this folder on startup; drop a compatible plugin DLL in and restart the app to load it.

> The installer does **not** ship with any plugins. Plugin development is a separate concern — see `docs/PLUGIN_DEVELOPMENT.md` (forthcoming) for the contract.

---

## 8. Logs and Diagnostics

| Location | Content |
|---|---|
| `%LOCALAPPDATA%\IISDeployStudio\Logs\*.log` | Rolling application logs (Serilog) |
| `release/build.manifest.json` (in the publish folder) | Build metadata for the installed binary |
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\IISDeployStudio` | MSI uninstall metadata including version |

If the application fails to start, check the Serilog files first — they include the full exception stack and the IIS discovery errors.

---

## 9. Troubleshooting

### "I cannot see any sites"
- Run as Administrator. IIS configuration read requires elevation.
- Confirm the IIS role is installed: `Get-WindowsFeature Web-Server`.
- Check `Microsoft.Web.Administration.dll` is present (it ships with IIS — reinstall the IIS Management Console role if missing).

### "The MSI fails with error 1603"
- A previous install left files locked. Reboot and retry.
- Another MSI is running. Wait for it to finish or kill `msiexec.exe`.
- Insufficient disk space. Free at least 250 MB.

### "The application crashes on startup"
- Confirm .NET 10 Desktop Runtime is installed: `dotnet --list-runtimes`.
- Check `%LOCALAPPDATA%\IISDeployStudio\Logs\` for the most recent log file.

### "Start Menu shortcut is missing"
- The install completed but the per-user shortcut creation failed. Re-run the MSI in **Repair** mode (`msiexec /fa IISDeployStudio-Setup.msi`).

---

## 10. Enterprise Deployment Notes

- The MSI is **per-machine** (`Scope="perMachine"` in the WiX authoring). All users on the box get the same install.
- The MSI registers an uninstall entry under `HKLM`, not `HKCU`. Group Policy can target it from the computer scope.
- The MSI is **not** a ClickOnce-style auto-updating app. Upgrade by deploying the new MSI.
- No telemetry, no network calls during install, no background services installed. The app is a standalone WPF executable.

For automated fleet rollout, wrap the `msiexec /qn` command in your tool of choice (SCCM application, Intune Win32 app, GPO startup script, Ansible `win_package`, etc.).
