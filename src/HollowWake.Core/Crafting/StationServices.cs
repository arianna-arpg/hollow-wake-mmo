using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Items;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Crafting;

/// <summary>
/// Station services (row I12): work a station does for a price, as content declares it (<see cref="StationServiceDef"/>)
/// over a registered service type. A hero asks for a service at the station whose pane they have open, naming the item,
/// weave slot or node it applies to; the service checks the target, the price is read from content over the hero and
/// the target, paid all at once, and the work done. Every target a service could apply to is quoted in the station's
/// pane with its price and whether it can be done now.
/// </summary>
public static class StationServices
{
    /// <summary>What a service applies to: an item, a weave slot or a passive node, as the service needs.</summary>
    public readonly record struct Target(long? Item, int? Slot, string? Node);

    /// <summary>A service quoted for one target: its price, and why it cannot be done when it cannot.</summary>
    public sealed record Quote(StationServiceDef Def, Target Target, Dictionary<string, double> Price, bool Enabled, string? Reason);

    /// <summary>A target a service applies to, the scope its price reads, and why it cannot be done (null when it can).</summary>
    public readonly record struct Candidate(Target Target, IExprScope Scope, string? Blocked);

    /// <summary>A registered service type.</summary>
    public interface IService
    {
        /// <summary>Every target the service applies to for the hero.</summary>
        void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into);

