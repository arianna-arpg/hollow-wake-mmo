using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.World;

namespace HollowWake.Tools;

/// <summary>An optional extra snapshot stream, measured after the ordinary entity/interface messages.</summary>
public interface ISnapshotProfileExtension
{
    string Name { get; }
    SnapshotProfileExtra Build(SnapshotView view, Entity viewer);
}

public readonly record struct SnapshotProfileExtra(Message Message, int Observations);

/// <summary>
/// Opt-in diagnostic only. Production builders and serializers are called once in their usual order.
/// Probe overhead is included in the total, never subtracted or presented as an ordinary budget pass.
/// </summary>
public sealed class SnapshotProfile : IDisposable
{
    public readonly record struct Cost(double Milliseconds, long AllocatedBytes, int Messages = 0, long EncodedBytes = 0);
    public readonly record struct Collections(int Gen0, int Gen1, int Gen2)
    {
        public static Collections Read() => new(GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        public Collections Since(Collections earlier) => new(Gen0 - earlier.Gen0, Gen1 - earlier.Gen1, Gen2 - earlier.Gen2);
    }
    public readonly record struct Counts(long Spawns, long Updates, long Deltas, long Refreshes, long Despawns, long SelfStates, long SelfSkipped, long Panels)
    {
        public static Counts Read(SnapshotView v) => new(v.Spawns, v.Updates, v.Deltas, v.Refreshes, v.Despawns, v.SelfStates, v.SelfStatesSkipped, v.Panels);
        public Counts Since(Counts earlier) => new(Spawns - earlier.Spawns, Updates - earlier.Updates, Deltas - earlier.Deltas,
            Refreshes - earlier.Refreshes, Despawns - earlier.Despawns, SelfStates - earlier.SelfStates,
            SelfSkipped - earlier.SelfSkipped, Panels - earlier.Panels);
    }
    public readonly record struct Frame(
        string Phase, int Round, int ViewerIndex, long ViewerKey, long Tick, int KnownBefore, int KnownAfter,
        bool SelfDue, bool Deltas, bool Interface, Cost Total, Cost Entities, Cost Self, Cost Panels,
        Cost EntityEncoding, Cost SelfEncoding, Cost PanelEncoding,
        string? Extension, Cost ExtensionCapture, Cost ExtensionEncoding, int Observations,
        Counts Emitted, Collections Gc);
    public sealed record Phase(string Name, long FirstTick, long LastTick, int FirstSample, int Samples,
        double WallMilliseconds, double ProcessCpuMilliseconds, Collections Gc, SnapshotSettings[] Settings);
    public sealed record Workload(string Seed, int Monsters, int Players, int Ticks, int Minions, bool CastCues, int SnapshotsPerPhase);

    private readonly string _path;
    private readonly Process _process;
    private Frame[] _frames = Array.Empty<Frame>();
    private readonly List<Phase> _phases = new(4);
    private Workload? _workload;
    private int _count;
    private string? _phase;
    private int _phaseFirst;
    private long _phaseTick, _phaseStart;
    private TimeSpan _phaseCpu;
    private Collections _phaseGc;
    private SnapshotSettings[] _phaseSettings = Array.Empty<SnapshotSettings>();

    public SnapshotProfile(string path, string only)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("--snapshot-profile requires an output file path");
        if (only is not "" and not "tick")
            throw new ArgumentException("--snapshot-profile requires the full benchmark or --only tick");
        _path = Path.GetFullPath(path);
        if (!Directory.Exists(Path.GetDirectoryName(_path)))
            throw new DirectoryNotFoundException("The snapshot profile output directory must already exist");
        if (File.Exists(_path) || Directory.Exists(_path))
            throw new IOException("The snapshot profile output already exists; preserve it and choose a new path");
        _process = Process.GetCurrentProcess();
    }

    public ReadOnlySpan<Frame> Samples => _frames.AsSpan(0, _count);

    public void Prepare(Workload workload)
    {
        if (_workload is not null) throw new InvalidOperationException("Profile already prepared");
        _workload = workload;
        _frames = new Frame[checked(workload.Players * workload.SnapshotsPerPhase * 4)];
        // Warm only the probe helpers, without capturing or advancing any gameplay state.
        var mark = Mark.Read();
        _ = mark.Finish();
        _ = Collections.Read();
    }

    public void BeginPhase(string name, long tick, IReadOnlyList<SnapshotView> views)
    {
        if (_phase is not null) throw new InvalidOperationException("Previous profile phase is still open");
        _phase = name;
        _phaseFirst = _count;
        _phaseTick = tick;
        _phaseSettings = views.Select(v => v.Settings).ToArray();
        _process.Refresh();
        _phaseCpu = _process.TotalProcessorTime;
        _phaseGc = Collections.Read();
        _phaseStart = Stopwatch.GetTimestamp();
    }

