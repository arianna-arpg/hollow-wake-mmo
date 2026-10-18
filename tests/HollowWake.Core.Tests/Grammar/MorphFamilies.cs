using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Effects;
using HollowWake.Core.Systems;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// The parameter tables of the combat grammar's delivery and agency families, one family per effect. Each table names
/// the parameters the morph test samples, each in its dimension: axes (one quantity with a unit) with a range (the
/// values shipped content gives them, widened to the handler's clamp, as each source says) and choices with their
/// registered words. Flags, nodes and presentation words are listed as unsampled, with why; a later pass that turns a
/// flag into an axis adds a row here and nothing else. Default samples place nothing at random, because the cast's
/// random stream is keyed by the weave and two weaves that differ in one parameter also differ in every roll.
/// </summary>
internal static class Families
{
    /// <summary>The words of the filter registry, <c>Filters.Names</c>: which bodies a delivery reaches (an unregistered word reaches nobody).</summary>
    public static readonly string[] FilterWords = Filters.Names.ToArray();

    /// <summary>The words of the pick registry, <c>Picks.Names</c>, read from it: the registry is internal to the core, so by name.</summary>
    public static readonly string[] PickWords = (string[])typeof(EffectRegistry).Assembly
        .GetType("HollowWake.Core.Effects.Handlers.Picks", throwOnError: true)!
        .GetField("Names")!.GetValue(null)!;

    public static readonly string[] Occlusions = { "blocked", "free" };

    /// <summary>The kinematic axes the <c>trajectory</c> probe reads (DeliveryProbes.Axes).</summary>
    public static readonly string[] Trajectory = { "homing", "guide", "erratic", "spiral", "orbit", "wave", "accel", "bounce", "recurve", "converge" };

    private static string[] ShapeNames => Shapes.Names.ToArray();

    /// <summary>The one group a summon or deploy raises its bodies into, so <c>max_active</c> counts every body.</summary>
    private const string RaisedGroup = "morph";

    private const string OpenArena = "the arena is flat and open: no walls or rising ground for anything to stop at or pass through";

    private const string SameShape = "the Shapes registry registers the sector a second time under the name cone";

    private const string NoBodyBetween = "the crowd stands no body inside one of these shapes and outside the other";

    private const MorphDimension Geometry = MorphDimension.Geometry;
    private const MorphDimension Kinematics = MorphDimension.Kinematics;
    private const MorphDimension Payload = MorphDimension.Payload;
    private const MorphDimension Timing = MorphDimension.Timing;
    private const MorphDimension Targeting = MorphDimension.Targeting;
    private const MorphDimension Agency = MorphDimension.Agency;
    private const MorphDimension Causality = MorphDimension.Causality;

    /// <summary>What a shell reads its cost and cooldown from, for every point: a cost to pay and a cooldown to start.</summary>
    private static Dictionary<string, double> Mana(params (string, double)[] more) => With(new() { ["cost.base.mana"] = 10, ["cooldown.base"] = 2 }, more);

    private static Dictionary<string, double> Stamina(params (string, double)[] more) => With(new() { ["cost.base.stamina"] = 10, ["cooldown.base"] = 2, ["range"] = 20 }, more);

    private static Dictionary<string, double> With(Dictionary<string, double> d, (string, double)[] more)
    {
        foreach (var (k, v) in more)
        {
            d[k] = v;
        }
        return d;
    }

    private static Dictionary<string, object> Map(params (string, object)[] pairs) => pairs.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    private static JsonArray Root(Point _, JsonObject node) => new(node);

    /// <summary>A plain flight east that strikes the first body and carries the given effects on its hit: what chains and target tethers ride.</summary>
    private static JsonObject CarrierFlight(params JsonNode[] onHit) => new()
    {
        ["type"] = "projectile",
        ["count"] = 1,
        ["speed"] = 14,
        ["range"] = 12,
        ["pierce"] = 0,
        ["on_hit"] = new JsonArray(onHit),
    };

    /// <summary>The seconds the carrier flight takes to strike or run out: its range over its speed.</summary>
    private const double CarrierSeconds = 12.0 / 14.0;

    private static double Count0(double v) => Math.Max(0, Math.Round(v));

    /// <summary>Nodes in a full tree of the given branching and depth: the flights one launch can become through forks.</summary>
    private static double Tree(int branching, double depth) => branching == 1 ? depth + 1 : (Math.Pow(branching, depth + 1) - 1) / (branching - 1);

    // ------------------------------------------------------------ projectile

