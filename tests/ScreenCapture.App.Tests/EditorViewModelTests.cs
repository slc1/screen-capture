using ScreenCapture.App.Editor;
using ScreenCapture.Core.Editing;
using ScreenCapture.Core.Export;
using SkiaSharp;

namespace ScreenCapture.App.Tests;

public sealed class EditorViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private readonly SKBitmap _image = new(4, 4);

    public void Dispose()
    {
        _image.Dispose();
        _env.Dispose();
    }

    private EditorViewModel CreateViewModel() => new(new EditorSession(_image), _env.Services, new NullHost());

    [Fact]
    public void Starts_with_the_session_colour_selected()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.SelectedSwatch);
        Assert.Equal(vm.Session.Color, vm.SelectedSwatch!.Value);
        Assert.Single(vm.Swatches, s => s.IsSelected);
    }

    [Fact]
    public void Picking_a_colour_updates_selection_and_raises_ColorPicked()
    {
        var vm = CreateViewModel();
        var picked = 0;
        vm.ColorPicked += (_, _) => picked++;
        var target = vm.Swatches[^1];

        vm.SelectColorCommand.Execute(target);

        Assert.Same(target, vm.SelectedSwatch);
        Assert.Equal(target.Value, vm.Session.Color);
        Assert.True(target.IsSelected);
        Assert.Single(vm.Swatches, s => s.IsSelected);
        Assert.Equal(1, picked);
    }

    [Fact]
    public void Picking_the_current_colour_still_raises_ColorPicked()
    {
        var vm = CreateViewModel();
        var picked = 0;
        vm.ColorPicked += (_, _) => picked++;

        vm.SelectColorCommand.Execute(vm.SelectedSwatch);

        Assert.Equal(1, picked);
    }

    private sealed class NullHost : IEditorHost
    {
        public void CommitPendingText()
        {
        }

        public void CloseEditor()
        {
        }

        public Task<string?> PickSavePathAsync(string folder, string suggestedName, ImageFormatKind defaultFormat) =>
            Task.FromResult<string?>(null);

        public Task<int?> AskJpegQualityAsync(int initial) => Task.FromResult<int?>(null);
    }
}
