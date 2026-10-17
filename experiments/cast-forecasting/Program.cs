using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using ForecastExperiment;

// An integration probe, not a forecast implementation. All attacks, movement and damage run the real core.
// The bounded geometry reader below only accepts one direct deterministic area and fails on wider graphs.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var owned = Path.Combine(root, "experiments", "cast-forecasting");
var config = JsonNode.Parse(File.ReadAllText(Path.Combine(owned, "scenarios.json")))!.AsObject();
var content = ContentRegistry.Load(new[] { Path.Combine(root, "content", "core") }, out var report);
Check(!report.HasErrors, "Shipped content loads: " + report);
content.With(new FactionDef { Id = "forecast_red", Relations = { ["forecast_blue"] = Stance.Hostile } });
content.With(new FactionDef { Id = "forecast_blue", Relations = { ["forecast_red"] = Stance.Hostile } });
var results = new List<object>();
var damageByRun = new Dictionary<string, double>();
var checks = new List<string>();
var providers = ForecastProviders.CreateDefault();
#if CAST_CUES
const bool confirmedLifecycleAvailable = true;
#else
const bool confirmedLifecycleAvailable = false;
#endif
foreach (var rateNode in config["tickRates"]!.AsArray())
{
    int rate = rateNode!.GetValue<int>();
    foreach (var scenarioNode in config["scenarios"]!.AsArray())
    {
        var scenario = scenarioNode!.AsObject();
        string id = scenario["id"]!.GetValue<string>();
        var log = new ListSimLog();
        var sim = ZoneBuilder.CreateArena(content, config["seed"]!.GetValue<ulong>(), log: log, ticksPerSecond: rate);
        var factory = sim.Service<PatternFactory>();
        var source = factory.Spawn(sim, config["sourcePattern"]!.GetValue<string>(), V3(config["sourcePosition"]!), new SpawnOptions { Faction = "forecast_red" });
        var target = factory.Spawn(sim, config["targetPattern"]!.GetValue<string>(), V3(scenario["targetPosition"] ?? config["targetPosition"]!), new SpawnOptions { Faction = scenario["friendly"]?.GetValue<bool>() == true ? "forecast_red" : "forecast_blue" });
        sim.World.Remove<Behavior>(source);
        sim.World.Remove<Behavior>(target);
        var st = sim.World.Get<Stats>(source)!;
        var tt = sim.World.Get<Stats>(target)!;
        Apply(st, config["controls"]!["source"]!.AsObject());
        Apply(tt, config["controls"]!["target"]!.AsObject());
        if (scenario["targetModifiers"] is JsonObject mods) Apply(tt, mods);
        var book = sim.World.Get<WeaveBook>(source)!;
        var spec = (scenario["spec"] ?? config["spec"])!.Deserialize<WeaveSpec>(ContentJson.Options)!;
        var slot = book.Add(spec);
        var runtime = sim.Service<WeaveRuntime>();
        var weave = runtime.Resolve(sim, slot, out var compileReason)!;
        Check(weave is not null, compileReason ?? "Weave did not compile");
        var direction = V2(config["aim"]!);
        var targetTransform = sim.World.Get<Transform>(target)!;
        var aimPoint = V3(scenario["point"] ?? scenario["targetPosition"] ?? config["targetPosition"]!);
        EffectContext Context(CastState? state = null) => new()
        {
            Sim = sim, Source = source, Target = state?.Target ?? Entity.None,
            Point = state is null ? aimPoint : state.Target.IsNone ? state.Point : sim.World.Get<Transform>(state.Target)?.Position ?? state.Point,
            Direction = state?.Direction ?? direction, Weave = weave, Tags = weave!.Tags, Rng = new HollowWake.Core.Maths.Rng(0)
        };
        var geometry = providers.Describe(Context());
        if (scenario["expectProvider"]?.GetValue<string>() == "unavailable")
        {
            Check(geometry is null, "Unsupported graphs must not invent an exact region");
            checks.Add($"{id}@{rate}: no exact provider; timing-only fallback required");
            results.Add(new { id, label = scenario["label"]!.GetValue<string>(), tickRate = rate, weave = weave!.Key, coverageOnly = true, provider = "unavailable" });
            continue;
        }
        Check(geometry is not null, "Fixture requires a registered provider");
        var initialGeometry = geometry!;
        Check(initialGeometry.Shape.Name == scenario["expectedShape"]?.GetValue<string>() || scenario["expectedShape"] is null, "Compiled shape matches fixture");
        if (scenario["expectedRadius"] is { } expectedRadius) Check(Math.Abs(initialGeometry.Size.Radius - expectedRadius.GetValue<double>()) < 1e-5, "Compiled mutator dimensions match fixture");
        if (scenario["targetAtRadiusPlus"] is { } offset)
            targetTransform.Position = initialGeometry.Anchor + new Vector3(initialGeometry.Size.Radius + offset.GetValue<float>(), 0, 0);
        if (scenario["targetHeight"] is { } height)
        {
            targetTransform.Position = new Vector3(targetTransform.Position.X, targetTransform.Position.Y, height.GetValue<float>());
            sim.World.Remove<Motion>(target); // Synthetic elevated body, not a simulated bridge.
        }
        sim.Spatial.Update(target, targetTransform.Position);
        var events = new List<object>();
        var frames = new List<object>();
        var hits = new List<DamageDealt>();
#if CAST_CUES
        var lifecycle = new List<CastPresentationEvent>();
#endif
        var started = 0;
        var resolved = 0;
        var interrupted = 0;
        var expectedRelease = double.PositiveInfinity;
#if CAST_CUES
        sim.Events.Subscribe<CastPresentationEvent>(e =>
        {
            if (e.Caster != source) return;
            lifecycle.Add(e);
            events.Add(new { kind = "confirmed_" + e.Phase, time = e.Time, tick = e.Tick, castId = e.CastId, e.Fired, e.Reason });
        });
#endif
        sim.Events.Subscribe<WeaveStarted>(e => { if (e.Caster == source) { started++; events.Add(new { kind = "started", time = sim.Clock.Time, tick = e.Tick }); } });
        sim.Events.Subscribe<WeaveCast>(e =>
        {
            if (e.Caster != source) return;
            resolved++;
            events.Add(new { kind = "resolved", time = sim.Clock.Time, tick = e.Tick });
        });
        sim.Events.Subscribe<WeaveInterrupted>(e =>
        {
            if (e.Caster != source) return;
            interrupted++;
            events.Add(new { kind = "interrupted", time = sim.Clock.Time, tick = e.Tick });
        });
        sim.Events.Subscribe<DamageDealt>(e =>
        {
            if (e.Source != source || e.Target != target) return;
            Check(sim.Clock.Time + 1e-8 >= expectedRelease, "No hit before the real cast duration");
            hits.Add(e);
            events.Add(new { kind = "damage", time = sim.Clock.Time, tick = e.Tick, amount = e.Total, e.Roll, e.Crit, e.Evaded, e.Blocked,
                byThread = e.ByThread, targetPosition = Pos(targetTransform.Position) });
        });
        Check(runtime.TryBegin(sim, source, slot, target, aimPoint, direction, out var reason), reason);
        var cast = book.Casting!;
        Check(cast is not null, "Scenario must exercise a timed cast");
        expectedRelease = cast!.StartedAt + cast.Duration;
        var duration = cast.Duration;
        var initialDirection = cast.Direction;
        var actions = scenario["actions"]!.AsArray();
        int nextAction = 0;
        void Frame()
        {
            var active = book.Casting;
            var footprint = providers.Describe(Context(cast))!;
            var position = targetTransform.Position;
            var inside = footprint.ContainsCenter(position);
            frames.Add(new { time = sim.Clock.Time, tick = sim.Clock.Tick, casting = active is not null,
                progress = active is null ? (double?)null : Math.Clamp((sim.Clock.Time - active.StartedAt) / active.Duration, 0, 1),
                source = Pos(sim.World.Get<Transform>(source)!.Position), anchor = Pos(footprint.Anchor), target = Pos(position), heading = Math.Atan2(footprint.Facing.Y, footprint.Facing.X),
                planarInside = inside, life = tt.Resource("life") });
        }
        var actionHandlers = new Dictionary<string, Action<JsonNode>>(StringComparer.Ordinal)
        {
            ["move"] = action => sim.World.Get<Motion>(target)!.MoveDir = V2(action["direction"]!),
            ["aim"] = action =>
            {
                var aim = V2(action["direction"]!);
                Check(runtime.Aim(sim, source, sim.World.Get<Transform>(source)!.Position + new Vector3(aim * 5, 0), aim), "Aim accepted");
            },
            ["stun"] = _ => st.Flags.Add("stunned")
        };
        Frame();
        while (sim.Clock.Time < duration + 0.4)
        {
            while (nextAction < actions.Count && actions[nextAction]!["at"]!.GetValue<double>() <= sim.Clock.Time + 1e-8)
            {
                var action = actions[nextAction++]!;
                var kind = action["kind"]!.GetValue<string>();
                if (!actionHandlers.TryGetValue(kind, out var handler)) throw new InvalidOperationException("Unregistered experiment action: " + kind);
                handler(action);
                events.Add(new { kind, time = sim.Clock.Time, tick = sim.Clock.Tick });
            }
            sim.Tick();
            if (book.Casting is { } ongoing)
                Check(Vector2.Distance(initialDirection, ongoing.Direction) < 1e-6, "Timed casts keep their committed direction");
            Frame();
        }
        Check(started == 1, "One authoritative start");
        Check(hits.Count == scenario["expectedHits"]!.GetValue<int>(), $"{id}@{rate}: unexpected hit count {hits.Count}");
        Check(hits.All(h => !h.Evaded && !h.Blocked), "Controlled fixture has no random defensive outcome");
        var expectedInterrupted = scenario["expectedInterrupted"]?.GetValue<bool>() == true;
        Check(interrupted == (expectedInterrupted ? 1 : 0), "Interruption is a confirmed event");
        Check(resolved == (expectedInterrupted ? 0 : 1), "Interruption runs no payload; spatial misses still resolve");
#if CAST_CUES
        Check(lifecycle.Count(e => e.Phase == "start") == 1, "Confirmed lifecycle has one start");
        Check(lifecycle.Select(e => e.CastId).Distinct().Count() == 1 && lifecycle[0].CastId > 0, "Lifecycle correlates this cast");
        Check(lifecycle.Count(e => e.Phase == "release") == (expectedInterrupted ? 0 : 1), "Confirmed release agrees with execution");
        Check(lifecycle.Count(e => e.Phase == "impact") == hits.Count, "Confirmed impact agrees with controlled damage events");
        Check(lifecycle.Count(e => e.Phase is "end" or "interrupt") == 1, "One confirmed terminal transition");
#endif
        Check(!log.Lines.Any(l => l.StartsWith("error:", StringComparison.Ordinal)), string.Join("\n", log.Lines));
        double damage = hits.Sum(h => h.Total);
        damageByRun[$"{id}@{rate}"] = damage;
        results.Add(new { id, label = scenario["label"]!.GetValue<string>(), note = scenario["note"]?.GetValue<string>() ?? "Recorded state from the shared runtime.", weave = weave!.Key, tickRate = rate, duration,
            shape = initialGeometry.Shape.Name, radius = initialGeometry.Size.Radius, angleRadians = initialGeometry.Size.Angle * Math.PI / 180,
            width = initialGeometry.Size.Width, innerDistance = initialGeometry.Size.Inner * initialGeometry.Size.Radius,
            outline = initialGeometry.Outline(),
            hitCount = hits.Count, damage, armor = tt.Get("armor"), frames, events });
        checks.Add($"{id}@{rate}: {hits.Count} hits; {damage:R} actual damage; lifecycle, direction and diagnostics passed");
    }
    var comparisons = new Dictionary<string, Func<double, double, bool>>(StringComparer.Ordinal)
    {
        ["less_positive"] = (actual, baseline) => actual > 0 && actual < baseline,
        ["equal"] = (actual, baseline) => actual == baseline
    };
    foreach (var comparison in config["comparisons"]!.AsArray())
    {
        var actual = damageByRun[$"{comparison!["actual"]!.GetValue<string>()}@{rate}"];
        var baseline = damageByRun[$"{comparison["baseline"]!.GetValue<string>()}@{rate}"];
        Check(comparisons[comparison["kind"]!.GetValue<string>()](actual, baseline), comparison["description"]!.GetValue<string>());
    }
}
var sourcePaths = Directory.EnumerateFiles(Path.Combine(root, "src", "HollowWake.Core"), "*.cs", SearchOption.AllDirectories)
    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
    .Concat(Directory.EnumerateFiles(Path.Combine(root, "content", "core"), "*.yaml", SearchOption.AllDirectories))
    .OrderBy(p => p, StringComparer.Ordinal).ToArray();
