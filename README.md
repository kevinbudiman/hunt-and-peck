# hunt-n-peck
[![Build](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml/badge.svg)](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml)

Simple vimium/vimperator style navigation for Windows applications based on the UI Automation framework. In essence, it works the same as screen readers or accessibility programs but with the goal of making any Windows program faster to use.

It works for any Windows program (excluding Modern UI apps :))

NOTE: hunt-n-peck is sporadically maintained, please consider one of the various forks.

# Download

Latest release: https://github.com/kevinbudiman/hunt-and-peck/releases/latest/download/HuntAndPeck.zip

Unzip anywhere and run `hap.exe`. Requires .NET Framework 4.8, which is included with Windows 10 (1903+) and Windows 11.

All releases are on the [Releases page](https://github.com/kevinbudiman/hunt-and-peck/releases). Every CI run also publishes the zip as the `HuntAndPeck` artifact on the [Actions page](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml) (kept for 90 days, sign-in required).

To cut a release, either:
- on GitHub: **Actions** → **Build** → **Run workflow**, keep the branch as `master`, enter a version (e.g. `1.8`) and click **Run workflow**; or
- push a `release/<version>` tag, e.g. `git tag release/1.8 && git push origin release/1.8`.

The last upstream .NET Framework release (1.7) is still available at https://github.com/zsims/hunt-and-peck/releases/download/release%2F1.7/HuntAndPeck-1.7.zip

# Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the app itself targets .NET Framework 4.8). From `src/`:

```
dotnet build HuntAndPeck.slnx -c Release
dotnet test --solution HuntAndPeck.slnx -c Release
```

The app is written to `HuntAndPeck/bin/Release/net48/`. Running the app and tests requires Windows; building works on any OS.

# How to change font size

Find the application icon in tray, click right mouse button, select `Options`, then use the `FontSize` menu to change the font size.

# Screenshots

![ScreenShot](https://raw.github.com/zsims/hunt-n-peck/master/screenshots/explorer.png)
![ScreenShot](https://raw.github.com/zsims/hunt-n-peck/master/screenshots/visual-studio.png)

## To use

1. Launch the executable.
2. With any window focused, press `Alt + ;`
    - The tray can be highlighted with `Ctrl + ;`
3. An overlay window will be displayed, type any of the hint characters you see.

Alternatively, Hunt and Peck can be launched via the command-line or AutoHotKey by specifying `/hint`:
```
hap.exe /hint
```

Or in tray mode with
```
hap.exe /tray
```

# Supported Elements
Only UI Automation elements with "Invoke" patterns are supported (and displayed).
