using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// The crowd scenario every point runs in: a seeded arena, the point's caster at the origin pressing its weave once,
/// aimed east, and seven still dummies: five in a line two units apart from two units out, and two flanking three units
/// north and south at four units east. The run records every event the press causes, ticks through the point's
/// expected lifetime and a quiet second, and checks what it may: that the press paid, that every life lost went through
/// the one damage pipeline, that what it made ended or may stay, and that it stayed within the caps its parameters allow.
/// </summary>
internal static class MorphArena
{
    public const int TicksPerSecond = 20;

    public const string Dummy = "morph_dummy";
    public const string Red = "morph_red";
    public const string Blue = "morph_blue";

    public static readonly Vector3 Origin = new(20, 20, 0);

    public static readonly Vector2[] Crowd = { new(2, 0), new(4, 0), new(6, 0), new(8, 0), new(10, 0), new(4, 3), new(4, -3) };

    /// <summary>Bodies one press can strike: the crowd and the caster itself (filters such as all reach it).</summary>
    public static int Bodies => Crowd.Length + 1;

    /// <summary>Seconds past the expected lifetime before what was due to end must be gone.</summary>
    public const double Margin = 1.0;

    /// <summary>Seconds of quiet the arena must keep after that.</summary>
    public const double Quiet = 1.0;

    /// <summary>The longest a run may simulate; a point expected to live longer is a finding of its own.</summary>
    public const double MaxSeconds = 300;

