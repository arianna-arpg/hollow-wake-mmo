using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems.Intents;

namespace HollowWake.Core.Systems;

/// <summary>Raised when the simulation refuses an intent, so the client can show why.</summary>
public sealed record IntentRejected(Entity Entity, string Intent, string Reason) : GameEvent;

/// <summary>What an intent handler gets: the simulation, the player's entity and component, and the intent.</summary>
public readonly struct IntentContext
{
    public IntentContext(Simulation sim, Entity entity, Player player, IIntent intent)
    {
        Sim = sim;
        Entity = entity;
        Player = player;
        Intent = intent;
    }

    public Simulation Sim { get; }
    public Entity Entity { get; }
    public Player Player { get; }
    public IIntent Intent { get; }

    public bool Dead => Sim.World.Has<Dead>(Entity);
    public Motion? Motion => Sim.World.Get<Motion>(Entity);
    public Transform? Transform => Sim.World.Get<Transform>(Entity);

    /// <summary>Refuses the intent with a reason the client can show.</summary>
    public void Reject(string reason) => IntentSystem.Reject(Sim, Entity, Intent, reason);
}

public delegate void IntentHandler<in T>(in IntentContext ctx, T intent) where T : IIntent;

/// <summary>
/// The registry of intent handlers, one per intent type. Each area registers its handlers from its own
/// file under <c>Systems/Intents/</c>; mods register more, or replace one, on the same registry. An intent
/// from a dead player is refused unless its handler was registered to run while dead.
/// </summary>
public sealed class IntentHandlers
{
    private abstract class Entry
    {
        public bool WhileDead;
        public abstract void Invoke(in IntentContext ctx, IIntent intent);
    }

    private sealed class Entry<T> : Entry where T : IIntent
    {
        public required IntentHandler<T> Handler;
        public override void Invoke(in IntentContext ctx, IIntent intent) => Handler(ctx, (T)intent);
    }

    private readonly Dictionary<Type, Entry> _handlers = new();

    /// <summary>Registers the handler for one intent type, replacing any earlier one.</summary>
    public void Register<T>(IntentHandler<T> handler, bool whileDead = false) where T : IIntent =>
        _handlers[typeof(T)] = new Entry<T> { Handler = handler, WhileDead = whileDead };

    public bool Has<T>() where T : IIntent => _handlers.ContainsKey(typeof(T));

    public IEnumerable<Type> IntentTypes => _handlers.Keys;

    /// <summary>
    /// Runs the handler for an intent. False when no handler takes the intent's type, or when its sender is
    /// dead and the handler does not run while dead.
    /// </summary>
    public bool TryHandle(in IntentContext ctx, out bool refusedDead)
    {
        refusedDead = false;
        if (!_handlers.TryGetValue(ctx.Intent.GetType(), out var entry))
        {
            return false;
        }
        if (!entry.WhileDead && ctx.Dead)
        {
            refusedDead = true;
            return false;
        }
        entry.Invoke(ctx, ctx.Intent);
        return true;
    }

    public static IntentHandlers CreateDefault()
    {
        var handlers = new IntentHandlers();
        MovementIntentHandlers.Register(handlers);
        WeaveIntentHandlers.Register(handlers);
        ItemIntentHandlers.Register(handlers);
        ProgressionIntentHandlers.Register(handlers);
        WorldIntentHandlers.Register(handlers);
        return handlers;
    }
}

/// <summary>
/// Consumes queued player intents and dispatches each to its handler in the intent handler registry.
/// Every intent is validated by its handler or the service it calls; nothing a client sends is trusted.
/// </summary>
public sealed class IntentSystem : SimSystem
{
    public override string Name => "intents";
    public override SimPhase Phase => SimPhase.Input;

    public override void Update(Simulation sim)
    {
        var handlers = HandlersOf(sim);
        var maxPerTick = (int)sim.Content.Rules.Number("server.max_intents_per_tick", 8);
        foreach (var (e, player) in sim.World.Query<Player>())
        {
            var handled = 0;
            while (player.Intents.Count > 0 && handled++ < maxPerTick)
            {
                var intent = player.Intents.Dequeue();
                try
                {
                    Handle(sim, handlers, e, player, intent);
                }
                catch (Exception ex)
                {
                    sim.Log.Error($"intent {intent.GetType().Name} failed for {e}", ex);
                }
            }
            if (sim.World.Has<Dead>(e) && sim.Clock.Time >= player.RespawnAt && sim.Content.Rules.Flag("combat.auto_respawn", true))
            {
                Respawn(sim, e);
            }
        }
    }

    private static IntentHandlers HandlersOf(Simulation sim)
    {
        if (!sim.TryService<IntentHandlers>(out var handlers))
        {
            handlers = IntentHandlers.CreateDefault();
            sim.AddService(handlers);
        }
        return handlers;
    }

    internal static void Reject(Simulation sim, Entity e, IIntent intent, string reason) =>
        sim.Events.Publish(new IntentRejected(e, intent.GetType().Name.Replace("Intent", ""), reason) { Tick = sim.Clock.Tick });

    private static void Handle(Simulation sim, IntentHandlers handlers, Entity e, Player player, IIntent intent)
    {
        var ctx = new IntentContext(sim, e, player, intent);
        if (handlers.TryHandle(ctx, out var refusedDead))
        {
            return;
        }
        Reject(sim, e, intent, refusedDead || sim.World.Has<Dead>(e) ? "dead" : "unknown_intent");
    }

    public static void Respawn(Simulation sim, Entity e)
    {
        Vector3 at;
        if (sim.Generator is not null && sim.Zone is not null)
        {
            var (x, y) = sim.Generator.StartPoint(sim.Zone.Value);
            at = new Vector3((float)x, (float)y, 0);
        }
        else
        {
            at = sim.World.Get<Transform>(e)?.Position ?? default;
        }
        var fraction = sim.Content.Rules.Number("combat.respawn_life_fraction", 1);
        sim.Service<DeathService>().Revive(sim, e, at, fraction);
    }
}