using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
foreach (var path in sourcePaths)
{
    hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\n"));
    hash.AppendData(File.ReadAllBytes(path));
}
var output = new { description = "Real current core and compiled content across area shapes, origins and glyph mutations. No forecast damage calculator.", confirmedLifecycleAvailable,
    sourceDigest = Convert.ToHexString(hash.GetHashAndReset()), coreModuleId = typeof(Simulation).Module.ModuleVersionId,
    contentDiagnostics = report.ToString(), checks, results };
Directory.CreateDirectory(Path.Combine(owned, "evidence"));
File.WriteAllText(Path.Combine(owned, "evidence", "core-results.json"), JsonSerializer.Serialize(output, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
Console.WriteLine($"PASS: {results.Count} real-core scenario/provider checks; configured damage comparisons passed. Confirmed lifecycle extension available: {confirmedLifecycleAvailable}.");

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static Vector2 V2(JsonNode node) => new(node[0]!.GetValue<float>(), node[1]!.GetValue<float>());
static Vector3 V3(JsonNode node) => new(node[0]!.GetValue<float>(), node[1]!.GetValue<float>(), node[2]!.GetValue<float>());
static float[] Pos(Vector3 v) => new[] { v.X, v.Y, v.Z };
static void Apply(Stats stats, JsonObject values)
{
    foreach (var (attribute, value) in values)
        stats.Attributes.Add(new Modifier { Attribute = attribute, Form = ModifierForm.Override, Value = value!.GetValue<double>(), Source = "forecast-fixture" });
}
