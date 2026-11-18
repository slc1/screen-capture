using System.Runtime.InteropServices;

namespace ScreenCapture.App.Common;

internal static class NativeDwm
{
    /// <summary>Blocks until the next DWM composition pass (one frame); returns false if DWM refused.</summary>
    public static bool Flush() => DwmFlush() >= 0;

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
