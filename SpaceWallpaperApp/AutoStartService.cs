using Microsoft.Win32;

namespace SpaceWallpaperApp;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpaceWallpaperApp";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return runKey?.GetValue(ValueName) is string existingValue
            && existingValue.Contains("--startup", StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the app executable path.");

        var command = $"\"{executablePath}\" --startup";

        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        runKey.SetValue(ValueName, command);
    }

    public void Disable()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (runKey.GetValue(ValueName) is not null)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
