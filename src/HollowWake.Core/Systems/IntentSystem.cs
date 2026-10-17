using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;
using HollowWake.Core.Progression;
using HollowWake.Core.Quests;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;

namespace HollowWake.Core.Systems;

/// <summary>Raised when the simulation refuses an intent, so the client can show why.</summary>
public sealed record IntentRejected(Entity Entity, string Intent, string Reason) : GameEvent;

/// <summary>
/// Consumes queued player intents and turns them into simulation actions. Every intent is validated
/// here or by the service it calls; nothing a client sends is trusted.
/// </summary>
public sealed class IntentSystem : SimSystem
{
    public override string Name => "intents";
    public override SimPhase Phase => SimPhase.Input;

    public override void Update(Simulation sim)
    {
        var maxPerTick = (int)sim.Content.Rules.Number("server.max_intents_per_tick", 8);
        foreach (var (e, player) in sim.World.Query<Player>())
        {
            var handled = 0;
            while (player.Intents.Count > 0 && handled++ < maxPerTick)
            {
                var intent = player.Intents.Dequeue();
                try
                {
                    Handle(sim, e, player, intent);
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

    private static void Reject(Simulation sim, Entity e, IIntent intent, string reason) =>
        sim.Events.Publish(new IntentRejected(e, intent.GetType().Name.Replace("Intent", ""), reason) { Tick = sim.Clock.Tick });

    private void Handle(Simulation sim, Entity e, Player player, IIntent intent)
    {
        var dead = sim.World.Has<Dead>(e);
        var motion = sim.World.Get<Motion>(e);
        var transform = sim.World.Get<Transform>(e);
        switch (intent)
        {
            case RespawnIntent:
                if (dead)
                {
                    Respawn(sim, e);
                }
                return;
        }
        if (dead)
        {
            Reject(sim, e, intent, "dead");
            return;
        }
        switch (intent)
        {
            case MoveToIntent m when motion is not null && transform is not null:
            {
                var target = sim.Terrain.SnapToGround(m.Point, 3);
                var path = sim.Service<Pathfinder>().FindPath(transform.Position, target);
                motion.MoveDir = Vector2.Zero;
                motion.Path.Clear();
                if (path.Count > 0)
                {
                    motion.Path.AddRange(path);
                    motion.StopDistance = 0.3f;
                }
                else
                {
                    motion.MoveTarget = target;
                }
                break;
            }
            case MoveDirIntent d when motion is not null:
                motion.Path.Clear();
                motion.MoveTarget = null;
                motion.MoveDir = d.Direction.LengthSquared() > 1 ? Vector2.Normalize(d.Direction) : d.Direction;
                break;
            case StopIntent when motion is not null:
                motion.Stop();
                break;
            case JumpIntent when motion is not null:
                motion.JumpRequested = true;
                break;
            case UseWeaveIntent u:
            {
                if (!sim.World.TryGet<WeaveBook>(e, out var book) || u.Slot < 0 || u.Slot >= book.Slots.Count)
                {
                    Reject(sim, e, intent, "no_slot");
                    break;
                }
                var target = sim.World.IsAlive(u.Target) ? u.Target : Entity.None;
                if (!sim.Service<WeaveRuntime>().TryBegin(sim, e, book.Slots[u.Slot], target, u.Point, u.Direction, out var reason))
                {
                    Reject(sim, e, intent, reason);
                }
                break;
            }
            case CancelCastIntent:
                sim.Service<WeaveRuntime>().Interrupt(sim, e);
                break;
            case InteractIntent i:
                if (!sim.Service<InteractionService>().Interact(sim, e, i.Target, out var ireason))
                {
                    Reject(sim, e, intent, ireason);
                }
                break;
            case PickUpIntent p:
                if (!sim.Service<LootService>().PickUp(sim, e, p.Drop, out var preason))
                {
                    Reject(sim, e, intent, preason);
                }
                break;
            case EquipIntent eq:
            {
                var inv = sim.World.Get<Inventory>(e);
                var item = inv?.Find(eq.ItemUid);
                if (item is null)
                {
                    Reject(sim, e, intent, "no_item");
                    break;
                }
                if (!sim.Service<EquipmentService>().Equip(sim, e, item, eq.Slot, fromInventory: true, out var ereason))
                {
                    Reject(sim, e, intent, ereason);
                }
                break;
            }
            case UnequipIntent un:
                if (!sim.Service<EquipmentService>().Unequip(sim, e, un.Slot, out var ureason))
                {
                    Reject(sim, e, intent, ureason);
                }
                break;
            case DropItemIntent di:
            {
                var inv = sim.World.Get<Inventory>(e);
                var item = inv?.Find(di.ItemUid);
                if (item is null || inv is null || transform is null)
                {
                    Reject(sim, e, intent, "no_item");
                    break;
                }
                inv.Remove(item);
                sim.Service<LootService>().Drop(sim, item, transform.Position, Entity.None, e);
                break;
            }
            case AllocatePassiveIntent ap:
                if (!sim.Service<ProgressionService>().Allocate(sim, e, ap.NodeId, out var areason))
                {
                    Reject(sim, e, intent, areason);
                }
                break;
            case SetWeaveIntent sw:
            {
                var book = sim.World.GetOrAdd<WeaveBook>(e);
                var compiler = sim.Service<WeaveCompiler>();
                if (!compiler.TryCompile(sw.Spec, out var compiled, out var error))
                {
                    Reject(sim, e, intent, "invalid:" + error);
                    break;
                }
                var maxSlots = (int)sim.Content.Rules.Number("weaves.max_slots", 8);
                if (sw.Slot < 0 || sw.Slot >= maxSlots)
                {
                    Reject(sim, e, intent, "bad_slot");
                    break;
                }
                while (book.Slots.Count <= sw.Slot)
                {
                    book.Slots.Add(new WeaveSlot { Spec = new Definitions.WeaveSpec { Preset = null, Form = null } });
                }
                book.Slots[sw.Slot] = new WeaveSlot { Spec = sw.Spec, Compiled = compiled };
                break;
            }
            case AcceptQuestIntent aq:
                if (!sim.Service<QuestService>().Accept(sim, e, aq.QuestId, out var qreason))
                {
                    Reject(sim, e, intent, qreason);
                }
                break;
            case TurnInQuestIntent tq:
                if (!sim.Service<QuestService>().TurnIn(sim, e, tq.QuestId, out var treason))
                {
                    Reject(sim, e, intent, treason);
                }
                break;
            case CraftStartIntent cs:
                if (sim.Service<CraftingService>().Start(sim, e, cs.RecipeId, cs.Station, out var creason) is null)
                {
                    Reject(sim, e, intent, creason);
                }
                break;
            case CraftSubmitIntent csub:
                if (sim.Service<CraftingService>().Submit(sim, e, csub.RecipeId, csub.Input, out var sreason) is null)
                {
                    Reject(sim, e, intent, sreason);
                }
                break;
            case BindFountIntent bf:
                if (!sim.Service<TerritoryService>().StartBinding(sim, e, bf.Fount, out var breason))
                {
                    Reject(sim, e, intent, breason);
                }
                break;
            case EnactDecreeIntent ed:
                if (!sim.Service<TerritoryService>().Enact(sim, e, ed.HoldingId, ed.DecreeId, out var dreason))
                {
                    Reject(sim, e, intent, dreason);
                }
                break;
            case BuildStructureIntent bs:
                if (!sim.Service<TerritoryService>().Build(sim, e, bs.HoldingId, bs.StructureId, bs.Point, out var sreason2))
                {
                    Reject(sim, e, intent, sreason2);
                }
                break;
            default:
                Reject(sim, e, intent, "unknown_intent");
                break;
        }
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