    public void EndPhase(long tick)
    {
        if (_phase is null) throw new InvalidOperationException("No profile phase is open");
        var wall = Stopwatch.GetElapsedTime(_phaseStart).TotalMilliseconds;
        var gc = Collections.Read().Since(_phaseGc);
        _process.Refresh();
        var cpu = (_process.TotalProcessorTime - _phaseCpu).TotalMilliseconds;
        _phases.Add(new Phase(_phase, _phaseTick, tick, _phaseFirst, _count - _phaseFirst, wall, cpu, gc, _phaseSettings));
        _phase = null;
    }

    public Frame Measure(Simulation sim, SnapshotBuilder builder, SnapshotView view, Entity viewer,
        List<Message> output, int round, int viewerIndex, ISnapshotProfileExtension? extension, out Message? extraMessage)
    {
        if (_phase is null || _count >= _frames.Length) throw new InvalidOperationException("Profile phase or sample capacity is invalid");
        var knownBefore = view.Known.Count;
        var countsBefore = Counts.Read(view);
        var gcBefore = Collections.Read();
        // Preserve the legacy boundary: full/delta/interface exclude Clear; combined includes it.
        if (extension is null) output.Clear();
        var totalStart = Mark.Read();
        if (extension is not null) output.Clear();

        var mark = Mark.Read();
        builder.Entities(view, viewer, output);
        var entityEnd = output.Count;
        var entities = mark.Finish(entityEnd);

        Cost self = default;
        var selfDue = round % 5 == 0;
        if (selfDue)
        {
            mark = Mark.Read();
            if (builder.Self(view, viewer) is { } message) output.Add(message);
            self = mark.Finish(output.Count - entityEnd);
        }
        var selfEnd = output.Count;
        Cost panels = default;
        if (view.SendInterface)
        {
            mark = Mark.Read();
            builder.Panels(view, viewer, output);
            panels = mark.Finish(output.Count - selfEnd);
        }

        var entityEncoding = Encode(output, 0, entityEnd);
        var selfEncoding = Encode(output, entityEnd, selfEnd);
        var panelEncoding = Encode(output, selfEnd, output.Count);
        Cost extraCapture = default, extraEncoding = default;
        var observations = 0;
        extraMessage = null;
        if (extension is not null)
        {
            mark = Mark.Read();
            var extra = extension.Build(view, viewer);
            extraMessage = extra.Message;
            observations = extra.Observations;
            extraCapture = mark.Finish(1);
            mark = Mark.Read();
            var bytes = MessageSerializer.Encode(extra.Message).Length;
            extraEncoding = mark.Finish(1, bytes);
        }
        var encodedBytes = entityEncoding.EncodedBytes + selfEncoding.EncodedBytes + panelEncoding.EncodedBytes + extraEncoding.EncodedBytes;
        var total = totalStart.Finish(output.Count + (extraMessage is null ? 0 : 1), encodedBytes);
        var gc = Collections.Read().Since(gcBefore);
        var frame = new Frame(_phase, round, viewerIndex, viewer.Key, sim.Clock.Tick, knownBefore, view.Known.Count,
            selfDue, view.SendDeltas, view.SendInterface, total, entities, self, panels,
            entityEncoding, selfEncoding, panelEncoding, extension?.Name, extraCapture, extraEncoding, observations,
            Counts.Read(view).Since(countsBefore), gc);
        _frames[_count++] = frame;
        return frame;
    }

    private static Cost Encode(List<Message> messages, int start, int end)
    {
        if (start == end) return default;
        var mark = Mark.Read();
        long bytes = 0;
        for (var i = start; i < end; i++) bytes += MessageSerializer.Encode(messages[i]).Length;
        return mark.Finish(end - start, bytes);
    }

    private readonly record struct Mark(long AllocatedBytes, long Timestamp)
    {
        public static Mark Read() => new(GC.GetAllocatedBytesForCurrentThread(), Stopwatch.GetTimestamp());
        public Cost Finish(int messages = 0, long bytes = 0)
        {
            var elapsed = Stopwatch.GetElapsedTime(Timestamp).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - AllocatedBytes;
            return new Cost(elapsed, allocated, messages, bytes);
        }
    }

    public void Save()
    {
        if (_phase is not null) throw new InvalidOperationException("Cannot save an unfinished profile phase");
        using var stream = new FileStream(_path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, new
        {
            SchemaVersion = 1,
            DiagnosticOnly = true,
            Limitations = new[]
            {
                "Probe overhead is included in total time and never subtracted; these rows do not replace ordinary budget evidence.",
                "Phases advance sequential simulation stretches. Viewer order and cache sharing are preserved.",
                "Phase wall/process CPU and GC include intervening simulation ticks and probe work, not just snapshot capture.",
                "GC counts are process-wide; CPU/wall ratios do not establish contention or its cause.",
                "Build messages and encoding messages describe the same payloads; do not double-count them. Updates/deltas count entity states."
            },
            Environment = new { Runtime = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Processors = Environment.ProcessorCount, ServerGc = GCSettings.IsServerGC, Latency = GCSettings.LatencyMode.ToString(), Stopwatch.Frequency },
            Workload = _workload,
            Phases = _phases,
            Samples = _frames.AsSpan(0, _count).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public void Dispose() => _process.Dispose();
}
