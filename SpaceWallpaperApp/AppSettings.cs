namespace SpaceWallpaperApp;

public sealed class AppSettings
{
    public string SelectedStyle { get; set; } = WallpaperStyle.Fit.ToString();

    public bool AutoStartEnabled { get; set; }

    public DateOnly? LastAutoAppliedDate { get; set; }

    public bool HasMigratedToFitDefault { get; set; }
}
