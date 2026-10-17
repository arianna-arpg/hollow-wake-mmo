using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Progression;
using HollowWake.Core.Quests;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;

namespace HollowWake.Server.World;

/// <summary>Turns simulation state into wire state. Nothing here mutates the simulation.</summary>
public static class Snapshots
{
    public static EntityState Capture(Simulation sim, Entity e)
    {
        var t = sim.World.Get<Transform>(e);
        var id = sim.World.Get<Identity>(e);
        var stats = sim.World.Get<Stats>(e);
        var motion = sim.World.Get<Motion>(e);
        var statuses = sim.World.Get<Statuses>(e);
        var book = sim.World.Get<WeaveBook>(e);
        var interactable = sim.World.Get<Interactable>(e);
        var casting = book?.Casting;
        var castProgress = casting is null || casting.Duration <= 0 ? 0f : (float)Math.Clamp((sim.Clock.Time - casting.StartedAt) / casting.Duration, 0, 1);
        var state = new EntityState
        {
            Key = e.Key,
            Name = id?.Name ?? "",
            Pattern = id?.Pattern,
            Look = id?.Look,
            X = t?.Position.X ?? 0,
            Y = t?.Position.Y ?? 0,
            Z = t?.Position.Z ?? 0,
            Yaw = t?.Yaw ?? 0,
            Radius = t?.Radius ?? 0.4f,
            Height = t?.Height ?? 1.8f,
            Faction = id?.Faction,
            Rarity = id?.Rarity,
            IsPlayer = id?.IsPlayer ?? false,
            Level = stats?.Level ?? 0,
            Flags = stats is null ? new List<string>() : stats.Flags.Where(f => !f.StartsWith("status:", StringComparison.Ordinal)).ToList(),
            Statuses = statuses?.Active.Select(s => s.Id).Distinct().ToList() ?? new List<string>(),
            Interactable = interactable?.Kind,
            Dead = sim.World.Has<Dead>(e),
            Casting = casting?.Slot.Compiled?.Name ?? casting?.Slot.Key,
            CastProgress = castProgress,
            Grounded = motion?.Grounded ?? true,
            VX = motion?.Velocity.X ?? 0,
            VY = motion?.Velocity.Y ?? 0,
            VZ = motion?.Velocity.Z ?? 0,
        };
        if (stats is not null)
        {
            foreach (var (res, cur) in stats.Resources)
            {
                state.Resources[res] = (float)cur;
                state.ResourceMax[res] = (float)stats.ResourceMax(res);
            }
        }
        return state;
    }

