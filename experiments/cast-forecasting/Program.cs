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
        var target = factory.Spawn(sim, config["targetPattern"]!.GetValue<string>(), V3(config["targetPosition"]!), new SpawnOptions { Faction = scenario["friendly"]?.GetValue<bool>() == true ? "forecast_red" : "forecast_blue" });
        sim.World.Remove<Behavior>(source);
        sim.World.Remove<Behavior>(target);
        var st = sim.World.Get<Stats>(source)!;
        var tt = sim.World.Get<Stats>(target)!;
        Apply(st, config["controls"]!["source"]!.AsObject());
        Apply(tt, config["controls"]!["target"]!.AsObject());
        if (scenario["targetModifiers"] is JsonObject mods) Apply(tt, mods);
        var book = sim.World.Get<WeaveBook>(source)!;
        var slot = book.Add(new WeaveSpec { Preset = config["weave"]!.GetValue<string>() });
        var runtime = sim.Service<WeaveRuntime>();
        var weave = runtime.Resolve(sim, slot, out var compileReason)!;
        Check(weave is not null, compileReason ?? "Weave did not compile");
        var direction = V2(config["aim"]!);
        var geometry = Area(sim, source, weave!, direction);
        var targetTransform = sim.World.Get<Transform>(target)!;
        if (scenario["targetAtRadiusPlus"] is { } offset)
            targetTransform.Position = sim.World.Get<Transform>(source)!.Position + new Vector3(geometry.Size.Radius + offset.GetValue<float>(), 0, 0);
        if (scenario["targetHeight"] is { } height)
        {
            targetTransform.Position = new Vector3(targetTransform.Position.X, targetTransform.Position.Y, height.GetValue<float>());
            sim.World.Remove<Motion>(target); // Synthetic elevated body, not a simulated bridge.
        }
        sim.Spatial.Update(target, targetTransform.Position);
        var events = new List<object>();
        var frames = new List<object>();
        var hits = new List<DamageDealt>();
        var lifecycle = new List<CastPresentationEvent>();
        var started = 0;
        var resolved = 0;
        var interrupted = 0;
        var expectedRelease = double.PositiveInfinity;
        sim.Events.Subscribe<CastPresentationEvent>(e =>
        {
            if (e.Caster != source) return;
            lifecycle.Add(e);
            events.Add(new { kind = "confirmed_" + e.Phase, time = e.Time, tick = e.Tick, castId = e.CastId, e.Fired, e.Reason });
        });
        sim.Events.Subscribe<WeaveStarted>(e => { if (e.Caster == source) started++; });
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
        Check(runtime.TryBegin(sim, source, slot, target, targetTransform.Position, direction, out var reason), reason);
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
            var footprint = Area(sim, source, weave!, active?.Direction ?? initialDirection);
            var position = targetTransform.Position;
            var inside = footprint.Shape.Contains(footprint.Size, new Vector2(position.X - footprint.Anchor.X, position.Y - footprint.Anchor.Y), footprint.Facing, 0);
            frames.Add(new { time = sim.Clock.Time, tick = sim.Clock.Tick, casting = active is not null,
                progress = active is null ? (double?)null : Math.Clamp((sim.Clock.Time - active.StartedAt) / active.Duration, 0, 1),
                source = Pos(footprint.Anchor), target = Pos(position), heading = Math.Atan2(footprint.Facing.Y, footprint.Facing.X),
                planarInside = inside, life = tt.Resource("life") });
        }
        Frame();
        while (sim.Clock.Time < duration + 0.4)
        {
            while (nextAction < actions.Count && actions[nextAction]!["at"]!.GetValue<double>() <= sim.Clock.Time + 1e-8)
            {
                var action = actions[nextAction++]!;
                var kind = action["kind"]!.GetValue<string>();
                switch (kind)
                {
                    case "move": sim.World.Get<Motion>(target)!.MoveDir = V2(action["direction"]!); break;
                    case "aim":
                        var aim = V2(action["direction"]!);
                        Check(runtime.Aim(sim, source, sim.World.Get<Transform>(source)!.Position + new Vector3(aim * 5, 0), aim), "Aim accepted");
                        break;
                    case "stun": st.Flags.Add("stunned"); break;
                    default: throw new InvalidOperationException("Unregistered experiment action: " + kind);
                }
                events.Add(new { kind, time = sim.Clock.Time, tick = sim.Clock.Tick });
            }
            sim.Tick();
            if (book.Casting is { } ongoing)
                Check(Vector2.Distance(initialDirection, ongoing.Direction) < 1e-6, "Cleave keeps its committed direction");
            Frame();
        }
        Check(started == 1, "One authoritative start");
        Check(hits.Count == scenario["expectedHits"]!.GetValue<int>(), $"{id}@{rate}: unexpected hit count {hits.Count}");
        Check(hits.All(h => !h.Evaded && !h.Blocked), "Controlled fixture has no random defensive outcome");
        Check(interrupted == (id == "interrupted" ? 1 : 0), "Interruption is a confirmed event");
        Check(resolved == (id == "interrupted" ? 0 : 1), "Interruption runs no payload; spatial misses still resolve");
        Check(lifecycle.Count(e => e.Phase == "start") == 1, "Confirmed lifecycle has one start");
        Check(lifecycle.Select(e => e.CastId).Distinct().Count() == 1 && lifecycle[0].CastId > 0, "Lifecycle correlates this cast");
        Check(lifecycle.Count(e => e.Phase == "release") == (id == "interrupted" ? 0 : 1), "Confirmed release agrees with execution");
        Check(lifecycle.Count(e => e.Phase == "impact") == hits.Count, "Confirmed impact agrees with controlled damage events");
        Check(lifecycle.Count(e => e.Phase is "end" or "interrupt") == 1, "One confirmed terminal transition");
        Check(!log.Lines.Any(l => l.StartsWith("error:", StringComparison.Ordinal)), string.Join("\n", log.Lines));
        double damage = hits.Sum(h => h.Total);
        damageByRun[$"{id}@{rate}"] = damage;
        results.Add(new { id, label = scenario["label"]!.GetValue<string>(), tickRate = rate, duration,
            shape = geometry.Shape.Name, radius = geometry.Size.Radius, angleRadians = geometry.Size.Angle * Math.PI / 180,
            width = geometry.Size.Width, innerDistance = geometry.Size.Inner * geometry.Size.Radius,
            hitCount = hits.Count, damage, armor = tt.Get("armor"), frames, events });
        checks.Add($"{id}@{rate}: {hits.Count} hits; {damage:R} actual damage; lifecycle, direction and diagnostics passed");
    }
    Check(damageByRun[$"tank@{rate}"] > 0 && damageByRun[$"tank@{rate}"] < damageByRun[$"stand@{rate}"], "Armor reduces real damage without making the footprint safe");
    Check(damageByRun[$"committed-aim@{rate}"] == damageByRun[$"stand@{rate}"], "Late aim leaves the same seeded hit");
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
var output = new { description = "Real current core source and shipped Cleave content; isolated deterministic controls. No forecast damage calculator.",
    sourceDigest = Convert.ToHexString(hash.GetHashAndReset()), coreModuleId = typeof(Simulation).Module.ModuleVersionId,
    contentDiagnostics = report.ToString(), checks, results };