    public static Outcome Run(ContentRegistry content, Point point)
    {
        var outcome = new Outcome();
        var log = new Log();
        var sim = ZoneBuilder.CreateArena(content, point.Seed, log: log, ticksPerSecond: TicksPerSecond);
        var factory = sim.Service<PatternFactory>();
        var caster = factory.Spawn(sim, point.Caster, Origin, new SpawnOptions { Faction = Red });
        if (caster.IsNone)
        {
            outcome.Broke("runs", $"the caster pattern {point.Caster} did not spawn");
            return outcome;
        }
        var crowd = Crowd.Select(c => factory.Spawn(sim, Dummy, Origin + new Vector3(c.X, c.Y, 0), new SpawnOptions { Faction = Blue })).ToArray();
        foreach (var body in crowd.Prepend(caster))
        {
            sim.World.Remove<Behavior>(body);
            var s = sim.World.Get<Stats>(body)!;
            foreach (var resource in new[] { "life", "mana", "stamina" })
            {
                s.SetResource(resource, s.ResourceMax(resource));
            }
        }
        var runtime = sim.Service<WeaveRuntime>();
        var book = sim.World.Get<WeaveBook>(caster)!;
        var slot = book.Slots[0];
        var weave = runtime.Resolve(sim, slot, out var error);
        if (weave is null)
        {
            outcome.Broke("compiles", $"the caster's slot does not compile: {error}");
            return outcome;
        }
        var recorder = new Recorder(sim, caster, crowd, outcome, HandlersOf(weave));
        sim.Events.SubscribeAll(recorder.On);
        sim.Tick();
        var stats = sim.World.Get<Stats>(caster)!;
        var costs = runtime.Costs(weave, stats);
        var before = costs.Keys.ToDictionary(r => r, stats.Resource, StringComparer.Ordinal);
        var cooldown = weave.Cooldown(stats);
        var pending = sim.Scheduler.Pending;
        var aim = Origin + new Vector3(point.Family.Aim.X, point.Family.Aim.Y, 0);
        recorder.Arm();
        if (!runtime.TryBegin(sim, caster, slot, Entity.None, aim, Vector2.UnitX, out var reason))
        {
            outcome.Broke("pays", $"the press was refused: {reason}");
            return outcome;
        }
        recorder.Settle();

        // Pays: the cost left the caster's pools, the cooldown started, and the cast was published once.
        if (!costs.Values.Any(c => c > 0))
        {
            outcome.Broke("pays", "the weave costs nothing, so its payment cannot be seen");
        }
        foreach (var (resource, cost) in costs)
        {
            var paid = before[resource] - stats.Resource(resource);
            if (Math.Abs(paid - cost) > 1e-6)
            {
                outcome.Broke("pays", $"{resource} fell by {paid:0.###}, the cost is {cost:0.###}");
            }
        }
        if (cooldown <= 0)
        {
            outcome.Broke("pays", "the weave has no cooldown to start");
        }
        else if (book.IsReady(slot, sim.Clock.Time))
        {
            outcome.Broke("pays", $"the {cooldown:0.##} s cooldown did not start");
        }
        var casts = recorder.Casts.Count(k => k == weave.Key);
        if (casts != 1)
        {
            outcome.Broke("pays", $"WeaveCast was published {casts} times for one press");
        }

        // Terminates: through the expected lifetime and a margin, everything the press made that was due to end is gone,
        // nothing is still scheduled or carried, and a quiet second follows.
        var budget = point.Family.Budget(new View(point, content));
        outcome.Budget = budget;
        if (!budget.Finite)
        {
            outcome.Broke("bounded", $"the cap is unbounded: {budget.Why}");
        }
        var life = budget.Seconds + Margin;
        if (life > MaxSeconds)
        {
            outcome.Broke("terminates", $"the expected life of {budget.Seconds:0.#} s is past the run budget of {MaxSeconds} s: {budget.Why}");
            life = MaxSeconds;
        }
        Advance(sim, recorder, life);
        var overdue = recorder.Made.Where(e => sim.World.IsAlive(e) && recorder.DueToEnd(e)).ToList();
        if (overdue.Count > 0)
        {
            outcome.Broke("terminates", $"{Tally(overdue, recorder.KindOf)} the press made still stand {life:0.##} s after it, past their end; expected life {budget.Seconds:0.##} s");
        }
        if (sim.Scheduler.Pending > pending)
        {
            outcome.Broke("terminates", $"{sim.Scheduler.Pending - pending} scheduled actions are still pending {life:0.##} s after the press");
        }
        if (sim.World.CountOf<Displaced>() > 0)
        {
            outcome.Broke("terminates", $"{sim.World.CountOf<Displaced>()} bodies are still being moved {life:0.##} s after the press");
        }
        var hits = recorder.Hits;
        var marks = recorder.Marks.Values.Sum(m => m.Count);
        Advance(sim, recorder, Quiet);
        if (recorder.Hits != hits || recorder.Marks.Values.Sum(m => m.Count) != marks)
        {
            outcome.Broke("terminates", $"{recorder.Hits - hits} hits and {recorder.Marks.Values.Sum(m => m.Count) - marks} markers came in the quiet second after the expected life of {budget.Seconds:0.##} s");
        }

        // Left behind: at the end, everything the press made is gone, or is a summon under its owner's cap with a
        // lifetime or a contract. What overstayed its end was reported above.
        foreach (var message in recorder.LeftBehind(budget, overdue))
        {
            outcome.Broke("left behind", message);
        }

        // Bounded: resolutions, what stands at once, entities made and once-per-delivery lists within the point's caps.
        if (recorder.Hits > budget.Damage)
        {
            outcome.Broke("bounded", $"{recorder.Hits} hits from one press, past the cap of {budget.Damage:0.#} ({budget.Why})");
        }
        if (recorder.MaxLive > budget.Live)
        {
            outcome.Broke("bounded", $"{recorder.MaxLive} of what the press made stood at once, past the cap of {budget.Live:0.#} ({budget.Why})");
        }
        if (recorder.Made.Count > budget.Created)
        {
            outcome.Broke("bounded", $"{recorder.Made.Count} entities made by one press, past the cap of {budget.Created:0.#} ({budget.Why})");
        }
        foreach (var (list, cap) in budget.Markers)
        {
            if (recorder.Marks.TryGetValue(Riders.Prefix + list, out var m) && m.Count > cap)
            {
                outcome.Broke("bounded", $"{list} ran {m.Count} times, past the cap of {cap:0.#} ({budget.Why})");
            }
        }

        foreach (var line in log.Lines)
        {
            outcome.Broke("runs", line);
        }
        outcome.Hits = recorder.Hits;
        outcome.MaxLive = recorder.MaxLive;
        outcome.Created = recorder.Made.Count;
        outcome.Events = recorder.Lines.Select(l => l.Line).ToList();
        outcome.Observable = recorder.Observable();
        return outcome;
    }

