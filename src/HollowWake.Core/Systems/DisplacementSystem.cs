using HollowWake.Core.Sim;

namespace HollowWake.Core.Systems;

/// <summary>
/// Moves bodies that are being displaced over time: dashes, leaps and impulses such as knockback, with
/// their hits along the way and their landings. Runs after ordinary movement and before projectiles.
/// </summary>
public sealed class DisplacementSystem : SimSystem
{
    public override string Name => "displacement";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 5;

    public override void Update(Simulation sim)
    {
    }
}