    public static ItemInfo Item(ItemResolver resolver, ItemInstance item)
    {
        var b = resolver.BaseOf(item);
        var local = resolver.LocalAttributes(item);
        var locals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var attr in local.BaseOverrides.Select(kv => kv.Key).Concat(local.ModifiedAttributes).Distinct())
        {
            locals[attr] = Math.Round(ItemResolver.LocalValue(local, attr), 2);
        }
        var lines = resolver.WearerModifiers(item).Select(m => m.ToString()).ToList();
        return new ItemInfo(item.Uid, item.Base, item.Name, item.Rarity, item.Count, item.Level, b.Slot, lines, locals);
    }

    public static QuestInfo Quest(Simulation sim, QuestProgress progress)
    {
        var def = progress.Generated ?? sim.Service<QuestService>().Find(sim, progress.QuestId);
        if (def is null)
        {
            return new QuestInfo(progress.QuestId, progress.QuestId, progress.State.ToString().ToLowerInvariant(), new List<string>(), new List<int>(), new List<int>(), null, 0);
        }
        return new QuestInfo(def.Id, def.Name, progress.State.ToString().ToLowerInvariant(),
            def.Objectives.Select(o => o.Text).ToList(),
            def.Objectives.Select(o => progress.Counts.GetValueOrDefault(o.Id)).ToList(),
            def.Objectives.Select(o => o.Count).ToList(),
            def.Giver, def.Level);
    }

    public static QuestInfo QuestOffer(Core.Definitions.QuestDef def) => new(def.Id, def.Name, "offered",
        def.Objectives.Select(o => o.Text).ToList(), def.Objectives.Select(_ => 0).ToList(), def.Objectives.Select(o => o.Count).ToList(), def.Giver, def.Level);

    public static SelfStateMsg Self(Simulation sim, Entity e)
    {
        var stats = sim.World.Require<Stats>(e);
        var resolver = sim.Service<ItemResolver>();
        var runtime = sim.Service<WeaveRuntime>();
        var progression = sim.Service<ProgressionService>();
        var book = sim.World.Get<WeaveBook>(e);
        var inv = sim.World.Get<Inventory>(e);
        var eq = sim.World.Get<Equipment>(e);
        var log = sim.World.Get<QuestLog>(e);
        var player = sim.World.Get<Player>(e);
        var world = sim.Service<WorldState>();

        var attributes = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var def in sim.Content.Attributes.All)
        {
            if (def.Hidden || def.Category is "weave")
            {
                continue;
            }
            try
            {
                attributes[def.Id] = Math.Round(stats.Get(def.Id), 3);
            }
            catch (Exception)
            {
                attributes[def.Id] = 0;
            }
        }
        var weaves = new List<WeaveSlotInfo>();
        if (book is not null)
        {
            for (var i = 0; i < book.Slots.Count; i++)
            {
                var slot = book.Slots[i];
                var compiled = runtime.Resolve(sim, slot, out _);
                var readyIn = book.ReadyAt.TryGetValue(slot.Key, out var at) ? Math.Max(0, at - sim.Clock.Time) : 0;
                weaves.Add(new WeaveSlotInfo(i, slot.Key, compiled?.Name ?? slot.Key, compiled?.Look, readyIn,
                    compiled is null ? new Dictionary<string, double>() : runtime.Costs(compiled, stats),
                    compiled?.Targeting.ToString().ToLowerInvariant() ?? "enemy",
                    compiled?.Range(stats) ?? 0));
            }
        }
        var resources = stats.Resources.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 1), StringComparer.Ordinal);
        var maxes = stats.Resources.Keys.ToDictionary(k => k, k => Math.Round(stats.ResourceMax(k), 1), StringComparer.Ordinal);
        return new SelfStateMsg(
            e.Key,
            stats.Level,
            Math.Round(stats.Xp, 1),
            progression.XpToNext(sim, stats.Level),
            progression.AvailablePoints(sim, stats),
            stats.Passives.ToList(),
            attributes,
            resources,
            maxes,
            weaves,
            inv?.Items.Select(i => Item(resolver, i)).ToList() ?? new List<ItemInfo>(),
            inv?.Capacity ?? 0,
            eq?.BySlot.ToDictionary(kv => kv.Key, kv => Item(resolver, kv.Value), StringComparer.Ordinal) ?? new Dictionary<string, ItemInfo>(),
            inv?.Currency.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) ?? new Dictionary<string, double>(),
            log?.Quests.Values.Select(q => Quest(sim, q)).ToList() ?? new List<QuestInfo>(),
            log?.Reputation.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) ?? new Dictionary<string, double>(),
            player is null ? new List<string>() : world.Holdings.Values.Where(h => h.OwnerCharacterId == player.CharacterId).Select(h => h.Id).ToList());
    }

    public static ChunkDataMsg Chunk(Core.World.ChunkData c)
    {
        var layers = c.Layers.SelectMany(kv => kv.Value.Select(s => new LayerInfo(kv.Key, s.Height, s.Walkable, string.Join(" ", s.Tags)))).ToList();
        return new ChunkDataMsg(c.Coord.X, c.Coord.Y, c.Size, c.Height, c.Flags.Select(f => (byte)f).ToArray(), c.TagTable.Select(t => string.Join(" ", t)).ToList(), c.SurfaceTag, layers, c.GenVersion);
    }
}
