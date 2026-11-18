using System.Diagnostics;
using ScreenCapture.Core.Diagnostics;

namespace ScreenCapture.App.Capture;

/// <summary>
/// Elapsed time of one capture from its trigger, written as a single log line when the session completes
/// so that logging does not add file I/O between the trigger and the overlay.
/// </summary>
public sealed class CaptureTiming
{
    private readonly Func<TimeSpan> _elapsed;
    private readonly List<(string Stage, TimeSpan At)> _stages = [];
    private bool _completed;

    public CaptureTiming(string name)
    {
        Name = name;
        var start = Stopwatch.GetTimestamp();
        _elapsed = () => Stopwatch.GetElapsedTime(start);
    }

    public CaptureTiming(string name, Func<TimeSpan> elapsed)
    {
        Name = name;
        _elapsed = elapsed;
    }

    public string Name { get; }

    /// <summary>Records the first occurrence of <paramref name="stage"/>; later ones are ignored.</summary>
    public void Mark(string stage)
    {
        if (_completed || _stages.Exists(s => s.Stage == stage)) return;
        _stages.Add((stage, _elapsed()));
    }

    /// <summary>Marks the final stage and logs the summary once.</summary>
    public void Complete(string stage)
    {
        if (_completed) return;
        Mark(stage);
        _completed = true;
        FileLog.Info(Summary);
    }

    public string Summary =>
        $"Capture timing ({Name}): " + string.Join(", ", _stages.Select(s => $"{s.Stage} {s.At.TotalMilliseconds:F0} ms"));
}
