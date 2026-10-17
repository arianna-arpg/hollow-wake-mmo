using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Minions;

/// <summary>
/// The minions of one simulation: birth order, what each owner is fighting (so minions can assist), what
/// minions inherit, the commands content defines, and the contracts that bring fallen bodies back. It
/// listens to the simulation's events rather than being called by other systems: damage and casts tell it
/// whom an owner fights, deaths schedule contract respawns, despawns end what their owner held, and
/// <see cref="MinionCommandRequested"/> carries players' commands in.
/// </summary>
public sealed class MinionService
{
    private sealed class OwnerState
    {
        public Entity Target = Entity.None;
        public double TargetAt = double.NegativeInfinity;
        public Entity Attacker = Entity.None;
        public double AttackedAt = double.NegativeInfinity;
        public long Casts;
        public string? LastWeave;
        public Vector3 LastPoint;
        public Entity LastCastTarget = Entity.None;
        public bool PresenceDirty;
    }

    private readonly Simulation _sim;
    private readonly Dictionary<Entity, OwnerState> _owners = new();
    private readonly List<MinionContract> _contracts = new();
    private readonly Stack<List<Entity>> _lists = new();
    private readonly List<(long Sequence, Entity Entity)> _pairs = new();
    private InheritRule[]? _inherit;
    private Dictionary<string, CommandDef>? _commands;
    private long _sequence;

    private static readonly Comparison<(long Sequence, Entity Entity)> BySequence = static (a, b) => a.Sequence.CompareTo(b.Sequence);

    public MinionService(Simulation sim)
    {
        _sim = sim;
        sim.Events.Subscribe<DamageDealt>(OnDamage);
        sim.Events.Subscribe<WeaveCast>(OnCast);
        sim.Events.Subscribe<EntityDied>(OnDied);
        sim.Events.Subscribe<EntityDespawned>(OnDespawned);
        sim.Events.Subscribe<MinionCommandRequested>(OnCommandRequested);
    }

    /// <summary>The simulation's minion service, created on first use where the composition root did not register one.</summary>
    public static MinionService Of(Simulation sim)
    {
        if (!sim.TryService<MinionService>(out var service))
        {
            service = new MinionService(sim);
            sim.AddService(service);
        }
        return service;
    }

    public Simulation Sim => _sim;

    /// <summary>The next birth number; bodies born earlier are older.</summary>
    internal long NextSequence() => ++_sequence;

    /// <summary>How many bodies were born here so far; the minion system looks at new ones on the tick they arrive.</summary>
    internal long Births => _sequence;

    /// <summary>Whether a body carrying a presence was ever born here; until one is, there are no presences to keep.</summary>
    internal bool PresencesSeen { get; set; }

    private TagSet? _minionTags;
    private TagSet? _constructTags;
    private string? _defaultBehavior;
    private bool _defaultBehaviorRead;

    /// <summary>Tags every summoned body wears (<c>minions.tags</c>), or every construct (<c>minions.construct_tags</c>).</summary>
    internal TagSet BodyTags(bool construct) => construct
        ? _constructTags ??= TagSet.From(_sim.Content.Rules.Strings("minions.construct_tags"))
        : _minionTags ??= TagSet.From(_sim.Content.Rules.Strings("minions.tags"));

    /// <summary>The behavior summoned bodies run unless their summon names one (<c>minions.behavior</c>); null keeps the pattern's.</summary>
    internal string? DefaultBehavior
    {
        get
        {
            if (!_defaultBehaviorRead)
            {
                _defaultBehaviorRead = true;
                var id = _sim.Content.Rules.Text("minions.behavior", "");
                _defaultBehavior = id.Length > 0 ? id : null;
            }
            return _defaultBehavior;
        }
    }

    /// <summary>Contracts running now, oldest first.</summary>
    public IReadOnlyList<MinionContract> Contracts => _contracts;

    // ------------------------------------------------------------ owners

