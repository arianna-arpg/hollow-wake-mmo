using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Quests;

/// <summary>
/// Offers, accepts, progresses and turns in quests, hand-authored and generated alike. Objective types
/// are matched against simulation events by a registry so new objective kinds need no engine change
/// beyond one registration.
/// </summary>
public sealed class QuestService
{
    private readonly Dictionary<string, Func<Simulation, Entity, QuestProgress, ObjectiveDef, GameEvent, int>> _matchers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDef> _generated = new(StringComparer.Ordinal);

    public QuestService()
    {
        RegisterBuiltins();
    }

    /// <summary>Registers an objective type: returns how much progress an event grants, or zero.</summary>
    public void Register(string type, Func<Simulation, Entity, QuestProgress, ObjectiveDef, GameEvent, int> matcher) => _matchers[type] = matcher;

    public void RegisterGenerated(IEnumerable<QuestDef> quests)
    {
        foreach (var q in quests)
        {
            _generated[q.Id] = q;
        }
    }

    public QuestDef? Find(Simulation sim, string id) => _generated.TryGetValue(id, out var g) ? g : sim.Content.Get<QuestDef>(id);

    public IEnumerable<QuestDef> AllQuests(Simulation sim) => sim.Content.Each<QuestDef>().Concat(_generated.Values);

    /// <summary>Quests a giver can offer to a player: matching giver id (pattern or point of interest), prerequisites met, not active or done.</summary>
    public IEnumerable<QuestDef> Offers(Simulation sim, Entity player, Interactable giver, Identity giverIdentity)
    {
        if (!sim.World.TryGet<QuestLog>(player, out var log) || !sim.World.TryGet<Stats>(player, out var stats))
        {
            yield break;
        }
        var scope = new ScopeOverlay(stats);
        foreach (var c in log.Completed)
        {
            scope.Flag("quest.completed." + c);
        }
        foreach (var q in AllQuests(sim))
        {
            if (q.Giver is null)
            {
                continue;
            }
            if (q.Giver != giver.Ref && q.Giver != giverIdentity.Pattern)
            {
                continue;
            }
            if (log.Quests.ContainsKey(q.Id))
            {
                continue;
            }
            if (log.Completed.Contains(q.Id) && !q.Repeatable)
            {
                continue;
            }
            if (q.Prerequisites is not null && !q.Prerequisites.IsTrue(scope))
            {
                continue;
            }
            if (q.Level > stats.Level + (int)sim.Content.Rules.Number("quests.level_slack", 3))
            {
                continue;
            }
            yield return q;
        }
    }

    public bool Accept(Simulation sim, Entity player, string questId, out string reason)
    {
        reason = "";
        var def = Find(sim, questId);
        if (def is null)
        {
            reason = "unknown";
            return false;
        }
        var log = sim.World.GetOrAdd<QuestLog>(player);
        if (log.Quests.ContainsKey(questId))
        {
            reason = "already";
            return false;
        }
        var max = (int)sim.Content.Rules.Number("quests.max_active", 20);
        if (log.Quests.Count(q => q.Value.State == QuestState.Active) >= max)
        {
            reason = "log_full";
            return false;
        }
        var progress = new QuestProgress { QuestId = questId, StartedAt = sim.Clock.Time, Generated = _generated.TryGetValue(questId, out var g) ? g : null };
        log.Quests[questId] = progress;
        sim.Events.Publish(new QuestAccepted(player, questId) { Tick = sim.Clock.Tick });
        // Collect objectives may already be satisfied by what is in the bag.
        Reevaluate(sim, player, progress, def);
        return true;
    }

    public bool Abandon(Simulation sim, Entity player, string questId)
    {
        return sim.World.TryGet<QuestLog>(player, out var log) && log.Quests.Remove(questId);
    }

