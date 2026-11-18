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
}
