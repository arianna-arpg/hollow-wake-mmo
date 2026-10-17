using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Components;

/// <summary>A weave bound to a slot, with its compiled form cached until the spec or owner changes.</summary>
public sealed class WeaveSlot
{
    public required WeaveSpec Spec;
    public Weaves.CompiledWeave? Compiled;

    private string? _key;
    private WeaveSpec? _keyOf;

    /// <summary>The spec as text, the key cooldowns and caches use; built once per spec.</summary>
    public string Key
    {
        get
        {
            if (!ReferenceEquals(_keyOf, Spec))
            {
                _key = Spec.ToString();
                _keyOf = Spec;
            }
            return _key!;
        }
    }

    /// <summary>Scopes the weave runtime keeps for the compiled weave and its caster. A cache, not state.</summary>
    internal Weaves.WeaveSlotCache? RuntimeCache;

    /// <summary>Glyphs grafted onto this seat by the caster's <c>glyph.slot{n}.{glyph}</c> attributes, as of the last arming.</summary>
    internal IReadOnlyList<GlyphRef>? Grafts;

    private Dictionary<string, double>? _state;

    /// <summary>
    /// What the slot remembers between casts: use banks, trigger latches, combo cursors, brims, marks, by
    /// name (see <see cref="SlotState"/>). Unlike <see cref="RuntimeCache"/> this is state, kept as long as
    /// the slot holds its weave.
    /// </summary>
    public Dictionary<string, double> State => _state ??= new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>True once anything was written to <see cref="State"/>, so readers need not create it.</summary>
    public bool HasState => _state is { Count: > 0 };

    /// <summary>A state value, or <paramref name="fallback"/> when the slot never wrote it.</summary>
    public double StateOr(string key, double fallback) => _state is not null && _state.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>
    /// The socketed glyphs that are dormant, each with the reason its gate fails, as of the slot's last
    /// compile. Dormant glyphs stay socketed and contribute nothing until something lifts their gate.
    /// </summary>
    public IReadOnlyList<Weaves.DormantGlyph> Dormant { get; internal set; } = Array.Empty<Weaves.DormantGlyph>();
}

/// <summary>The names the weave runtime keeps in <see cref="WeaveSlot.State"/>.</summary>
public static class SlotState
{
    /// <summary>One while a triggered weave is armed (the default), zero once its key disarmed it.</summary>
    public const string Armed = "armed";
    /// <summary>Use charges in the bank, and when the bank was last brought up to date.</summary>
    public const string Uses = "uses";
    public const string UsesAt = "uses.at";
    /// <summary>When an emptied magazine finishes refilling.</summary>
    public const string UsesReload = "uses.reload";
    /// <summary>One while a toggle is on, and when it turned on.</summary>
    public const string Toggle = "toggle";
    public const string ToggleAt = "toggle.at";
    /// <summary>The brim's fill from zero to one, and when it was last brought up to date.</summary>
    public const string Brim = "brim";
    public const string BrimAt = "brim.at";
    /// <summary>Self stacks held, and when they last changed.</summary>
    public const string Stack = "stack";
    public const string StackAt = "stack.at";
    /// <summary>Uses counted toward the next cycle.</summary>
    public const string Cycle = "cycle";
    /// <summary>The combo chain's next step, and when the last step was used.</summary>
    public const string Chain = "chain";
    public const string ChainAt = "chain.at";
    /// <summary>The fraction of a repeat carried to the next use.</summary>
    public const string RepeatCarry = "repeat.carry";
    /// <summary>When the slot was last used: seals of unleash bank from here.</summary>
    public const string LastUse = "last_use";
    /// <summary>When a running meta chain ends.</summary>
    public const string MetaUntil = "meta.until";
}

/// <summary>A cast in progress: what it aims at, when it started and how it is being held.</summary>
public sealed class CastState
{
    public required WeaveSlot Slot;

    /// <summary>The weave this cast resolves: the slot's own, or the face a convert or a combo chain put on it.</summary>
    public Weaves.CompiledWeave? Weave;

    /// <summary>How the cast is held: a bar, a channel, a charge-up, an overcharge or a guard.</summary>
    public CastMode Mode = CastMode.Cast;