    private static void Advance(Simulation sim, Recorder recorder, double seconds)
    {
        var ticks = (int)Math.Ceiling(seconds * TicksPerSecond - 1e-9);
        for (var i = 0; i < ticks; i++)
        {
            sim.Tick();
            recorder.Settle();
        }
    }

    /// <summary>The effect types in a compiled weave's graph, nested lists included: the handlers a life lost outside the pipeline could have come from.</summary>
    private static string HandlersOf(CompiledWeave weave)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["type"] is JsonValue v && v.TryGetValue<string>(out var type))
                    {
                        types.Add(type);
                    }
                    foreach (var (_, child) in o)
                    {
                        Walk(child);
                    }
                    break;
                case JsonArray a:
                    foreach (var child in a)
                    {
                        Walk(child);
                    }
                    break;
            }
        }
        foreach (var spec in weave.Specs)
        {
            types.Add(spec.Type);
            Walk(spec.Params);
        }
        return string.Join(", ", types);
    }

    /// <summary>"2 projectile, 1 field": entities counted by kind.</summary>
    private static string Tally(IEnumerable<Entity> entities, Func<Entity, string> kind) =>
        string.Join(", ", entities.GroupBy(kind).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Count()} {g.Key}"));

    /// <summary>What one run of a point did: the invariants it broke, its event log and what the crowd shows.</summary>
    internal sealed class Outcome
    {
        public List<(string Invariant, string Message)> Broken { get; } = new();
        public List<string> Events { get; set; } = new();
        public string Observable { get; set; } = "";
        public Budget? Budget { get; set; }
        public int Hits { get; set; }
        public int MaxLive { get; set; }
        public int Created { get; set; }

        public void Broke(string invariant, string message) => Broken.Add((invariant, message));
    }

    /// <summary>Warnings and errors the simulation logs: effect chains past their depth, systems that threw, expressions that failed.</summary>
    private sealed class Log : ISimLog
    {
        public List<string> Lines { get; } = new();

        public void Info(string message)
        {
        }

        public void Warn(string message) => Lines.Add("warning: " + message);

        public void Error(string message, Exception? exception = null) => Lines.Add("error: " + message + (exception is null ? "" : $" ({exception.GetType().Name}: {exception.Message})"));
    }

    /// <summary>
    /// Everything the press causes, as the determinism check compares it (a canonical line per event) and as the
    /// sensitivity check compares it (who was struck, how often, how hard, when, where the bodies stand, what the press
    /// made, and how often each marked list ran); and the lives of every body that can be struck, so a life lost
    /// without a published hit is caught in the tick it happens.
    /// </summary>
    private sealed class Recorder
    {
        private readonly Simulation _sim;
        private readonly Entity _caster;
        private readonly Entity[] _crowd;
        private readonly Outcome _outcome;
        private readonly string _handlers;
        private bool _armed;
        private long _pressed;
        private readonly Dictionary<Entity, string> _kinds = new();
        private readonly Dictionary<Entity, double> _lives = new();
        private readonly Dictionary<(long Tick, Entity Target), double> _damage = new();
        private readonly SortedSet<string> _struck = new(StringComparer.Ordinal);
        private double _dealt;
        private long _first = -1;
        private long _last = -1;
        private long _gone = -1;

        public Recorder(Simulation sim, Entity caster, Entity[] crowd, Outcome outcome, string handlers)
        {
            _sim = sim;
            _caster = caster;
            _crowd = crowd;
            _outcome = outcome;
            _handlers = handlers;
        }

        public List<(long Tick, string Line)> Lines { get; } = new();
        public List<string> Casts { get; } = new();

        /// <summary>Every entity the press made, in the order it made them.</summary>
        public List<Entity> Made { get; } = new();

        public SortedDictionary<string, (int Count, long First)> Marks { get; } = new(StringComparer.Ordinal);
        public int Hits { get; private set; }
        public int MaxLive { get; private set; }

        public void Arm()
        {
            _armed = true;
            _pressed = _sim.Clock.Tick;
            foreach (var body in _crowd.Prepend(_caster))
            {
                Watch(body);
            }
        }

        public string KindOf(Entity e) => _kinds.TryGetValue(e, out var kind) ? kind : "entity";

        /// <summary>Whether a made entity has an end of its own: a delivery, a mark, or a body with a lifetime.</summary>
        public bool DueToEnd(Entity e)
        {
            var world = _sim.World;
            return world.Has<Projectile>(e) || world.Has<Field>(e) || world.Has<Tether>(e) || world.Has<Lifetime>(e)
                || (world.TryGet<Minion>(e, out var m) && double.IsFinite(m.ExpiresAt));
        }

        /// <summary>
        /// After the press and after every tick: how much of what the press made stands, and whether every life that fell
        /// this tick fell through a published hit on that body, by no more than those hits say.
        /// </summary>
        public void Settle()
        {
            var live = 0;
            foreach (var e in Made)
            {
                if (_sim.World.IsAlive(e))
                {
                    live++;
                }
            }
            MaxLive = Math.Max(MaxLive, live);
            var tick = _sim.Clock.Tick;
            foreach (var body in _lives.Keys.ToList())
            {
                if (!_sim.World.IsAlive(body) || !_sim.World.TryGet<Stats>(body, out var stats))
                {
                    _lives.Remove(body);
                    continue;
                }
                var now = stats.Resource("life");
                var drop = _lives[body] - now;
                _lives[body] = now;
                if (drop <= 1e-6)
                {
                    continue;
                }
                if (!_damage.TryGetValue((tick, body), out var total))
                {
                    var happened = Lines.Where(l => l.Tick == tick).Select(l => l.Line).Take(6).ToList();
                    _outcome.Broke("pipeline", $"{Who(body)} lost {drop:0.###} life in tick {tick - _pressed} with no DamageDealt on it (handlers in the weave: {_handlers}); that tick: {(happened.Count == 0 ? "nothing published" : string.Join("; ", happened))}");
                }
                else if (drop > total + 1e-6)
                {
                    _outcome.Broke("pipeline", $"{Who(body)} lost {drop:0.###} life in tick {tick - _pressed}, more than the {total:0.###} its DamageDealt events carry (handlers in the weave: {_handlers})");
                }
            }
        }

        /// <summary>
        /// What the press made that is still here at the end, by the rule: a summon or construct may stay while it stands
        /// under its owner's cap with a lifetime or a contract; anything else is left behind. What overstayed its own end
        /// was already reported.
        /// </summary>
        public IEnumerable<string> LeftBehind(Budget budget, List<Entity> overdue)
        {
            var world = _sim.World;
            var standing = Made.Where(e => world.IsAlive(e) && !overdue.Contains(e)).ToList();
            var bare = new List<Entity>();
            var other = new List<Entity>();
            var minions = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in standing)
            {
                if (world.TryGet<Minion>(e, out var minion))
                {
                    var group = standing.Count(o => world.TryGet<Minion>(o, out var m) && m.Owner == minion.Owner && m.Group == minion.Group);
                    var termed = double.IsFinite(minion.ExpiresAt) || minion.Contract is not null;
                    if (!termed || group > budget.Live)
                    {
                        var what = $"{(minion.Construct ? "construct" : "summon")} {(termed ? "with a lifetime" : "with no lifetime and no contract")}, {group} of its group standing against a cap of {budget.Live:0.#}";
                        minions[what] = minions.TryGetValue(what, out var n) ? n + 1 : 1;
                    }
                }
                else if (world.Has<Stats>(e) && !world.Has<Lifetime>(e))
                {
                    bare.Add(e);
                }
                else
                {
                    other.Add(e);
                }
            }
            foreach (var (what, n) in minions)
            {
                yield return $"{n} of the bodies the press raised {(n == 1 ? "is" : "are")} still here: a {what}";
            }
            if (bare.Count > 0)
            {
                yield return $"{bare.Count} bare spawn{(bare.Count == 1 ? "" : "s")} the press made {(bare.Count == 1 ? "is" : "are")} still here: a body with no lifetime, no owner's cap and no contract";
            }
            if (other.Count > 0)
            {
                yield return $"{Tally(other, KindOf)} the press made still stand at the end";
            }
        }

        public void On(GameEvent e)
        {
            if (!_armed)
            {
                return;
            }
            var t = e.Tick - _pressed;
            switch (e)
            {
                case WeaveCast c:
                    Casts.Add(c.Weave);
                    Add(e.Tick, $"{t} cast {c.Caster.Key} {c.Weave} {Pos(c.Point)}");
                    break;
                case DamageDealt d:
                    Add(e.Tick, $"{t} hit {d.Source.Key} {d.Target.Key} {Num(d.Total)} {d.Crit} {d.Evaded} {d.Blocked} {string.Join("+", d.Tags.Items)}");
                    _damage[(e.Tick, d.Target)] = (_damage.TryGetValue((e.Tick, d.Target), out var sum) ? sum : 0) + d.Total;
                    if (d.Source == _caster)
                    {
                        Hits++;
                        _dealt += d.Total;
                        _first = _first < 0 ? t : _first;
                        _last = t;
                        _struck.Add(Who(d.Target));
                    }
                    break;
                case EntityDied died:
                    Add(e.Tick, $"{t} died {died.Entity.Key} {died.Killer.Key}");
                    break;
                case EntitySpawned s:
                    var kind = s.Pattern ?? _sim.World.Get<Identity>(s.Entity)?.Name ?? "entity";
                    _kinds[s.Entity] = kind;
                    Made.Add(s.Entity);
                    Watch(s.Entity);
                    Add(e.Tick, $"{t} spawn {s.Entity.Key} {kind} {Pos(s.Position)}");
                    break;
                case EntityDespawned gone:
                    if (_kinds.ContainsKey(gone.Entity))
                    {
                        _gone = t;
                    }
                    Add(e.Tick, $"{t} despawn {gone.Entity.Key} {KindOf(gone.Entity)}");
                    break;
                case CustomEvent custom:
                    if (custom.Name.StartsWith(Riders.Prefix, StringComparison.Ordinal))
                    {
                        Marks[custom.Name] = Marks.TryGetValue(custom.Name, out var m) ? (m.Count + 1, m.First) : (1, t);
                    }
                    Add(e.Tick, $"{t} marker {custom.Name} {custom.Target.Key}");
                    break;
                case ProjectileForked or ProjectileChained or ProjectileBounced or ProjectileTurned or ProjectileReturned or PlacementTelegraphed or PlacementLanded
                    or DisplacementStarted or DisplacementEnded or BodyCollided or EntityMoved or StatusApplied or MinionSummoned:
                    Add(e.Tick, $"{t} {e}");
                    break;
            }
        }

        private void Add(long tick, string line) => Lines.Add((tick, line));

        /// <summary>Keeps a body's life, so the next settle sees what it lost.</summary>
        private void Watch(Entity body)
        {
            if (_sim.World.TryGet<Stats>(body, out var stats))
            {
                _lives[body] = stats.Resource("life");
            }
        }

        private string Who(Entity e)
        {
            if (e == _caster)
            {
                return "caster";
            }
            var i = Array.IndexOf(_crowd, e);
            return i >= 0 ? "d" + i : $"made {KindOf(e)} {e}";
        }

        /// <summary>What the crowd shows of the press, as one comparable line.</summary>
        public string Observable()
        {
            var text = new StringBuilder();
            text.Append($"struck [{string.Join(" ", _struck)}] hits {Hits} damage {_dealt.ToString("0.##", CultureInfo.InvariantCulture)} first {_first} last {_last}");
            text.Append($" | made {Made.Count} most {MaxLive} gone {_gone}");
            text.Append(" | at");
            foreach (var body in _crowd.Prepend(_caster))
            {
                var p = _sim.World.Get<Transform>(body)?.Position ?? default;
                text.Append(' ').Append(Who(body)).Append(' ').Append((p.X - Origin.X).ToString("0.##", CultureInfo.InvariantCulture)).Append(',').Append((p.Y - Origin.Y).ToString("0.##", CultureInfo.InvariantCulture));
            }
            text.Append(" | marks");
            foreach (var (name, (count, first)) in Marks)
            {
                text.Append($" {name[Riders.Prefix.Length..]} {count}@{first}");
            }
            return text.ToString();
        }

        private static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string Pos(Vector3 p) => $"{Num(p.X)},{Num(p.Y)},{Num(p.Z)}";
    }
}

