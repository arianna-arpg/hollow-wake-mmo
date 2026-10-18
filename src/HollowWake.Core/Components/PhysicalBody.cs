using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>Rest envelope and material mixture; presentation is never consulted for physics.</summary>
public sealed class PhysicalBody : IComponent
{
    public required PhysicalBodySpec Profile { get; init; }
    public double Density { get; init; }
    public double Radius => Math.Max(Profile.Width, Profile.Depth) / 2;
}
