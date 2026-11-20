using System.Reflection;
using System.Runtime.InteropServices;
using ScreenCapture.Core.Ocr;
using SkiaSharp;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.InteropDotNet;

namespace ScreenCapture.Platform.Windows;

/// <summary>
/// Tesseract 5.5 with the tessdata_fast Ukrainian and English models. The native DLLs and models are embedded
/// resources, extracted under <see cref="Root"/> on first use so the app stays a single exe.
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    public const string Languages = "ukr+eng";

    private const string ResourcePrefix = "ocr/";

    /// <summary>What the native DLLs import beyond Windows itself (the VC++ 2015–2022 x64 redistributable).</summary>
    private static readonly string[] VcRuntime = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll"];

    private readonly object _lock = new();
    private bool _extracted;

    /// <param name="root">Folder that receives <c>x64\</c> and <c>tessdata\</c>; versioned so an upgrade never mixes files.</param>
    public TesseractOcrService(string root) => Root = root;

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenCapture", "ocr", "tesseract-5.5.2-fast");

    public string Root { get; }

    public string Recognize(SKBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        byte[] png;
        using (var prepared = OcrImagePrep.Prepare(image))
        using (var data = prepared.Encode(SKEncodedImageFormat.Png, 100))
            png = data?.ToArray() ?? throw new InvalidOperationException("Could not encode the image for text recognition.");

        lock (_lock)
        {
            using var engine = CreateEngine();
            using var pix = TesseractOCR.Pix.Image.LoadFromMemory(png);
            using var page = engine.Process(pix);
            return OcrText.Clean(page.Text);
        }
    }

    private Engine CreateEngine()
    {
        EnsureVcRuntime();
        EnsureExtracted();
        try
        {
            return new Engine(Path.Combine(Root, "tessdata"), Languages, EngineMode.LstmOnly);
        }
        catch (Exception ex)
        {
            // Re-check the files next time: a cleaner or antivirus may have removed one since extraction.
            _extracted = false;
            throw new OcrUnavailableException(OcrUnavailableReason.LoadFailed, $"Could not start Tesseract from '{Root}'.", ex);
        }
    }

    private static void EnsureVcRuntime()
    {
        foreach (var name in VcRuntime)
        {
            if (!NativeLibrary.TryLoad(name, out var handle))
                throw new OcrUnavailableException(OcrUnavailableReason.RuntimeMissing, $"{name} could not be loaded.");
            NativeLibrary.Free(handle);
        }
    }

    /// <summary>Writes each embedded file whose copy is missing or a different size (a torn earlier write).</summary>
    private void EnsureExtracted()
    {
        if (_extracted) return;
        try
        {
            var assembly = typeof(TesseractOcrService).Assembly;
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
                ExtractIfNeeded(assembly, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OcrUnavailableException(OcrUnavailableReason.LoadFailed, $"Could not prepare the OCR files in '{Root}'.", ex);
        }

        // The wrapper looks for <path>\x64\tesseract55.dll; the exe folder has no copy.
        LibraryLoader.Instance.CustomSearchPath = Root;
        _extracted = true;
    }

    private void ExtractIfNeeded(Assembly assembly, string resourceName)
    {
        using var source = assembly.GetManifestResourceStream(resourceName)
            ?? throw new IOException($"Missing embedded resource {resourceName}.");
        var target = Path.Combine(Root, resourceName[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
        var existing = new FileInfo(target);
        if (existing.Exists && existing.Length == source.Length) return;

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = File.Create(temp))
                source.CopyTo(output);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
