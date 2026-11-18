using Microsoft.Win32;

namespace ScreenCapture.Platform.Windows;

public sealed class RegistryAutostartService(string valueName, string executablePath) : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (key?.GetValue(valueName) is not string value) return false;
        return string.Equals(value.Trim().Trim('"'), executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(valueName, "\"" + executablePath + "\"", RegistryValueKind.String);
        else key.DeleteValue(valueName, false);
    }
}