    public Entity Target = Entity.None;

    /// <summary>The body a held cast keeps turning toward when its caster sends no aim of its own.</summary>
    public Entity Focus = Entity.None;

    public Vector3 Point;
    public Vector2 Direction;
    public double StartedAt;

    /// <summary>The bar's length in seconds: a timed cast's cast time, or the natural length of a hold.</summary>
    public double Duration;

    public double NextChannelAt;
    public bool Channeling;
    public int Pulses;

    /// <summary>Seconds the cast has been held: a channel's hold, a charge-up's charge.</summary>
    public double Held;

    /// <summary>How full a charge-up or brim is, from zero to one.</summary>
    public double Fill;

    /// <summary>The overcharge stage reached.</summary>
    public int Stage;

    /// <summary>The most stages the overcharge can reach.</summary>
    public int Stages;

    /// <summary>The slot's brim while a channel with one is held, from zero to one.</summary>
    public double Brim;

    /// <summary>
    /// The fraction of movement speed the caster keeps while the cast runs: zero roots, one moves freely.
    /// Set when the cast begins; the movement system reads it.
    /// </summary>
    public float MoveFactor;

    /// <summary>Seconds after <see cref="StartedAt"/> at which a hold ends by itself.</summary>
    public double MaxHold;

    /// <summary>When a charge-up filled or an overcharge reached its last stage; negative until then.</summary>
    public double FullAt = -1;

    /// <summary>When the current overcharge stage began.</summary>
    public double StageAt;

    /// <summary>Seconds a channel's current beat lasts.</summary>
    public double Interval;

    /// <summary>When a timed strike's marker sits, in seconds after the start; negative without one.</summary>
    public double Marker = -1;

    /// <summary>Presses during the bar, and whether a timed press landed.</summary>
    public int Presses;
    public bool Flawless;

    /// <summary>True once this cast started its weave's cooldown.</summary>
    public bool CooldownStarted;

    /// <summary>Use charges the press spent from its slot's bank.</summary>
    public int UsesSpent;

    /// <summary>The live aim while the cast runs, mirrored from the weave book's.</summary>
    public Vector3? AimPoint;
    public Vector2? AimDirection;
    public double AimAt = double.NegativeInfinity;

    /// <summary>The cast's own random stream: channel jitter, strike markers.</summary>
    internal Rng? Rng;

    /// <summary>The source of the modifiers ramps put on the caster while the cast lasts.</summary>
    internal string? CasterSource;

    /// <summary>The caster's view of the cast's weave, for the formulas a hold reads each beat.</summary>
    internal Expressions.IExprScope? Scope;

    private List<Modifier>? _modifiers;

    /// <summary>
    /// Modifiers local to this cast, such as ramps and charge-up scales, joining the weave's fold while
    /// it lasts. Created on first use and kept for the cast's next beat.
    /// </summary>
    public List<Modifier> Modifiers => _modifiers ??= new List<Modifier>();

    /// <summary>True when the cast carries local modifiers.</summary>
    public bool HasModifiers => _modifiers is { Count: > 0 };
}

/// <summary>The weaves an entity knows and can use, their cooldowns, and what it is casting now.</summary>
public sealed class WeaveBook : IComponent
{
    public List<WeaveSlot> Slots { get; } = new();
    public Dictionary<string, double> ReadyAt { get; } = new(StringComparer.Ordinal);
    public CastState? Casting;
    public double GlobalReadyAt;

    /// <summary>
    /// The live aim the caster last sent (rate-limited by rule <c>casting.max_aim_rate</c> and clamped by
    /// the server): a point on the ground, a facing, and when it arrived. Held casts, tracking channels and
    /// salvos read it; a running cast mirrors it.
    /// </summary>
    public Vector3? AimPoint;
    public Vector2? AimDirection;
    public double AimAt = double.NegativeInfinity;

    /// <summary>Increments when the slots change through <see cref="Add"/>, <see cref="Set"/>, <see cref="Swap"/> or <see cref="InvalidateCompiled"/>.</summary>
    public int Version { get; private set; }

    public WeaveSlot Add(WeaveSpec spec)
    {
        var slot = new WeaveSlot { Spec = spec };
        Slots.Add(slot);
        Version++;
        return slot;
    }