        /// <summary>Checks a target the hero named: the scope its price reads, or why not (a reason code).</summary>
        bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason);

        /// <summary>Does the work, once checked and paid.</summary>
        void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target);
    }

    private static readonly Dictionary<string, IService> Types = new(StringComparer.Ordinal)
    {
        ["level_item"] = new LevelItem(),
        ["merge_codices"] = new MergeCodices(),
        ["reset_passives"] = new ResetPassives(),
        ["add_socket"] = new AddSocket(),
        ["reset_tree"] = new ResetTree(),
    };

    public static void Register(string type, IService service) => Types[type] = service;

    public static bool Has(string type) => Types.ContainsKey(type);

    public static IEnumerable<string> TypeNames => Types.Keys;

    /// <summary>Whether a station offers a service: its interactable kind or one of its tags is in the service's <c>at</c>.</summary>
    public static bool Offered(Simulation sim, Entity station, StationServiceDef def)
    {
        if (!sim.World.TryGet<Interactable>(station, out var i))
        {
            return false;
        }
        foreach (var at in def.At)
        {
            if (i.Kind == at || i.Tags.Has(at))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Every service the station offers, quoted for every target it applies to for the hero, in content order.</summary>
    public static List<Quote> For(Simulation sim, Entity hero, Entity station)
    {
        var quotes = new List<Quote>();
        var candidates = new List<Candidate>();
        var inv = sim.World.Get<Inventory>(hero);
        foreach (var def in sim.Content.Each<StationServiceDef>().OrderBy(d => d.Order).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            if (!Offered(sim, station, def) || !Types.TryGetValue(def.Type, out var service))
            {
                continue;
            }
            candidates.Clear();
            service.Candidates(sim, hero, def, candidates);
            foreach (var c in candidates)
            {
                var price = PriceOf(def, c.Scope);
                var afford = Affords(inv, price, out var short_);
                var reason = c.Blocked is not null ? ReasonText(sim, c.Blocked) : afford ? null : CannotAfford(sim, short_!);
                quotes.Add(new Quote(def, c.Target, price, reason is null, reason));
            }
        }
        return quotes;
    }

    /// <summary>Performs a service the hero asked for at a station whose pane they have open.</summary>
    public static bool Perform(Simulation sim, Entity hero, Entity station, string serviceId, Target target, out string reason)
    {
        if (sim.World.Get<Panes>(hero)?.Station is not { } pane || pane.Source != station)
        {
            reason = "no_station";
            return false;
        }
        if (sim.Content.Get<StationServiceDef>(serviceId) is not { } def || !Offered(sim, station, def) || !Types.TryGetValue(def.Type, out var service))
        {
            reason = "no_service";
            return false;
        }
        if (!service.Check(sim, hero, def, target, out var scope, out reason))
        {
            return false;
        }
        var price = PriceOf(def, scope);
        if (!sim.World.TryGet<Inventory>(hero, out var inv))
        {
            reason = "no_bag";
            return false;
        }
        if (!Affords(inv, price, out var short_))
        {
            reason = "cannot_afford:" + short_;
            return false;
        }
        foreach (var (currency, amount) in price)
        {
            if (amount > 0)
            {
                inv.AddCurrency(currency, -amount);
                sim.Events.Publish(new CurrencyChanged(hero, currency, -amount, inv.CurrencyOf(currency)) { Tick = sim.Clock.Tick });
            }
        }
        service.Perform(sim, hero, def, target);
        sim.Events.Publish(new StationServed(hero, station, def.Id, price) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    public static Dictionary<string, double> PriceOf(StationServiceDef def, IExprScope scope)
    {
        var price = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (currency, expr) in def.Price)
        {
            double amount;
            try
            {
                amount = expr.Evaluate(scope);
            }
            catch (ExprEvalException)
            {
                amount = 0;
            }
            price[currency] = Math.Max(0, Math.Round(amount));
        }
        return price;
    }

    private static bool Affords(Inventory? inv, Dictionary<string, double> price, out string? short_)
    {
        foreach (var (currency, amount) in price)
        {
            if ((inv?.CurrencyOf(currency) ?? 0) < amount - 1e-9)
            {
                short_ = currency;
                return false;
            }
        }
        short_ = null;
        return true;
    }

    private static string CannotAfford(Simulation sim, string currency) =>
        sim.Content.Rules.Text("inventory.vendor.cannot_afford_text", "Not enough {currency}.")
            .Replace("{currency}", (sim.Content.Get<CurrencyDef>(currency)?.DisplayName ?? Definition.Humanize(currency)).ToLowerInvariant(), StringComparison.Ordinal);

    /// <summary>A reason code as the player reads it: rule <c>crafting.service_reasons.{code}</c>, else the code humanized.</summary>
    public static string ReasonText(Simulation sim, string code)
    {
        var colon = code.IndexOf(':');
        var head = colon < 0 ? code : code[..colon];
        return sim.Content.Rules.Text("crafting.service_reasons." + head, Definition.Humanize(head) + ".");
    }

    // ------------------------------------------------------------ where the hero keeps things

    /// <summary>An item the hero has, in the bag, worn, or learned or socketed in the rack (with its seat).</summary>
    private static ItemInstance? Find(Simulation sim, Entity hero, long uid, out int? seat, out bool worn)
    {
        seat = null;
        worn = false;
        if (sim.World.Get<Inventory>(hero)?.Find(uid) is { } carried)
        {
            return carried;
        }
        if (sim.World.TryGet<Equipment>(hero, out var eq))
        {
            foreach (var item in eq.BySlot.Values)
            {
                if (item.Uid == uid)
                {
                    worn = true;
                    return item;
                }
            }
        }
        if (sim.World.TryGet<Rack>(hero, out var rack))
        {
            for (var i = 0; i < rack.Seats.Count; i++)
            {
                var s = rack.Seats[i];
                if (s.Codex?.Uid == uid)
                {
                    seat = i;
                    return s.Codex;
                }
                foreach (var stone in s.Stones)
                {
                    if (stone?.Uid == uid)
                    {
                        seat = i;
                        return stone;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>Everything the hero holds that a service could work on: the bag, worn gear and the rack.</summary>
    private static IEnumerable<ItemInstance> Held(Simulation sim, Entity hero)
    {
        if (sim.World.TryGet<Inventory>(hero, out var inv))
        {
            foreach (var item in inv.Items)
            {
                yield return item;
            }
        }
        if (sim.World.TryGet<Equipment>(hero, out var eq))
        {
            foreach (var item in eq.BySlot.Values)
            {
                yield return item;
            }
        }
        if (sim.World.TryGet<Rack>(hero, out var rack))
        {
            foreach (var s in rack.Seats)
            {
                if (s.Codex is not null)
                {
                    yield return s.Codex;
                }
                foreach (var stone in s.Stones)
                {
                    if (stone is not null)
                    {
                        yield return stone;
                    }
                }
            }
        }
    }

    private static bool Takes(Simulation sim, StationServiceDef def, ItemInstance item) =>
        def.Accepts.IsEmpty || sim.Service<ItemResolver>().TagsOf(item).HasAny(def.Accepts);

    private static int RankOf(Simulation sim, string rarity) => sim.Content.Get<ItemRarityDef>(rarity)?.Rank ?? 0;

    private static ScopeOverlay ItemScope(Simulation sim, Entity hero, ItemInstance item) =>
        new ScopeOverlay(new HeroScope(sim, hero))
            .Set("item_level", item.Payload?.Level ?? item.Level)
            .Set("rarity_rank", RankOf(sim, item.Rarity))
            .Set("sockets", item.Sockets?.Count ?? 0);

    // ------------------------------------------------------------ level_item

    /// <summary>Raises a glyph stone or a codex one level, up to rule <c>glyphs.max_level</c> or <c>inventory.codex_max_level</c>.</summary>
    private sealed class LevelItem : IService
    {
        private static int MaxOf(Simulation sim, ItemInstance item) => WeaveItems.IsStone(item)
            ? (int)sim.Content.Rules.Number("glyphs.max_level", 20)
            : (int)sim.Content.Rules.Number("inventory.codex_max_level", 20);

        private static string? Blocked(Simulation sim, StationServiceDef def, ItemInstance item)
        {
            if (item.Payload is null || !(WeaveItems.IsStone(item) || WeaveItems.IsCodex(item)) || !Takes(sim, def, item))
            {
                return "not_levelled_here";
            }
            return Math.Max(1, item.Payload.Level) >= MaxOf(sim, item) ? "max_level" : null;
        }

        public void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into)
        {
            foreach (var item in Held(sim, hero))
            {
                if (item.Payload is null || !(WeaveItems.IsStone(item) || WeaveItems.IsCodex(item)) || !Takes(sim, def, item))
                {
                    continue;
                }
                into.Add(new Candidate(new Target(item.Uid, null, null), ItemScope(sim, hero, item), Blocked(sim, def, item)));
            }
        }

        public bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason)
        {
            scope = EmptyScope.Instance;
            if (target.Item is not { } uid || Find(sim, hero, uid, out _, out _) is not { } item)
            {
                reason = "no_item";
                return false;
            }
            if (Blocked(sim, def, item) is { } blocked)
            {
                reason = blocked;
                return false;
            }
            scope = ItemScope(sim, hero, item);
            reason = "";
            return true;
        }

        public void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target)
        {
            var item = Find(sim, hero, target.Item!.Value, out var seat, out _)!;
            item.Payload!.Level = Math.Max(1, item.Payload.Level) + 1;
            if (seat is { } s)
            {
                sim.Service<RackService>().Rebuild(sim, hero, s);
            }
        }
    }

    // ------------------------------------------------------------ merge_codices

    /// <summary>
    /// Merges two copies of a codex (the same teaching, the same rarity) into one of the next rarity that gives a
    /// weave slot sockets (rule <c>inventory.codex_sockets</c>); the second copy, which must be in the bag, is used up.
    /// </summary>
    private sealed class MergeCodices : IService
    {
        private static string? NextRarity(Simulation sim, string rarity)
        {
            var rank = RankOf(sim, rarity);
            foreach (var r in sim.Content.Each<ItemRarityDef>().OrderBy(r => r.Rank).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                if (r.Rank > rank && sim.Content.Rules.Has("inventory.codex_sockets." + r.Id))
                {
                    return r.Id;
                }
            }
            return null;
        }

        private static bool SameTeaching(ItemInstance a, ItemInstance b) =>
            a.Uid != b.Uid && a.Rarity == b.Rarity && a.Payload is { } pa && b.Payload is { } pb && pa.Form == pb.Form && pa.Weave == pb.Weave;

        /// <summary>The second copy in the bag, the lowest level first so the better copy stays.</summary>
        private static ItemInstance? Duplicate(Simulation sim, Entity hero, ItemInstance codex)
        {
            ItemInstance? best = null;
            if (sim.World.TryGet<Inventory>(hero, out var inv))
            {
                foreach (var item in inv.Items)
                {
                    if (WeaveItems.IsCodex(item) && SameTeaching(codex, item) && (best is null || item.Payload!.Level < best.Payload!.Level))
                    {
                        best = item;
                    }
                }
            }
            return best;
        }

        public void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into)
        {
            foreach (var item in Held(sim, hero))
            {
                if (!WeaveItems.IsCodex(item) || item.Payload is null || !Takes(sim, def, item))
                {
                    continue;
                }
                var duplicate = Duplicate(sim, hero, item);
                if (duplicate is null)
                {
                    continue;
                }
                // A pair in the bag is quoted once, on the copy that stays.
                if (sim.World.Get<Inventory>(hero)?.Find(item.Uid) is not null && Duplicate(sim, hero, duplicate) is { } other && other.Uid == item.Uid && duplicate.Uid < item.Uid)
                {
                    continue;
                }
                into.Add(new Candidate(new Target(item.Uid, null, null), ItemScope(sim, hero, item), NextRarity(sim, item.Rarity) is null ? "max_rarity" : null));
            }
        }

        public bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason)
        {
            scope = EmptyScope.Instance;
            if (target.Item is not { } uid || Find(sim, hero, uid, out _, out _) is not { } codex || !WeaveItems.IsCodex(codex) || codex.Payload is null || !Takes(sim, def, codex))
            {
                reason = "no_item";
                return false;
            }
            if (Duplicate(sim, hero, codex) is null)
            {
                reason = "no_duplicate";
                return false;
            }
            if (NextRarity(sim, codex.Rarity) is null)
            {
                reason = "max_rarity";
                return false;
            }
            scope = ItemScope(sim, hero, codex);
            reason = "";
            return true;
        }

        public void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target)
        {
            var codex = Find(sim, hero, target.Item!.Value, out var seat, out _)!;
            var duplicate = Duplicate(sim, hero, codex)!;
            sim.World.Get<Inventory>(hero)!.Remove(duplicate);
            codex.Rarity = NextRarity(sim, codex.Rarity)!;
            codex.Payload!.Level = Math.Max(codex.Payload.Level, duplicate.Payload!.Level);
            if (seat is { } s)
            {
                sim.Service<RackService>().Rebuild(sim, hero, s);
            }
        }
    }

    // ------------------------------------------------------------ reset_passives

    /// <summary>Refunds every passive the hero bought; the calling's start and realm roots stay.</summary>
    private sealed class ResetPassives : IService
    {
        private static IExprScope Scope(Simulation sim, Entity hero) =>
            new ScopeOverlay(new HeroScope(sim, hero)).Set("refunds", sim.World.Get<PassiveState>(hero)?.Refunds ?? 0);

        private static bool AnyBought(Simulation sim, Entity hero)
        {
            if (!sim.World.TryGet<Stats>(hero, out var stats))
            {
                return false;
            }
            var anchors = sim.Service<ProgressionService>().Anchors(sim, hero);
            foreach (var node in stats.Passives)
            {
                if (!anchors.Contains(node))
                {
                    return true;
                }
            }
            return false;
        }

        public void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into) =>
            into.Add(new Candidate(default, Scope(sim, hero), AnyBought(sim, hero) ? null : "nothing_to_reset"));

        public bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason)
        {
            scope = Scope(sim, hero);
            if (!AnyBought(sim, hero))
            {
                reason = "nothing_to_reset";
                return false;
            }
            reason = "";
            return true;
        }

        public void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target) =>
            sim.Service<ProgressionService>().Reset(sim, hero);
    }

    // ------------------------------------------------------------ reset_tree

    /// <summary>
    /// Clears a weave slot's tree, giving back every point spent in it (<see cref="WeaveRuntime.ResetTree"/>); quoted per
    /// slot with a tree spent, the price reading <c>tree_spent</c> (<see cref="WeaveRuntime.TreePoints"/>) and <c>item_level</c>.
    /// </summary>
    private sealed class ResetTree : IService
    {
        private static IExprScope Scope(Simulation sim, Entity hero, WeaveSlot slot)
        {
            var (spent, _) = sim.Service<WeaveRuntime>().TreePoints(sim, slot);
            return new ScopeOverlay(new HeroScope(sim, hero)).Set("tree_spent", spent).Set("item_level", slot.Spec.Level);
        }

        public void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into)
        {
            if (!sim.World.TryGet<WeaveBook>(hero, out var book))
            {
                return;
            }
            for (var i = 0; i < book.Slots.Count; i++)
            {
                if (book.Slots[i].Spec.Tree.Count > 0)
                {
                    into.Add(new Candidate(new Target(null, i, null), Scope(sim, hero, book.Slots[i]), null));
                }
            }
        }

        public bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason)
        {
            scope = EmptyScope.Instance;
            if (target.Slot is not { } index || !sim.World.TryGet<WeaveBook>(hero, out var book) || index < 0 || index >= book.Slots.Count)
            {
                reason = "no_slot";
                return false;
            }
            if (book.Slots[index].Spec.Tree.Count == 0)
            {
                reason = "no_tree";
                return false;
            }
            scope = Scope(sim, hero, book.Slots[index]);
            reason = "";
            return true;
        }

        public void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target) =>
            sim.Service<WeaveRuntime>().ResetTree(sim, hero, target.Slot!.Value, out _);
    }

    // ------------------------------------------------------------ add_socket

    /// <summary>Chisels an empty socket into a gear item in the bag or worn, up to its class's cap (row I7).</summary>
    private sealed class AddSocket : IService
    {
        public void Candidates(Simulation sim, Entity hero, StationServiceDef def, List<Candidate> into)
        {
            foreach (var item in Held(sim, hero))
            {
                var cap = GearSockets.CapOf(sim, item);
                if (cap <= 0 || !Takes(sim, def, item))
                {
                    continue;
                }
                into.Add(new Candidate(new Target(item.Uid, null, null), ItemScope(sim, hero, item), (item.Sockets?.Count ?? 0) >= cap ? "socket_cap" : null));
            }
        }

        public bool Check(Simulation sim, Entity hero, StationServiceDef def, Target target, out IExprScope scope, out string reason)
        {
            scope = EmptyScope.Instance;
            if (target.Item is not { } uid || Find(sim, hero, uid, out var seat, out _) is not { } item || seat is not null || !Takes(sim, def, item))
            {
                reason = "no_item";
                return false;
            }
            var cap = GearSockets.CapOf(sim, item);
            if (cap <= 0)
            {
                reason = "no_sockets";
                return false;
            }
            if ((item.Sockets?.Count ?? 0) >= cap)
            {
                reason = "socket_cap";
                return false;
            }
            scope = ItemScope(sim, hero, item);
            reason = "";
            return true;
        }

        public void Perform(Simulation sim, Entity hero, StationServiceDef def, Target target) =>
            GearSockets.AddSocket(sim, Find(sim, hero, target.Item!.Value, out _, out _)!, out _);
    }
}