    public static readonly Family Projectile = new()
    {
        Name = "projectile",
        Effect = "projectile",
        Shell = "missile_quick",
        Probe = "projectile",
        Aim = new Vector2(6, 0),
        Attributes = Mana(),
        Parameters = new[]
        {
            Parameter.Count("count", Geometry, "flights", 1, 64, "shipped 2..3 on the node, 1..24 as projectile.count (catalog 1..20); the handler clamps 1..64") with { Attribute = "projectile.count", Absent = 0.2, Live = Map(("spread", 30.0)) },
            Parameter.Axis("speed", Kinematics, "units a second", 0.5, 40, "shipped 14..15 on the node, 0.88..28.13 as projectile.speed; the handler floors 0.5") with { Attribute = "projectile.speed", Absent = 0.2 },
            Parameter.Axis("range", Kinematics, "units", 1, 30, "shipped 4..28.13; the handler floors 1") with { Attribute = "range", Absent = 0.2, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("spread", Geometry, "degrees", 0, 180, "shipped 24..110, scaled by projectile.spread; no clamp") with { Default = 15.0, Absent = 0.3, Live = Map(("count", 3.0)) },
            Parameter.Count("pierce", Kinematics, "bodies", 0, 10, "shipped 0..999 as projectile.pierce (999 passes everything); the handler floors 0; ten passes the whole crowd") with { Attribute = "projectile.pierce", Absent = 0.2 },
            Parameter.Axis("radius", Geometry, "units", 0.1, 1.5, "shipped 0.16..1; a literal number, no clamp") with { Default = 0.5, Absent = 0.2 },
            Parameter.Choice("shape", Geometry, ShapeNames, "the Shapes registry") with { Default = "circle", Absent = 0.4, Live = Map(("shape_size", 4.0), ("pierce", 10.0)) },
            Parameter.Axis("shape_size", Geometry, "units", 0.05, 3, "never shipped; the handler floors 0.05, default the radius") with { Absent = 0.6 },
            Parameter.Axis("shape_angle", Geometry, "degrees", 5, 360, "never shipped") with { Absent = 0.6, Live = Map(("shape", "sector"), ("shape_size", 3.0), ("pierce", 10.0)) },
            Parameter.Axis("shape_width", Geometry, "units", 0.1, 4, "never shipped") with { Absent = 0.6, Live = Map(("shape", "bar"), ("shape_size", 2.0), ("pierce", 10.0)) },
            Parameter.Choice("fire", Geometry, new[] { "fan", "volley", "ring", "salvo" }, "the handler's firing styles") with { Default = "fan", Absent = 0.4, Live = Map(("count", 3.0), ("spread", 90.0)) },
            Parameter.Axis("salvo_interval", Timing, "seconds", 0.05, 0.5, "shipped 0.08; floored at a tick, default rule delivery.salvo_interval") with { Absent = 0.5, Live = Map(("fire", "salvo"), ("count", 3.0)) },
            Parameter.Axis("volley_spacing", Geometry, "units", 0, 2, "shipped 0.8; a literal number") with { Default = 0.6, Absent = 0.5, Live = Map(("fire", "volley"), ("count", 3.0), ("pierce", 10.0)) },
            Parameter.Axis("ring_jitter", Geometry, "degrees", 0, 180, "shipped 15..90; a literal number") with { Default = 0.0, Absent = 0.5, Rolls = true, Live = Map(("fire", "ring"), ("count", 8.0), ("pierce", 10.0)) },
            Parameter.Choice("origin", Geometry, new[] { "caster", "point" }, "the handler's origins") with { Default = "caster", Absent = 0.5 },
            Parameter.Axis("origin_range", Geometry, "units", 0, 60, "shipped 10.63..60; floored 0") with { Absent = 0.5, Live = Map(("origin", "point")) },
            Parameter.Axis("duration", Timing, "seconds", 0.25, 6, "shipped 2.4..6; floored at a tick; absent, the range alone ends a flight") with { Absent = 0.5, Live = Map(("pierce", 10.0)) },
            Parameter.Choice("return", Kinematics, new[] { "none", "origin", "caster" }, "the handler's returns") with { Default = "none", Absent = 0.4, Live = Map(("origin", "point")) },
            Parameter.Count("forks", Kinematics, "forks", 0, 4, "shipped +1 as projectile.forks; no clamp, and each fork doubles the flights") with { Attribute = "projectile.forks", Absent = 0.3 },
            Parameter.Count("chains", Kinematics, "chains", 0, 5, "shipped +2 as projectile.chain; no clamp") with { Attribute = "projectile.chain", Absent = 0.3 },
            Parameter.Axis("rehit", Kinematics, "seconds", 0, 1.5, "shipped 0.5..0.6 (0.55..999 as projectile.rehit, where 999 stands for once a flight); floored 0; sampled to 1.5, since a rehit past a flight's life strikes each body once, as 999 does") with { Attribute = "projectile.rehit", Absent = 0.5 },
            Parameter.Count("bounce", Kinematics, "bounces", 0, 6, "shipped 2..6 as projectile.bounce; no clamp") with { Attribute = "projectile.bounce", Absent = 0.4 },
            Parameter.Axis("homing", Kinematics, "degrees a second", 0, 360, "shipped 15..286 as projectile.homing") with { Attribute = "projectile.homing", Absent = 0.5, Live = Map(("count", 2.0), ("spread", 120.0)) },
            Parameter.Axis("guide", Kinematics, "degrees a second", 0, 360, "shipped 126..172 as projectile.guide") with { Attribute = "projectile.guide", Absent = 0.6, Live = Map(("count", 2.0), ("spread", 120.0)) },
            Parameter.Axis("erratic", Kinematics, "degrees a second", 0, 1000, "shipped 199..997 as projectile.erratic") with { Attribute = "projectile.erratic", Absent = 0.6, Rolls = true, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("spiral", Kinematics, "degrees a second", -360, 360, "shipped 298 as projectile.spiral; either way round") with { Attribute = "projectile.spiral", Absent = 0.7, Pair = (0.0, 360.0) },
            Parameter.Axis("orbit", Kinematics, "degrees a second", 90, 360, "shipped 169..271 as projectile.orbit; sampled from 90: an orbit lives range / (orbit_radius x rate), unbounded as the rate falls to zero") with { Attribute = "projectile.orbit", Absent = 0.75, Pair = (0.0, 360.0) },
            Parameter.Axis("spin", Kinematics, "degrees a second", -1000, 1000, "shipped 900; turns the hit shape only") with { Attribute = "projectile.spin", Absent = 0.6, Pair = (0.0, 1000.0), Live = Map(("shape", "line"), ("shape_size", 3.5), ("pierce", 10.0)) },
            Parameter.Axis("wave", Kinematics, "sways a second", 0, 4, "shipped 1.6 as projectile.wave") with { Attribute = "projectile.wave", Absent = 0.6, Live = Map(("amplitude", 2.0), ("pierce", 10.0)) },
            Parameter.Axis("amplitude", Kinematics, "units", 0, 3, "shipped 1.5; default rule delivery.wave_amplitude") with { Absent = 0.6, Live = Map(("wave", 2.0), ("pierce", 10.0)) },
            Parameter.Axis("accel", Kinematics, "units a second squared", -10, 10, "shipped -5.8..1.9 as projectile.accel; never below rule delivery.min_speed") with { Attribute = "projectile.accel", Absent = 0.6, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("recurve", Kinematics, "degrees a second", -180, 180, "never set by shipped content") with { Attribute = "projectile.recurve", Absent = 0.7, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("converge", Kinematics, "degrees a second", 0, 360, "shipped 126 as projectile.converge") with { Attribute = "projectile.converge", Absent = 0.7, Live = Map(("count", 2.0), ("spread", 120.0)) },
            Parameter.Axis("orbit_radius", Kinematics, "units", 0.5, 4, "shipped 1.72..2.97; floored 0.5, default rule delivery.orbit_radius") with { Absent = 0.6, Live = Map(("orbit", 180.0)) },
            Parameter.Axis("zigzag", Kinematics, "degrees", 0, 90, "shipped 55") with { Absent = 0.7, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("zigzag_every", Kinematics, "units", 0.25, 4, "shipped 3.3; floored 0.25") with { Absent = 0.6, Live = Map(("zigzag", 60.0), ("pierce", 10.0)) },
            Parameter.Axis("tick", Causality, "seconds", 0.05, 1, "shipped 0.18..0.55; a literal, validation wants it above zero, the handler floors 0.05") with { Default = 0.5, Absent = 0.5, Live = Map(("pierce", 10.0)) },
            Parameter.Axis("travel", Causality, "units", 0.25, 4, "shipped 1.5..1.81; a literal, the handler floors 0.25") with { Default = 2.0, Absent = 0.5 },
            Parameter.Choice("occlusion", Geometry, Occlusions, "blocked or free") with { Default = "blocked", Absent = 0.5 },
        },
        Defaults = Map(("count", 1.0), ("speed", 14.0), ("range", 12.0), ("pierce", 0.0)),
        Lists = new[]
        {
            new ListFill("on_hit", Rider.Damage),
            new ListFill("on_first_hit", Rider.Damage, 0.5),
            new ListFill("on_end", Rider.Marker, 0.7),
            new ListFill("on_expire", Rider.Marker, 0.5),
            new ListFill("on_tick", Rider.Marker, 0.5),
            new ListFill("on_travel", Rider.Marker, 0.5),
            new ListFill("on_land", Rider.Marker, 0.3),
            new ListFill("on_return", Rider.Marker, 0.5),
        },
        Carrier = Root,
        Budget = ProjectileBudget,
        States = ProjectileStates,
        Checks = SalvoBeats,
        Lands = _ => (false, "a flight lands only lobbed, and the table does not sample gravity"),
        Exempt = new Dictionary<string, string>
        {
            ["bounce"] = "flights glance only off walls, rising ground or (lobbed) the ground, and gravity is a flag the table does not sample: " + OpenArena,
            ["occlusion"] = OpenArena,
        },
        Alike = new[]
        {
            new Alike("shape", "sector", "cone", SameShape),
            new Alike("shape", "bar", "wave", "swept along the flight, the two blades laid across it pass over the crowd's bodies on the same ticks"),
        },
        Unsampled = new Dictionary<string, string>
        {
            ["gravity"] = "a flag (lob); the capability map proposes the axis projectile.arc",
            ["spread_by_aim"] = "a block of literals; the capability map proposes dropping it for aim.distance",
            ["look"] = "presentation only",
        },
    };

    private static Budget ProjectileBudget(View v)
    {
        var b = MorphArena.Bodies;
        var count = Math.Clamp((int)Math.Round(v.Num("count")), 1, 64);
        var k = Math.Max(1, (int)v.Rule("delivery.fork_children", 2));
        var forks = Count0(v.Num("forks"));
        var chains = Count0(v.Num("chains"));
        var pierce = (int)Math.Max(0, v.Num("pierce"));
        var rehit = Math.Max(0, v.Num("rehit"));
        var orbit = v.Num("orbit");
        var returns = v.Word("return") is "origin" or "caster" && orbit == 0;
        var flights = count * Tree(k, forks);
        var (seconds, distance) = ProjectileSeconds(v, count, forks, chains, returns);
        var perFlight = rehit > 0
            ? b * (1 + Math.Ceiling(seconds / Math.Max(rehit, View.Dt)))
            : Math.Min(b, (pierce + 1.0) * (chains + 1)) + (returns ? b : 0);
        // A flight runs on_tick once each tick interval it lives and on_travel once each travel interval it flies, each
        // flight once more for where it starts; on_end, on_expire, on_return and on_land run once a flight at most.
        var tick = Math.Max(0.05, v.Num("tick", 0.5));
        var travel = Math.Max(0.25, v.Num("travel", 2));
        var markers = new Dictionary<string, double>
        {
            ["on_end"] = flights,
            ["on_expire"] = flights,
            ["on_return"] = flights,
            ["on_land"] = flights,
            ["on_tick"] = flights * Math.Ceiling(seconds / tick) + flights,
            ["on_travel"] = flights * Math.Ceiling(distance / travel) + flights,
        };
        return new Budget(flights * perFlight + count, flights, flights, seconds,
            $"{count} flights x {Tree(k, forks)} through {forks} forks of {k}; each strikes at most {perFlight} (pierce {pierce}, chains {chains}, rehit {rehit:0.##}, returns {returns}); lives {seconds:0.##} s and flies {distance:0.##} at most")
        {
            Markers = markers,
        };
    }

    /// <summary>
    /// How long a launch lives: the salvo's beats, then every leg (the first, each chain and each fork generation flies
    /// a fresh range, at the speed the last one left it: an acceleration carries across legs; a duration counts from the
    /// launch and never resets), then the way back, and a tick for each leg and the way back to land on. Each salvo beat
    /// is scheduled from the one before and waits a whole number of ticks, its interval rounded up. The way back is no
    /// longer than the way out plus how far the flight's start stands from where it returns to, flown at the slowest
    /// speed the flight can have by then, and never past the rule's longest return. Also how far one flight can fly in
    /// all: its legs out and its way back.
    /// </summary>
    private static (double Seconds, double Distance) ProjectileSeconds(View v, int count, double forks, double chains, bool returns)
    {
        var speed = Math.Max(0.5, v.Num("speed"));
        var accel = v.Num("accel");
        var floor = v.Rule("delivery.min_speed", 0.5);
        var hasDuration = v.Sets("duration");
        var rangeOnNode = v.Sets("range") && !v.Point.ViaAttribute.Contains("range");
        var range = !rangeOnNode && hasDuration ? double.PositiveInfinity : Math.Max(1, v.Num("range"));
        var orbit = v.Num("orbit");
        var legs = 1 + chains + forks;
        double outward;
        double arc = 0;
        if (orbit != 0)
        {
            var radius = Math.Max(0.5, v.Num("orbit_radius", v.Rule("delivery.orbit_radius", 2.5)));
            arc = radius * Math.Abs(orbit) * Math.PI / 180;
            outward = legs * (range / arc + View.Dt);
        }
        else
        {
            outward = TimeToTravel(legs * range, speed, accel, floor);
        }
        if (hasDuration)
        {
            outward = Math.Min(Math.Max(View.Dt, v.Num("duration")), outward);
        }
        // An orbit's flight goes round its caster at its own pace; any other flies at its speed, as its acceleration takes
        // it; and a leg ends on the first step past its range, so each leg may run a step long.
        var step = (orbit != 0 ? arc : speed + Math.Max(0, accel) * outward) * View.Dt;
        var flown = (orbit != 0 ? arc * outward : Distance(outward, speed, accel, floor)) + legs * step;
        var back = 0.0;
        var home = 0.0;
        if (returns)
        {
            // A flight returning to its caster from a launch at the aim point has that far to come back besides.
            var offset = v.Word("return") == "caster" && v.Word("origin") == "point"
                ? Math.Min(v.Point.Family.Aim.Length(), v.Sets("origin_range") ? Math.Max(0, v.Num("origin_range")) : double.PositiveInfinity)
                : 0;
            home = flown + offset;
            var turning = accel >= 0 ? speed : Math.Max(floor, speed + accel * outward);
            back = Math.Min(v.Rule("delivery.return_max_seconds", 6), TimeToTravel(home, turning, accel, floor)) + View.Dt;
        }
        var beat = Math.Max(1, Math.Ceiling(Math.Max(View.Dt, v.Num("salvo_interval", v.Rule("delivery.salvo_interval", 0.15))) * MorphArena.TicksPerSecond - 1e-6)) * View.Dt;
        var salvo = v.Word("fire") == "salvo" && count > 1 ? (count - 1) * beat : 0;
        var seconds = salvo + outward + back + legs * View.Dt;
        // The way back is flown straight home, a step past it at most, at a speed an acceleration may raise.
        var fastest = speed + Math.Max(0, accel) * seconds;
        return (seconds, flown + (returns ? home + fastest * View.Dt : 0));
    }

    /// <summary>How far a flight goes in a time from a speed under a steady acceleration that never takes the speed below a floor.</summary>
    internal static double Distance(double seconds, double speed, double accel, double floor)
    {
        if (accel >= 0 || speed <= floor)
        {
            return accel >= 0 ? speed * seconds + accel * seconds * seconds / 2 : floor * seconds;
        }
        var toFloor = (speed - floor) / -accel;
        return seconds <= toFloor
            ? speed * seconds + accel * seconds * seconds / 2
            : (speed + floor) / 2 * toFloor + floor * (seconds - toFloor);
    }

    /// <summary>
    /// A salvo's last beat, in ticks from the press: where it should land, the tick its seconds reach (a beat k
    /// intervals out lands on the tick k intervals name, as EffectHelpers.Later promises for sums of short waits), and
    /// where beats chained from one another land, each wait rounded up to a whole tick. Null when the point fires no
    /// salvo of more than one flight.
    /// </summary>
    internal static (long Nominal, long Chained, int Beats, double Interval)? SalvoTicks(View v)
    {
        var count = Math.Clamp((int)Math.Round(v.Num("count")), 1, 64);
        if (v.Word("fire") != "salvo" || count < 2)
        {
            return null;
        }
        var interval = Math.Max(View.Dt, v.Num("salvo_interval", v.Rule("delivery.salvo_interval", 0.15)));
        long Ticks(double seconds) => Math.Max(1, (long)Math.Ceiling(seconds * MorphArena.TicksPerSecond - 1e-6));
        var beats = count - 1;
        return (Ticks(beats * interval), beats * Ticks(interval), beats, interval);
    }

    /// <summary>Whether a point's salvo is due to drift: its chained beats land later than its last beat's nominal tick.</summary>
    internal static bool SalvoDrifts(View v) => SalvoTicks(v) is { } s && s.Chained > s.Nominal;

    /// <summary>The last flight a salvo launches from the caster flies on the tick its beats name.</summary>
    private static IEnumerable<(string Invariant, string Message)> SalvoBeats(View v, MorphArena.Outcome o)
    {
        if (SalvoTicks(v) is not { } s)
        {
            yield break;
        }
        var last = o.Launches.Count == 0 ? -1 : o.Launches.Max();
        if (last != s.Nominal)
        {
            yield return ("timing", $"the last salvo beat flew at tick {last}, not on its nominal tick {s.Nominal}: {s.Beats} beats {s.Interval:0.###} s apart");
        }
    }

    /// <summary>Seconds to fly a distance from a speed under a steady acceleration that never takes the speed below a floor.</summary>
    internal static double TimeToTravel(double distance, double speed, double accel, double floor)
    {
        if (double.IsInfinity(distance))
        {
            return distance;
        }
        if (accel == 0)
        {
            return distance / speed;
        }
        if (accel > 0)
        {
            return (-speed + Math.Sqrt(speed * speed + 2 * accel * distance)) / accel;
        }
        var slow = -accel;
        if (speed <= floor)
        {
            return distance / floor;
        }
        var toFloor = (speed - floor) / slow;
        var covered = (speed + floor) / 2 * toFloor;
        return distance <= covered
            ? (speed - Math.Sqrt(Math.Max(0, speed * speed - 2 * slow * distance))) / slow
            : toFloor + (distance - covered) / floor;
    }

    /// <summary>What a projectile point's compiled weave must state, and must not, of every mechanism the table's samples move.</summary>
    private static IEnumerable<Statement> ProjectileStates(View v)
    {
        yield return new Statement("projectile", true, "the graph fires a projectile");
        yield return new Statement("flight", true, "the graph fires a projectile");
        foreach (var s in FireStates(v.Word("fire")).Concat(ShapeStates(v.Word("shape"))))
        {
            yield return s;
        }
        yield return new Statement("returning", v.Word("return") != "none", $"return {v.Word("return")}");
        yield return new Statement("homing", v.Num("homing", 0) != 0, $"homing {v.Num("homing", 0):0.##}");
        var bent = Trajectory.FirstOrDefault(axis => v.Num(axis, 0) != 0);
        yield return bent is null
            ? new Statement("trajectory", false, "no trajectory axis is set")
            : new Statement("trajectory", true, $"{bent} {v.Num(bent):0.##}");
        yield return new Statement("chain", Count0(v.Num("chains")) > 0, $"chains {v.Num("chains")}");
        yield return new Statement("flight:children", Count0(v.Num("forks")) > 0, $"forks {v.Num("forks")}");
        yield return v.Num("rehit") > 0
            ? new Statement("flight:spends", false, $"rehit {v.Num("rehit"):0.##}: a hit never ends the flight")
            : new Statement("flight:spends", true, "rehit 0: a hit ends the flight");
        yield return Landing(v);
    }

    /// <summary>The firing styles, bare (any but a fan) and by name.</summary>
    private static IEnumerable<Statement> FireStates(string fire)
    {
        yield return new Statement("fire", fire != "fan", $"fire {fire}");
        foreach (var style in Firing)
        {
            yield return new Statement("fire:" + style, fire == style, $"fire {fire}");
        }
    }

    private static readonly string[] Firing = { "fan", "volley", "ring", "salvo" };

    /// <summary>The shapes, bare (any but a circle) and by name; the registry's cone is its sector.</summary>
    private static IEnumerable<Statement> ShapeStates(string shape)
    {
        yield return new Statement("shape", shape != "circle", $"shape {shape}");
        foreach (var name in ShapeNames)
        {
            var same = name == shape || (name, shape) is ("sector", "cone") or ("cone", "sector");
            yield return new Statement("shape:" + name, same, $"shape {shape}");
        }
    }

    /// <summary>Whether the point's delivery lands, as its family says, stated both ways.</summary>
    private static Statement Landing(View v)
    {
        var (lands, because) = v.Point.Family.Lands?.Invoke(v) ?? (false, $"a {v.Point.Family.Name} never lands");
        return new Statement("landing", lands, because);
    }

    /// <summary>Whether a point's delivery lands, as its family says (false for a family whose deliveries never land).</summary>
    internal static bool Lands(View v) => v.Point.Family.Lands?.Invoke(v).Lands ?? false;

    // ------------------------------------------------------------ area

    public static readonly Family Area = new()
    {
        Name = "area",
        Effect = "area",
        Shell = "slam_quick",
        Probe = "area",
        Aim = new Vector2(6, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Choice("shape", Geometry, ShapeNames, "the Shapes registry") with { Default = "circle", Absent = 0.3, Live = Map(("center", "focus"), ("radius", 7.0)) },
            Parameter.Axis("radius", Geometry, "units", 0.1, 9, "shipped 1.72..6 on the node, 1.1..9 as area.radius; the handler floors 0.1") with { Attribute = "area.radius", Absent = 0.2 },
            Parameter.Axis("angle", Geometry, "degrees", 5, 360, "shipped 60..120, scaled by area.angle") with { Absent = 0.5, Live = Map(("shape", "sector"), ("radius", 9.0)) },
            Parameter.Axis("width", Geometry, "units", 0.1, 8, "shipped 1 (area.width 2 by default)") with { Absent = 0.5, Live = Map(("shape", "line"), ("radius", 10.0)) },
            Parameter.Axis("inner", Geometry, "fraction of the radius", 0, 1, "shipped 0.72; the handler clamps 0..1") with { Absent = 0.6, Live = Map(("radius", 4.0)) },
            Parameter.Choice("center", Geometry, new[] { "focus", "source", "point" }, "the handler's centers") with { Absent = 0.4 },
            Parameter.Count("max_targets", Targeting, "bodies", 0, 24, "shipped 1..24; floored 0") with { Absent = 0.5, Live = Map(("radius", 4.0)) },
            Parameter.Choice("pick", Targeting, PickWords, "Picks.Names") with { Default = "random", Absent = 0.5, Rolling = new[] { "random" }, Live = Map(("max_targets", 1.0), ("radius", 3.8)) },
            Parameter.Choice("reach", Geometry, new[] { "center", "edge" }, "the handler's reaches") with { Default = "center", Absent = 0.5, Live = Map(("shape", "sector"), ("radius", 1.8)) },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("center", "source"), ("radius", 5.5)) },
            Parameter.Choice("occlusion", Geometry, Occlusions, "blocked or free") with { Default = "blocked", Absent = 0.5 },
            Parameter.Count("area.scatter", Causality, "aftershocks", 0, 4, "shipped +2 as a glyph modifier; no clamp") with { Attribute = "area.scatter", AttributeOnly = true, Absent = 0.5 },
            Parameter.Count("area.pulses", Causality, "repeats", 0, 4, "never moved by shipped content; no clamp") with { Attribute = "area.pulses", AttributeOnly = true, Absent = 0.5 },
        },
        Defaults = Map(("shape", "circle"), ("radius", 2.5)),
        Lists = new[]
        {
            new ListFill("effects", Rider.Damage),
            // What the grammar names beyond a hit, on sampled points: a drain standing alone in the list, so the life it
            // takes has no hit in its tick; a status that ticks; a cast of another weave. Each marks the depth it runs at.
            new ListFill("effects", Rider.Drain, 0.2) { Sampled = true, Alone = true },
            new ListFill("effects", Rider.Status, 0.35) { Sampled = true },
            new ListFill("effects", Rider.Cast, 0.35) { Sampled = true },
        },
        Relay = 0.35,
        Carrier = Root,
        Budget = v =>
        {
            var echoes = Count0(v.Num("area.scatter")) + Count0(v.Num("area.pulses"));
            var per = v.Sets("max_targets") ? Math.Min(MorphArena.Bodies, Math.Max(0, Math.Round(v.Num("max_targets")))) : MorphArena.Bodies;
            var reached = (1 + echoes) * per;
            var seconds = Math.Max(Count0(v.Num("area.scatter")) * v.Rule("delivery.aftershock_delay", 0.15), Count0(v.Num("area.pulses")) * v.Rule("delivery.pulse_interval", 0.4));
            var markers = new Dictionary<string, double>();
            if (v.Fills("effects+status"))
            {
                // Each body reached wears the mark for its seconds after the last strike that reached it, ticking on.
                seconds += MorphLayer.MarkSeconds;
                markers["status"] = reached;
                markers["status_tick"] = reached * (Math.Ceiling(MorphLayer.MarkSeconds / MorphLayer.MarkInterval) + 1);
            }
            if (v.Fills("effects+cast"))
            {
                markers["cast"] = reached;
                markers["cast_run"] = reached;
            }
            if (v.Fills(MorphLayer.Relay))
            {
                // The relay casts once, its follow-up a beat later, and the proc answers both casts.
                seconds = Math.Max(seconds, MorphLayer.FollowDelay);
                markers["relay"] = 1;
                markers["follow_up"] = 1;
                markers["proc"] = 2;
            }
            return new Budget(reached, 0, 0, seconds + View.Dt, $"1 strike and {echoes} echoes, each on at most {per} bodies") { Markers = markers };
        },
        States = v =>
        {
            var shape = v.Word("shape");
            var states = new List<Statement> { new("area", shape != "line", $"shape {shape}") };
            states.AddRange(ShapeStates(shape));
            // The hit list's damage strikes with the weave's Thread (ember, whose ailment is burning); a drain standing alone deals none.
            var hits = v.Fills("effects");
            var because = hits ? "the hit list deals damage with the ember Thread" : "the hit list holds a drain alone";
            states.Add(new Statement("strikes", hits, because));
            states.Add(new Statement("affliction", hits, because));
            states.Add(new Statement("affliction:burning", hits, because));
            var marks = v.Fills("effects+status");
            var applies = marks ? "the hit list applies the marking status, which ticks" : "the hit list applies no status";
            states.Add(new Statement("status", marks, applies));
            states.Add(new Statement("status:" + MorphLayer.Mark, marks, applies));
            states.Add(new Statement("affliction:" + MorphLayer.Mark, marks, applies));
            states.Add(Landing(v));
            return states;
        },
        Exempt = new Dictionary<string, string> { ["occlusion"] = OpenArena },
        Alike = new[]
        {
            new Alike("shape", "sector", "cone", SameShape),
            new Alike("shape", "circle", "square", NoBodyBetween),
            new Alike("shape", "arc", "wave", NoBodyBetween),
            new Alike("center", "focus", "point", "the press names no target, and an area's focus is its target, else its point"),
        },
        Unsampled = new Dictionary<string, string>
        {
            ["include_center"] = "a flag; the capability map proposes a shared spare list",
            ["require"] = "a node (a predicate per body)",
            ["melee.reach"] = "read only by weaves tagged melee; the slam shell is not",
        },
    };

    // ------------------------------------------------------------ chain

    public static readonly Family Chain = new()
    {
        Name = "chain",
        Effect = "chain",
        Shell = "missile_quick",
        Probe = "chain",
        Aim = new Vector2(6, 0),
        Attributes = Mana(),
        Parameters = new[]
        {
            Parameter.Count("count", Kinematics, "leaps", 0, 8, "shipped weave.chain.count (3, +2); no clamp") with { Attribute = "chain.count", Absent = 0.2 },
            Parameter.Axis("range", Geometry, "units", 0, 12, "shipped 6..8.75; no clamp") with { Default = 6.0, Absent = 0.3 },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4 },
            Parameter.Choice("pick", Targeting, PickWords, "Picks.Names") with { Default = "nearest", Absent = 0.4, Rolling = new[] { "random" } },
        },
        Defaults = Map(("count", 3.0), ("range", 6.0)),
        Lists = new[] { new ListFill("effects", Rider.Damage) },
        // A chain leaps from a struck body: it rides a plain flight's hit, as shipped chains do.
        Carrier = (_, node) => new JsonArray(CarrierFlight(Riders.Damage("carrier"), node)),
        Budget = v =>
        {
            var leaps = Math.Min(MorphArena.Bodies, Count0(v.Num("count")));
            return new Budget(1 + leaps, 1, 1, CarrierSeconds + View.Dt, $"the carrier's hit and at most {leaps} leaps");
        },
        States = v => new[] { new Statement("chain", true, "the graph chains"), Landing(v) },
    };

    // ------------------------------------------------------------ field

    public static readonly Family Field = new()
    {
        Name = "field",
        Effect = "field",
        Shell = "slam_quick",
        Probe = "surface",
        Aim = new Vector2(7, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Axis("radius", Geometry, "units", 0.25, 9, "shipped 0.75..3.44 on the node, 1.1..9 as area.radius; no clamp in the handler") with { Attribute = "area.radius", Absent = 0.2 },
            Parameter.Axis("duration", Timing, "seconds", 0.1, 30, "shipped 1.2..6 on the node, and as duration 0.8..30 on timed casts (600 only on a toggle's status, past the run's cap); floored at a tick") with { Attribute = "duration", Absent = 0.2 },
            Parameter.Axis("interval", Timing, "seconds", 0.05, 2, "shipped 0.18..1; floored at a tick") with { Default = 0.5, Absent = 0.2 },
            Parameter.Choice("shape", Geometry, ShapeNames, "the Shapes registry") with { Default = "circle", Absent = 0.5, Live = Map(("radius", 4.0)) },
            Parameter.Axis("angle", Geometry, "degrees", 5, 360, "shipped 75, scaled by area.angle") with { Absent = 0.6, Live = Map(("shape", "sector"), ("radius", 4.0)) },
            Parameter.Axis("width", Geometry, "units", 0.1, 8, "never shipped on a field") with { Absent = 0.6, Live = Map(("shape", "bar"), ("radius", 4.0)) },
            Parameter.Axis("inner", Geometry, "fraction of the radius", 0, 1, "never shipped on a field; clamped 0..1") with { Absent = 0.6, Live = Map(("radius", 4.0)) },
            Parameter.Axis("delay", Timing, "seconds", 0, 2, "never shipped as a number; floored 0") with { Absent = 0.5 },
            Parameter.Axis("exposure", Timing, "seconds", 0, 2, "never shipped; floored 0") with { Absent = 0.6 },
            Parameter.Axis("pull", Kinematics, "units a second", -5, 5, "shipped 2.5; negative pushes") with { Absent = 0.6 },
            Parameter.Axis("drift", Kinematics, "units a second", -4, 4, "never shipped") with { Absent = 0.7 },
            Parameter.Axis("seek", Kinematics, "units a second", 0, 4, "shipped 1.72; floored 0") with { Absent = 0.7 },
            Parameter.Axis("rotate", Kinematics, "degrees a second", -360, 360, "never shipped") with { Absent = 0.7, Pair = (0.0, 360.0), Live = Map(("shape", "sector"), ("angle", 60.0), ("radius", 4.0)) },
            Parameter.Count("pulses", Timing, "pulses", 1, 10, "never shipped; floored 1, absent unlimited") with { Absent = 0.5 },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("radius", 8.0)) },
            Parameter.Choice("occlusion", Geometry, Occlusions, "blocked or free") with { Default = "blocked", Absent = 0.5 },
        },
        Defaults = Map(("radius", 2.5), ("duration", 2.0), ("interval", 0.5)),
        Lists = new[]
        {
            new ListFill("pulse", Rider.Damage),
            new ListFill("on_enter", Rider.Damage, 0.5),
            new ListFill("on_exit", Rider.Damage, 0.5),
            new ListFill("on_tick", Rider.Marker, 0.5),
            new ListFill("on_land", Rider.Marker, 0.5),
            new ListFill("on_end", Rider.Marker, 0.7),
        },
        Carrier = Root,
        Budget = v =>
        {
            var b = MorphArena.Bodies;
            var duration = Math.Max(View.Dt, v.Num("duration"));
            var interval = Math.Max(View.Dt, v.Num("interval"));
            var delay = Math.Max(0, v.Num("delay", 0));
            var natural = Math.Floor(duration / interval) + 1;
            var pulses = v.Sets("pulses") ? Math.Min(natural, Math.Max(1, Math.Round(v.Num("pulses")))) : natural;
            var moves = v.Num("pull", 0) != 0 || v.Num("drift", 0) != 0 || v.Num("seek", 0) != 0 || v.Num("rotate", 0) != 0;
            // Bodies enter once each while nothing moves; with a pull, a drift, a seek or a turn, at most once a tick each.
            var transitions = moves ? b * ((delay + duration) * MorphArena.TicksPerSecond + 2) : b;
            var damage = pulses * b + (v.Fills("on_enter") ? transitions : 0) + (v.Fills("on_exit") ? transitions : 0);
            // A field lands after its delay and pulses on the first tick past each interval; it ends at its last pulse or
            // when its duration is out, whichever comes first, on the tick after.
            var spacing = Math.Ceiling(interval * MorphArena.TicksPerSecond - 1e-9) * View.Dt;
            var lasts = v.Sets("pulses") ? Math.Min(duration, (Math.Max(1, Math.Round(v.Num("pulses"))) - 1) * spacing) : duration;
            return new Budget(damage, 1, 1, delay + lasts + View.Dt, $"{pulses} pulses on at most {b} bodies and {transitions} entries or exits")
            {
                Markers = new Dictionary<string, double> { ["on_end"] = 1, ["on_land"] = 1, ["on_tick"] = pulses },
            };
        },
        States = v =>
        {
            var states = new List<Statement>
            {
                new("area", true, "a field"),
                new("surface", true, "a field"),
                new("surface:standing", true, "a field that does not follow"),
                new("telegraph", v.Num("delay", 0) > 0, $"delay {v.Num("delay", 0):0.##}"),
                Landing(v),
            };
            states.AddRange(ShapeStates(v.Word("shape")));
            return states;
        },
        // A field placed after a delay lands when the delay is over; one placed at once never lands.
        Lands = v => (v.Num("delay", 0) > 0, $"delay {v.Num("delay", 0):0.##}"),
        Exempt = new Dictionary<string, string> { ["occlusion"] = OpenArena },
        Alike = new[]
        {
            new Alike("shape", "sector", "cone", SameShape),
            new Alike("shape", "line", "band", NoBodyBetween),
            new Alike("shape", "bar", "arc", NoBodyBetween),
        },
        Unsampled = new Dictionary<string, string>
        {
            ["follow"] = "a flag; the capability map proposes an anchor choice",
            ["hit_once"] = "a flag; the capability map proposes a rehit lockout axis",
            ["rearm_on_exit"] = "a flag",
            ["radius_curve"] = "a block of literals; the capability map proposes an envelope node",
            ["segments"] = "a block of literals; the capability map proposes a cascade instead",
            ["domain"] = "a node (modifiers)",
            ["domain_filter"] = "read only with a domain",
            ["exclusive"] = "a key",
            ["look"] = "presentation only",
        },
    };

