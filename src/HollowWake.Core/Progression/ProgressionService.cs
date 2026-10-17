using System.Globalization;
using System.Runtime.CompilerServices;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>
/// Experience, levels and the passive constellation: allocating nodes and the options choice nodes deal, realms
/// with their own adjacency and currencies, and refunds at a font. Curves, costs and point grants come from the
/// <c>progression</c> and <c>passives</c> rules.
/// </summary>
public sealed class ProgressionService
{
    /// <summary>The currency of the main constellation, which levels earn.</summary>
    public const string MainCurrency = "passive";

    /// <summary>The main constellation's realm id.</summary>
    public const string MainRealm = "star";

    public double XpToNext(Simulation sim, int level) =>
        Math.Max(1, sim.Content.Rules.Eval("progression.xp_to_next", "round(80 * level ^ 1.6)", new DictScope().Set("level", level)));

    public int MaxLevel(Simulation sim) => (int)sim.Content.Rules.Number("progression.max_level", 60);

    public int PassivePointsFor(Simulation sim, int level) =>
        (int)Math.Max(0, sim.Content.Rules.Eval("progression.passive_points", "level - 1", new DictScope().Set("level", level)));

    public void GrantXp(Simulation sim, Entity e, double amount)
    {
        if (amount <= 0 || !sim.World.TryGet<Stats>(e, out var stats) || !sim.World.Has<Player>(e))
        {
            return;
        }
        var bonus = stats.Get("xp.gain");
        amount *= Math.Max(0, 1 + bonus);
        stats.Xp += amount;
        sim.Events.Publish(new ExperienceGained(e, amount, stats.Xp) { Tick = sim.Clock.Tick });
        var max = MaxLevel(sim);
        var guard = 0;
        while (stats.Level < max && stats.Xp >= XpToNext(sim, stats.Level) && guard++ < 200)
        {
            stats.Xp -= XpToNext(sim, stats.Level);
            stats.Level++;
            if (sim.Content.Rules.Flag("progression.heal_on_level", true))
            {
                stats.FillResources();
            }
            sim.Events.Publish(new LevelUp(e, stats.Level) { Tick = sim.Clock.Tick });
        }
    }

    // ------------------------------------------------------------ points

    /// <summary>Unspent points of the main constellation from levels alone (no entity needed).</summary>
    public int AvailablePoints(Simulation sim, Stats stats) => PassivePointsFor(sim, stats.Level) - stats.PassivePointsSpent;