    /// <summary>Puts a slot at an index, growing the list with empty slots as needed.</summary>
    public void Set(int index, WeaveSlot slot)
    {
        while (Slots.Count <= index)
        {
            Slots.Add(new WeaveSlot { Spec = new WeaveSpec { Preset = null, Form = null } });
        }
        Slots[index] = slot;
        Version++;
    }

    /// <summary>Swaps two seats of the rack.</summary>
    public void Swap(int a, int b)
    {
        (Slots[a], Slots[b]) = (Slots[b], Slots[a]);
        Version++;
    }

    /// <summary>Marks the slots changed after code edited <see cref="Slots"/> directly.</summary>
    public void Changed() => Version++;

    public WeaveSlot? Find(string key)
    {
        foreach (var s in Slots)
        {
            if (s.Key == key || s.Spec.Preset == key || s.Spec.Name == key)
            {
                return s;
            }
        }
        return null;
    }

    public bool IsReady(WeaveSlot slot, double now) => IsReady(slot.Key, now);

    public bool IsReady(string key, double now) => !ReadyAt.TryGetValue(key, out var t) || t <= now;

    public void InvalidateCompiled()
    {
        foreach (var s in Slots)
        {
            s.Compiled = null;
        }
        Version++;
    }

    // ------------------------------------------------------------ the recent-cast ring

    private string[] _recent = Array.Empty<string>();
    private int _recentCount;
    private int _recentNext;

    /// <summary>How many casts the ring remembers now, at most its capacity (rule <c>casting.recent_casts</c>).</summary>
    public int RecentCount => _recentCount;

    /// <summary>The key of a recent cast: zero is the latest, one the cast before it.</summary>
    public string? Recent(int ago)
    {
        if (ago < 0 || ago >= _recentCount)
        {
            return null;
        }
        var i = _recentNext - 1 - ago;
        if (i < 0)
        {
            i += _recent.Length;
        }
        return _recent[i];
    }

    /// <summary>True when the last <paramref name="casts"/> casts were all the same weave.</summary>
    public bool RecentlyRepeated(int casts)
    {
        if (casts < 2 || _recentCount < casts)
        {
            return false;
        }
        var first = Recent(0);
        for (var i = 1; i < casts; i++)
        {
            if (!string.Equals(Recent(i), first, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>True when the last <paramref name="casts"/> casts were all different weaves.</summary>
    public bool RecentlyVaried(int casts)
    {
        if (casts < 2 || _recentCount < casts)
        {
            return false;
        }
        for (var i = 0; i < casts; i++)
        {
            var a = Recent(i);
            for (var j = i + 1; j < casts; j++)
            {
                if (string.Equals(a, Recent(j), StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Remembers a cast in the ring of the given capacity, dropping the oldest.</summary>
    internal void RecordCast(string key, int capacity)
    {
        capacity = Math.Max(1, capacity);
        if (_recent.Length != capacity)
        {
            var kept = new string[capacity];
            var keep = Math.Min(_recentCount, capacity);
            for (var i = 0; i < keep; i++)
            {
                kept[keep - 1 - i] = Recent(i)!;
            }
            _recent = kept;
            _recentCount = keep;
            _recentNext = keep % capacity;
        }
        _recent[_recentNext] = key;
        _recentNext = (_recentNext + 1) % capacity;
        _recentCount = Math.Min(_recentCount + 1, capacity);
    }

    // ------------------------------------------------------------ the runtime's bookkeeping

    /// <summary>The slot version the runtime last armed triggers and worn modifiers for.</summary>
    internal int ArmedVersion = -1;

    /// <summary>The slot count the runtime last armed for, so slots edited in place re-arm too.</summary>
    internal int ArmedCount = -1;

    /// <summary>Triggered slots by kind, subscriptions and round-robin cursors.</summary>
    internal Weaves.BookTriggers? Triggers;

    /// <summary>Sources of modifiers worn because of slotted weaves, so re-arming can take them off.</summary>
    internal List<string>? WornSources;

    /// <summary>True while the runtime keeps this book on its upkeep list.</summary>
    internal bool InUpkeep;
}
