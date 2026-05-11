# Project Context

This is a Windows desktop application that:
- Downloads high-resolution space images from the NASA Image and Video Library API
- Filters images based on resolution (minimum 2560x1440) and aspect ratio (1.2-2.25)
- Adds text overlays with image title and description
- Sets them as the user's desktop wallpaper
- Supports automatic wallpaper changes on startup

## Platform
- Windows desktop application (WinForms)
- Written in C# (.NET 10)
- Uses System.Drawing for image manipulation
- Uses Windows Registry for auto-start functionality
- Interacts with Win32 APIs to set wallpaper

## Architecture
The application follows a service-based architecture with clear separation of concerns:

- **NasaImageService**: Handles NASA API queries, image filtering, and downloading. Filters out non-photographic content (illustrations, diagrams, composites). Uses HttpClient with base address "https://images-api.nasa.gov/"
- **WallpaperService**: Creates captioned wallpapers by overlaying title/description text on images. Calculates image positioning based on screen resolution and selected style (Fill/Fit/Stretch)
- **AutoStartService**: Manages Windows startup registry entries
- **AppSettingsStore**: Persists user preferences (topic, style, auto-start)
- **Form1**: Main UI with topic selector, style dropdown, preview, and apply functionality

## Key Features
- Supports multiple search topics (nebula, galaxy, planets, etc.) with fallback queries
- Three wallpaper styles: Fill, Fit, and Stretch
- Auto-start mode with `--startup` command-line argument
- Daily auto-apply when launched via startup
- Image caching in LocalApplicationData folder
- High-quality text rendering with shadows and semi-transparent backgrounds
- Links to original NASA source pages

## Technical Details
- Minimum image requirements: 2560x1440, landscape orientation, aspect ratio 1.2-2.25
- Downloads original quality images (~orig.jpg preferred)
- Stores downloaded images in `%LocalAppData%\SpaceWallpaperApp\Wallpapers`
- Stores generated wallpapers in `%LocalAppData%\SpaceWallpaperApp\Generated`
- Uses JSON serialization for NASA API responses
- Handles taskbar positioning to avoid overlapping text captions

## Coding Preferences
- Use async/await for all I/O operations (API calls, file operations)
- Prefer sealed classes and records where appropriate
- Use clear, descriptive names for methods and variables
- Use built-in .NET libraries (System.Drawing, System.Net.Http.Json)
- Handle API errors and edge cases gracefully
- Use string interpolation and modern C# features
- Avoid unnecessary abstractions

## Error Handling
- Network code handles API failures with null checks and fallback queries
- Gracefully degrades when suitable images aren't found
- Validates image dimensions before accepting candidates
- Uses CancellationToken for async operations