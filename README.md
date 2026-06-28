# CGCLauncher

A custom Minecraft launcher built with WPF (.NET 8). This milestone implements the
**Version Manager**: fetching the official version list, downloading a version's
client jar / libraries / natives / assets, and launching it.

## Scope & honesty note

This talks only to Mojang's public, unauthenticated metadata/CDN endpoints (the same
ones the official launcher and tools like Prism Launcher / MultiMC use) and launches
the game exactly as the vanilla client expects. It does not fake, spoof, or bypass any
ownership/auth check. Until Microsoft OAuth is added (next milestone), launches use a
locally-generated "offline" identity, so the game will run in vanilla Demo Mode -
this is the game's own normal behavior for unauthenticated sessions, not something
this code works around.

## Requirements to build/run

- Windows (WPF requires the Windows Desktop runtime)
- .NET 8 SDK
- A Java runtime on PATH (or pass a custom path to `GameLauncher.LaunchGame`)

## Build & run

```
dotnet build
dotnet run
```

## Project layout

```
CGCLauncher/
├── Models/          Version manifest, version details, libraries, rules, asset index
├── Services/
│   ├── MojangApiService.cs   Fetches manifest + per-version JSON
│   ├── DownloadService.cs    Downloads client jar, libraries, natives, assets
│   └── GameLauncher.cs       Builds JVM/game args and starts the process
├── Utils/
│   ├── OsUtils.cs            OS/arch detection + rule evaluation
│   └── UuidUtils.cs          Offline-mode UUID generation
├── Styles/
│   └── Theme.xaml            Colors + control styles for the gem/mining theme
├── MainWindow.xaml(.cs)      Custom-chrome window: nav rail, launch button, console, status banner
└── App.xaml(.cs)
```

## Theme

`Styles/Theme.xaml` holds the palette (pulled from the server icon) and every control
style: pill-shaped combo box/text box, the nav rail buttons, and the faceted gem launch
button. The window uses custom chrome (`WindowStyle="None"` + `WindowChrome`) so the
title bar can carry the wordmark instead of the default OS frame.

Two things worth knowing if you want to push the look further:
- The wordmark currently uses bold Consolas with manual letter spacing (literal spaces
  in the string) rather than a real pixel font, to avoid bundling a font file. Swapping
  in something like Press Start 2P just means adding the `.ttf` as a Resource in the
  `.csproj` and referencing it via a pack URI in the `FontFamily`.
  

## Roadmap (per the original architecture)

- [x] Version Manager (this milestone)
- [ ] Microsoft OAuth — device-code or auth-code flow, token refresh, replaces the
      offline placeholder token so real online-mode sessions work
- [ ] Forge/Fabric loader installation
- [ ] Mod manager (enable/disable, folder profiles)
- [ ] Config generator (`servers.dat`, `options.txt`)
- [ ] Self-update from your own CDN/GitHub releases

## Notes

- Build wasn't verified in this sandbox (Linux container, no WPF/Windows toolchain
  available here) — double-check it compiles cleanly with `dotnet build` on your
  Windows machine and let me know if anything needs fixing.
- Network calls hit `launchermeta.mojang.com` and `resources.download.minecraft.net`
  for manifest/version data and assets respectively.
