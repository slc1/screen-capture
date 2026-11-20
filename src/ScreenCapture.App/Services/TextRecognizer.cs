using System.ComponentModel;
using System.Diagnostics;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Localization;
using ScreenCapture.Core.Ocr;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Services;

public enum RecognitionOutcome { Busy, RuntimeMissing, Failed, NoText, Done, CopyFailed, OpenFailed, OutputFailed }

/// <summary>
/// Recognizes text off the UI thread, then puts it on the clipboard and opens it as a temporary .txt file.
/// One recognition at a time: a request while busy is dropped. Call from the UI thread.
/// </summary>
public sealed class TextRecognizer
{
    private readonly IOcrService _ocr;
    private readonly IClipboardTextService _clipboard;
    private readonly Loc _loc;
    private readonly string _folder;
    private readonly string _logPath;
    private readonly Func<string, bool> _openFile;
    private readonly Action<string, bool> _report;
    private readonly Func<DateTime> _now;

    /// <param name="report">Shows a message to the user; the flag is true for an error.</param>
    public TextRecognizer(
        IOcrService ocr,
        IClipboardTextService clipboard,
        Loc loc,
        string folder,
        string logPath,
        Func<string, bool> openFile,
        Action<string, bool> report,
        Func<DateTime>? now = null)
    {
        _ocr = ocr;
        _clipboard = clipboard;
        _loc = loc;
        _folder = folder;
        _logPath = logPath;
        _openFile = openFile;
        _report = report;
        _now = now ?? (() => DateTime.Now);
    }

    public static string DefaultFolder => Path.Combine(Path.GetTempPath(), "ScreenCapture", "ocr");

    public static TextRecognizer Create(AppServices services) => new(
        services.Ocr,
        services.TextClipboard,
        services.Loc,
        DefaultFolder,
        Program.LogPath,
        OpenWithDefaultApp,
        (message, isError) =>
        {
            if (isError) services.Notifier.ShowError(message);
            else services.Notifier.ShowInfo(message);
        });

    public bool IsBusy { get; private set; }

    public event EventHandler? BusyChanged;

    /// <summary>Takes ownership of <paramref name="image"/>.</summary>
    public async Task<RecognitionOutcome> RecognizeAsync(SKBitmap image)
    {
        if (IsBusy)
        {
            image.Dispose();
            _report(_loc["Ocr_Busy"], false);
            return RecognitionOutcome.Busy;
        }

        SetBusy(true);
        try
        {
            string text;
            var watch = Stopwatch.StartNew();
            try
            {
                text = await Task.Run(() => _ocr.Recognize(image));
                FileLog.Info($"Text recognized in {watch.ElapsedMilliseconds} ms: {text.Length} chars from {image.Width}x{image.Height}");
            }
            catch (OcrUnavailableException ex) when (ex.Reason == OcrUnavailableReason.RuntimeMissing)
            {
                FileLog.Error("Text recognition unavailable", ex);
                _report(_loc["Error_OcrRuntimeMissing"], true);
                return RecognitionOutcome.RuntimeMissing;
            }
            catch (Exception ex)
            {
                FileLog.Error("Text recognition failed", ex);
                _report(_loc.Format("Error_OcrFailed", _logPath), true);
                return RecognitionOutcome.Failed;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _report(_loc["Ocr_NoText"], false);
                return RecognitionOutcome.NoText;
            }

            return Deliver(text);
        }
        finally
        {
            image.Dispose();
            SetBusy(false);
        }
    }

    private RecognitionOutcome Deliver(string text)
    {
        var copied = _clipboard.TrySetText(text);
        if (!copied) FileLog.Error($"Copying recognized text failed: {_clipboard.LastError ?? "unknown cause"}");

        string? path = null;
        try
        {
            OcrTextFiles.DeleteOlderThan(_folder, _now(), OcrTextFiles.KeepFor);
            path = OcrTextFiles.Write(_folder, _now(), text);
        }
        catch (Exception ex)
        {
            FileLog.Error("Writing recognized text failed", ex);
        }

        var opened = path is not null && _openFile(path);
        switch (copied, opened)
        {
            case (true, true):
                return RecognitionOutcome.Done;
            case (false, true):
                _report(_loc["Error_OcrCopyFailed"], true);
                return RecognitionOutcome.CopyFailed;
            case (true, false):
                _report(_loc.Format("Error_OcrOpenFailed", path ?? _folder), true);
                return RecognitionOutcome.OpenFailed;
            default:
                _report(_loc.Format("Error_OcrOutputFailed", _logPath), true);
                return RecognitionOutcome.OutputFailed;
        }
    }

    private void SetBusy(bool value)
    {
        IsBusy = value;
        BusyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool OpenWithDefaultApp(string path)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            FileLog.Error($"Could not open '{path}'", ex);
            return false;
        }
    }
}