    /// <summary>Starts following an owner's fights; called when the owner gains a minion.</summary>
    internal void Track(Entity owner)
    {
        if (owner.IsNone)
        {
            return;
        }
        if (!_owners.TryGetValue(owner, out var state))
        {
            state = new OwnerState();
            _owners[owner] = state;
        }
        state.PresenceDirty = true;
    }

    /// <summary>Whether the owner's minions changed since the presences were last applied; clears the mark.</summary>
    internal bool TakePresenceDirty(Entity owner)
    {
        if (_owners.TryGetValue(owner, out var state) && state.PresenceDirty)
        {
            state.PresenceDirty = false;
            return true;
        }
        return false;
    }

    /// <summary>Owners whose minions changed, copied into the list.</summary>
    internal void DirtyOwners(List<Entity> into)
    {
        foreach (var (owner, state) in _owners)
        {
            if (state.PresenceDirty)
            {
                into.Add(owner);
            }
        }
    }

    /// <summary>
    /// What the owner is fighting: its AI's target when it has one, else what it last hit or cast at within
    /// <c>minions.assist_memory</c> seconds; <see cref="Entity.None"/> when nothing.
    /// </summary>
    public Entity OwnerTarget(Entity owner)
    {
        if (_sim.World.TryGet<Behavior>(owner, out var b) && Valid(b.Target))
        {
            return b.Target;
        }
        if (_owners.TryGetValue(owner, out var state) && Valid(state.Target) && _sim.Clock.Time - state.TargetAt <= AssistMemory)
        {
            return state.Target;
        }
        return Entity.None;
    }

    /// <summary>Who last hurt the owner within <c>minions.assist_memory</c> seconds; <see cref="Entity.None"/> when nobody.</summary>
    public Entity OwnerAttacker(Entity owner)
    {
        if (_owners.TryGetValue(owner, out var state) && Valid(state.Attacker) && _sim.Clock.Time - state.AttackedAt <= AssistMemory)
        {
            return state.Attacker;
        }
        return Entity.None;
    }

    /// <summary>How many weaves the owner has cast while tracked, and the last one: what an echo replays.</summary>
    internal bool LastCast(Entity owner, out long count, out string? weave, out Vector3 point, out Entity target)
    {
        if (_owners.TryGetValue(owner, out var state) && state.LastWeave is not null)
        {
            count = state.Casts;
            weave = state.LastWeave;
            point = state.LastPoint;
            target = state.LastCastTarget;
            return true;
        }
        count = 0;
        weave = null;
        point = default;
        target = Entity.None;
        return false;
    }

    private double AssistMemory => _sim.Content.Rules.Number("minions.assist_memory", 4);

    private bool Valid(Entity e) => !e.IsNone && _sim.World.IsAlive(e) && !_sim.World.Has<Dead>(e);

    // ------------------------------------------------------------ the owner's minions

    /// <summary>A list from the service's pool, for collecting minions without allocating; give it back with <see cref="ReturnList"/>.</summary>
    internal List<Entity> RentList() => _lists.Count > 0 ? _lists.Pop() : new List<Entity>(16);

    internal void ReturnList(List<Entity> list)
    {
        list.Clear();
        _lists.Push(list);
    }

    /// <summary>The owner's living minions the scope admits, oldest first, added to <paramref name="into"/>.</summary>
    internal void Living(Entity owner, in MinionScope scope, List<Entity> into)
    {
        var world = _sim.World;
        if (world.CountOf<Minion>() == 0)
        {
            return;
        }
        _pairs.Clear();
        foreach (var (e, m) in world.Query<Minion>())
        {
            if (m.Owner == owner && scope.Admits(m) && !world.Has<Dead>(e))
            {
                _pairs.Add((m.Sequence, e));
            }
        }
        _pairs.Sort(BySequence);
        foreach (var (_, e) in _pairs)
        {
            into.Add(e);
        }
        _pairs.Clear();
    }

