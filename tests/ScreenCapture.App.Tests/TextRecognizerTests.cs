using System.Text;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Localization;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Tests;

[Collection("FileLog")]
public sealed class TextRecognizerTests : IDisposable
{
    private const string LogPath = @"C:\logs\app.log";
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ScreenCaptureAppTests", Guid.NewGuid().ToString("N"));
    private readonly FakeOcr _ocr = new();
    private readonly FakeTextClipboard _clipboard = new();
    private readonly List<string> _opened = new();
    private readonly List<(string Message, bool IsError)> _reports = new();
    private bool _openSucceeds = true;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private TextRecognizer Create() => new(
        _ocr,
        _clipboard,
        Loc.Instance,
        _folder,
        LogPath,
        path =>
        {
            _opened.Add(path);
            return _openSucceeds;
        },
        (message, isError) => _reports.Add((message, isError)),
        () => Now);

    private static SKBitmap Image() => new(30, 20);

    [Fact]
    public async Task Recognized_text_is_copied_written_as_utf8_and_opened()
    {
        _ocr.Result = _ => "Привіт\r\nworld";
        var image = Image();

        var outcome = await Create().RecognizeAsync(image);

        Assert.Equal(RecognitionOutcome.Done, outcome);
        Assert.Equal(["Привіт\r\nworld"], _clipboard.Texts);
        var path = Assert.Single(_opened);
        Assert.Equal(Path.Combine(_folder, "Text 2026-11-20 10-00-00.txt"), path);
        Assert.Equal("Привіт\r\nworld", File.ReadAllText(path, Encoding.UTF8));
        Assert.Empty(_reports);
        Assert.Equal([(30, 20)], _ocr.Calls);
        Assert.Equal(IntPtr.Zero, image.Handle);
    }

    [Fact]
    public async Task No_text_shows_an_info_message_and_outputs_nothing()
    {
        _ocr.Result = _ => "";

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.NoText, outcome);
        Assert.Equal([(Loc.Instance["Ocr_NoText"], false)], _reports);
        Assert.Empty(_clipboard.Texts);
        Assert.Empty(_opened);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public async Task Missing_vc_runtime_shows_the_install_message()
    {
        _ocr.Result = _ => throw new OcrUnavailableException(OcrUnavailableReason.RuntimeMissing, "msvcp140.dll");

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.RuntimeMissing, outcome);
        Assert.Equal([(Loc.Instance["Error_OcrRuntimeMissing"], true)], _reports);
    }

    [Fact]
    public async Task Engine_failure_points_to_the_log()
    {
        _ocr.Result = _ => throw new InvalidOperationException("boom");

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.Failed, outcome);
        Assert.Equal([(Loc.Instance.Format("Error_OcrFailed", LogPath), true)], _reports);
    }

    [Fact]
    public async Task Clipboard_failure_still_opens_the_file()
    {
        _clipboard.Succeeds = false;

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.CopyFailed, outcome);
        Assert.Single(_opened);
        Assert.Equal([(Loc.Instance["Error_OcrCopyFailed"], true)], _reports);
    }

    [Fact]
    public async Task Open_failure_keeps_the_clipboard_copy_and_names_the_file()
    {
        _openSucceeds = false;

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.OpenFailed, outcome);
        Assert.Single(_clipboard.Texts);
        Assert.Equal([(Loc.Instance.Format("Error_OcrOpenFailed", _opened[0]), true)], _reports);
    }

    [Fact]
    public async Task Both_outputs_failing_points_to_the_log()
    {
        _openSucceeds = false;
        _clipboard.Succeeds = false;

        var outcome = await Create().RecognizeAsync(Image());

        Assert.Equal(RecognitionOutcome.OutputFailed, outcome);
        Assert.Equal([(Loc.Instance.Format("Error_OcrOutputFailed", LogPath), true)], _reports);
    }

    [Fact]
    public async Task A_request_while_busy_is_dropped_and_its_image_released()
    {
        var gate = new TaskCompletionSource();
        _ocr.Result = _ =>
        {
            gate.Task.Wait();
            return "text";
        };
        var recognizer = Create();
        var busyChanges = new List<bool>();
        recognizer.BusyChanged += (_, _) => busyChanges.Add(recognizer.IsBusy);

        var first = recognizer.RecognizeAsync(Image());
        var second = Image();
        var dropped = await recognizer.RecognizeAsync(second);
        gate.SetResult();
        var done = await first;

        Assert.Equal(RecognitionOutcome.Busy, dropped);
        Assert.Equal(RecognitionOutcome.Done, done);
        Assert.Equal(IntPtr.Zero, second.Handle);
        Assert.Equal([true, false], busyChanges);
        Assert.False(recognizer.IsBusy);
    }

    [Fact]
    public async Task Old_text_files_are_cleaned_up()
    {
        Directory.CreateDirectory(_folder);
        var old = Path.Combine(_folder, "Text 2026-11-18 10-00-00.txt");
        File.WriteAllText(old, "old");
        File.SetLastWriteTime(old, Now.AddDays(-2));

        await Create().RecognizeAsync(Image());

        Assert.False(File.Exists(old));
    }
}
