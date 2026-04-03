# Space Wallpaper App

Windows desktop app for automatically finding real NASA space imagery, previewing it, and setting it as your wallpaper in high resolution.

## What It Does

Space Wallpaper App searches NASA's public image library for real space photography, filters out low-quality or unsuitable results, and prepares wallpapers that look good on a 2K display. It can also add a small descriptive caption onto the final wallpaper and rotate to a new random image automatically when Windows starts.

## Features

- Fetches real NASA space images from the NASA Image and Video Library
- Targets wallpapers suitable for 2K displays
- Filters out many illustrations, collages, and poor wallpaper candidates
- Lets you preview the image before applying it
- Supports `Fill`, `Fit`, `Stretch`, `Center`, `Tile`, and `Span`
- Can place a short caption onto the wallpaper itself
- Can auto-run at Windows sign-in and set one random wallpaper per day

## Tech Stack

- .NET 10
- Windows Forms
- Windows desktop wallpaper API via `SystemParametersInfo`
- NASA Images API

## Run Locally

```powershell
$env:DOTNET_CLI_HOME='D:\programovani\BackedUpProjects\CodexSpaceImages'
dotnet run --project D:\programovani\BackedUpProjects\CodexSpaceImages\SpaceWallpaperApp\SpaceWallpaperApp.csproj
```

## Build

```powershell
$env:DOTNET_CLI_HOME='D:\programovani\BackedUpProjects\CodexSpaceImages'
dotnet build D:\programovani\BackedUpProjects\CodexSpaceImages\SpaceWallpaperApp\SpaceWallpaperApp.csproj
```

## Data And Generated Files

- Downloaded NASA images: `%LOCALAPPDATA%\SpaceWallpaperApp\Wallpapers`
- Generated wallpaper with caption: `%LOCALAPPDATA%\SpaceWallpaperApp\Generated`
- Saved app settings: `%LOCALAPPDATA%\SpaceWallpaperApp\settings.json`

## Notes

- Internet access is required when fetching new NASA images.
- Startup automation is registered through the current user's Windows `Run` key.
- The app keeps the original downloaded image and generates a separate final wallpaper file when adding caption text.

## Credit

This app was created from scratch for Tomas Mikulaj with the help of OpenAI Codex, which designed and implemented the application, wallpaper workflow, startup automation, caption rendering, and project structure.
