namespace SpaceWallpaperApp;

public sealed record NasaImageCandidate(
    string Title,
    string Description,
    int Width,
    int Height,
    string LocalPath,
    string NasaDetailsUrl);
