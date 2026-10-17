using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>A weave bound to a slot, with its compiled form cached until the spec or owner changes.</summary>
public sealed class WeaveSlot
{
    public required WeaveSpec Spec;
    public Weaves.CompiledWeave? Compiled;
    public string Key => Spec.ToString();

    /// <summary>Scopes the weave runtime keeps for the compiled weave and its caster. A cache, not state.</summary>
    internal Weaves.WeaveSlotCache? RuntimeCache;

    private Dictionary<string, double>? _state;

    /// <summary>
    /// What the slot remembers between casts: use banks, trigger latches, combo cursors, brims, marks, by
    /// name. Unlike <see cref="RuntimeCache"/> this is state, kept as long as the slot holds its weave.
    /// </summary>
    public Dictionary<string, double> State => _state ??= new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>True once anything was written to <see cref="State"/>, so readers need not create it.</summary>
    public bool HasState => _state is { Count: > 0 };
}

/// <summary>A cast in progress: what it aims at, when it started and how it is being held.</summary>
public sealed class CastState
{
    public required WeaveSlot Slot;
    public Entity Target = Entity.None;
    public Vector3 Point;
    public Vector2 Direction;
    public double StartedAt;
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

    /// <summary>
    /// The fraction of movement speed the caster keeps while the cast runs: zero roots, one moves freely.
    /// Set when the cast begins; the movement system reads it.
    /// </summary>
    public float MoveFactor;

    private List<Modifier>? _modifiers;

    /// <summary>
    /// Modifiers local to this cast, such as ramps and charge-up scales, joining the weave's fold while
    /// it lasts. Created on first use.
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

    public WeaveSlot Add(WeaveSpec spec)
    {
        var slot = new WeaveSlot { Spec = spec };
        Slots.Add(slot);
        return slot;
    }

    public WeaveSlot? Find(string key) => Slots.FirstOrDefault(s => s.Key == key || s.Spec.Preset == key || s.Spec.Name == key);

    public bool IsReady(WeaveSlot slot, double now) => !ReadyAt.TryGetValue(slot.Key, out var t) || t <= now;

    public void InvalidateCompiled()
    {
        foreach (var s in Slots)
        {
            s.Compiled = null;
        }
    }
}
