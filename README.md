# CGCLauncher

A custom Minecraft launcher built with WPF (.NET 8) for the **CrystalGemCraft** server.

## Requirements

- Windows 10/11 (WPF requires the Windows Desktop runtime)
- .NET 8 SDK — https://dotnet.microsoft.com/download/dotnet/8.0
- Java 17+ on PATH (needed to actually run Minecraft, not to build the launcher)

## Build & run

```
dotnet restore
dotnet build
dotnet run
```

---

## Configuration — things you need to set before distributing

### 1. Your server IP

Open `MainWindow.xaml.cs` and find this block near the top of the class:

```csharp
// TODO: replace with your real server address
private static readonly DefaultServerEntry[] DefaultServers =
{
    new("Crystal GemCraft", "CHANGE_ME.example.net"),
};
```

Replace `"CHANGE_ME.example.net"` with your actual server address, for example:

```csharp
private static readonly DefaultServerEntry[] DefaultServers =
{
    new("Crystal GemCraft", "play.crystalgemcraft.net"),
};
```

This writes `servers.dat` on the player's first launch so your server appears
in the Minecraft multiplayer list automatically. The file is only written if it
doesn't already exist — returning players keep their own server list untouched.

---

## Project layout

```
CGCLauncher/
├── Models/
│   ├── AuthModels.cs         Xbox Live / XSTS / Minecraft auth response shapes
│   ├── MsaTokens.cs          Microsoft OAuth token response + stored session model
│   ├── LauncherVersion.cs    Unified version entry (vanilla or OptiFine) for the combo box
│   ├── OptiFineVersion.cs    OptiFine version entry and download URL builder
│   ├── VersionManifest.cs    Top-level Mojang version manifest
│   ├── VersionDetails.cs     Per-version JSON (downloads, libraries, args, assets)
│   ├── Rule.cs               Library/argument rule evaluation
│   └── AssetIndexFile.cs     Asset index (hash → download URL map)
│
├── Services/
│   ├── AuthService.cs        Microsoft OAuth → Xbox Live → XSTS → Minecraft auth chain
│   ├── LoginWindow.xaml(.cs) Embedded browser window for Microsoft sign-in
│   ├── MojangApiService.cs   Fetches version manifest + per-version JSON from Mojang
│   ├── DownloadService.cs    Downloads client jar, libraries, natives, assets
│   ├── GameLauncher.cs       Builds JVM/game args and launches Minecraft
│   ├── OptiFineService.cs    Scrapes optifine.net for available OptiFine versions
│   └── ServerListService.cs  Writes servers.dat on first launch
│
├── Utils/
│   ├── OsUtils.cs            OS/arch detection + Mojang rule evaluation
│   └── UuidUtils.cs          Offline-mode UUID generation (used when not signed in)
│
├── Styles/
│   └── Theme.xaml            Full pixel-art theme: stone brick, wood plank, gem button
│
├── Assets/
│   ├── logo.png              Server icon (used in title bar and as window icon)
│   ├── logo.ico              Multi-resolution .ico built from logo.png
│   └── VT323-Regular.ttf     Embedded pixel font (no installation required)
│
├── MainWindow.xaml(.cs)      Main window: version picker, launch button, console, auth banner
└── App.xaml(.cs)             Application entry point + theme dictionary merge
```

---

## Authentication

Sign-in uses Microsoft's own Xbox app client ID (`00000000402b5328`) — the same
approach used by Prism Launcher, ATLauncher, and GDLauncher. No Azure app
registration is required.

The auth chain:
```
Microsoft login (browser window)
  → MS access token
    → Xbox Live token
      → XSTS token
        → Minecraft access token
          → Profile fetch (username + UUID)
```

The refresh token is saved to `%AppData%\CGCLauncher\auth\session.json` so
players stay signed in between sessions. To force a fresh login, delete that file.

Players who don't want to sign in can type any name in the offline username
field — the game will launch in vanilla Demo Mode.

---

## Roadmap

- [x] Version Manager — downloads and launches any official Minecraft release
- [x] OptiFine support — OptiFine versions appear in the list when the checkbox is ticked
- [x] Server pre-load — `servers.dat` seeded with your server IP on first launch
- [x] Pixel-art UI — stone brick / wood plank theme matching the Crystal GemCraft aesthetic
- [x] Microsoft OAuth — full auth chain, refresh token persistence, sign-out
- [ ] Forge/Fabric loader installation
- [ ] Mod manager (enable/disable, folder profiles per version)
- [ ] Self-update from your own CDN/GitHub releases

---

## Notes

- Network calls: `launchermeta.mojang.com` (manifest), `resources.download.minecraft.net`
  (assets), `login.live.com` (auth), `user.auth.xboxlive.com` / `xsts.auth.xboxlive.com`
  (Xbox), `api.minecraftservices.com` (Minecraft token + profile).
- Build was not verified in this sandbox (Linux, no WPF toolchain) — run
  `dotnet build` on your Windows machine and report any errors.
