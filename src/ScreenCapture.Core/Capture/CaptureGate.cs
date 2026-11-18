namespace ScreenCapture.Core.Capture;

public sealed class CaptureGate
{
    private int _busy;

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public bool TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _busy, 0);

    /// <summary>Enters the gate; disposing the lease exits it at most once.</summary>
    public CaptureLease? TryAcquire() => TryEnter() ? new CaptureLease(this) : null;
}

public sealed class CaptureLease : IDisposable
{
    private CaptureGate? _gate;

    internal CaptureLease(CaptureGate gate) => _gate = gate;

    public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Exit();
}
