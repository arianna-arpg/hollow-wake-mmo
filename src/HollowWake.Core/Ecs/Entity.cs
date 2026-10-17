namespace HollowWake.Core.Ecs;

/// <summary>
/// A handle to a simulated thing: an index into the world plus a generation so stale handles never
/// alias a recycled slot. <see cref="Key"/> is the stable 64-bit form used on the wire and in saves.
/// </summary>
public readonly record struct Entity(int Index, int Generation)
{
    public static readonly Entity None = new(-1, 0);

    public bool IsNone => Index < 0;

    public long Key => ((long)Generation << 32) | (uint)Index;

    public static Entity FromKey(long key) => new((int)(uint)(key & 0xFFFFFFFF), (int)(key >> 32));

    public override string ToString() => IsNone ? "E(none)" : $"E{Index}.{Generation}";
}

/// <summary>Marker for component classes. Components are plain mutable state; systems hold the logic.</summary>
public interface IComponent
{
}
