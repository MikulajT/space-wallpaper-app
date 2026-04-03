namespace SpaceWallpaperApp;

public sealed class AppSettings
{
    public string SelectedTopic { get; set; } = "Deep space";

    public string SelectedStyle { get; set; } = WallpaperStyle.Fill.ToString();

    public bool AutoStartEnabled { get; set; }

    public DateOnly? LastAutoAppliedDate { get; set; }
}