Directory.CreateDirectory(Path.Combine(owned, "evidence"));
File.WriteAllText(Path.Combine(owned, "evidence", "core-results.json"), JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
Console.WriteLine($"PASS: {results.Count} real-core scenarios; armor comparisons and committed-aim comparisons passed.");

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static Vector2 V2(JsonNode node) => new(node[0]!.GetValue<float>(), node[1]!.GetValue<float>());
static Vector3 V3(JsonNode node) => new(node[0]!.GetValue<float>(), node[1]!.GetValue<float>(), node[2]!.GetValue<float>());
static float[] Pos(Vector3 v) => new[] { v.X, v.Y, v.Z };
static void Apply(Stats stats, JsonObject values)
{
    foreach (var (attribute, value) in values)
        stats.Attributes.Add(new Modifier { Attribute = attribute, Form = ModifierForm.Override, Value = value!.GetValue<double>(), Source = "forecast-fixture" });
}
static (Shape Shape, ShapeSize Size, Vector3 Anchor, Vector2 Facing) Area(Simulation sim, Entity source, CompiledWeave weave, Vector2 direction)
{
    // This adapter is explicitly limited. A production provider must use a shared delivery-plan evaluator.
    if (weave.Specs.Count != 1 || weave.Specs[0].Type != "area") throw new InvalidOperationException("Probe requires one direct area");
    var p = weave.Specs[0].Params;
    if (p["center"]?.GetValue<string>() != "source" || p["reach"] is not null || p["inner"] is not null)
        throw new InvalidOperationException("Probe requires source-centered, center-reach, non-hollow area");
    Expr? Parameter(string name)
    {
        if (p[name] is not { } node) return null;
        var expr = Expr.Parse(node.ToString());
        // Geometry probing must never consume gameplay randomness or inspect targets.
        if (expr.Source.Contains('(') || expr.Identifiers.Any(x => !x.StartsWith("weave.", StringComparison.Ordinal)))
            throw new InvalidOperationException("Probe only accepts constants and weave geometry attributes");
        return expr;
    }
    var ctx = new EffectContext { Sim = sim, Source = source, Direction = direction, Weave = weave, Tags = weave.Tags, Rng = new HollowWake.Core.Maths.Rng(0) };
    var shape = Shapes.Get(p["shape"]!.GetValue<string>());
    var radius = (float)Math.Max(0.1, EffectHelpers.ParamOrAttr(ctx, Parameter("radius"), "area.radius", 2));
    if (shape.Anchored && ctx.Tags.Has("melee")) radius += (float)Math.Max(0, EffectHelpers.WeaveAttr(ctx, "melee.reach", 0));
    return (shape, EffectHelpers.SizeOf(ctx, shape, radius, Parameter("angle"), Parameter("width"), null, true),
        EffectHelpers.Center(ctx, "source"), EffectHelpers.Facing(ctx));
}