    /// <summary>
    /// Makes room for <paramref name="incoming"/> bodies under a capacity: counts the owner's living minions
    /// of the pool group (or of the group without one) and dismisses the oldest until the newcomers fit.
    /// </summary>
    internal void MakeRoom(Entity owner, string group, string? poolGroup, int capacity, int incoming)
    {
        var list = RentList();
        Living(owner, poolGroup is null ? MinionScope.ForGroup(group) : MinionScope.ForPool(poolGroup), list);
        var excess = list.Count + incoming - capacity;
        for (var i = 0; i < list.Count && excess > 0; i++, excess--)
        {
            Dismiss(list[i]);
        }
        ReturnList(list);
    }

    /// <summary>How many living minions of the group (or pool group) the owner has.</summary>
    internal int CountLiving(Entity owner, string group, string? poolGroup)
    {
        var list = RentList();
        Living(owner, poolGroup is null ? MinionScope.ForGroup(group) : MinionScope.ForPool(poolGroup), list);
        var n = list.Count;
        ReturnList(list);
        return n;
    }

    /// <summary>Dismisses the owner's minions of other groups sharing an exclusive group, and ends their contracts.</summary>
    internal void DismissExclusive(Entity owner, string exclusiveGroup, string keep)
    {
        for (var i = _contracts.Count - 1; i >= 0; i--)
        {
            var c = _contracts[i];
            if (c.Owner == owner && c.ExclusiveGroup == exclusiveGroup && c.Group != keep)
            {
                EndContract(c);
            }
        }
        var list = RentList();
        Living(owner, MinionScope.ForExclusive(exclusiveGroup), list);
        foreach (var e in list)
        {
            if (_sim.World.Get<Minion>(e)?.Group != keep)
            {
                Dismiss(e);
            }
        }
        ReturnList(list);
    }

    /// <summary>Removes a minion quietly: no death, no credit, no corpse.</summary>
    public void Dismiss(Entity e) => Systems.LifetimeSystem.Despawn(_sim, e);

    /// <summary>
    /// Moves a body to a point at once, as far along the way from <paramref name="anchor"/> as the ground
    /// allows, dropping its movement plan.
    /// </summary>
    internal void Teleport(Entity e, Vector3 anchor, Vector3 destination)
    {
        if (!_sim.World.TryGet<Transform>(e, out var t))
        {
            return;
        }
        var to = Movement.MovementSystem.ResolveDestination(_sim, anchor, destination);
        var from = t.Position;
        t.Position = _sim.Terrain.SnapToGround(to, 1.5);
        if (_sim.World.TryGet<Motion>(e, out var m))
        {
            m.Stop();
            m.Velocity = Vector3.Zero;
        }
        if (_sim.World.TryGet<Behavior>(e, out var b))
        {
            b.Planned = false;
        }
        _sim.Spatial.Update(e, t.Position);
        _sim.Events.Publish(new EntityMoved(e, from, t.Position) { Tick = _sim.Clock.Tick });
    }

    // ------------------------------------------------------------ inheritance

    /// <summary>The inheritance rules from <c>minions.inherit</c>, family patterns expanded over the attribute catalog.</summary>
    public IReadOnlyList<InheritRule> InheritRules => _inherit ??= InheritRule.Read(_sim.Content);

    /// <summary>
    /// Gives a newborn minion what it inherits from its owner: the owner's minion attributes folded with the
    /// summoning weave's tags and the body's own, and the weave's modifiers joining the fold.
    /// </summary>
    internal void Inherit(Minion m, Stats minionStats, Entity owner, Stats ownerStats, CompiledWeave? weave)
    {
        var context = weave is null ? minionStats.Tags : weave.Tags.Union(minionStats.Tags);
        m.Inheritance = new MinionInheritance
        {
            Source = "owner:" + owner.Key,
            Context = context,
            Extras = weave is { Modifiers.Count: > 0 } ? weave.Modifiers : null,
        };
        Refold(minionStats, m.Inheritance, ownerStats);
    }

