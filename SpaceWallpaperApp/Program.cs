namespace SpaceWallpaperApp;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var startupMode = args.Any(arg => string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));
        Application.Run(new Form1(startupMode));
    }
}