    // ------------------------------------------------------------ move

    private static readonly string[] Shoves = { "knockback", "pull", "buffet" };

    public static readonly Family Move = new()
    {
        Name = "move",
        Effect = "move",
        Shell = "slam_quick",
        Probe = "displaces",
        Aim = new Vector2(6, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Choice("kind", Kinematics, new[] { "teleport", "dash", "leap", "blink", "knockback", "pull", "buffet" }, "the handler's kinds but recall, which needs a mark an earlier cast left") with { Default = "teleport", Rolling = new[] { "buffet" } },
            Parameter.Axis("distance", Kinematics, "units", 0, 10, "shipped 0.94..8.13; scaled by knockback for knockbacks and buffets") with { Default = 4.0, Absent = 0.2 },
            Parameter.Axis("speed", Kinematics, "units a second", 0.5, 40, "shipped 24; floored 0.5; absent, a dash is instant") with { Absent = 0.4, Live = Map(("kind", "dash")) },
            Parameter.Axis("width", Geometry, "units", 0, 3, "never shipped; the dash's corridor, floored 0") with { Default = 1.5, Absent = 0.4, Live = Map(("kind", "dash")) },
            Parameter.Axis("air_time", Timing, "seconds", 0.05, 1.5, "shipped 0.55; floored at a tick") with { Default = 0.6, Absent = 0.4, Live = Map(("kind", "leap")) },
            Parameter.Axis("arc", Kinematics, "units", 0, 4, "shipped 2.2; floored 0") with { Default = 2.0, Absent = 0.4, Live = Map(("kind", "leap")) },
            Parameter.Axis("delay", Timing, "seconds", 0, 1.5, "never shipped; a blink's wait, floored 0") with { Absent = 0.5, Live = Map(("kind", "blink")) },
            Parameter.Choice("anchor", Kinematics, new[] { "from", "caster", "point" }, "the handler's anchors") with { Default = "from", Absent = 0.4, Live = Map(("kind", "knockback")) },
            Parameter.Axis("travel", Causality, "units", 0.25, 4, "never shipped on a move; a literal, floored 0.25") with { Default = 2.0, Absent = 0.5, Live = Map(("kind", "dash")) },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("kind", "dash")) },
        },
        Defaults = Map(("kind", "dash"), ("distance", 6.0), ("speed", 20.0)),
        Lists = new[]
        {
            new ListFill("on_contact", Rider.Damage),
            new ListFill("on_collide", Rider.Damage, 0.5),
            new ListFill("on_travel", Rider.Marker, 0.5),
            new ListFill("on_land", Rider.Marker, 0.5),
            new ListFill("on_end", Rider.Marker, 0.7),
        },
        // Moves of the source sit at the root; shoves move a struck body, so they ride the hit of a fixed area around the aim.
        Carrier = (p, node) => Shoves.Contains(p.Value("kind") as string ?? "teleport")
            ? new JsonArray(new JsonObject { ["type"] = "area", ["radius"] = 2.5, ["effects"] = new JsonArray(Riders.Damage("carrier"), node) })
            : new JsonArray(node),
        Budget = v =>
        {
            var b = MorphArena.Bodies;
            var kind = v.Word("kind");
            var distance = Math.Max(0, v.Num("distance"));
            if (Shoves.Contains(kind))
            {
                // A shove starts its body at the speed that carries it the distance (a knockback's and a buffet's scaled
                // by the weave's knockback; a pull's never longer) and moves it from the next tick until the impulse
                // falls under the rule's floor, as the displacement system works it out; collisions run once per body.
                var shoved = kind == "pull" ? distance : distance * v.Attribute("knockback", 1);
                var settle = shoved <= 0.01 ? 0 : DisplacementSystem.SettleSeconds(v.Sim!, DisplacementSystem.SpeedFor(v.Sim!, (float)shoved));
                return new Budget(2 * b, 0, 0, settle + 2 * View.Dt, $"the carrier's hit and one collision per shoved body; a shove of {shoved:0.##} settles in {settle:0.##} s");
            }
            var reach = Math.Min(distance, 6);
            var seconds = kind switch
            {
                "dash" when v.Sets("speed") => Math.Max(View.Dt, reach / Math.Max(0.5, v.Num("speed"))),
                "leap" => Math.Max(View.Dt, v.Num("air_time")),
                "blink" => Math.Max(0, v.Num("delay", 0)),
                _ => 0,
            };
            var travel = Math.Max(0.25, v.Num("travel"));
            return new Budget(b, 0, 0, seconds + View.Dt, $"a {kind} strikes each body in its way once")
            {
                Markers = new Dictionary<string, double> { ["on_end"] = 1, ["on_land"] = 1, ["on_travel"] = Math.Ceiling(reach / travel) + 1 },
            };
        },
        States = v =>
        {
            var kind = v.Word("kind");
            var shove = Shoves.Contains(kind);
            return new List<Statement>
            {
                new("displaces", true, $"kind {kind}"),
                new("dash", kind == "dash", $"kind {kind}"),
                new("leap", kind == "leap", $"kind {kind}"),
                new("shove", shove, $"kind {kind}"),
                // A shove rides the hit of the carrier, a circle of an area.
                new("area", shove, $"kind {kind}{(shove ? ", on the carrier's circle" : "")}"),
                new("shape:circle", shove, $"kind {kind}{(shove ? ", on the carrier's circle" : "")}"),
                Landing(v),
            };
        },
        // Of the moves, a leap lands: it arcs through the air and comes down where it aimed.
        Lands = v => (v.Word("kind") == "leap", $"kind {v.Word("kind")}"),
        Exempt = new Dictionary<string, string>
        {
            ["arc"] = "a leap's height shows only in the air: it lands where and when it would have, and its body is untargetable throughout",
        },
        Alike = new[]
        {
            new Alike("filter", "ally", "ally_or_self", "a dash strikes the bodies in its way but never its own mover, the caster"),
            new Alike("filter", "not_self", "all", "a dash strikes the bodies in its way but never its own mover, the caster"),
        },
        // The distance row runs a dash; a shove reads distance through its own path (an impulse, scaled by knockback).
        Rows = new[] { new Row("distance", "shove", Map(("kind", "knockback"))) },
        Unsampled = new Dictionary<string, string>
        {
            ["who"] = "fixed by the carrier: the source for moves, the struck body for shoves",
            ["phase"] = "a flag; the capability map proposes reading phasing",
            ["behind"] = "a flag; the capability map proposes an arrival bearing",
            ["telegraph"] = "a flag; the capability map proposes warning seconds",
            ["mark"] = "read only by recall",
            ["status"] = "a node (a status id)",
        },
    };

    // ------------------------------------------------------------ scatter

    public static readonly Family Scatter = new()
    {
        Name = "scatter",
        Effect = "scatter",
        Shell = "slam_quick",
        Probe = "storm",
        Aim = new Vector2(6, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            // Compared up to six strikes, fewer than the enemies in reach: past them the rest fall at random, on a stream keyed by the weave.
            Parameter.Count("count", Geometry, "strikes", 0, 16, "shipped 5 as area.strikes; no clamp") with { Attribute = "area.strikes", Absent = 0.2, Pair = (0.0, 6.0) },
            Parameter.Axis("radius", Geometry, "units", 0.1, 8, "shipped 4; floored 0.1, default twice area.radius") with { Absent = 0.3 },
            Parameter.Axis("interval", Timing, "seconds", 0, 0.5, "shipped 0.15; floored 0") with { Default = 0.0, Absent = 0.4 },
            Parameter.Choice("at", Targeting, new[] { "random", "enemies" }, "the handler's placements") with { Default = "random", Absent = 0.4, Rolling = new[] { "random" } },
            Parameter.Axis("jitter", Geometry, "units", 0, 2, "shipped 0.6; floored 0") with { Default = 0.0, Absent = 0.5, Live = Map(("at", "enemies")) },
            Parameter.Axis("telegraph", Timing, "seconds", 0, 1.5, "shipped 0.5; floored 0") with { Default = 0.0, Absent = 0.5 },
            Parameter.Axis("telegraph_radius", Geometry, "units", 0.1, 4, "never shipped; default area.radius") with { Absent = 0.6, Live = Map(("telegraph", 0.5)) },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("at", "enemies"), ("count", 1.0), ("radius", 6.5)) },
        },
        // Strikes under the nearest bodies: random placement would roll differently for every weave, so the default places none at random.
        Defaults = Map(("count", 5.0), ("radius", 4.0), ("at", "enemies")),
        Lists = new[] { new ListFill("effects", Rider.Burst) },
        Carrier = Root,
        Budget = v =>
        {
            var strikes = Count0(v.Num("count"));
            var interval = Math.Max(0, v.Num("interval", 0));
            var telegraph = Math.Max(0, v.Num("telegraph", 0));
            var marks = telegraph > 0 ? strikes : 0;
            return new Budget(strikes * MorphArena.Bodies, marks, marks, interval * Math.Max(0, strikes - 1) + telegraph + View.Dt, $"{strikes} strikes on at most {MorphArena.Bodies} bodies each, {marks} marks");
        },
        States = v => new[]
        {
            new Statement("storm", true, "a scatter"),
            new Statement("telegraph", v.Num("telegraph", 0) > 0, $"telegraph {v.Num("telegraph", 0):0.##}"),
            Landing(v),
        },
        // A telegraphed strike is marked where it will land and lands when the mark is done: a delayed placement.
        Lands = v => (v.Num("telegraph", 0) > 0, $"telegraph {v.Num("telegraph", 0):0.##}"),
        Exempt = new Dictionary<string, string>
        {
            ["telegraph_radius"] = "the mark's radius is what clients draw; strikes land as they would have",
        },
        // The filter row lays one strike, under the body nearest the focus the filter admits.
        Alike = Alike.Among("filter", "one strike falls under the admitted body nearest the focus, and an enemy stands at the focus", "enemy", "not_self", "all")
            .Append(new Alike("filter", "ally", "ally_or_self", "one strike falls under the admitted body nearest the focus, and the ally stands nearer it than the caster"))
            .ToArray(),
    };

    // ------------------------------------------------------------ cascade

    public static readonly Family Cascade = new()
    {
        Name = "cascade",
        Effect = "cascade",
        Shell = "slam_quick",
        Probe = "cascade",
        // Half a unit off the facing, so a cascade stepping out from the caster through the aim leaves the facing.
        Aim = new Vector2(2, 0.5f),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Count("count", Geometry, "placements", 0, 10, "shipped 3 as area.cascade (+2 from a glyph); no clamp") with { Attribute = "area.cascade", Absent = 0.2 },
            Parameter.Choice("dir", Geometry, new[] { "forward", "backward", "axis", "out", "random" }, "the handler's directions") with { Default = "forward", Absent = 0.4, Rolling = new[] { "random" } },
            Parameter.Axis("step", Geometry, "units", 0, 4, "shipped 2; no clamp, default 1.5 x area.radius") with { Absent = 0.3 },
            Parameter.Axis("scale_step", Geometry, "fraction a step", -0.5, 1, "shipped 0.15; the scale floors 0.1") with { Default = 0.0, Absent = 0.5 },
            Parameter.Axis("damage_step", Payload, "fraction a step", -0.5, 1, "never shipped; the scale floors 0") with { Default = 0.0, Absent = 0.5 },
            Parameter.Axis("interval", Timing, "seconds", 0, 0.5, "shipped 0.18..0.2; each wait floors 0") with { Default = 0.15, Absent = 0.3 },
            Parameter.Axis("interval_step", Timing, "seconds a step", -0.2, 0.2, "never shipped") with { Default = 0.0, Absent = 0.5 },
        },
        Defaults = Map(("count", 4.0), ("step", 2.0)),
        Lists = new[] { new ListFill("effects", Rider.Burst) },
        Carrier = Root,
        Budget = v =>
        {
            var extra = Count0(v.Num("count"));
            var interval = v.Num("interval");
            var step = v.Num("interval_step");
            var seconds = 0.0;
            for (var i = 1; i <= extra; i++)
            {
                seconds += Math.Max(0, interval + step * (i - 1));
            }
            return new Budget((1 + extra) * MorphArena.Bodies, 0, 0, seconds + View.Dt, $"{1 + extra} placements on at most {MorphArena.Bodies} bodies each");
        },
        States = v => new[] { new Statement("cascade", Count0(v.Num("count")) >= 1, $"count {Count0(v.Num("count"))}: a cascade repeats only past its first placement"), Landing(v) },
    };

    // ------------------------------------------------------------ aura

    public static readonly Family Aura = new()
    {
        Name = "aura",
        Effect = "aura",
        Shell = "burst",
        Probe = "engagement",
        Aim = new Vector2(0, 0),
        Attributes = Mana(("cast.time", 0)),
        Parameters = new[]
        {
            Parameter.Axis("radius", Geometry, "units", 0.25, 9, "shipped weave.area.radius (1.1..9 as area.radius); no clamp in the handler") with { Attribute = "area.radius", Absent = 0.2 },
            Parameter.Axis("duration", Timing, "seconds", 0.1, 6, "never shipped as a number (the shipped aura toggles); floored at a tick") with { Attribute = "duration", Absent = 0.2 },
            Parameter.Axis("interval", Timing, "seconds", 0.05, 2, "shipped 1; floored at a tick") with { Default = 1.0, Absent = 0.2 },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("radius", 5.5)) },
            Parameter.Choice("occlusion", Geometry, Occlusions, "blocked or free") with { Default = "blocked", Absent = 0.5 },
        },
        Defaults = Map(("radius", 3.0), ("duration", 2.0), ("interval", 0.5)),
        Lists = new[]
        {
            new ListFill("pulse", Rider.Damage),
            new ListFill("on_enter", Rider.Damage, 0.5),
            new ListFill("on_exit", Rider.Damage, 0.5),
            new ListFill("on_tick", Rider.Marker, 0.5),
            new ListFill("on_end", Rider.Marker, 0.7),
        },
        Carrier = Root,
        Budget = v =>
        {
            var b = MorphArena.Bodies;
            var duration = Math.Max(View.Dt, v.Num("duration"));
            var pulses = Math.Floor(duration / Math.Max(View.Dt, v.Num("interval"))) + 1;
            var damage = pulses * b + (v.Fills("on_enter") ? b : 0) + (v.Fills("on_exit") ? b : 0);
            return new Budget(damage, 1, 1, duration + View.Dt, $"{pulses} pulses on at most {b} bodies, each body entering once")
            {
                Markers = new Dictionary<string, double> { ["on_end"] = 1, ["on_tick"] = pulses },
            };
        },
        States = v => new[] { new Statement("engagement", true, "an aura"), Landing(v) },
        Exempt = new Dictionary<string, string> { ["occlusion"] = OpenArena },
        Unsampled = new Dictionary<string, string>
        {
            ["toggle"] = "a flag; the capability map moves toggling to the cast layer",
            ["domain"] = "a node (modifiers)",
            ["domain_filter"] = "read only with a domain",
            ["key"] = "a key",
            ["look"] = "presentation only",
        },
    };

    // ------------------------------------------------------------ tether

    public static readonly Family Tether = new()
    {
        Name = "tether",
        Effect = "tether",
        Shell = "missile_quick",
        Probe = "tether",
        Aim = new Vector2(6, 0),
        Attributes = Mana(),
        Parameters = new[]
        {
            Parameter.Choice("link", Targeting, new[] { "point", "target" }, "the handler's links but owner, which needs a caster that has one") with { Default = "target", Absent = 0.2 },
            Parameter.Axis("width", Geometry, "units", 0.05, 8, "shipped 0.8; floored 0.05") with { Default = 1.0, Absent = 0.3 },
            Parameter.Axis("duration", Timing, "seconds", 0.1, 6, "shipped weave.duration; floored at a tick") with { Attribute = "duration", Absent = 0.2 },
            Parameter.Axis("interval", Timing, "seconds", 0.05, 2, "shipped 0.5; floored at a tick") with { Default = 0.5, Absent = 0.2 },
            Parameter.Axis("max_length", Geometry, "units", 0, 15, "shipped 12; no clamp, absent unlimited") with { Absent = 0.5 },
            Parameter.Choice("filter", Targeting, FilterWords, "Filters.Names") with { Default = "enemy", Absent = 0.4, Live = Map(("width", 2.6)) },
        },
        Defaults = Map(("link", "point"), ("duration", 2.0), ("interval", 0.5)),
        Lists = new[]
        {
            new ListFill("pulse", Rider.Damage),
            new ListFill("on_end", Rider.Marker, 0.7),
        },
        // A tether to a target needs a struck body: it rides a plain flight's hit, as a chain does.
        Carrier = (p, node) => (p.Value("link") as string ?? "target") == "target" ? new JsonArray(CarrierFlight(node)) : new JsonArray(node),
        Budget = v =>
        {
            var duration = Math.Max(View.Dt, v.Num("duration"));
            var pulses = Math.Floor(duration / Math.Max(View.Dt, v.Num("interval"))) + 1;
            var carried = v.Word("link") == "target";
            return new Budget(pulses * MorphArena.Bodies, carried ? 2 : 1, carried ? 2 : 1, duration + (carried ? CarrierSeconds : 0) + View.Dt, $"{pulses} pulses on at most {MorphArena.Bodies} bodies")
            {
                Markers = new Dictionary<string, double> { ["on_end"] = 1 },
            };
        },
        States = v =>
        {
            // A tether to a target rides a plain flight's hit: the carrier's flight is what the projectile probes see.
            var carried = v.Word("link") == "target";
            var because = carried ? "link target, on the carrier's flight" : $"link {v.Word("link")}, with no carrier";
            return new[]
            {
                new Statement("tether", true, "a tether"),
                new Statement("projectile", carried, because),
                new Statement("flight", carried, because),
                new Statement("flight:spends", carried, because),
                new Statement("fire:fan", carried, because),
                new Statement("shape:circle", carried, because),
                Landing(v),
            };
        },
        Alike = new[]
        {
            new Alike("filter", "ally", "ally_or_self", "a tether strikes the bodies it crosses but never the one it runs from, the caster"),
            new Alike("filter", "not_self", "all", "a tether strikes the bodies it crosses but never the one it runs from, the caster"),
        },
        Unsampled = new Dictionary<string, string> { ["look"] = "presentation only" },
    };

    // ------------------------------------------------------------ summon, deploy, spawn: who acts after the press

    /// <summary>
    /// A summon or a deploy: bodies of the crowd's dummy pattern for the caster, so they cast nothing and strike no one.
    /// Their lifetime is the axis <paramref name="lifetime"/> (zero raises them for good), their cap <c>max_active</c>.
    /// </summary>
    private static Family Raising(string name, string probe, string lifetime, string lifetimeSource, IReadOnlyDictionary<string, string> unsampled) => new()
    {
        Name = name,
        Effect = name,
        Shell = "slam_quick",
        Probe = probe,
        Aim = new Vector2(6, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Count("count", Agency, "bodies", 1, 8, "shipped 1 to randi(4, 6) and weave.summon.count; clamped 1..rule minions.max_per_cast") with { Attribute = "summon.count", Absent = 0.2 },
            // Compared with bodies that outlast the cooldown, so the second press finds the first press's still standing.
            Parameter.Count("max_active", Agency, "bodies", 1, 12, "shipped 1..12 and weave.summon.max; floored 1") with { Attribute = "summon.max", Absent = 0.2, Live = Map(("count", 4.0), (lifetime, 6.0)) },
            Parameter.Axis(lifetime, Agency, "seconds", 0, 20, lifetimeSource) with { Attribute = "duration", Absent = 0.2, Zero = 0.2, Pair = (0.5, 6.0) },
        },
        Defaults = Map(("count", 2.0), ("max_active", 8.0), (lifetime, 2.0)),
        Lists = new[]
        {
            new ListFill("on_birth", Rider.Marker, 0.7),
            new ListFill("on_death", Rider.Marker, 0.5),
        },
        Carrier = (_, node) =>
        {
            node["pattern"] = MorphArena.Dummy;
            node["group"] = RaisedGroup;
            return new JsonArray(node);
        },
        // Pressed twice, the second press after the first one's cooldown, where the cap on what stands bites.
        Presses = 2,
        Group = RaisedGroup,
        Budget = v =>
        {
            var count = Math.Clamp((int)Math.Round(v.Num("count")), 1, (int)v.Rule("minions.max_per_cast", 32));
            var cap = Math.Max(1, (int)Math.Round(v.Num("max_active")));
            var life = v.Num(lifetime);
            var raised = 2 * count;
            return new Budget(0, cap, raised, life > 0 ? life + View.Dt : 0, $"{count} bodies a press over two presses, at most {cap} at once, for {(life > 0 ? $"{life:0.##} s" : "good")}")
            {
                Markers = new Dictionary<string, double> { ["on_birth"] = raised, ["on_death"] = raised },
            };
        },
        States = v =>
        {
            var states = new List<Statement> { new(probe, true, $"a {name}"), Landing(v) };
            if (name == "summon")
            {
                states.Add(new Statement("minion", true, "a summon"));
            }
            else
            {
                states.Add(new Statement("construct:massed", Math.Round(v.Num("count")) > 1, $"count {v.Num("count")}"));
            }
            return states;
        },
        Unsampled = unsampled,
    };

    public static readonly Family Summon = Raising("summon", "summon", "duration",
        "shipped 0 (for good) to 20 and weave.duration; zero raises a body for good",
        new Dictionary<string, string>
        {
            ["pattern"] = "fixed: the crowd's dummy, which casts nothing",
            ["pool"] = "a node",
            ["group"] = "fixed: one group, so max_active counts every body",
            ["pool_group"] = "a key",
            ["exclusive_group"] = "a key",
            ["place"] = "a block",
            ["behavior"] = "a key; the rule minions.behavior by default",
            ["inherit"] = "a flag",
            ["contract"] = "a block (reserve, respawn, toggle)",
            ["decay"] = "a block",
            ["waves"] = "a block",
            ["presence"] = "a node (modifiers)",
            ["replenish"] = "a block",
            ["from_corpse"] = "a choice the crowd scenario has no corpses for",
            ["search_radius"] = "read only from corpses",
        });

    public static readonly Family Deploy = Raising("deploy", "construct", "lifetime",
        "shipped weave.duration 8..20; zero places a construct for good",
        new Dictionary<string, string>
        {
            ["pattern"] = "fixed: the crowd's dummy, which casts nothing",
            ["group"] = "fixed: one group, so max_active counts every body",
            ["place"] = "a block",
            ["inherit_caster"] = "a flag",
            ["intercept"] = "a block",
            ["decay"] = "a block",
            ["presence"] = "a node (modifiers)",
        });

    public static readonly Family Spawn = new()
    {
        Name = "spawn",
        Effect = "spawn",
        Shell = "slam_quick",
        Probe = "construct",
        Aim = new Vector2(6, 0),
        Attributes = Stamina(),
        Parameters = new[]
        {
            Parameter.Count("count", Agency, "bodies", 1, 32, "shipped 2; clamped 1..32") with { Default = 1.0, Absent = 0.2 },
            Parameter.Axis("lifetime", Agency, "seconds", 0.1, 6, "never shipped; absent, a spawn has no end") with { Absent = 0.4, Pair = (0.5, 6.0) },
        },
        Defaults = Map(("count", 2.0), ("lifetime", 2.0)),
        Lists = Array.Empty<ListFill>(),
        Carrier = (_, node) =>
        {
            node["pattern"] = MorphArena.Dummy;
            return new JsonArray(node);
        },
        Budget = v =>
        {
            var count = Math.Clamp((int)Math.Round(v.Num("count")), 1, 32);
            var life = v.Num("lifetime", 0);
            return new Budget(0, count, count, life > 0 ? life + View.Dt : 0, $"{count} bodies {(v.Sets("lifetime") ? $"for {life:0.##} s" : "with no end")}");
        },
        States = v =>
        {
            return new List<Statement>
            {
                new("construct", true, "a spawn"),
                new("construct:massed", Math.Round(v.Num("count")) > 1, $"count {v.Num("count")}"),
                Landing(v),
            };
        },
        Unsampled = new Dictionary<string, string>
        {
            ["pattern"] = "fixed: the crowd's dummy",
            ["as_corpse"] = "a flag",
        },
    };

    /// <summary>Every family, in the order the test runs them.</summary>
    public static readonly Family[] All = { Projectile, Area, Chain, Field, Move, Scatter, Cascade, Aura, Tether, Summon, Deploy, Spawn };

    public static Family Named(string name) => All.First(f => f.Name == name);
}

/// <summary>The riders the families put in the lists they fill, tagged or named by the list so a hit or a marker says where it came from.</summary>
internal static class Riders
{
    public const string Prefix = "morph.";

    public static JsonObject Damage(string list) => new()
    {
        ["type"] = "damage",
        ["amount"] = 10,
        ["can_crit"] = false,
        ["tags"] = new JsonArray(Prefix + list),
    };

    /// <summary>A counted marker, carrying the effect depth it runs at.</summary>
    public static JsonObject Marker(string list) => new()
    {
        ["type"] = "event",
        ["name"] = Prefix + list,
        ["data"] = new JsonObject { ["depth"] = "depth" },
    };

    /// <summary>The life a drain asks to take from the body it reaches.</summary>
    public const int Drained = 5;

    /// <summary>
    /// A drain: either direct resource writer told to lower the reached body's life, by a formula, since validation refuses a
    /// literal. The handler refuses it (life falls only through the damage ladder) and logs the refusal, so the run
    /// loses no life to it; were the refusal lost, the pipeline check would see the life it takes with no hit.
    /// </summary>
    public static JsonObject Drain(string type) => new() { ["type"] = type, ["resource"] = "life", ["amount"] = $"-min({Drained}, target.life)" };

    /// <summary>A status rider: a marker of the depth it is applied at, then the marking status, whose every tick runs a marker of the depth the tick runs at.</summary>
    public static JsonObject[] Status() => new[]
    {
        Marker("status"),
        new JsonObject
        {
            ["type"] = "apply_status",
            ["status"] = MorphLayer.Mark,
            ["fixed_duration"] = MorphLayer.MarkSeconds,
            ["on_status_tick"] = new JsonArray(Marker("status_tick")),
        },
    };

    /// <summary>A cast rider: a marker of the depth the cast effect runs at, then a cast of a weave whose graph marks the depth it runs at.</summary>
    public static JsonObject[] Cast() => new[]
    {
        Marker("cast"),
        new JsonObject { ["type"] = "cast", ["weave"] = MorphLayer.Echo },
    };

    /// <summary>
    /// A small burst of damage where a plain list runs (scattered strikes, cascade placements): its radius scales with
    /// the placement, and it strikes every body it covers, so where a placement falls shows whoever stands there.
    /// </summary>
    public static JsonObject Burst(string list) => new() { ["type"] = "area", ["radius"] = 1, ["filter"] = "all", ["effects"] = new JsonArray(Damage(list)) };

    public static JsonArray Of(ListFill list) => list.Rider switch
    {
        Rider.Damage => new JsonArray(Damage(list.Name)),
        Rider.Marker => new JsonArray(Marker(list.Name)),
        Rider.Drain => new JsonArray(Drain("resource"), Drain("heal")),
        Rider.Status => new JsonArray(Status()),
        Rider.Cast => new JsonArray(Cast()),
        _ => new JsonArray(Burst(list.Name)),
    };
}