    /// <summary>
    /// Unspent points of a currency: levels (the main currency) or rule <c>passives.points.{currency}</c> (an
    /// expression of level) for the others, plus points granted by rewards, minus points spent.
    /// </summary>
    public int Points(Simulation sim, Entity e, string currency)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return 0;
        }
        var state = sim.World.Get<PassiveState>(e);
        var granted = state is not null && state.Granted.TryGetValue(currency, out var g) ? g : 0;
        if (currency == MainCurrency)
        {
            return PassivePointsFor(sim, stats.Level) + granted - stats.PassivePointsSpent;
        }
        var earned = sim.Content.Rules.Has("passives.points." + currency)
            ? (int)Math.Max(0, sim.Content.Rules.Eval("passives.points." + currency, "0", new DictScope().Set("level", stats.Level)))
            : 0;
        var spent = state is not null && state.Spent.TryGetValue(currency, out var s) ? s : 0;
        return earned + granted - spent;
    }

    /// <summary>Every point currency: the main one, then each realm's.</summary>
    public IEnumerable<string> Currencies(Simulation sim)
    {
        yield return MainCurrency;
        var seen = new HashSet<string>(StringComparer.Ordinal) { MainCurrency };
        foreach (var realm in sim.Content.Each<PassiveRealmDef>().OrderBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            if (seen.Add(realm.Currency))
            {
                yield return realm.Currency;
            }
        }
    }

    /// <summary>Grants points of a currency (a quest reward for a realm).</summary>
    public void GrantPoints(Simulation sim, Entity e, string currency, int amount)
    {
        var state = sim.World.GetOrAdd<PassiveState>(e);
        state.Granted[currency] = (state.Granted.TryGetValue(currency, out var had) ? had : 0) + amount;
        sim.Events.Publish(new PassivePointsGranted(e, currency, amount) { Tick = sim.Clock.Tick });
    }

    private static string CurrencyOf(Simulation sim, PassiveNodeDef node) =>
        node.Realm == MainRealm ? MainCurrency : sim.Content.Get<PassiveRealmDef>(node.Realm)?.Currency ?? MainCurrency;

    private static void Spend(Simulation sim, Stats stats, PassiveState state, string currency, int amount)
    {
        if (currency == MainCurrency)
        {
            stats.PassivePointsSpent += amount;
        }
        else
        {
            state.Spent[currency] = (state.Spent.TryGetValue(currency, out var had) ? had : 0) + amount;
        }
    }

    // ------------------------------------------------------------ realms

    /// <summary>Whether a realm is open to the hero: the main star always; another once its unlock held.</summary>
    public bool RealmOpen(Simulation sim, Entity e, string realm) =>
        realm == MainRealm || (sim.World.Get<PassiveState>(e)?.OpenRealms.Contains(realm) ?? false);

    /// <summary>
    /// Opens every realm whose unlock now holds and grants its roots free. Called when the hero enters the world,
    /// levels, turns in a quest, or allocates.
    /// </summary>
    public void RefreshRealms(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return;
        }
        var state = sim.World.GetOrAdd<PassiveState>(e);
        HeroScope? scope = null;
        string? calling = null;
        foreach (var realm in sim.Content.Each<PassiveRealmDef>())
        {
            if (state.OpenRealms.Contains(realm.Id))
            {
                continue;
            }
            // A vocation opens only to its own calling, whatever else it asks (row H5).
            if (realm.Calling is not null && realm.Calling != (calling ??= CallingOf(sim, e) ?? ""))
            {
                continue;
            }
            if (realm.Unlock is not null && !realm.Unlock.IsTrue(scope ??= new HeroScope(sim, e)))
            {
                continue;
            }
            state.OpenRealms.Add(realm.Id);
            foreach (var root in realm.Roots)
            {
                if (!stats.Passives.Contains(root) && sim.Content.Get<PassiveNodeDef>(root) is { } node)
                {
                    stats.Passives.Add(root);
                    stats.Attributes.AddRange(node.Modifiers, "passive:" + root);
                }
            }
            sim.Events.Publish(new RealmOpened(e, realm.Id) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Why a sealed realm is sealed, or null when it is open. A vocation of another calling says whose it is.</summary>
    public string? SealedReason(Simulation sim, Entity e, PassiveRealmDef realm)
    {
        if (RealmOpen(sim, e, realm.Id))
        {
            return null;
        }
        if (realm.Calling is { } owner && owner != CallingOf(sim, e))
        {
            var name = sim.Content.Get<CallingDef>(owner)?.DisplayName ?? Definition.Humanize(owner);
            return sim.Content.Rules.Text("passives.calling_sealed_text", "Only a {calling} walks this path.").Replace("{calling}", name, StringComparison.Ordinal);
        }
        return realm.SealedText.Length > 0 ? realm.SealedText : "Sealed.";
    }

    // ------------------------------------------------------------ the graph

    private sealed class Graph
    {
        public readonly Dictionary<string, List<string>> Neighbors = new(StringComparer.Ordinal);
    }

    private static readonly ConditionalWeakTable<ContentRegistry, Graph> Graphs = new();

    /// <summary>Links read both ways: a node's neighbors are the nodes it links and the nodes linking it.</summary>
    private static List<string> NeighborsOf(ContentRegistry content, string node)
    {
        var graph = Graphs.GetValue(content, static c =>
        {
            var g = new Graph();
            foreach (var n in c.Each<PassiveNodeDef>())
            {
                foreach (var link in n.Links)
                {
                    Add(g, n.Id, link);
                    Add(g, link, n.Id);
                }
            }
            return g;
        });
        return graph.Neighbors.TryGetValue(node, out var list) ? list : Empty;
    }

    private static readonly List<string> Empty = new();

    private static void Add(Graph g, string from, string to)
    {
        if (!g.Neighbors.TryGetValue(from, out var list))
        {
            g.Neighbors[from] = list = new List<string>();
        }
        if (!list.Contains(to))
        {
            list.Add(to);
        }
    }

    private static string? CallingOf(Simulation sim, Entity e)
    {
        if (sim.World.TryGet<Identity>(e, out var identity))
        {
            foreach (var tag in identity.Tags)
            {
                if (tag.StartsWith("calling.", StringComparison.Ordinal))
                {
                    return tag[8..];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The nodes allocation and refunds hang from: the calling's start node and the roots of open realms. They
    /// are free and never refunded; another calling's start bought as travel is not one.
    /// </summary>
    public HashSet<string> Anchors(Simulation sim, Entity e)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        if (CallingOf(sim, e) is { } calling && sim.Content.Get<CallingDef>(calling)?.StartNode is { } start)
        {
            anchors.Add(start);
        }
        if (sim.World.TryGet<PassiveState>(e, out var state))
        {
            foreach (var realmId in state.OpenRealms)
            {
                if (sim.Content.Get<PassiveRealmDef>(realmId) is { } realm)
                {
                    foreach (var root in realm.Roots)
                    {
                        anchors.Add(root);
                    }
                }
            }
        }
        return anchors;
    }

    // ------------------------------------------------------------ allocation

    /// <summary>
    /// Allocates a node, picking <paramref name="option"/> when the node deals choices; on an allocated choice node
    /// with picks left, picks another option without spending a point.
    /// </summary>
    public bool Allocate(Simulation sim, Entity e, string nodeId, string? option, out string reason)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            reason = "no_stats";
            return false;
        }
        if (!sim.Content.TryGet<PassiveNodeDef>(nodeId, out var node) || node is null)
        {
            reason = "unknown_node";
            return false;
        }
        var state = sim.World.GetOrAdd<PassiveState>(e);
        RefreshRealms(sim, e);
        if (stats.Passives.Contains(nodeId))
        {
            if (option is null || node.Choice is null)
            {
                reason = "already";
                return false;
            }
            if (!Deals(sim, stats, state, node))
            {
                reason = "deals_nothing";
                return false;
            }
            if (!CanPick(sim, state, node, option, out reason))
            {
                return false;
            }
            Pick(sim, e, stats, state, node, option);
            return true;
        }
        if (!CanAllocate(sim, e, stats, state, node, out reason))
        {
            return false;
        }
        var deals = Deals(sim, stats, state, node);
        if (deals && PassiveChoices.OptionsOf(sim.Content, node.Choice!.Group).Count > 0)
        {
            if (option is null)
            {
                reason = "needs_option";
                return false;
            }
            if (!CanPick(sim, state, node, option, out reason))
            {
                return false;
            }
        }
        else if (option is not null)
        {
            reason = node.Choice is null ? "not_a_choice" : "deals_nothing";
            return false;
        }
        stats.Passives.Add(nodeId);
        Spend(sim, stats, state, CurrencyOf(sim, node), node.Cost);
        stats.Attributes.AddRange(node.Modifiers, "passive:" + nodeId);
        sim.Events.Publish(new PassiveAllocated(e, nodeId) { Tick = sim.Clock.Tick });
        if (option is not null)
        {
            Pick(sim, e, stats, state, node, option);
        }
        return true;
    }

    /// <summary>Allocates a node that deals nothing (the old intent without an option).</summary>
    public bool Allocate(Simulation sim, Entity e, string nodeId, out string reason) => Allocate(sim, e, nodeId, null, out reason);

    private bool CanAllocate(Simulation sim, Entity e, Stats stats, PassiveState state, PassiveNodeDef node, out string reason)
    {
        if (node.Kind == PassiveKind.Start && Anchors(sim, e).Contains(node.Id))
        {
            reason = "already";
            return false;
        }
        if (!RealmOpen(sim, e, node.Realm))
        {
            reason = "realm_sealed";
            return false;
        }
        if (Points(sim, e, CurrencyOf(sim, node)) < node.Cost)
        {
            reason = "no_points";
            return false;
        }
        var realm = node.Realm == MainRealm ? null : sim.Content.Get<PassiveRealmDef>(node.Realm);
        if (realm?.Adjacency != "free")
        {
            var connected = false;
            foreach (var neighbor in NeighborsOf(sim.Content, node.Id))
            {
                if (stats.Passives.Contains(neighbor))
                {
                    connected = true;
                    break;
                }
            }
            if (!connected)
            {
                reason = "not_connected";
                return false;
            }
        }
        // A cluster claimed by its first node keeps every other node of the group out.
        if (node.Choice is { } choice && sim.Content.Get<PassiveChoiceDef>(choice.Group)?.Deal == ChoiceDeal.First)
        {
            foreach (var other in stats.Passives)
            {
                if (other != node.Id && sim.Content.Get<PassiveNodeDef>(other)?.Choice?.Group == choice.Group)
                {
                    reason = "exclusive";
                    return false;
                }
            }
        }
        reason = "";
        return true;
    }

    /// <summary>Whether a node deals its group's options: always for each and first; for sole only the group's first node.</summary>
    private static bool Deals(Simulation sim, Stats stats, PassiveState state, PassiveNodeDef node)
    {
        if (node.Choice is not { } choice || sim.Content.Get<PassiveChoiceDef>(choice.Group) is not { } group)
        {
            return false;
        }
        if (group.Deal != ChoiceDeal.Sole)
        {
            return true;
        }
        foreach (var other in stats.Passives)
        {
            if (other != node.Id && state.PicksOf(other) > 0 && sim.Content.Get<PassiveNodeDef>(other)?.Choice?.Group == choice.Group)
            {
                return false;
            }
        }
        return true;
    }

    private static bool CanPick(Simulation sim, PassiveState state, PassiveNodeDef node, string option, out string reason)
    {
        var groupId = node.Choice!.Group;
        var group = sim.Content.Get<PassiveChoiceDef>(groupId);
        if (group is null || PassiveChoices.Option(sim.Content, groupId, option) is null)
        {
            reason = "unknown_option";
            return false;
        }
        if (state.PicksOf(node.Id) >= PassiveChoices.PicksAt(sim.Content, node))
        {
            reason = "no_picks";
            return false;
        }
        var groupPicks = 0;
        foreach (var (other, picks) in state.Picks)
        {
            if (sim.Content.Get<PassiveNodeDef>(other)?.Choice?.Group != groupId)
            {
                continue;
            }
            groupPicks += picks.Count;
            var clash = group.Unique switch
            {
                ChoiceUnique.Node => other == node.Id && picks.Contains(option),
                ChoiceUnique.Character => picks.Contains(option),
                _ => false,
            };
            if (clash)
            {
                reason = "already_picked";
                return false;
            }
        }
        if (group.Max is { } max && groupPicks >= max)
        {
            reason = "group_full";
            return false;
        }
        reason = "";
        return true;
    }

    private static void Pick(Simulation sim, Entity e, Stats stats, PassiveState state, PassiveNodeDef node, string option)
    {
        if (!state.Picks.TryGetValue(node.Id, out var picks))
        {
            state.Picks[node.Id] = picks = new List<string>();
        }
        picks.Add(option);
        var def = PassiveChoices.Option(sim.Content, node.Choice!.Group, option)!;
        stats.Attributes.AddRange(def.Modifiers, PassiveChoices.SourceOf(node.Id, option));
        sim.Events.Publish(new PassiveChosen(e, node.Id, option) { Tick = sim.Clock.Tick });
    }

    // ------------------------------------------------------------ refunds

    /// <summary>Refunds one node at a font within reach of the hero.</summary>
    public bool RefundAtFont(Simulation sim, Entity e, string nodeId, out string reason)
    {
        if (Interaction.Fonts.InReach(sim, e) is null)
        {
            reason = "no_font";
            return false;
        }
        return Refund(sim, e, nodeId, out reason);
    }

    /// <summary>
    /// Refunds one allocated node (row H8): never an anchor; only when every other allocated node still connects
    /// to an anchor without it (nodes of realms with free adjacency need no path); its picks go with it; its
    /// points return to their own currency; the price is rule <c>passives.refund_cost</c>, paid from the wallet.
    /// </summary>
    public bool Refund(Simulation sim, Entity e, string nodeId, out string reason)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats) || !stats.Passives.Contains(nodeId) || sim.Content.Get<PassiveNodeDef>(nodeId) is not { } node)
        {
            reason = "not_allocated";
            return false;
        }
        var anchors = Anchors(sim, e);
        if (anchors.Contains(nodeId))
        {
            reason = "anchor";
            return false;
        }
        if (!StaysConnected(sim, stats, anchors, nodeId))
        {
            reason = "would_disconnect";
            return false;
        }
        var cost = RefundCost(sim, e, node);
        var inv = sim.World.Get<Inventory>(e);
        foreach (var (currency, amount) in cost)
        {
            if ((inv?.CurrencyOf(currency) ?? 0) < amount - 1e-9)
            {
                reason = "cannot_afford:" + currency;
                return false;
            }
        }
        foreach (var (currency, amount) in cost)
        {
            if (amount > 0)
            {
                inv!.AddCurrency(currency, -amount);
                sim.Events.Publish(new CurrencyChanged(e, currency, -amount, inv.CurrencyOf(currency)) { Tick = sim.Clock.Tick });
            }
        }
        var state = sim.World.GetOrAdd<PassiveState>(e);
        if (state.Picks.Remove(nodeId, out var picks))
        {
            foreach (var option in picks)
            {
                stats.Attributes.RemoveSource(PassiveChoices.SourceOf(nodeId, option));
            }
        }
        stats.Attributes.RemoveSource("passive:" + nodeId);
        stats.Passives.Remove(nodeId);
        Spend(sim, stats, state, CurrencyOf(sim, node), -node.Cost);
        state.Refunds++;
        sim.Events.Publish(new PassiveRefunded(e, nodeId) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>What refunding a node costs, per currency: rule <c>passives.refund_cost</c>, expressions of the hero, the node's cost and refunds made.</summary>
    public Dictionary<string, double> RefundCost(Simulation sim, Entity e, PassiveNodeDef node)
    {
        var cost = new Dictionary<string, double>(StringComparer.Ordinal);
        if (sim.Content.Rules.Node("passives.refund_cost") is not System.Text.Json.Nodes.JsonObject prices)
        {
            return cost;
        }
        var scope = new ScopeOverlay(new HeroScope(sim, e))
            .Set("node_cost", node.Cost)
            .Set("refunds", sim.World.Get<PassiveState>(e)?.Refunds ?? 0);
        foreach (var (currency, _) in prices)
        {
            var amount = Math.Max(0, Math.Round(sim.Content.Rules.Eval("passives.refund_cost." + currency, "0", scope)));
            if (amount > 0)
            {
                cost[currency] = amount;
            }
        }
        return cost;
    }

    /// <summary>A refund's price as a player reads it ("12 Shards"), for a node of the given cost.</summary>
    public string RefundCostText(Simulation sim, Entity e, PassiveNodeDef node)
    {
        var parts = new List<string>();
        foreach (var (currency, amount) in RefundCost(sim, e, node))
        {
            parts.Add(amount.ToString("0", CultureInfo.InvariantCulture) + " " + (sim.Content.Get<CurrencyDef>(currency)?.DisplayName ?? Definition.Humanize(currency)));
        }
        return parts.Count == 0 ? "Free" : string.Join(", ", parts);
    }

    /// <summary>Whether every allocated node but one still reaches an anchor through allocated nodes.</summary>
    private static bool StaysConnected(Simulation sim, Stats stats, HashSet<string> anchors, string removed)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var anchor in anchors)
        {
            if (anchor != removed && stats.Passives.Contains(anchor) && reached.Add(anchor))
            {
                queue.Enqueue(anchor);
            }
        }
        while (queue.Count > 0)
        {
            foreach (var next in NeighborsOf(sim.Content, queue.Dequeue()))
            {
                if (next != removed && stats.Passives.Contains(next) && reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        foreach (var id in stats.Passives)
        {
            if (id == removed || reached.Contains(id))
            {
                continue;
            }
            var node = sim.Content.Get<PassiveNodeDef>(id);
            var free = node is not null && node.Realm != MainRealm && sim.Content.Get<PassiveRealmDef>(node.Realm)?.Adjacency == "free";
            if (!free)
            {
                return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------ loading and resetting

    /// <summary>Re-applies every allocated node's modifiers; used when a character is materialized.</summary>
    public void ApplyAllocated(Simulation sim, Stats stats) => ApplyAllocated(sim, stats, null);

    /// <summary>
    /// Re-applies every allocated node's modifiers and every pick's; a choice node allocated before it dealt choices
    /// gets its default pick (the node's, else the group's).
    /// </summary>
    public void ApplyAllocated(Simulation sim, Stats stats, PassiveState? state)
    {
        foreach (var id in stats.Passives)
        {
            if (!sim.Content.TryGet<PassiveNodeDef>(id, out var node) || node is null)
            {
                continue;
            }
            stats.Attributes.RemoveSource("passive:" + id);
            stats.Attributes.AddRange(node.Modifiers, "passive:" + id);
            if (state is null || node.Choice is not { } choice)
            {
                continue;
            }
            if (state.PicksOf(id) == 0 && (choice.Default ?? sim.Content.Get<PassiveChoiceDef>(choice.Group)?.Default) is { } fallback
                && PassiveChoices.Option(sim.Content, choice.Group, fallback) is not null)
            {
                state.Picks[id] = new List<string> { fallback };
            }
            if (state.Picks.TryGetValue(id, out var picks))
            {
                foreach (var option in picks)
                {
                    if (PassiveChoices.Option(sim.Content, choice.Group, option) is { } def)
                    {
                        stats.Attributes.RemoveSource(PassiveChoices.SourceOf(id, option));
                        stats.Attributes.AddRange(def.Modifiers, PassiveChoices.SourceOf(id, option));
                    }
                }
            }
        }
    }

    /// <summary>The points spent per currency by the allocated nodes, anchors free.</summary>
    public void RecountSpent(Simulation sim, Entity e, Stats stats, PassiveState state)
    {
        var anchors = Anchors(sim, e);
        stats.PassivePointsSpent = 0;
        state.Spent.Clear();
        foreach (var id in stats.Passives)
        {
            if (anchors.Contains(id) || sim.Content.Get<PassiveNodeDef>(id) is not { } node)
            {
                continue;
            }
            Spend(sim, stats, state, CurrencyOf(sim, node), node.Cost);
        }
    }

    /// <summary>Takes back every node but the anchors, with every pick.</summary>
    public void Reset(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return;
        }
        var anchors = Anchors(sim, e);
        var state = sim.World.GetOrAdd<PassiveState>(e);
        foreach (var id in stats.Passives.ToList())
        {
            if (anchors.Contains(id))
            {
                continue;
            }
            if (state.Picks.Remove(id, out var picks))
            {
                foreach (var option in picks)
                {
                    stats.Attributes.RemoveSource(PassiveChoices.SourceOf(id, option));
                }
            }
            stats.Attributes.RemoveSource("passive:" + id);
            stats.Passives.Remove(id);
        }
        RecountSpent(sim, e, stats, state);
    }
}
