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

/// <summary>One still body of the crowd: its name in what the crowd shows, where it stands from the caster, its side, and the share of its life it starts with.</summary>
internal sealed record CrowdBody(string Name, Vector2 At, string Faction, double Life = 1);

/// <summary>
/// The crowd scenario every point runs in: a seeded arena, the point's caster at the origin pressing its weave once,
/// aimed east, and a fixed crowd of still dummies: nine enemies (five in a line two units apart from two units out, one
/// of them at half life, two flanking three units north and south at four units east, and two further out, south at
/// six units east and north at ten) and one ally of the caster beside them. The run records every event the press
/// causes, ticks through the point's expected lifetime and a quiet second, and checks what it may: that the press
/// paid, that every life lost went through the one damage pipeline, that what it made ended or may stay, and that it
/// stayed within the caps its parameters allow.
/// </summary>
internal static class MorphArena
{
    public const int TicksPerSecond = 20;

    public const string Dummy = "morph_dummy";
    public const string Red = "morph_red";
    public const string Blue = "morph_blue";

    public static readonly Vector3 Origin = new(20, 20, 0);

    /// <summary>
    /// The crowd, the same for every point. The hurt body lets the life picks choose apart from the nearest and the
    /// farthest; the ally, a body of the caster's side standing by the line between the first enemies, lets the
    /// filter words that reach allies show; the two outer enemies stand where the registry's hit shapes reach
    /// differently, and outside the shove carrier's reach.
    /// </summary>
    public static readonly CrowdBody[] Crowd =
    {
        new("d0", new(2, 0), Blue),
        new("d1", new(4, 0), Blue),
        new("d2", new(6, 0), Blue),
        new("d3", new(8, 0), Blue, Life: 0.5),
        new("d4", new(10, 0), Blue),
        new("d5", new(4, 3), Blue),
        new("d6", new(4, -3), Blue),
        new("d7", new(6, -3.5f), Blue),
        new("d8", new(10, 2.5f), Blue),
        new("a0", new(5, 1.1f), Red),
    };

    /// <summary>Bodies one press can strike: the crowd and the caster itself (filters such as all reach it).</summary>
    public static int Bodies => Crowd.Length + 1;

    /// <summary>Seconds past the expected lifetime before what was due to end must be gone: a few ticks, since each family works its expected life out to the tick.</summary>
    public const double Margin = 3.0 / TicksPerSecond;

    /// <summary>Seconds of quiet the arena must keep after that.</summary>
    public const double Quiet = 1.0;

    /// <summary>The longest a run may simulate; a point expected to live longer is a finding of its own.</summary>
    public const double MaxSeconds = 300;

