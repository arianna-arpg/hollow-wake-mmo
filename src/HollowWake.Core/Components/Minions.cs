using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>Summons and companions belong to someone and time out.</summary>
public sealed class Minion : IComponent
{
    public Entity Owner = Entity.None;
    public double ExpiresAt = double.PositiveInfinity;
    public string? Group;
}
