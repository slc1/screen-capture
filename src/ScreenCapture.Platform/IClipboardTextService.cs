namespace ScreenCapture.Platform;

public interface IClipboardTextService
{
    /// <summary>Replaces the clipboard contents with Unicode text.</summary>
    bool TrySetText(string text);

    /// <summary>Why the last <see cref="TrySetText"/> returned false; null after a success.</summary>
    string? LastError { get; }
}
