using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using SkiaSharp;
using static ScreenCapture.Platform.Windows.NativeMethods;

namespace ScreenCapture.Platform.Windows;

/// <summary>
/// Captures monitors at physical pixels with one BitBlt per monitor. The process must be
/// Per-Monitor-V2 DPI aware, otherwise coordinates are virtualized.
/// </summary>
public sealed unsafe class GdiScreenCapturer : IScreenCapturer
{
    public IReadOnlyList<MonitorInfo> GetMonitors() => EnumerateMonitors().Select(e => e.Info).ToList();

    public IReadOnlyList<ScreenSnapshot> CaptureAllMonitors()
    {
        var result = new List<ScreenSnapshot>();
        try
        {
            foreach (var (info, _) in EnumerateMonitors())
                result.Add(new ScreenSnapshot(info, Capture(info.BoundsPx)));
            return result;
        }
        catch
        {
            foreach (var s in result) s.Image.Dispose();
            throw;
        }
    }

    public ScreenSnapshot CaptureMonitorAtCursor()
    {
        if (!GetCursorPos(out var pt))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        var monitors = EnumerateMonitors();
        var index = monitors.FindIndex(m => m.Handle == hMon);
        if (index < 0) index = Math.Max(0, monitors.FindIndex(m => m.Info.IsPrimary));
        var info = monitors[index].Info;
        return new ScreenSnapshot(info, Capture(info.BoundsPx));
    }

    private static List<(MonitorInfo Info, nint Handle)> EnumerateMonitors()
    {
        var list = new List<(MonitorInfo Info, nint Handle)>();
        MonitorEnumProc proc = (hMonitor, _, _, _) =>
        {
            var mi = new MONITORINFOEXW { cbSize = (uint)sizeof(MONITORINFOEXW) };
            if (!GetMonitorInfoW(hMonitor, ref mi)) return 1;
            var r = mi.rcMonitor;
            double scale = 1.0;
            if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                scale = dpiX / 96.0;
            var name = new string(mi.szDevice).TrimEnd('\0');
            list.Add((new MonitorInfo(name, new SKRectI(r.Left, r.Top, r.Right, r.Bottom), scale,
                (mi.dwFlags & MONITORINFOF_PRIMARY) != 0), hMonitor));
            return 1;
        };
        if (!EnumDisplayMonitors(0, 0, proc, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        GC.KeepAlive(proc);
        if (list.Count == 0) throw new InvalidOperationException("No monitors found.");
        return list;
    }

    /// <summary>
    /// BitBlt from the screen leaves the alpha byte undefined (usually 0). The Opaque tag alone is not enough:
    /// Skia still reads that byte when drawing, so Flatten and the blur would turn the image transparent.
    /// </summary>
    internal static void SetOpaque(ReadOnlySpan<uint> src, Span<uint> dst)
    {
        var mask = new Vector<uint>(0xFF000000u);
        var i = 0;
        for (; i <= src.Length - Vector<uint>.Count; i += Vector<uint>.Count)
            (new Vector<uint>(src[i..]) | mask).CopyTo(dst[i..]);
        for (; i < src.Length; i++) dst[i] = src[i] | 0xFF000000u;
    }

    private static SKBitmap Capture(SKRectI bounds)
    {
        int w = bounds.Width, h = bounds.Height;
        nint screenDc = 0, memDc = 0, dib = 0, oldObj = 0;
        try
        {
            screenDc = GetDC(0);
            if (screenDc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            memDc = CreateCompatibleDC(screenDc);
            if (memDc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = w,
                biHeight = -h, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };
            dib = CreateDIBSection(screenDc, bmi, DIB_RGB_COLORS, out var bits, 0, 0);
            if (dib == 0 || bits == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

            oldObj = SelectObject(memDc, dib);
            if (!BitBlt(memDc, 0, 0, w, h, screenDc, bounds.Left, bounds.Top, SRCCOPY | CAPTUREBLT))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque));
            try
            {
                var count = w * h;
                var src = new ReadOnlySpan<uint>((void*)bits, count);
                var dst = new Span<uint>((void*)bitmap.GetPixels(), count);
                SetOpaque(src, dst);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        finally
        {
            if (oldObj != 0) SelectObject(memDc, oldObj);
            if (dib != 0) DeleteObject(dib);
            if (memDc != 0) DeleteDC(memDc);
            if (screenDc != 0) ReleaseDC(0, screenDc);
        }
    }
}
