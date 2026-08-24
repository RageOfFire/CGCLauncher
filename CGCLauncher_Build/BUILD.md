# Building the CGCLauncher installer

## Tools you need on your Windows machine

| Tool | Download |
|------|----------|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 |
| NSIS 3.x | https://nsis.sourceforge.io/Download |
| signtool (optional) | Comes with Visual Studio or the Windows SDK |

---

## Step 1 — Publish the C# project

Open a terminal in the `CGCLauncher/` source folder and run:

```
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

This creates a `publish/` folder containing `CGCLauncher.exe`, all required
DLLs, and the `Assets/` subfolder.

---

## Step 2 — Folder layout before compiling

Place the NSIS files next to the `publish/` folder like this:

```
(your working folder)/
├── CGCLauncher.nsi
├── LICENSE.txt
└── publish/
    ├── CGCLauncher.exe
    ├── *.dll
    └── Assets/
        ├── logo.ico
        ├── logo.png
        └── VT323-Regular.ttf
```

---

## Step 3 — Compile the installer

**Option A — GUI:**
Right-click `CGCLauncher.nsi` → "Compile NSIS Script"

**Option B — Command line:**
```
makensis CGCLauncher.nsi
```

Output: `CGCLauncher_Setup.exe` in the same folder.

---

## Step 4 — Sign the installer (removes SmartScreen warning)

### Understanding the two separate Windows security dialogs

There are two different warnings Windows can show — they have different causes
and different fixes:

**1. UAC prompt — the blue shield "Do you want to allow this app to make changes?"**
This installer uses `RequestExecutionLevel user` so it installs to
`%LocalAppData%` instead of Program Files. This means:
- No UAC prompt at all — players never see the blue shield
- Works on accounts that don't have administrator rights
- No administrator password required

**2. SmartScreen — "Windows protected your PC"**
This is a separate reputation system. Any new `.exe` that Microsoft hasn't seen
before will trigger this, regardless of what it does or where it installs.
It has nothing to do with UAC or admin rights.

To dismiss it manually (for testing): click "More info" → "Run anyway".

To eliminate it for players: sign the installer with a code signing certificate.

### How to sign (removes SmartScreen permanently)

Buy a certificate from Sectigo (~$70–100/yr) or DigiCert (~$200/yr), then:

```
signtool sign ^
  /fd SHA256 ^
  /tr http://timestamp.sectigo.com ^
  /td SHA256 ^
  /f YourCert.pfx ^
  /p YourCertPassword ^
  CGCLauncher_Setup.exe
```

**OV certificate (~$70/yr):** SmartScreen warning softens but may still appear
for new files until your installer builds reputation (a few hundred downloads).

**EV certificate (~$200/yr):** SmartScreen warning disappears immediately on
the very first download. Recommended if you're doing a public launch.

### Free alternative — Microsoft's reputation bypass

You can submit your app to Microsoft's Security Intelligence portal for manual
review: https://www.microsoft.com/en-us/wdsi/filesubmission

Slow (days to weeks), not guaranteed, but free. Good option while building up
natural reputation.

---

## What the installer does

- Checks Windows 10+ (blocks on older Windows)
- Checks for .NET 8 Desktop Runtime and opens Microsoft's download page if missing
- Detects an existing version and offers an in-place upgrade
- Installs to `%LocalAppData%\CGCLauncher\` (no admin rights needed)
- Creates Start Menu folder and shortcut
- Creates optional Desktop shortcut
- Registers in Settings → Apps (Add/Remove Programs) under HKCU
- Leaves `%AppData%\CGCLauncher\` untouched on uninstall (players keep their game data)

## Updating

Bump `APP_VERSION` in `CGCLauncher.nsi`, re-publish, recompile, re-sign.
The installer detects the old version and offers an in-place upgrade automatically.
