using System.Collections.Concurrent;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;

namespace HollowWake.Server.World;

/// <summary>Reusable event-family adapters. Skill names and actor roles never select a presentation path.</summary>
public static class OutcomeCueAdapters
{
    private static readonly ConcurrentDictionary<Type, Action<ZoneInstance, GameEvent>> Adapters = new();

    static OutcomeCueAdapters()
    {
        Register<DamageDealt>((zone, e) =>
        {
            if (e.Evaded) zone.PresentOutcome("evade", e.Source, e.Target, e.Tags);
            else if (e.Blocked) zone.PresentOutcome("block", e.Source, e.Target, e.Tags);
            else if (e.CastId == 0 && e.Total > 0) zone.PresentOutcome("damage", e.Source, e.Target, e.Tags);
            // Correlated positive damage already has its cast impact. Do not duplicate it.
        });
        Register<Healed>((zone, e) => { if (e.Amount > 0) zone.PresentOutcome("heal", e.Source, e.Target, TagSet.Of(e.Resource)); });
        Register<StatusApplied>((zone, e) =>
        {
            var beneficial = zone.Sim.Content.TryGet<StatusDef>(e.Status, out var status) && status!.Beneficial;
            zone.PresentOutcome("status", e.Source, e.Target, TagSet.Of("status", "status." + e.Status, beneficial ? "beneficial" : "harmful"));
        });
        Register<StatusExpired>((zone, e) => zone.PresentOutcome("status_expired", Entity.None, e.Target, TagSet.Of("status", "status." + e.Status)));
        Register<EntityRevived>((zone, e) => zone.PresentOutcome("revive", Entity.None, e.Entity, TagSet.Empty));
    }

    public static void Register<T>(Action<ZoneInstance, T> adapter) where T : GameEvent =>
        Adapters[typeof(T)] = (zone, e) => adapter(zone, (T)e);

    public static void Present(ZoneInstance zone, GameEvent e)
    {
        if (zone.Sim.EmitCastPresentation && Adapters.TryGetValue(e.GetType(), out var adapter)) adapter(zone, e);
    }
}
