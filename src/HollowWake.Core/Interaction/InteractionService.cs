using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Loot;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;

namespace HollowWake.Core.Interaction;

/// <summary>Raised when a player uses a portal or waypoint; the server performs the travel.</summary>
public sealed record TravelRequested(Entity Entity, double X, double Y, string? Label) : GameEvent;

/// <summary>Raised when a player opens a quest giver, listing the quest ids on offer and those ready to turn in.</summary>
public sealed record QuestsOffered(Entity Entity, Entity Giver, IReadOnlyList<string> Offers, IReadOnlyList<string> TurnIns) : GameEvent;

/// <summary>Raised when a player opens a station, so the client can show recipes.</summary>
public sealed record StationOpened(Entity Entity, Entity Station, Tags.TagSet Tags) : GameEvent;

/// <summary>
/// Dispatches player interactions by the target's interactable kind. Kinds are an open set: new ones
/// register a handler; unknown kinds still raise the generic event so quests can count them.
/// </summary>
public sealed class InteractionService
{
    private readonly Dictionary<string, Action<Simulation, Entity, Entity, Interactable>> _handlers = new(StringComparer.Ordinal);

    public InteractionService()
    {
        // A quest giver lists what it offers and, when it holds a conversation, opens it too: clients that draw the
        // conversation front it, and clients that only show quest offers keep working.
        Register("quest_giver", (sim, player, target, _) =>
        {
            Quests.QuestOffers.Publish(sim, player, target);
            Talk(sim, player, target);
        });
        Register("talk", (sim, player, target, _) => Talk(sim, player, target));
        Register("vendor", (sim, player, target, i) =>
        {
            if (i.Ref is not null && sim.TryService<Items.VendorService>(out var vendors))
            {
                vendors.Open(sim, player, target, i.Ref, out _);
            }
        });
        // A font and a stash chest open a station pane too: their services (raising stones, merging codices, resets)
        // are quoted in it, and the boards they reach stand beside it.
        Register("font", (sim, player, target, i) => OpenStation(sim, player, target, i));
        Register("stash", (sim, player, target, i) => OpenStation(sim, player, target, i));
        Register("station", (sim, player, target, i) =>
        {
            if (sim.TryService<Crafting.StationService>(out var stations))
            {
                stations.Open(sim, player, target, i.Tags);
            }
            else
            {
                sim.Events.Publish(new StationOpened(player, target, i.Tags) { Tick = sim.Clock.Tick });
            }
        });
        Register("portal", (sim, player, _, i) =>
        {
            if (i.Data.TryGetValue("x", out var xs) && i.Data.TryGetValue("y", out var ys) && double.TryParse(xs, out var x) && double.TryParse(ys, out var y))
            {
                sim.Events.Publish(new TravelRequested(player, x, y, i.Data.GetValueOrDefault("label")) { Tick = sim.Clock.Tick });
            }
        });
        Register("fount", (sim, player, target, i) =>
        {
            if (sim.TryService<TerritoryService>(out var territory))
            {
                territory.StartBinding(sim, player, target, out _);
            }
        });
        Register("marker", (sim, player, _, i) => Discover(sim, player, i.Ref, i.Data.GetValueOrDefault("name") ?? i.Ref ?? ""));
        Register("chest", (sim, player, target, i) =>
        {
            if (i.Ref is not null && sim.TryService<LootService>(out var loot) && sim.World.TryGet<Transform>(target, out var t))
            {
                var level = sim.World.Get<Stats>(player)?.Level ?? 1;
                loot.DropTable(sim, i.Ref, level, t.Position, player, target, sim.RngFor(target, "chest"));
                i.Kind = "opened";
                i.Ref = null;
            }
        });
        Register("resource", (sim, player, target, i) =>
        {
            if (i.Ref is not null && sim.TryService<LootService>(out var loot))
            {
                var level = sim.World.Get<Stats>(player)?.Level ?? 1;
                loot.GiveTable(sim, player, i.Ref, level, sim.RngFor(target, "harvest:" + sim.Clock.Tick));
                var respawn = sim.Content.Rules.Number("world.resource_respawn_seconds", 180);
                var kind = i.Kind;
                var reference = i.Ref;
                i.Kind = "depleted";
                i.Ref = null;
                sim.Scheduler.After(respawn, () =>
                {
                    if (sim.World.IsAlive(target))
                    {
                        i.Kind = kind;
                        i.Ref = reference;
                    }
                });
            }
        });
    }

    public void Register(string kind, Action<Simulation, Entity, Entity, Interactable> handler) => _handlers[kind] = handler;

    private static void OpenStation(Simulation sim, Entity player, Entity target, Interactable i)
    {
        if (sim.TryService<Crafting.StationService>(out var stations))
        {
            stations.Open(sim, player, target, i.Tags.With(i.Kind));
        }
    }

    /// <summary>Opens the conversation the entity holds for the hero, if any.</summary>
    private static void Talk(Simulation sim, Entity player, Entity target)
    {
        if (sim.TryService<Dialogue.DialogueService>(out var dialogues) && dialogues.For(sim, player, target) is { } conversation)
        {
            dialogues.Open(sim, player, target, conversation, out _);
        }
    }

    public bool Interact(Simulation sim, Entity player, Entity target, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Interactable>(target, out var i) || !sim.World.TryGet<Transform>(target, out var tt) || !sim.World.TryGet<Transform>(player, out var pt))
        {
            reason = "not_interactable";
            return false;
        }
        if (sim.World.Has<Dead>(player))
        {
            reason = "dead";
            return false;
        }
        if (pt.GroundDistanceTo(tt.Position) > i.Range + pt.Radius + tt.Radius)
        {
            reason = "too_far";
            return false;
        }
        sim.Events.Publish(new Interacted(player, target, i.Kind, i.Ref) { Tick = sim.Clock.Tick });
        if (_handlers.TryGetValue(i.Kind, out var handler))
        {
            handler(sim, player, target, i);
        }
        return true;
    }

    public static void Discover(Simulation sim, Entity player, string? poiId, string name)
    {
        if (poiId is null || !sim.World.TryGet<QuestLog>(player, out var log))
        {
            return;
        }
        if (log.Discovered.Add(poiId))
        {
            sim.Events.Publish(new PoiDiscovered(player, poiId, name) { Tick = sim.Clock.Tick });
        }
    }
}

/// <summary>Discovers points of interest as players walk into them.</summary>
public sealed class DiscoverySystem : SimSystem
{
    public override string Name => "discovery";
    public override SimPhase Phase => SimPhase.React;
    public override int Order => 5;

    private double _nextAt = double.NegativeInfinity;

    public override void Update(Simulation sim)
    {
        if (sim.Clock.Time < _nextAt || sim.Generator is null || sim.Zone is null)
        {
            return;
        }
        _nextAt = sim.Clock.Time + 1.0;
        var pois = sim.Generator.PoisAround(sim.Zone.Value).ToList();
        foreach (var (e, _, t) in sim.World.Query<Player, Transform>())
        {
            foreach (var poi in pois)
            {
                var dx = t.Position.X - poi.X;
                var dy = t.Position.Y - poi.Y;
                var r = poi.Def.Radius + 6;
                if (dx * dx + dy * dy <= r * r)
                {
                    InteractionService.Discover(sim, e, poi.Id, poi.Name);
                }
            }
        }
    }
}
