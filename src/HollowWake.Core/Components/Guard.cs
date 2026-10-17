using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>
/// A held guard on its bearer: a frontal arc that absorbs hits into its own pool. The <c>guard</c> hit stage
/// reads and drains it; the weave runtime raises it when a guard-mode cast begins and takes it off when the
/// cast ends, bashing if the guard was armed. A fresh guard parries whole hits for a moment.
/// </summary>
public sealed class Guard : IComponent
{
    /// <summary>The slot whose cast holds the guard.</summary>
    public required WeaveSlot Slot;

    /// <summary>The cast that holds the guard; the guard is stale once the bearer's cast is another.</summary>
    public required CastState Cast;

    public double Pool;
    public double MaxPool;

    /// <summary>The cosine of half the guarded arc: hits from directions within it are absorbed.</summary>
    public double ArcCos;

    /// <summary>Until when a hit inside the arc is parried whole.</summary>
    public double ParryUntil;

    /// <summary>Damage absorbed so far, and hits absorbed or parried.</summary>
    public double Absorbed;
    public int Hits;

    /// <summary>Set by the hit stage: the pool broke, or a hit was parried; the runtime ends the guard on its next beat.</summary>
    public bool Broken;
    public bool Parried;

    /// <summary>When the guard's pulse next fires.</summary>
    public double NextPulseAt;
}
