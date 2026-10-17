namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Effects over named charges: banking, draining and spending them (<c>charge</c>). The charge service and
/// the <c>Charges</c> component hold the counts; handlers registered here change them.
/// </summary>
public static class ChargeHandlers
{
    public static void Register(EffectRegistry r)
    {
        _ = r;
    }
}