    /// <summary>
    /// Folds the owner's minion attributes again and lands what changed on the minion. Each rule gives the
    /// owner's value minus the attribute's neutral base, as a modifier of the rule's form; nothing is touched
    /// when no value moved.
    /// </summary>
    internal bool Refold(Stats minionStats, MinionInheritance inheritance, Stats ownerStats)
    {
        var rules = InheritRules;
        if (inheritance.Values.Length != rules.Count)
        {
            inheritance.Values = new double[rules.Count];
            Array.Fill(inheritance.Values, double.NaN);
        }
        var changed = false;
        var attributes = ownerStats.Attributes;
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var value = attributes.Get(rule.From, ownerStats, inheritance.Context, inheritance.Extras) - rule.Neutral;
            if (Math.Abs(value) < 1e-12)
            {
                value = 0;
            }
            if (!(Math.Abs(value - inheritance.Values[i]) <= 1e-12))
            {
                inheritance.Values[i] = value;
                changed = true;
            }
        }
        inheritance.OwnerVersion = attributes.Version;
        if (!changed)
        {
            return false;
        }
        minionStats.Attributes.RemoveSource(inheritance.Source);
        for (var i = 0; i < rules.Count; i++)
        {
            var value = inheritance.Values[i];
            if (value != 0)
            {
                minionStats.Attributes.Add(new Modifier { Attribute = rules[i].Attribute, Form = rules[i].Form, Value = value, Source = inheritance.Source });
            }
        }
        return true;
    }

    // ------------------------------------------------------------ commands

    /// <summary>The commands content defines in <c>minions.commands</c>, by name.</summary>
    public IReadOnlyDictionary<string, CommandDef> Commands => _commands ??= CommandDef.Read(_sim.Content);

    public bool TryCommand(string name, out CommandDef command) => Commands.TryGetValue(name, out command!);

    private void OnCommandRequested(MinionCommandRequested r)
    {
        if (!TryCommand(r.Command, out var command))
        {
            _sim.Log.Warn($"ignored unknown minion command '{r.Command}' from {r.Owner}");
            return;
        }
        if (!_sim.World.IsAlive(r.Owner))
        {
            return;
        }
        var list = RentList();
        Living(r.Owner, MinionScope.Commandable(r.Group), list);
        var discipline = _sim.World.Get<Stats>(r.Owner)?.Get("command.discipline") ?? 0;
        Ai.MinionCommands.Issue(_sim, r.Owner, command, r.Point, r.Target, command.Duration, discipline, list, _sim.RngFor(r.Owner, "command:" + r.Command));
        ReturnList(list);
    }

    // ------------------------------------------------------------ contracts

    /// <summary>The running contract of an owner's weave, or null.</summary>
    public MinionContract? ContractFor(Entity owner, string key)
    {
        foreach (var c in _contracts)
        {
            if (c.Owner == owner && c.Key == key)
            {
                return c;
            }
        }
        return null;
    }

    internal void AddContract(MinionContract contract)
    {
        _contracts.Add(contract);
        _sim.Events.Publish(new ContractStarted(contract.Owner, contract.Key) { Tick = _sim.Clock.Tick });
    }

    /// <summary>Ends a contract: its bodies are dismissed, its reservation released and its slot freed.</summary>
    public void EndContract(MinionContract contract)
    {
        if (contract.Ended)
        {
            return;
        }
        contract.Ended = true;
        _contracts.Remove(contract);
        contract.Due.Clear();
        if (_sim.World.TryGet<Stats>(contract.Owner, out var stats))
        {
            stats.Attributes.RemoveSource(contract.Source);
        }
        contract.Slot?.State.Remove(MinionContract.SlotState);
        var list = RentList();
        Living(contract.Owner, MinionScope.All, list);
        foreach (var e in list)
        {
            if (ReferenceEquals(_sim.World.Get<Minion>(e)?.Contract, contract))
            {
                Dismiss(e);
            }
        }
        ReturnList(list);
        _sim.Events.Publish(new ContractEnded(contract.Owner, contract.Key) { Tick = _sim.Clock.Tick });
    }

    // ------------------------------------------------------------ event feeds

    private void OnDamage(DamageDealt d)
    {
        if (_owners.Count == 0 || d.Source == d.Target)
        {
            return;
        }
        var now = _sim.Clock.Time;
        if (_owners.TryGetValue(d.Source, out var dealer))
        {
            dealer.Target = d.Target;
            dealer.TargetAt = now;
        }
        if (_owners.TryGetValue(d.Target, out var struck))
        {
            struck.Attacker = d.Source;
            struck.AttackedAt = now;
        }
    }

    private void OnCast(WeaveCast w)
    {
        if (_owners.Count == 0 || !_owners.TryGetValue(w.Caster, out var state))
        {
            return;
        }
        state.Casts++;
        state.LastWeave = w.Weave;
        state.LastPoint = w.Point;
        state.LastCastTarget = w.Target;
        if (!w.Target.IsNone && w.Target != w.Caster && Combat.Relations.IsHostile(_sim, w.Caster, w.Target))
        {
            state.Target = w.Target;
            state.TargetAt = _sim.Clock.Time;
        }
    }

    private void OnDied(EntityDied d)
    {
        if (!_sim.World.TryGet<Minion>(d.Entity, out var m))
        {
            return;
        }
        if (_owners.TryGetValue(m.Owner, out var state))
        {
            state.PresenceDirty = true;
        }
        if (m.Contract is { Ended: false } contract && _sim.World.TryGet<Stats>(m.Owner, out var ownerStats))
        {
            var scale = ownerStats.Attributes.Get("minion.respawn_time", ownerStats, contract.Context);
            contract.Due.Add(_sim.Clock.Time + Math.Max(0, contract.Respawn * scale));
        }
    }

    private void OnDespawned(EntityDespawned d)
    {
        if (_sim.World.TryGet<Minion>(d.Entity, out var m) && _owners.TryGetValue(m.Owner, out var ownerState))
        {
            ownerState.PresenceDirty = true;
        }
        if (_owners.Remove(d.Entity))
        {
            for (var i = _contracts.Count - 1; i >= 0; i--)
            {
                if (_contracts[i].Owner == d.Entity)
                {
                    EndContract(_contracts[i]);
                }
            }
        }
    }
}

