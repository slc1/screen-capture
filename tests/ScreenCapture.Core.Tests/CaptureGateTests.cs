using ScreenCapture.Core.Capture;

namespace ScreenCapture.Core.Tests;

public class CaptureGateTests
{
    [Fact]
    public void SecondEnter_IsRejectedUntilExit()
    {
        var gate = new CaptureGate();
        Assert.True(gate.TryEnter());
        Assert.True(gate.IsBusy);
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.False(gate.IsBusy);
        Assert.True(gate.TryEnter());
    }

    [Fact]
    public void TryAcquire_WhileHeld_ReturnsNull()
    {
        var gate = new CaptureGate();
        using var lease = gate.TryAcquire();
        Assert.NotNull(lease);
        Assert.Null(gate.TryAcquire());
    }

    [Fact]
    public void LeaseDispose_ReleasesGate()
    {
        var gate = new CaptureGate();
        gate.TryAcquire()!.Dispose();
        Assert.False(gate.IsBusy);
    }

    [Fact]
    public void LeaseDisposedTwice_DoesNotReleaseLaterCapture()
    {
        var gate = new CaptureGate();
        var first = gate.TryAcquire()!;
        first.Dispose();
        var second = gate.TryAcquire();
        Assert.NotNull(second);

        first.Dispose();

        Assert.True(gate.IsBusy);
        Assert.Null(gate.TryAcquire());
        second!.Dispose();
        Assert.False(gate.IsBusy);
    }
}
