using Microsoft.Win32;
using ScreenCapture.Platform.Windows;

namespace ScreenCapture.Platform.Windows.Tests;

public class RegistryAutostartServiceTests
{
    [Fact]
    public void Autostart_EnableDisable_RoundTrips()
    {
        var name = "ScreenCapture.Tests." + Guid.NewGuid();
        var svc = new RegistryAutostartService(name, @"C:\Temp\App.exe");
        try
        {
            Assert.False(svc.IsEnabled());
            svc.SetEnabled(true);
            Assert.True(svc.IsEnabled());
            Assert.False(new RegistryAutostartService(name, @"C:\Other\App.exe").IsEnabled());
            svc.SetEnabled(false);
            Assert.False(svc.IsEnabled());
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue(name, false);
        }
    }
}