    /// <summary>Runs a point in the crowd scenario, on its own seed or on another of its seeds.</summary>
    public static Outcome Run(ContentRegistry content, Point point, ulong? seed = null)
    {
        var outcome = new Outcome();
        var log = new Log();
        var sim = ZoneBuilder.CreateArena(content, seed ?? point.Seed, log: log, ticksPerSecond: TicksPerSecond);
        var factory = sim.Service<PatternFactory>();
        var caster = factory.Spawn(sim, point.Caster, Origin, new SpawnOptions { Faction = Red });
        if (caster.IsNone)
        {
            outcome.Broke("runs", $"the caster pattern {point.Caster} did not spawn");
            return outcome;
        }
        var crowd = Crowd.Select(c => factory.Spawn(sim, Dummy, Origin + new Vector3(c.At.X, c.At.Y, 0), new SpawnOptions { Faction = c.Faction })).ToArray();
        foreach (var (body, share) in crowd.Select((e, i) => (e, Crowd[i].Life)).Prepend((caster, 1.0)))
        {
            sim.World.Remove<Behavior>(body);
            var s = sim.World.Get<Stats>(body)!;
            foreach (var resource in new[] { "life", "mana", "stamina" })
            {
                s.SetResource(resource, s.ResourceMax(resource) * (resource == "life" ? share : 1));
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
        var cooldown = weave.Cooldown(stats);
        var pending = sim.Scheduler.Pending;
        var aim = Origin + new Vector3(point.Family.Aim.X, point.Family.Aim.Y, 0);
        recorder.Arm(costs, book, slot, cooldown > 0);

        // Pays: a press has paid its cost and started its cooldown before anything it causes resolves (the grammar's
        // invariant 8). The recorder looks at the caster's pools and the slot's cooldown when the first resolution
        // comes, or when the press returns having resolved nothing yet.
        if (!costs.Values.Any(c => c > 0))
        {
            outcome.Broke("pays", "the weave costs nothing, so its payment cannot be seen");
        }
        if (cooldown <= 0)
        {
            outcome.Broke("pays", "the weave has no cooldown to start");
        }
        var presses = 0;
        var capacity = point.Family.Group is null ? (int?)null : (int)Math.Max(1, Math.Round(new View(point, content).Num("max_active")));
        bool Press()
        {
            recorder.Press();
            if (!runtime.TryBegin(sim, caster, slot, Entity.None, aim, Vector2.UnitX, out var reason))
            {
                outcome.Broke("pays", $"the press was refused: {reason}");
                return false;
            }
            presses++;
            recorder.Pressed();
            recorder.Settle();
            // A family that raises bodies under a cap stands no more of its group than the cap after any press: the
            // service makes room for a press's bodies by dismissing the oldest, the press's own among them.
            if (capacity is { } cap && recorder.Standing(point.Family.Group!) is var standing && standing > cap)
            {
                outcome.Broke("bounded", $"after press {presses}, {standing} of the group stood at once, past the cap of {cap} its owner keeps");
            }
            return true;
        }
        if (!Press())
        {
            return outcome;
        }
        // A cap bites on a later press: wait out the cooldown and press again, and the window starts from the last press.
        while (presses < point.Family.Presses)
        {
            var waited = 0.0;
            while (!book.IsReady(slot, sim.Clock.Time) && waited < MaxSeconds)
            {
                sim.Tick();
                recorder.Settle();
                waited += View.Dt;
            }
            if (!Press())
            {
                return outcome;
            }
        }

        // Terminates: through the expected lifetime and a margin, everything the press made that was due to end is gone,
        // nothing is still scheduled or carried, and a quiet second follows.
        var budget = point.Family.Budget(new View(point, content) { Sim = sim });
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

        // Pays, over the whole run: each press paid before it resolved, the cast was published once a press, and each
        // pool the weave costs fell once a press (a later drop is a payment the press did not make up front).
        foreach (var message in recorder.Unpaid)
        {
            outcome.Broke("pays", message);
        }
        var casts = recorder.Casts.Count(k => k == weave.Key);
        if (casts != presses)
        {
            outcome.Broke("pays", $"WeaveCast was published {casts} times for {presses} press{(presses == 1 ? "" : "es")}");
        }
        foreach (var (resource, drops) in recorder.Drops)
        {
            if (drops != presses)
            {
                outcome.Broke("pays", $"{resource} fell {drops} times over the run for {presses} press{(presses == 1 ? "" : "es")}");
            }
        }

        // Left behind: at the end, everything the press made is gone, or is a summon or construct that its lifetime, its
        // contract or its owner's cap will end. What overstayed its end was reported above.
        foreach (var message in recorder.LeftBehind(overdue, capacity))
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

        // Causality continues the budget (the grammar's rule 8): what a resolution sets going runs deeper than it.
        foreach (var message in Causality(recorder.Depths, point))
        {
            outcome.Broke(message.Invariant, message.Message);
        }

        // The family's own checks on what the run showed.
        outcome.Launches = recorder.Launches;
        if (point.Family.Checks is { } checks)
        {
            foreach (var (invariant, message) in checks(new View(point, content), outcome))
            {
                outcome.Broke(invariant, message);
            }
        }

        foreach (var line in log.Lines)
        {
            outcome.Broke("runs", line);
        }
        outcome.Hits = recorder.Hits;
        outcome.MaxLive = recorder.MaxLive;
        outcome.Created = recorder.Made.Count;
        outcome.Moved = recorder.Moved();
        outcome.Events = recorder.Lines.Select(l => l.Line).ToList();
        outcome.Observable = recorder.Observable();
        return outcome;
    }

    /// <summary>
    /// The depths the causal riders marked, against what rule 8 asks: a status ticks deeper than the depth it was
    /// applied at, a cast runs deeper than the cast effect, a follow-up deeper than the use it follows, and a cast event
    /// raises what answers it deeper than the use that cast. The relay, when the point carries it, must have fired.
    /// </summary>
    private static IEnumerable<(string Invariant, string Message)> Causality(List<(string Name, int Depth)> marks, Point point)
    {
        int[] Of(string name) => marks.Where(m => m.Name == name).Select(m => m.Depth).ToArray();
        var applied = Of("status");
        var ticks = Of("status_tick");
        if (applied.Length > 0 && ticks.Length > 0 && ticks.Min() <= applied.Min())
        {
            yield return ("depth", $"status ticks restart the depth: a tick of the status ran at depth {ticks.Min()}, though the status was applied at depth {applied.Min()}");
        }
        for (var i = 0; i < marks.Count; i++)
        {
            // A cast runs its weave at once, so the cast effect's marker is followed by the cast weave's.
            if (marks[i].Name == "cast" && i + 1 < marks.Count && marks[i + 1].Name == "cast_run" && marks[i + 1].Depth <= marks[i].Depth)
            {
                yield return ("depth", $"a cast restarts the depth: the weave a cast effect at depth {marks[i].Depth} cast ran at depth {marks[i + 1].Depth}");
            }
        }
        var relay = Of("relay");
        if (point.Lists.Contains(MorphLayer.Relay) && relay.Length == 0)
        {
            yield return ("runs", "the relay seat never fired on the caster's cast");
        }
        if (relay.Length > 0)
        {
            var use = relay.Min();
            var follow = Of("follow_up");
            if (follow.Length > 0 && follow.Min() <= use)
            {
                yield return ("depth", $"follow-ups restart the depth: the relay's follow-up ran at depth {follow.Min()}, though the relay's use ran at depth {use}");
            }
            var procs = Of("proc");
            if (procs.Length > 0 && procs.Max() <= use)
            {
                yield return ("depth", $"cast events restart the depth: the procs the casts raised ran at depth {procs.Max()} at most, though the relay's use, one of those casts, ran at depth {use}");
            }
        }
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

        /// <summary>How many of the crowd's bodies stand away from where they stood when the run ended.</summary>
        public int Moved { get; set; }

        /// <summary>The ticks, from the press, at which flights were launched from where the caster stands.</summary>
        public List<long> Launches { get; set; } = new();

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

        // Who was struck, in the order of their first hits: a chain that leaps to the same bodies in another order shows it.
        private readonly List<string> _struck = new();
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

        // What the press made, for looking up whose hits are the press's.
        private readonly HashSet<Entity> _made = new();

        /// <summary>Every entity the press made, in the order it made them.</summary>
        public List<Entity> Made { get; } = new();

        public SortedDictionary<string, (int Count, long First)> Marks { get; } = new(StringComparer.Ordinal);

        /// <summary>Every marker in the order it ran, by its list's name, with the effect depth it ran at.</summary>
        public List<(string Name, int Depth)> Depths { get; } = new();

        /// <summary>The ticks, from the press, at which flights were launched from where the caster stands (a fork's children start where it hit).</summary>
        public List<long> Launches { get; } = new();
        public int Hits { get; private set; }
        public int MaxLive { get; private set; }

        /// <summary>Starts recording, with what each press must pay up front: the costs per pool, and the slot whose cooldown it starts.</summary>
        public void Arm(Dictionary<string, double> costs, WeaveBook book, WeaveSlot slot, bool hasCooldown)
        {
            _armed = true;
            _pressed = _sim.Clock.Tick;
            foreach (var body in _crowd.Prepend(_caster))
            {
                Watch(body);
            }
            _resources = costs.Keys.ToArray();
            _costs = _resources.Select(r => costs[r]).ToArray();
            _book = book;
            _slot = slot;
            _hasCooldown = hasCooldown;
            _stats = _sim.World.Get<Stats>(_caster)!;
            _seen = _resources.Select(_stats.Resource).ToArray();
            _before = (double[])_seen.Clone();
            foreach (var resource in _resources)
            {
                Drops[resource] = 0;
            }
        }

        // ---- payment: what the caster's pools and the slot's cooldown say when a press first resolves

        private string[] _resources = Array.Empty<string>();
        private double[] _costs = Array.Empty<double>();
        private double[] _before = Array.Empty<double>();
        private double[] _seen = Array.Empty<double>();
        private Stats? _stats;
        private WeaveBook? _book;
        private WeaveSlot? _slot;
        private bool _hasCooldown;
        private bool _awaiting;

        /// <summary>What a press had not paid by its first resolution: a pool not yet fallen by its cost, a cooldown not yet started.</summary>
        public List<string> Unpaid { get; } = new();

        /// <summary>How many times each pool the weave costs fell over the run.</summary>
        public Dictionary<string, int> Drops { get; } = new(StringComparer.Ordinal);

        /// <summary>A press is about to be made: its payment is due by its first resolution.</summary>
        public void Press()
        {
            Sample();
            Array.Copy(_seen, _before, _seen.Length);
            _awaiting = true;
        }

        /// <summary>The press returned: if nothing it caused has resolved yet, it must have paid by now.</summary>
        public void Pressed() => Snapshot("when the press returned, before anything resolved");

        /// <summary>Counts each fall of a pool since it was last looked at.</summary>
        private void Sample()
        {
            for (var i = 0; i < _resources.Length; i++)
            {
                var value = _stats!.Resource(_resources[i]);
                if (value < _seen[i] - 1e-9)
                {
                    Drops[_resources[i]]++;
                }
                _seen[i] = value;
            }
        }

        private void Snapshot(string when)
        {
            if (!_awaiting)
            {
                return;
            }
            _awaiting = false;
            Sample();
            for (var i = 0; i < _resources.Length; i++)
            {
                var paid = _before[i] - _seen[i];
                if (Math.Abs(paid - _costs[i]) > 1e-6)
                {
                    Unpaid.Add($"{_resources[i]} had fallen by {paid:0.###} {when}, the cost is {_costs[i]:0.###}");
                }
            }
            if (_hasCooldown && _book!.IsReady(_slot!, _sim.Clock.Time))
            {
                Unpaid.Add($"the cooldown had not started {when}");
            }
        }

        /// <summary>The events that are a resolution of a press: what it struck, made, moved, marked or applied.</summary>
        private static bool Resolves(GameEvent e) => e is DamageDealt or EntitySpawned or CustomEvent or StatusApplied or DisplacementStarted or EntityMoved
            or MinionSummoned or PlacementTelegraphed or PlacementLanded or ProjectileForked or ProjectileChained or ProjectileBounced or ProjectileTurned
            or ProjectileReturned or BodyCollided or EntityDied or Healed;

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
            Sample();
            // What stands: alive and not dismissed this tick (a dismissed body leaves the world when the tick ends).
            var live = 0;
            foreach (var e in Made)
            {
                if (_sim.World.IsAlive(e) && !_sim.World.IsDeferredDestroy(e))
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

        /// <summary>How many of the caster's raised bodies of a group stand now: alive, not fallen, not dismissed this tick.</summary>
        public int Standing(string group)
        {
            var world = _sim.World;
            var n = 0;
            foreach (var (e, m) in world.Query<Minion>())
            {
                if (m.Owner == _caster && m.Group == group && !world.Has<Dead>(e) && !world.IsDeferredDestroy(e))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// What the press made that is still here at the end, by the rule: a summon or construct may stay while something
        /// will end it, its own lifetime or contract, or its owner's cap. A body raised for good stands under its owner's
        /// cap and ends with its owner: the cap replaces it, the owner dismisses it, or the owner falls or leaves. It
        /// stands under the cap while its group stands no more than the capacity the summon gives its owner
        /// (<paramref name="capacity"/>, from <c>max_active</c>); past it, the body is left behind. Anything else is left
        /// behind; what overstayed its own end was already reported.
        /// </summary>
        public IEnumerable<string> LeftBehind(List<Entity> overdue, int? capacity)
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
                    var termed = double.IsFinite(minion.ExpiresAt) || minion.Contract is not null;
                    var group = minion.Group is { } g ? Standing(g) : 0;
                    var capped = !minion.Owner.IsNone && minion.Group is not null && capacity is { } cap && group <= cap;
                    if (!termed && !capped)
                    {
                        var body = minion.Construct ? "construct" : "summon";
                        var what = capacity is { } most && minion.Group is not null
                            ? $"{body} with no lifetime and no contract, its group standing {group} past its owner's cap of {most}"
                            : $"{body} with no lifetime, no contract and no owner's cap";
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
            if (_awaiting && Resolves(e))
            {
                Snapshot("when the press's first resolution came");
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
                    // A hit counts as the press's when the caster or anything the press made struck it.
                    if (d.Source == _caster || _made.Contains(d.Source))
                    {
                        Hits++;
                        _dealt += d.Total;
                        _first = _first < 0 ? t : _first;
                        _last = t;
                        if (!_struck.Contains(Who(d.Target)))
                        {
                            _struck.Add(Who(d.Target));
                        }
                    }
                    break;
                case EntityDied died:
                    Add(e.Tick, $"{t} died {died.Entity.Key} {died.Killer.Key}");
                    break;
                case EntitySpawned s:
                    var kind = s.Pattern ?? _sim.World.Get<Identity>(s.Entity)?.Name ?? "entity";
                    _kinds[s.Entity] = kind;
                    Made.Add(s.Entity);
                    _made.Add(s.Entity);
                    Watch(s.Entity);
                    Add(e.Tick, $"{t} spawn {s.Entity.Key} {kind} {Pos(s.Position)}");
                    if (kind == "projectile" && _sim.World.Get<Transform>(_caster) is { } at
                        && Vector2.Distance(new Vector2(s.Position.X, s.Position.Y), new Vector2(at.Position.X, at.Position.Y)) < 0.01f)
                    {
                        Launches.Add(t);
                    }
                    break;
                case EntityDespawned gone:
                    if (_kinds.ContainsKey(gone.Entity))
                    {
                        _gone = t;
                    }
                    Add(e.Tick, $"{t} despawn {gone.Entity.Key} {KindOf(gone.Entity)}");
                    break;
                case CustomEvent custom:
                    var depth = custom.Data.TryGetValue("depth", out var d0) ? (int)d0 : -1;
                    if (custom.Name.StartsWith(Riders.Prefix, StringComparison.Ordinal))
                    {
                        Marks[custom.Name] = Marks.TryGetValue(custom.Name, out var m) ? (m.Count + 1, m.First) : (1, t);
                        Depths.Add((custom.Name[Riders.Prefix.Length..], depth));
                    }
                    Add(e.Tick, $"{t} marker {custom.Name} {custom.Target.Key} depth {depth}");
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
            return i >= 0 ? Crowd[i].Name : $"made {KindOf(e)} {e}";
        }

        /// <summary>How many of the crowd's bodies stand away from their places (by more than a hair).</summary>
        public int Moved()
        {
            var moved = 0;
            for (var i = 0; i < _crowd.Length; i++)
            {
                var p = _sim.World.Get<Transform>(_crowd[i])?.Position ?? default;
                if (Vector2.Distance(new Vector2(p.X - Origin.X, p.Y - Origin.Y), Crowd[i].At) > 0.01f)
                {
                    moved++;
                }
            }
            return moved;
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

/// <summary>
/// The checks a point passes before it runs: its layer entries validate, its weave compiles strictly, and the compiled
/// weave states what the point is. A point compiles once: its checks and what every mechanism says of it are kept, by
/// the point's weave, for every theory that asks (the layer is built once and never changes).
/// </summary>
internal static class MorphChecks
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (List<(string Invariant, string Message)> Broken, IReadOnlyDictionary<string, bool>? Held)> Cache = new(StringComparer.Ordinal);

    public static IReadOnlyList<(string Invariant, string Message)> Static(MorphLayer layer, Point p) => Checked(layer, p).Broken;

    /// <summary>What every registered mechanism says of a point's compiled weave, or null when it does not compile.</summary>
    public static IReadOnlyDictionary<string, bool>? Held(MorphLayer layer, Point p) => Checked(layer, p).Held;

    private static (List<(string Invariant, string Message)> Broken, IReadOnlyDictionary<string, bool>? Held) Checked(MorphLayer layer, Point p) =>
        Cache.GetOrAdd(p.Id, _ =>
        {
            var compiler = new WeaveCompiler(layer.Content, EffectRegistry.Shared);
            var compiled = compiler.TryCompile(new WeaveSpec { Preset = p.Id }, out var weave, out var error);
            var probe = compiled ? ProbeOf(layer.Content, weave!, p.Id) : null;
            var broken = Check(layer, p, weave, probe, error).ToList();
            var held = probe is null ? null : Variants.ToDictionary(m => m, m => MechanismRegistry.Holds(probe, m), StringComparer.Ordinal);
            return (broken, held);
        });

    private static IEnumerable<(string Invariant, string Message)> Check(MorphLayer layer, Point p, CompiledWeave? weave, WeaveProbe? probe, string? error)
    {
        foreach (var issue in layer.IssuesOf(p))
        {
            yield return ("validates", $"{issue.Severity}: {issue.Kind}/{issue.Id}: {issue.Message}");
        }
        if (weave is null || probe is null)
        {
            yield return ("compiles", $"the weave does not compile: {error}");
            yield break;
        }
        var states = p.Family.States(new View(p, layer.Content)).ToList();
        if (!states.Any(s => s.Mechanism == p.Family.Probe))
        {
            yield return ("states", $"the family's table says nothing of its own probe '{p.Family.Probe}'");
        }
        foreach (var s in states)
        {
            var holds = MechanismRegistry.Holds(probe, s.Mechanism);
            var carried = s.Mechanism.Contains(':') ? weave.HasMechanism(s.Mechanism) : weave.Mechanisms.Has(s.Mechanism);
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
    /// Every registered mechanism a probe can answer of a point, by name: each probe bare, with each word its parameter
    /// takes, and with each status a point can apply or inflict (the marking status and the ember Thread's ailment).
    /// </summary>
    public static readonly IReadOnlyList<string> Variants = MechanismRegistry.All.SelectMany(info =>
    {
        var names = new List<string> { info.Name };
        if (ReferenceEquals(info.Parameter, ProbeParameter.Status))
        {
            names.Add(info.Name + ":" + MorphLayer.Mark);
            names.Add(info.Name + ":burning");
        }
        else if (!ReferenceEquals(info.Parameter, ProbeParameter.None) && !ReferenceEquals(info.Parameter, ProbeParameter.Any))
        {
            // A probe taking one of a fixed set of words describes itself by them.
            names.AddRange(info.Parameter.Description.Split(", ").Select(word => info.Name + ":" + word));
        }
        return names;
    }).ToList();

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
            HeldBlocks = weave.HeldBlocks,
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