/// <summary>Which of an owner's minions an effect or command addresses: a group, the minions of one weave, constructs or not.</summary>
internal readonly struct MinionScope
{
    private readonly string? _group;
    private readonly string? _pool;
    private readonly string? _exclusive;
    private readonly string? _weave;
    private readonly bool _minions;
    private readonly bool _constructs;

    private MinionScope(string? group, string? pool, string? exclusive, string? weave, bool minions, bool constructs)
    {
        _group = group;
        _pool = pool;
        _exclusive = exclusive;
        _weave = weave;
        _minions = minions;
        _constructs = constructs;
    }

    public static MinionScope All => new(null, null, null, null, true, true);

    public static MinionScope ForGroup(string group) => new(group, null, null, null, true, true);

    public static MinionScope ForPool(string pool) => new(null, pool, null, null, true, true);

    public static MinionScope ForExclusive(string exclusive) => new(null, null, exclusive, null, true, true);

    /// <summary>Mobile minions (no constructs), of one group or weave when given.</summary>
    public static MinionScope Commandable(string? group, string? weave = null) => new(group, null, null, weave, true, false);

    /// <summary>Constructs only, of one group or weave when given.</summary>
    public static MinionScope Constructs(string? group, string? weave = null) => new(group, null, null, weave, false, true);

    /// <summary>Minions and constructs alike, of one group or weave when given.</summary>
    public static MinionScope Any(string? group, string? weave = null) => new(group, null, null, weave, true, true);

    public bool Admits(Minion m) =>
        (m.Construct ? _constructs : _minions)
        && (_group is null || string.Equals(m.Group, _group, StringComparison.Ordinal))
        && (_pool is null || string.Equals(m.PoolGroup, _pool, StringComparison.Ordinal))
        && (_exclusive is null || string.Equals(m.ExclusiveGroup, _exclusive, StringComparison.Ordinal))
        && (_weave is null || string.Equals(m.Weave, _weave, StringComparison.Ordinal));
}

