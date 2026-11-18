using System.Runtime.InteropServices;

namespace ScreenCapture.App.Common;

internal static class NativeMenuFade
{
    private const uint SPI_GETMENUFADE = 0x1012;
    private const uint SPI_GETSELECTIONFADE = 0x1014;

    /// <summary>True when a closed native menu (or its clicked item) keeps fading out on screen for a while.</summary>
    public static bool IsEnabled() => Query(SPI_GETSELECTIONFADE) || Query(SPI_GETMENUFADE);

    private static bool Query(uint action)
    {
        try
        {
            return SystemParametersInfoW(action, 0, out var value, 0) && value != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint param, out int value, uint winIni);
}
