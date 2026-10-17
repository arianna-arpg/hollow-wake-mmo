using HollowWake.Core.Sim;

namespace HollowWake.Core.Systems;

/// <summary>
/// Keeps minions in step with their owners: what they inherit from the owner, contracts that respawn the
/// fallen, and the decay of what does not last. Runs after spawners and before behaviors think.
/// </summary>
public sealed class MinionSystem : SimSystem
{
    public override string Name => "minions";
    public override SimPhase Phase => SimPhase.Think;
    public override int Order => -5;

    public override void Update(Simulation sim)
    {
    }
}
