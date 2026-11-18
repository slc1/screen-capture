using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Capture;

/// <summary>
/// Runs one capture at a time. <c>openEditor</c> takes ownership of the bitmap it receives.
/// </summary>
public sealed class CaptureCoordinator
{
    private readonly AppServices _services;
    private readonly Action<SKBitmap> _openEditor;

    public CaptureCoordinator(AppServices services, Action<SKBitmap> openEditor)
    {
        _services = services;
        _openEditor = openEditor;
    }

    public void StartArea()
    {
        var lease = _services.Gate.TryAcquire();
        if (lease is null) return;
        new AreaSession(this, lease).Start();
    }

    public void StartFullScreen()
    {
        SKBitmap image;
        using (var lease = _services.Gate.TryAcquire())
        {
            if (lease is null) return;
            try
            {
                image = _services.Capturer.CaptureMonitorAtCursor().Image;
            }
            catch (Exception ex)
            {
                ReportCaptureFailed(ex);
                return;
            }
        }

        OpenEditor(image);
    }

    private void OpenEditor(SKBitmap image)
    {
        try
        {
            _openEditor(image);
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
    private sealed class AreaSession(CaptureCoordinator owner, CaptureLease lease)
    {
        private readonly List<ScreenSnapshot> _snapshots = [];
        private readonly List<OverlayWindow> _windows = [];
        private bool _ended;

        public void Start()
        {
            try
            {
                _snapshots.AddRange(owner._services.Capturer.CaptureAllMonitors());
                foreach (var snapshot in _snapshots)
                {
                    var window = new OverlayWindow(snapshot);
                    window.SelectionCompleted += OnSelectionCompleted;
                    window.CancelRequested += End;
                    window.CoverageFailed += OnCoverageFailed;
                    window.Closed += (_, _) => End();
                    _windows.Add(window);
                }

                foreach (var window in _windows)
                {
                    if (_ended) return;
                    window.Show();
                }

                if (!_ended) WindowUnderCursor()?.Activate();
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
            owner.OpenEditor(crop);
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