/// <summary>The checks a point passes before it runs: its layer entries validate, its weave compiles strictly, and the compiled weave states what the point is.</summary>
internal static class MorphChecks
{
    public static IEnumerable<(string Invariant, string Message)> Static(MorphLayer layer, Point p)
    {
        foreach (var issue in layer.IssuesOf(p))
        {
            yield return ("validates", $"{issue.Severity}: {issue.Kind}/{issue.Id}: {issue.Message}");
        }
        var compiler = new WeaveCompiler(layer.Content, EffectRegistry.Shared);
        if (!compiler.TryCompile(new WeaveSpec { Preset = p.Id }, out var weave, out var error))
        {
            yield return ("compiles", $"the weave does not compile: {error}");
            yield break;
        }
        var probe = ProbeOf(layer.Content, weave!, p.Id);
        var states = p.Family.States(new View(p, layer.Content)).ToList();
        if (!states.Any(s => s.Mechanism == p.Family.Probe))
        {
            yield return ("states", $"the family's table says nothing of its own probe '{p.Family.Probe}'");
        }
        foreach (var s in states)
        {
            var holds = MechanismRegistry.Holds(probe, s.Mechanism);
            var carried = s.Mechanism.Contains(':') ? weave!.HasMechanism(s.Mechanism) : weave!.Mechanisms.Has(s.Mechanism);
            if (s.Holds && !(holds && carried))
            {
                yield return ("states", $"'{s.Mechanism}' does not hold though {s.Because} (probe {holds}, compiled {carried})");
            }
            else if (!s.Holds && holds)
            {
                yield return ("states", $"'{s.Mechanism}' holds though {s.Because}");
            }
        }
    }

    /// <summary>
    /// The probe a compiled weave was stated from, rebuilt from its public parts, so a mechanism is tested on the graph
    /// itself rather than on what the shell declares.
    /// </summary>
    public static WeaveProbe ProbeOf(ContentRegistry content, CompiledWeave weave, string preset)
    {
        var graph = new WeaveGraph(EffectRegistry.Shared);
        foreach (var spec in weave.Specs)
        {
            graph.Add(spec, patchable: false);
        }
        return new WeaveProbe
        {
            Content = content,
            Form = weave.Form,
            Preset = content.Get<WeaveDef>(preset),
            Threads = weave.Threads,
            Tags = weave.Tags,
            Declared = weave.DeclaredMechanisms,
            Modifiers = weave.Modifiers,
            Bases = weave.Bases,
            Graph = graph,
            Glyphs = weave.Glyphs,
        };
    }
}
