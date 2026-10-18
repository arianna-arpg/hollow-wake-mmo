using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HollowWake.Tools;
using Microsoft.Diagnostics.Tracing;

if (args is ["fixture"])
{
    // Synthetic plumbing control only; never a game performance measurement.
    _ = SnapshotProfileTrace.Log;
    Console.WriteLine($"TRACE PROVIDER {SnapshotProfileTrace.Log.Name} enabled={SnapshotProfileTrace.Log.IsEnabled()}");
    for (var i = 0; i < 2; i++)
    {
        var start = Stopwatch.GetTimestamp();
        Fixture.Work();
        using var gate = new ManualResetEvent(false);
        gate.WaitOne(30);
        var end = Stopwatch.GetTimestamp();
        SnapshotProfileTrace.Log.FrameCompleted(i, "trace-fixture", i, 0, i, start, end, Stopwatch.GetTimestamp());
    }
    Console.WriteLine("TRACE FIXTURE COMPLETE");
    return 0;
}
if (args is not ["inspect", var input, var output])
{
    Console.Error.WriteLine("TraceTools fixture | inspect <local.nettrace> <new-local.json>");
    return 2;
}
if (File.Exists(output)) throw new IOException("Preserve existing trace analysis");
var markers = new List<object>();
var counts = new Dictionary<string, long>(StringComparer.Ordinal);
var runtimeCounts = new Dictionary<string, long>(StringComparer.Ordinal);
using var source = new EventPipeEventSource(input);
source.Clr.All += e => runtimeCounts[e.EventName] = runtimeCounts.GetValueOrDefault(e.EventName) + 1;
source.Dynamic.All += e =>
{
    var key = e.ProviderName + "/" + e.EventName;
    counts[key] = counts.GetValueOrDefault(key) + 1;
    if (e.ProviderName != SnapshotProfileTrace.ProviderName || e.EventName != "FrameCompleted") return;
    markers.Add(new
    {
        Sample = e.PayloadByName("sample"), Phase = e.PayloadByName("phase"), Round = e.PayloadByName("round"),
        Viewer = e.PayloadByName("viewer"), Tick = e.PayloadByName("tick"),
        StartTimestamp = e.PayloadByName("started"), EndTimestamp = e.PayloadByName("finished"),
        AnchorTimestamp = e.PayloadByName("anchor"), e.TimeStampRelativeMSec, e.TimeStamp,
        NativeThreadId = e.ThreadID, ProcessId = e.ProcessID
    });
};
source.Process();
using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
JsonSerializer.Serialize(stream, new
{
    LocalOnly = true,
    Note = "Contains process/thread identities for local correlation. Publish sanitized summaries only. Managed samples are thread-time observations, not proof of OS scheduling or CPU time.",
    source.EventsLost,
    Markers = markers,
    EventCounts = counts.OrderBy(kv => kv.Key).ToDictionary(),
    RuntimeEventCounts = runtimeCounts.OrderBy(kv => kv.Key).ToDictionary(),
    CounterScope = "Dynamic and typed runtime views can overlap. Do not add their counts."
}, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"Trace inspected: {markers.Count} correlation markers; events lost {source.EventsLost}.");
return markers.Count > 0 && source.EventsLost == 0 ? 0 : 1;

static class Fixture
{
    private static double _sink;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Work()
    {
        for (var i = 0; i < 200_000; i++) _sink += Math.Sqrt(i + 1);
    }
}
