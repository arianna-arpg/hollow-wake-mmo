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

/// <summary>
/// A sender that is not a client and hears an intent's refusal at once instead of through an
/// <see cref="IntentRejected"/> event: the AI, which dispatches intents during its think and records each
/// refusal on that think. Refusals of a client's intents travel as the event, to the client that sent them.
/// </summary>
public interface IIntentSender
{
    void Refused(IIntent intent, string reason);
}

/// <summary>
/// What an intent handler gets: the simulation, the sending entity, the intent, and the player component when
/// the sender is a player. Any actor sends intents: players through the queue the intent system drains, the
/// AI through <c>Ai.AiIntents</c> during its think. A handler that needs a player's state is registered
/// player-only and never sees another sender.
/// </summary>
public readonly struct IntentContext
{
    private readonly Player? _player;
    private readonly IIntentSender? _sender;

    /// <summary>A player's queued intent: refusals are published as <see cref="IntentRejected"/>.</summary>
    public IntentContext(Simulation sim, Entity entity, Player player, IIntent intent)
        : this(sim, entity, intent, player, null)
    {
    }

    /// <summary>An intent any actor sends; refusals go to <paramref name="sender"/> when given, else they are published.</summary>
    public IntentContext(Simulation sim, Entity entity, IIntent intent, IIntentSender? sender)
        : this(sim, entity, intent, null, sender)
    {
    }

    private IntentContext(Simulation sim, Entity entity, IIntent intent, Player? player, IIntentSender? sender)
    {
        Sim = sim;
        Entity = entity;
        Intent = intent;
        _player = player;
        _sender = sender;
    }

    public Simulation Sim { get; }
    public Entity Entity { get; }
    public IIntent Intent { get; }

    /// <summary>The sender's player component; null when the sender is not a player.</summary>
    public Player? Player => _player ?? Sim.World.Get<Player>(Entity);

    public bool Dead => Sim.World.Has<Dead>(Entity);
    public Motion? Motion => Sim.World.Get<Motion>(Entity);
    public Transform? Transform => Sim.World.Get<Transform>(Entity);

    /// <summary>Refuses the intent with a reason: told to a sender that listens, else published for the client to show.</summary>
    public void Reject(string reason)
    {
        if (_sender is not null)
        {
            _sender.Refused(Intent, reason);
            return;
        }
        IntentSystem.Reject(Sim, Entity, Intent, reason);
    }
}

public delegate void IntentHandler<in T>(in IntentContext ctx, T intent) where T : IIntent;

/// <summary>
/// The registry of intent handlers, one per intent type. Each area registers its handlers from its own
/// file under <c>Systems/Intents/</c>; mods register more, or replace one, on the same registry. An intent
/// from a dead sender is refused unless its handler was registered to run while dead, and an intent whose
/// handler needs a player's state (a character, a session, a bag) is registered player-only and refused
/// from any other actor.
/// </summary>
public sealed class IntentHandlers
{
    /// <summary>Why no handler ran: none takes the intent's type.</summary>
    public const string Unknown = "unknown_intent";

    /// <summary>Why no handler ran: the sender is dead and the handler does not run while dead.</summary>
    public const string Dead = "dead";

    /// <summary>Why no handler ran: the handler is a player's and the sender is not a player.</summary>
    public const string PlayersOnly = "players_only";

    private abstract class Entry
    {
        public bool WhileDead;
        public bool PlayerOnly;
        public abstract void Invoke(in IntentContext ctx, IIntent intent);
    }

    private sealed class Entry<T> : Entry where T : IIntent
    {
        public required IntentHandler<T> Handler;
        public override void Invoke(in IntentContext ctx, IIntent intent) => Handler(ctx, (T)intent);
    }

    private readonly Dictionary<Type, Entry> _handlers = new();

    /// <summary>
    /// Registers the handler for one intent type, replacing any earlier one. <paramref name="playerOnly"/>
    /// marks a handler that works a player's state: any other sender is refused before it runs.
    /// </summary>
    public void Register<T>(IntentHandler<T> handler, bool whileDead = false, bool playerOnly = false) where T : IIntent =>
        _handlers[typeof(T)] = new Entry<T> { Handler = handler, WhileDead = whileDead, PlayerOnly = playerOnly };

    public bool Has<T>() where T : IIntent => _handlers.ContainsKey(typeof(T));

    /// <summary>Whether the handler for an intent type takes players only; false when none is registered.</summary>
    public bool IsPlayerOnly<T>() where T : IIntent => _handlers.TryGetValue(typeof(T), out var entry) && entry.PlayerOnly;

    public IEnumerable<Type> IntentTypes => _handlers.Keys;

    /// <summary>
    /// Runs the handler for an intent: null when it ran (it may still have refused through the context's
    /// <see cref="IntentContext.Reject"/>), else why none did: <see cref="Unknown"/>, <see cref="Dead"/> or
    /// <see cref="PlayersOnly"/>.
    /// </summary>
    public string? Dispatch(in IntentContext ctx)
    {
        if (!_handlers.TryGetValue(ctx.Intent.GetType(), out var entry))
        {
            return Unknown;
        }
        if (!entry.WhileDead && ctx.Dead)
        {
            return Dead;
        }
        if (entry.PlayerOnly && ctx.Player is null)
        {
            return PlayersOnly;
        }
        entry.Invoke(ctx, ctx.Intent);
        return null;
    }

    /// <summary>
    /// Runs the handler for an intent. False when no handler takes the intent's type, when its sender is dead
    /// and the handler does not run while dead, or when the handler is a player's and the sender is not.
    /// </summary>
    public bool TryHandle(in IntentContext ctx, out bool refusedDead)
    {
        var refusal = Dispatch(ctx);
        refusedDead = ReferenceEquals(refusal, Dead);
        return refusal is null;
    }

    /// <summary>The simulation's intent handlers, the default set registered on first use where the composition root did not register one.</summary>
    public static IntentHandlers Of(Simulation sim)
    {
        if (!sim.TryService<IntentHandlers>(out var handlers))
        {
            handlers = CreateDefault();
            sim.AddService(handlers);
        }
        return handlers;
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
/// The AI sends the same intents through the same registry during its think (<c>Ai.AiIntents</c>).
/// </summary>
public sealed class IntentSystem : SimSystem
{
    public override string Name => "intents";
    public override SimPhase Phase => SimPhase.Input;

    public override void Update(Simulation sim)
    {
        var handlers = IntentHandlers.Of(sim);
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

    internal static void Reject(Simulation sim, Entity e, IIntent intent, string reason) =>
        sim.Events.Publish(new IntentRejected(e, intent.GetType().Name.Replace("Intent", ""), reason) { Tick = sim.Clock.Tick });

    private static void Handle(Simulation sim, IntentHandlers handlers, Entity e, Player player, IIntent intent)
    {
        var ctx = new IntentContext(sim, e, player, intent);
        var refusal = handlers.Dispatch(ctx);
        if (refusal is null)
        {
            return;
        }
        // A dead player hears "dead" whatever it sent.
        Reject(sim, e, intent, sim.World.Has<Dead>(e) ? IntentHandlers.Dead : refusal);
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
