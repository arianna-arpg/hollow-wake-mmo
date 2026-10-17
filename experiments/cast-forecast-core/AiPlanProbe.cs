using System.Diagnostics;
using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

// Small allocation probe, not a substitute for the production zone benchmark.
// Run from the repository root; setup/warmup are excluded from the measurement.
var content = ContentRegistry.Load(new[] { Path.GetFullPath("content/core") }, out var report);
if (report.HasErrors) throw new InvalidOperationException(report.ToString());
content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
var sim = ZoneBuilder.CreateArena(content, 11);
var factory = sim.Service<PatternFactory>();
var caster = factory.Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
var target = factory.Spawn(sim, "hob", new Vector3(11.5f, 10, 0), new SpawnOptions { Faction = "blue" });
sim.World.Remove<Behavior>(caster);
sim.World.Remove<Behavior>(target);
var book = sim.World.Get<WeaveBook>(caster)!;
book.Slots.Clear();
book.Add(new WeaveSpec { Form = "strike" });
var runtime = sim.Service<WeaveRuntime>();
for (var i = 0; i < 512; i++) runtime.ChooseForAi(sim, caster, target);
const int count = 2500;
var before = GC.GetAllocatedBytesForCurrentThread();
var start = Stopwatch.GetTimestamp();
var selected = 0;
for (var i = 0; i < count; i++)
    if (runtime.ChooseForAi(sim, caster, target) is not null) selected++;
var elapsed = Stopwatch.GetElapsedTime(start);
var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Console.WriteLine($"AI primary-area query: {count} calls, {selected} selected, {(double)allocated / count:0.###} bytes/call, {elapsed.TotalMilliseconds:0.###} ms total.");
