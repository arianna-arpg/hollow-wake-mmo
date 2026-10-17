namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// A held guard absorbs hits inside its arc into its own pool before evasion is rolled. Bodies without a
/// guard pass through untouched, which is every body until guard stances exist.
/// </summary>
internal sealed class GuardStage : IHitStage
{
    public string Name => "guard";

    public HitFlow Run(HitScope hit) => HitFlow.Continue;
}
