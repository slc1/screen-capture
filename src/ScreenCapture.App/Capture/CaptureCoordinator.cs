using Avalonia.Controls;
using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Capture;

/// <summary>
/// Runs one capture at a time. <c>openEditor</c> takes ownership of the bitmap it receives and returns the
/// shown editor window; <c>recognize</c> takes ownership of the selection it is given for text recognition.
/// </summary>
public sealed class CaptureCoordinator
{
    private readonly AppServices _services;
    private readonly Func<SKBitmap, TopLevel> _openEditor;
    private readonly Action<SKBitmap> _recognize;

    public CaptureCoordinator(AppServices services, Func<SKBitmap, TopLevel> openEditor, Action<SKBitmap> recognize)
    {
        _services = services;
        _openEditor = openEditor;
        _recognize = recognize;
    }

    public void StartArea(CaptureTiming timing)
    {
        var lease = _services.Gate.TryAcquire();
        if (lease is null) return;
        new AreaSession(this, lease, timing, crop => OpenEditor(crop, null)).Start();
    }

    /// <summary>The same frozen selection as <see cref="StartArea"/>, but the selection is read as text.</summary>
    public void StartRecognizeArea(CaptureTiming timing)
    {
        var lease = _services.Gate.TryAcquire();
        if (lease is null) return;
        new AreaSession(this, lease, timing, Recognize).Start();
    }

    private void Recognize(SKBitmap selection)
    {
        try
        {
            _recognize(selection);
        }
        catch (Exception ex)
        {
            selection.Dispose();
            FileLog.Error("Could not start text recognition", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_Unexpected", Program.LogPath));
        }
    }

    public void StartFullScreen(CaptureTiming timing)
    {
        SKBitmap image;
        using (var lease = _services.Gate.TryAcquire())
        {
            if (lease is null) return;
            try
            {
                image = _services.Capturer.CaptureMonitorAtCursor().Image;
                timing.Mark("captured");
            }
            catch (Exception ex)
            {
                ReportCaptureFailed(ex);
                return;
            }
        }

        OpenEditor(image, timing);
    }

    private void OpenEditor(SKBitmap image, CaptureTiming? timing)
    {
        try
        {
            var editor = _openEditor(image);
            if (timing is not null)
            {
                timing.Mark("editor shown");
                editor.RequestAnimationFrame(_ => timing.Complete("editor first frame"));
            }
        }
        catch (Exception ex)
        {
            image.Dispose();
            FileLog.Error("Could not open the editor", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_Unexpected", Program.LogPath));
        }
    }

    private void ReportCaptureFailed(Exception ex)
    {
        FileLog.Error("Capture failed", ex);
        _services.Notifier.ShowError(_services.Loc["Error_CaptureFailed"]);
    }

    /// <summary>Owns the snapshots, the overlays and the gate lease until the first end path wins.</summary>
    private sealed class AreaSession(CaptureCoordinator owner, CaptureLease lease, CaptureTiming timing, Action<SKBitmap> deliver)
    {
        private readonly List<ScreenSnapshot> _snapshots = [];
        private readonly List<OverlayWindow> _windows = [];
        private bool _ended;

        public void Start()
        {
            try
            {
                _snapshots.AddRange(owner._services.Capturer.CaptureAllMonitors());
                timing.Mark("captured");
                foreach (var snapshot in _snapshots)
                {
                    var window = new OverlayWindow(snapshot);
                    window.Opened += (_, _) =>
                    {
                        timing.Mark("overlay opened");
                        window.RequestAnimationFrame(_ => timing.Complete("overlay first frame"));
                    };
                    window.SelectionCompleted += OnSelectionCompleted;
                    window.CancelRequested += End;
                    window.CoverageFailed += OnCoverageFailed;
                    window.Closed += (_, _) => End();
                    _windows.Add(window);
                }

                timing.Mark("overlays created");

                foreach (var window in _windows)
                {
                    if (_ended) return;
                    window.Show();
                }

                timing.Mark("overlays shown");
                if (!_ended && WindowUnderCursor() is { } active) NativeForeground.Bring(active);
            }
            catch (Exception ex)
            {
                End();
                owner.ReportCaptureFailed(ex);
            }
        }

        private void OnCoverageFailed(string details)
        {
            if (_ended) return;
            End();
            owner.ReportCaptureFailed(new InvalidOperationException(details));
        }

        private OverlayWindow? WindowUnderCursor()
        {
            var cursor = NativeCursor.Position();
            var hit = cursor is { } p
                ? _windows.Find(w => w.Snapshot!.Monitor.BoundsPx.Contains(p.X, p.Y))
                : null;
            return hit ?? _windows.FirstOrDefault();
        }

        private void OnSelectionCompleted(OverlayWindow window, SKRectI rect)
        {
            if (_ended) return;
            SKBitmap crop;
            try
            {
                crop = Crop(window.Snapshot!.Image, rect);
            }
            catch (Exception ex)
            {
                End();
                owner.ReportCaptureFailed(ex);
                return;
            }

            End();
            deliver(crop);
        }

        private static SKBitmap Crop(SKBitmap source, SKRectI rect)
        {
            using var subset = new SKBitmap();
            if (!source.ExtractSubset(subset, rect))
                throw new InvalidOperationException($"Could not extract {rect} from a {source.Width}x{source.Height} image.");
            return subset.Copy() ?? throw new InvalidOperationException("Could not copy the selection.");
        }

        private void End()
        {
            if (_ended) return;
            _ended = true;
            try
            {
                foreach (var window in _windows)
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        FileLog.Error("Could not close an overlay", ex);
                    }
                }
            }
            finally
            {
                // A window that never opened gets no OnClosed, so its frozen bitmap is released here.
                foreach (var window in _windows) window.ReleaseFrozen();
                foreach (var snapshot in _snapshots) snapshot.Image.Dispose();
                lease.Dispose();
            }
        }
    }
}