    public bool TurnIn(Simulation sim, Entity player, string questId, out string reason)
    {
        reason = "";
        var def = Find(sim, questId);
        if (def is null || !sim.World.TryGet<QuestLog>(player, out var log) || !log.Quests.TryGetValue(questId, out var progress))
        {
            reason = "not_active";
            return false;
        }
        if (progress.State != QuestState.Complete)
        {
            reason = "incomplete";
            return false;
        }
        // Consume collected items.
        if (sim.World.TryGet<Inventory>(player, out var inv) && sim.TryService<ItemGenerator>(out var gen))
        {
            foreach (var o in def.Objectives.Where(o => o.Type == "collect"))
            {
                var remaining = o.Count;
                foreach (var item in inv.Items.ToList())
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    if (!Matches(gen.Resolver, item, o))
                    {
                        continue;
                    }
                    var take = Math.Min(remaining, item.Count);
                    item.Count -= take;
                    remaining -= take;
                    if (item.Count <= 0)
                    {
                        inv.Remove(item);
                    }
                }
            }
        }
        progress.State = QuestState.TurnedIn;
        log.Quests.Remove(questId);
        log.Completed.Add(questId);
        GrantRewards(sim, player, def);
        sim.Events.Publish(new QuestTurnedIn(player, questId) { Tick = sim.Clock.Tick });
        return true;
    }

    private static bool Matches(ItemResolver resolver, ItemInstance item, ObjectiveDef o)
    {
        if (o.Target is not null && item.Base == o.Target)
        {
            return true;
        }
        return !o.Tags.IsEmpty && resolver.TagsOf(item).HasAll(o.Tags);
    }

    private void GrantRewards(Simulation sim, Entity player, QuestDef def)
    {
        var r = def.Rewards;
        var stats = sim.World.Get<Stats>(player);
        if (r.Xp is not null && sim.TryService<ProgressionService>(out var progression))
        {
            var scope = new ScopeOverlay(stats).Set("level", stats?.Level ?? 1).Set("quest_level", def.Level);
            progression.GrantXp(sim, player, r.Xp.Evaluate(scope));
        }
        if (sim.TryService<LootService>(out var loot) && sim.TryService<ItemGenerator>(out var gen))
        {
            var rng = sim.RngFor(player, "quest:" + def.Id);
            foreach (var g in r.Items)
            {
                var item = g.Unique is not null ? gen.RollUnique(g.Unique, g.Level ?? def.Level, rng) : gen.Roll(g.Item, g.Level ?? def.Level, g.Rarity, rng);
                item.Count = Math.Clamp(g.Count, 1, Math.Max(1, item.Stack));
                loot.Give(sim, player, item);
            }
            if (r.Loot is not null)
            {
                loot.GiveTable(sim, player, r.Loot, def.Level, rng);
            }
            foreach (var (currency, amount) in r.Currency)
            {
                loot.GiveCurrency(sim, player, currency, amount);
            }
        }
        if (sim.World.TryGet<QuestLog>(player, out var log))
        {
            foreach (var (faction, amount) in r.Reputation)
            {
                log.Reputation[faction] = log.Reputation.GetValueOrDefault(faction) + amount;
            }
        }
        if (r.Weaves.Count > 0 && sim.World.TryGet<WeaveBook>(player, out var book))
        {
            foreach (var w in r.Weaves)
            {
                book.Add(w);
            }
        }
    }

    // ------------------------------------------------------------ progress

    public void OnEvent(Simulation sim, GameEvent evt)
    {
        foreach (var (player, log) in sim.World.Query<QuestLog>())
        {
            if (!sim.World.Has<Player>(player))
            {
                continue;
            }
            foreach (var progress in log.Quests.Values.ToList())
            {
                if (progress.State != QuestState.Active)
                {
                    continue;
                }
                var def = progress.Generated ?? sim.Content.Get<QuestDef>(progress.QuestId);
                if (def is null)
                {
                    continue;
                }
                var changed = false;
                foreach (var o in def.Objectives)
                {
                    if (!_matchers.TryGetValue(o.Type, out var matcher))
                    {
                        continue;
                    }
                    var current = progress.Counts.GetValueOrDefault(o.Id);
                    if (current >= o.Count)
                    {
                        continue;
                    }
                    var gained = matcher(sim, player, progress, o, evt);
                    if (gained == 0)
                    {
                        continue;
                    }
                    var next = o.Type == "collect" ? Math.Min(o.Count, gained) : Math.Min(o.Count, current + gained);
                    if (next != current)
                    {
                        progress.Counts[o.Id] = next;
                        changed = true;
                        sim.Events.Publish(new QuestProgressed(player, def.Id, o.Id, next, o.Count) { Tick = sim.Clock.Tick });
                    }
                }
                if (changed)
                {
                    CheckComplete(sim, player, progress, def);
                }
            }
        }
    }

    private void Reevaluate(Simulation sim, Entity player, QuestProgress progress, QuestDef def)
    {
        if (sim.World.TryGet<Inventory>(player, out var inv) && sim.TryService<ItemGenerator>(out var gen))
        {
            foreach (var o in def.Objectives.Where(o => o.Type == "collect"))
            {
                var have = inv.CountOf(i => Matches(gen.Resolver, i, o));
                progress.Counts[o.Id] = Math.Min(o.Count, have);
            }
        }
        CheckComplete(sim, player, progress, def);
    }

    private static void CheckComplete(Simulation sim, Entity player, QuestProgress progress, QuestDef def)
    {
        var done = def.Objectives.All(o => o.Optional || progress.Counts.GetValueOrDefault(o.Id) >= o.Count);
        if (done && progress.State == QuestState.Active)
        {
            progress.State = QuestState.Complete;
            sim.Events.Publish(new QuestCompleted(player, def.Id) { Tick = sim.Clock.Tick });
        }
    }

    private void RegisterBuiltins()
    {
        Register("kill", (sim, player, _, o, evt) =>
        {
            if (evt is not EntityDied died || died.Killer != player)
            {
                return 0;
            }
            if (o.Target is not null && died.Pattern != o.Target && !died.Tags.Has(o.Target))
            {
                return 0;
            }
            if (!o.Tags.IsEmpty && !died.Tags.HasAll(o.Tags))
            {
                return 0;
            }
            return 1;
        });
        Register("collect", (sim, player, _, o, evt) =>
        {
            if (evt is not ItemAcquired acquired || acquired.Entity != player)
            {
                return 0;
            }
            if (!sim.World.TryGet<Inventory>(player, out var inv) || !sim.TryService<ItemGenerator>(out var gen))
            {
                return 0;
            }
            return inv.CountOf(i => Matches(gen.Resolver, i, o));
        });
        Register("reach", (sim, player, _, o, evt) => evt is PoiDiscovered d && d.Entity == player && (o.Poi == d.Poi || o.Target == d.Poi) ? 1 : 0);
        Register("discover", (sim, player, _, o, evt) => evt is PoiDiscovered d && d.Entity == player && (o.Poi is null || o.Poi == d.Poi) ? 1 : 0);
        Register("interact", (sim, player, _, o, evt) => evt is Interacted i && i.Entity == player && (o.Target is null || i.Ref == o.Target || i.Kind == o.Target) ? 1 : 0);
        Register("craft", (sim, player, _, o, evt) => evt is ItemCrafted c && c.Crafter == player && (o.Target is null || c.Recipe == o.Target) ? 1 : 0);
        Register("bind", (sim, player, _, o, evt) => evt is FountBound b && b.Entity == player && (o.Target is null || b.Fount == o.Target) ? 1 : 0);
        Register("event", (sim, player, _, o, evt) => evt is CustomEvent c && (c.Source == player || c.Target == player) && c.Name == o.Target ? 1 : 0);
        Register("level", (sim, player, _, o, evt) => evt is LevelUp l && l.Entity == player ? l.Level : 0);
        Register("cast", (sim, player, _, o, evt) => evt is WeaveCast w && w.Caster == player && (o.Tags.IsEmpty || w.Tags.HasAll(o.Tags)) ? 1 : 0);
    }
}

/// <summary>Routes events into the quest service and revives dead players when their timer runs out.</summary>
public sealed class QuestSystem : SimSystem
{
    public override string Name => "quests";
    public override SimPhase Phase => SimPhase.React;

    private readonly List<GameEvent> _pending = new();
    private bool _subscribed;

    public override void Update(Simulation sim)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            sim.Events.SubscribeAll(e => _pending.Add(e));
        }
        if (_pending.Count == 0)
        {
            return;
        }
        var service = sim.Service<QuestService>();
        var batch = _pending.ToArray();
        _pending.Clear();
        foreach (var evt in batch)
        {
            if (evt is EntityDied or ItemAcquired or PoiDiscovered or Interacted or ItemCrafted or FountBound or CustomEvent or LevelUp or WeaveCast)
            {
                service.OnEvent(sim, evt);
            }
        }
    }
}
