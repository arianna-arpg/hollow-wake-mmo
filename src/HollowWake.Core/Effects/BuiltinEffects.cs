using HollowWake.Core.Effects.Handlers;

namespace HollowWake.Core.Effects;

/// <summary>
/// The built-in effect vocabulary. Each family of handlers lives in its own file under Handlers/ and
/// registers itself through a static <c>Register(EffectRegistry)</c>; this hub only calls them. New
/// handlers go into their family's file (or a new family file with its own <c>Register</c>), never here.
/// Mods register more handlers on the same registry.
/// </summary>
public static class BuiltinEffects
{
    public static void RegisterAll(EffectRegistry r)
    {
        DamageHandlers.Register(r);
        StatusHandlers.Register(r);
        ChargeHandlers.Register(r);
        StatusTransferHandlers.Register(r);
        DeliveryHandlers.Register(r);
        PlacementHandlers.Register(r);
        MovementHandlers.Register(r);
        SummonHandlers.Register(r);
        MinionHandlers.Register(r);
        AiHandlers.Register(r);
        FlowHandlers.Register(r);
        CastHandlers.Register(r);
        UtilityHandlers.Register(r);
    }
}
