using System.Text;

namespace ScreenCapture.Platform.Windows;

public sealed class Win32ClipboardTextService : IClipboardTextService
{
    private const uint CF_UNICODETEXT = 13;

    public string? LastError { get; private set; }

    public bool TrySetText(string text)
    {
        LastError = null;
        try
        {
            var data = Encoding.Unicode.GetBytes(text + "\0");
            LastError = Win32Clipboard.Replace((CF_UNICODETEXT, data, "CF_UNICODETEXT"));
            return LastError is null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }
}
