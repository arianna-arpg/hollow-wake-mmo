using HollowWake.Core.Sim;

namespace HollowWake.Core.Systems;

/// <summary>
/// Charges the running costs of toggles and contracts each tick and turns off whatever its bearer can no
/// longer pay for. Runs with the statuses, after regeneration.
/// </summary>
public sealed class UpkeepSystem : SimSystem
{
    public override string Name => "upkeep";
    public override SimPhase Phase => SimPhase.Status;
    public override int Order => 5;

    public override void Update(Simulation sim)
    {
    }
}
