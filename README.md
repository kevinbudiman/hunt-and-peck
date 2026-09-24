# hunt-n-peck
[![Build](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml/badge.svg)](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml)

Simple vimium/vimperator style navigation for Windows applications based on the UI Automation framework. In essence, it works the same as screen readers or accessibility programs but with the goal of making any Windows program faster to use.

It works for any Windows program (excluding Modern UI apps :))

NOTE: hunt-n-peck is sporadically maintained, please consider one of the various forks.

# Download

Every CI run publishes a self-contained `hap.exe` (win-x64, no .NET install required) as the `HuntAndPeck-win-x64` artifact on the [Actions page](https://github.com/kevinbudiman/hunt-and-peck/actions/workflows/build.yml).

The last upstream .NET Framework release (1.7) is still available at https://github.com/zsims/hunt-and-peck/releases/download/release%2F1.7/HuntAndPeck-1.7.zip

# Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). From `src/`:

```
dotnet build HuntAndPeck.slnx -c Release
dotnet test --solution HuntAndPeck.slnx -c Release
dotnet publish HuntAndPeck/HuntAndPeck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
```

Running the app and tests requires Windows; building works on any OS.

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
