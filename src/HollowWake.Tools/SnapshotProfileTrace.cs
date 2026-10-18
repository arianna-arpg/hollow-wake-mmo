using System.Diagnostics.Tracing;

namespace HollowWake.Tools;

/// <summary>Tools-only trace correlation. No simulation data, actor names, target details or paths.</summary>
[EventSource(Name = ProviderName)]
public sealed class SnapshotProfileTrace : EventSource
{
    public const string ProviderName = "HollowWake-SnapshotProfile";
    public static readonly SnapshotProfileTrace Log = new();
    private SnapshotProfileTrace() { }

    [Event(1, Level = EventLevel.Informational)]
    public void FrameCompleted(int sample, string phase, int round, int viewer, long tick,
        long started, long finished, long anchor)
    {
        if (IsEnabled()) WriteEvent(1, sample, phase, round, viewer, tick, started, finished, anchor);
    }
}