/// <summary>
/// One line of <c>minions.inherit</c>: the owner attribute folded, the minion attribute it lands on, the form
/// of the modifier, and the neutral value subtracted (the owner attribute's declared base, so a multiplier
/// based at one lands as a <c>more</c> of its excess).
/// </summary>
public sealed record InheritRule(string From, string Attribute, ModifierForm Form, double Neutral)
{
    /// <summary>Reads the rules, expanding placeholders such as <c>minion.status.{status}.chance</c> over the catalog's attributes.</summary>
    public static InheritRule[] Read(ContentRegistry content)
    {
        var list = new List<InheritRule>();
        if (content.Rules.Node("minions.inherit") is not JsonArray entries)
        {
            return Array.Empty<InheritRule>();
        }
        foreach (var node in entries)
        {
            if (node is not JsonObject entry || JsonNum.StringOf(entry["from"]) is not { } from || JsonNum.StringOf(entry["attribute"]) is not { } to)
            {
                continue;
            }
            var form = ParseForm(JsonNum.StringOf(entry["form"]));
            if (!from.Contains('{'))
            {
                list.Add(new InheritRule(from, to, form, content.Attributes.Get(from)?.Base ?? 0));
                continue;
            }
            var pattern = new Regex("^" + Regex.Replace(Regex.Escape(from), @"\\\{([A-Za-z_][A-Za-z0-9_]*)}", "(?<$1>[^.]+)") + "$", RegexOptions.CultureInvariant);
            foreach (var def in content.Attributes.All.OrderBy(a => a.Id, StringComparer.Ordinal))
            {
                var match = pattern.Match(def.Id);
                if (!match.Success)
                {
                    continue;
                }
                var target = to;
                foreach (Group g in match.Groups)
                {
                    if (!int.TryParse(g.Name, out _))
                    {
                        target = target.Replace("{" + g.Name + "}", g.Value, StringComparison.Ordinal);
                    }
                }
                list.Add(new InheritRule(def.Id, target, form, def.Base));
            }
        }
        return list.ToArray();
    }

    /// <summary>A modifier form named in content: flat, increased, more or override.</summary>
    public static ModifierForm ParseForm(string? text) => text switch
    {
        "increased" => ModifierForm.Increased,
        "more" => ModifierForm.More,
        "override" => ModifierForm.Override,
        _ => ModifierForm.Flat,
    };
}

/// <summary>
/// A command content defines in <c>minions.commands</c>: the handler kind that drives it (registered in
/// <see cref="MinionCommands"/>), how long its order stands, how wide its mark reaches, how close counts as
/// arrived, and how near the mark a foe must stand to be pinned.
/// </summary>
public sealed record CommandDef(string Name, string Kind, double Duration, double MarkRadius, double Arrive, double PinRadius)
{
    public static Dictionary<string, CommandDef> Read(ContentRegistry content)
    {
        var map = new Dictionary<string, CommandDef>(StringComparer.Ordinal);
        if (content.Rules.Node("minions.commands") is not JsonObject commands)
        {
            return map;
        }
        foreach (var (name, node) in commands)
        {
            if (node is not JsonObject c)
            {
                continue;
            }
            map[name] = new CommandDef(
                name,
                JsonNum.StringOf(c["kind"]) ?? name,
                JsonNum.Of(c["duration"], 6),
                JsonNum.Of(c["mark_radius"], 6),
                JsonNum.Of(c["arrive"], 1.8),
                JsonNum.Of(c["pin_radius"], 1.7));
        }
        return map;
    }
}
