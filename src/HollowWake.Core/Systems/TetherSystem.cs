using HollowWake.Core.Sim;

namespace HollowWake.Core.Systems;

/// <summary>
/// Pulses the live lines between linked bodies and points, testing each as a segment against whoever
/// crosses it. Runs after projectiles and fields have moved.
/// </summary>
public sealed class TetherSystem : SimSystem
{
    public override string Name => "tethers";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 12;

    public override void Update(Simulation sim)
    {
    }
}
