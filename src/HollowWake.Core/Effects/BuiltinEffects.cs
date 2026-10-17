using HollowWake.Core.Effects.Handlers;

namespace HollowWake.Core.Effects;

/// <summary>
/// The built-in effect vocabulary. Each handler lives in Handlers/ grouped by family; each compiles its
/// parameters once and executes cheaply. Mods register more handlers on the same registry.
/// </summary>
public static class BuiltinEffects
{
    public static void RegisterAll(EffectRegistry r)
    {
        r.Register(new DamageHandler());
        r.Register(new HealHandler());
        r.Register(new ResourceHandler());
        r.Register(new ApplyStatusHandler());
        r.Register(new RemoveStatusHandler());
        r.Register(new ModifyHandler());
        r.Register(new AreaHandler());
        r.Register(new ProjectileHandler());
        r.Register(new ChainHandler());
        r.Register(new SummonHandler());
        r.Register(new SpawnHandler());
        r.Register(new FieldHandler());
        r.Register(new MoveHandler());
        r.Register(new ConditionHandler());
        r.Register(new RandomHandler());
        r.Register(new RepeatHandler());
        r.Register(new SequenceHandler());
        r.Register(new PublishEventHandler());
        r.Register(new InterruptHandler());
        r.Register(new TauntHandler());
        r.Register(new GrantItemHandler());
        r.Register(new GrantXpHandler());
        r.Register(new LogHandler());
    }
}
