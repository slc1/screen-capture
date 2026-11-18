using ScreenCapture.Core.Export;

namespace ScreenCapture.Core.Tests;

public class SaveTargetTests
{
    [Theory]
    [InlineData(@"C:\a\shot.png", ImageFormatKind.Jpg, @"C:\a\shot.png", ImageFormatKind.Png)]
    [InlineData(@"C:\a\shot.JPG", ImageFormatKind.Png, @"C:\a\shot.JPG", ImageFormatKind.Jpg)]
    [InlineData(@"C:\a\shot.jpeg", ImageFormatKind.Png, @"C:\a\shot.jpeg", ImageFormatKind.Jpg)]
    [InlineData(@"C:\a\shot", ImageFormatKind.Png, @"C:\a\shot.png", ImageFormatKind.Png)]
    [InlineData(@"C:\a\shot", ImageFormatKind.Jpg, @"C:\a\shot.jpg", ImageFormatKind.Jpg)]
    [InlineData(@"C:\a\shot 12.30", ImageFormatKind.Jpg, @"C:\a\shot 12.30.jpg", ImageFormatKind.Jpg)]
    [InlineData(@"C:\a\shot.bmp", ImageFormatKind.Png, @"C:\a\shot.bmp.png", ImageFormatKind.Png)]
    public void Resolve_PicksFormatAndAppendsMissingExtension(string picked, ImageFormatKind def, string path, ImageFormatKind format)
    {
        var r = SaveTarget.Resolve(picked, def);
        Assert.Equal(path, r.Path);
        Assert.Equal(format, r.Format);
    }

    [Fact]
    public void EnsureFolder_CreatesMissingFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-test-" + Guid.NewGuid().ToString("N"), "nested");
        try
        {
            Assert.Equal(dir, SaveTarget.EnsureFolder(dir, "fallback"));
            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureFolder_FallsBackWhenFolderIsBlank(string configured)
    {
        Assert.Equal("fallback", SaveTarget.EnsureFolder(configured, "fallback"));
    }

    [Fact]
    public void EnsureFolder_FallsBackWhenFolderCannotBeCreated()
    {
        var file = Path.GetTempFileName();
        try
        {
            // A folder cannot be created under an existing file.
            Assert.Equal("fallback", SaveTarget.EnsureFolder(Path.Combine(file, "sub"), "fallback"));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
