namespace ScreenCapture.Core.Capture;

public sealed class CaptureGate
{
    private int _busy;

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public bool TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _busy, 0);
}
