using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Items;

/// <summary>A stock entry as the hero sees it now: the offer, its price, units left, and whether it can be bought and why not.</summary>
public sealed record VendorOfferView(VendorOffer Offer, int? Remaining, bool Enabled, string? Reason);

/// <summary>
/// Trade with vendors on the server. Opening a vendor mints its stock for the hero (deterministic for the vendor, the
/// hero and the restock period), so what is bought is what was shown. Prices come from the vendor's formulas or rules
/// <c>inventory.vendor.price</c> and <c>sell_price</c>, never from a client. Buying, selling and buying back are all or
/// nothing: a purchase the bag cannot hold, or one the wallet cannot pay, changes nothing.
/// </summary>
public sealed class VendorService
{
    public bool Open(Simulation sim, Entity hero, Entity source, string vendorId, out string reason)
    {
        if (sim.Content.Get<VendorDef>(vendorId) is not { } def)
        {
            reason = "unknown_vendor";
            return false;
        }
        var scope = new HeroScope(sim, hero);
        if (def.When is not null && !def.When.IsTrue(scope))
        {
            reason = "refuses";
            return false;
        }
        var panes = sim.World.GetOrAdd<Panes>(hero);
        panes.Vendor = new VendorPane { Vendor = def.Id, Source = source, Reach = def.Reach ?? sim.Content.Rules.Number("dialogue.reach", 6) };
        Mint(sim, hero, source, def, panes.Vendor, scope);
        sim.Events.Publish(new VendorOpened(hero, source, def.Id) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    public void Close(Simulation sim, Entity hero, string why)
    {
        if (sim.World.Get<Panes>(hero) is not { Vendor: { } pane } panes)
        {
            return;
        }
        panes.Vendor = null;
        sim.Events.Publish(new VendorClosed(hero, pane.Source, why) { Tick = sim.Clock.Tick });
    }

    /// <summary>The restock period stock is minted for: rule <c>inventory.vendor.restock_seconds</c> of simulation time.</summary>
    private static long Period(Simulation sim) => (long)Math.Floor(sim.Clock.Time / Math.Max(1, sim.Content.Rules.Number("inventory.vendor.restock_seconds", 1800)));

    private void Mint(Simulation sim, Entity hero, Entity source, VendorDef def, VendorPane pane, HeroScope scope)
    {
        var gen = sim.Service<ItemGenerator>();
        var heroId = sim.World.Get<Identity>(hero)?.CharacterId ?? hero.Key.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var rng = new Rng(Rng.Combine(Rng.Combine(sim.Rng.Seed, (ulong)source.Key), Rng.Hash64(def.Id + ":" + heroId + ":" + Period(sim))));
        var level = sim.World.Get<Stats>(hero)?.Level ?? 1;
        foreach (var entry in def.Stock)
        {
            if (!sim.Content.Has<ItemBaseDef>(entry.Item) || (entry.When is not null && !entry.When.IsTrue(scope)))
            {
                continue;
            }
            var itemLevel = entry.Level is null ? level : Math.Max(1, (int)Math.Round(entry.Level.Evaluate(scope)));
            var item = gen.Roll(entry.Item, itemLevel, entry.Rarity ?? "common", rng.Fork(Rng.Hash64(entry.Id)),
                new ItemGenerator.PayloadRequest { Glyph = entry.Glyph, Form = entry.Form, Weave = entry.Weave });
            item.Count = Math.Clamp(entry.Count, 1, Math.Max(1, item.Stack));
            var offer = new VendorOffer { Id = entry.Id, Item = item };
            if (entry.Prices.Count > 0)
            {
                var priceScope = ItemScope(sim, scope, item);
                foreach (var (currency, expr) in entry.Prices)
                {
                    offer.Price[currency] = Math.Max(0, Math.Round(expr.Evaluate(priceScope)));
                }
            }
            else
            {
                offer.Price[def.Currency] = Price(sim, scope, def.Price, "inventory.vendor.price", item);
            }
            if (entry.Enabled is not null && !entry.Enabled.IsTrue(scope))
            {
                offer.Enabled = false;
                offer.Reason = entry.Reason.Length > 0 ? entry.Reason : null;
            }
            pane.Stock.Add(offer);
        }
    }

    /// <summary>An item as price formulas read it, over the hero: value, item_level, rarity_rank, tier, quality, count.</summary>
    private static IExprScope ItemScope(Simulation sim, IExprScope hero, ItemInstance item)
    {
        var b = sim.Content.Get<ItemBaseDef>(item.Base);
        return new ScopeOverlay(hero)
            .Set("value", b?.Value ?? 0)
            .Set("item_level", item.Level)
            .Set("rarity_rank", sim.Content.Get<ItemRarityDef>(item.Rarity)?.Rank ?? 0)
            .Set("tier", item.Tier)
            .Set("quality", item.Quality)
            .Set("count", item.Count);
    }

    private static double Price(Simulation sim, IExprScope hero, Expr? own, string rule, ItemInstance item)
    {
        var expr = own ?? sim.Content.Rules.Expr(rule, "max(1, round((value + 2 + item_level) * (1 + rarity_rank * 1.5))) * count");
        return Math.Max(0, Math.Round(expr.Evaluate(ItemScope(sim, hero, item))));
    }

    /// <summary>What the vendor pays for an item from the hero's bag, or null when it does not buy that kind of item.</summary>
    public Dictionary<string, double>? SellPrice(Simulation sim, Entity hero, VendorDef def, ItemInstance item, int? units = null)
    {
        if (item.Locked || (!def.Buys.IsEmpty && !sim.Service<ItemResolver>().TagsOf(item).HasAny(def.Buys)))
        {
            return null;
        }
        var counted = units is { } u && u != item.Count ? Copy(item, u) : item;
        var expr = def.SellPrice ?? sim.Content.Rules.Expr("inventory.vendor.sell_price", "max(1, floor((value + 1 + item_level * 0.5) * (1 + rarity_rank))) * count");
        var amount = Math.Max(0, Math.Round(expr.Evaluate(ItemScope(sim, new HeroScope(sim, hero), counted))));
        return new Dictionary<string, double>(StringComparer.Ordinal) { [def.Currency] = amount };
    }

    private static ItemInstance Copy(ItemInstance item, int count)
    {
        var copy = item.Clone(item.Uid);
        copy.Count = count;
        return copy;
    }

    /// <summary>The stock as the hero sees it now: units left per entry, and whether each can be bought.</summary>
    public List<VendorOfferView> Offers(Simulation sim, Entity hero)
    {
        var views = new List<VendorOfferView>();
        if (sim.World.Get<Panes>(hero)?.Vendor is not { } pane || sim.Content.Get<VendorDef>(pane.Vendor) is not { } def)
        {
            return views;
        }
        var stock = sim.World.Get<VendorStock>(pane.Source);
        var inv = sim.World.Get<Inventory>(hero);
        foreach (var offer in pane.Stock)
        {
            var entry = def.Stock.Find(s => s.Id == offer.Id);
            int? remaining = entry?.Stock is { } limit ? (stock is not null && stock.Remaining.TryGetValue(offer.Id, out var left) ? left : limit) : null;
            var enabled = offer.Enabled;
            var reason = offer.Reason;
            if (enabled && remaining is <= 0)
            {
                enabled = false;
                reason = sim.Content.Rules.Text("inventory.vendor.sold_out_text", "Sold out.");
            }
            if (enabled)
            {
                foreach (var (currency, amount) in offer.Price)
                {
                    if ((inv?.CurrencyOf(currency) ?? 0) < amount)
                    {
                        enabled = false;
                        reason = sim.Content.Rules.Text("inventory.vendor.cannot_afford_text", "Not enough {currency}.")
                            .Replace("{currency}", sim.Content.Get<CurrencyDef>(currency)?.DisplayName ?? Definition.Humanize(currency), StringComparison.Ordinal);
                        break;
                    }
                }
            }
            views.Add(new VendorOfferView(offer, remaining, enabled, reason));
        }
        return views;
    }

    // ------------------------------------------------------------ trades

    public bool Buy(Simulation sim, Entity hero, Entity vendor, string stockId, int count, out string reason)
    {
        if (!Session(sim, hero, vendor, out var pane, out var def, out reason))
        {
            return false;
        }
        var offer = pane.Stock.Find(o => o.Id == stockId);
        if (offer is null)
        {
            reason = "unknown_stock";
            return false;
        }
        if (!offer.Enabled)
        {
            reason = "not_for_sale";
            return false;
        }
        count = Math.Max(1, count);
        var entry = def.Stock.Find(s => s.Id == stockId);
        var stock = sim.World.GetOrAdd<VendorStock>(vendor);
        if (entry?.Stock is { } limit)
        {
            var left = stock.Remaining.TryGetValue(stockId, out var l) ? l : limit;
            if (left < count)
            {
                reason = "sold_out";
                return false;
            }
        }
        var total = Scaled(offer.Price, count);
        var inv = sim.World.Get<Inventory>(hero)!;
        if (Unaffordable(inv, total) is { } missing)
        {
            reason = "cannot_afford:" + missing;
            return false;
        }
        var gen = sim.Service<ItemGenerator>();
        var units = new List<ItemInstance>(count);
        for (var i = 0; i < count; i++)
        {
            units.Add(offer.Item.Clone(gen.NextUid()));
        }
        if (!AddAll(inv, units))
        {
            reason = "bag_full";
            return false;
        }
        Pay(sim, hero, inv, total, -1);
        if (entry?.Stock is { } cap)
        {
            stock.Remaining[stockId] = (stock.Remaining.TryGetValue(stockId, out var had) ? had : cap) - count;
        }
        var tags = sim.Service<ItemResolver>().TagsOf(offer.Item);
        foreach (var unit in units)
        {
            sim.Events.Publish(new ItemAcquired(hero, unit.Uid, unit.Base, offer.Item.Count, tags, unit.Rarity) { Tick = sim.Clock.Tick });
        }
        sim.Events.Publish(new ItemBought(hero, vendor, stockId, units[0].Uid, total) { Tick = sim.Clock.Tick });
        sim.Events.Publish(new VendorChanged(hero, vendor) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    public bool Sell(Simulation sim, Entity hero, Entity vendor, long itemUid, int? count, out string reason)
    {
        if (!Session(sim, hero, vendor, out _, out var def, out reason))
        {
            return false;
        }
        var inv = sim.World.Get<Inventory>(hero)!;
        var item = inv.Find(itemUid);
        if (item is null)
        {
            reason = "no_item";
            return false;
        }
        var units = Math.Clamp(count ?? item.Count, 1, item.Count);
        if (SellPrice(sim, hero, def, item, units) is not { } price)
        {
            reason = item.Locked ? "locked" : "not_bought_here";
            return false;
        }
        ItemInstance sold;
        if (units < item.Count)
        {
            sold = item.Clone(sim.Service<ItemGenerator>().NextUid());
            sold.Count = units;
            item.Count -= units;
        }
        else
        {
            inv.Remove(item);
            sold = item;
        }
        Pay(sim, hero, inv, price, +1);
        var panes = sim.World.GetOrAdd<Panes>(hero);
        if (!panes.BuyBack.TryGetValue(vendor.Key, out var list))
        {
            panes.BuyBack[vendor.Key] = list = new List<SoldItem>();
        }
        list.Add(new SoldItem { Item = sold, Paid = new Dictionary<string, double>(price, StringComparer.Ordinal) });
        while (list.Count > Math.Max(0, def.BuyBack))
        {
            list.RemoveAt(0);
        }
        sim.Events.Publish(new ItemSold(hero, vendor, sold.Uid, price) { Tick = sim.Clock.Tick });
        sim.Events.Publish(new VendorChanged(hero, vendor) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    public bool BuyBack(Simulation sim, Entity hero, Entity vendor, long itemUid, out string reason)
    {
        if (!Session(sim, hero, vendor, out _, out _, out reason))
        {
            return false;
        }
        var panes = sim.World.Get<Panes>(hero)!;
        if (!panes.BuyBack.TryGetValue(vendor.Key, out var list) || list.Find(s => s.Item.Uid == itemUid) is not { } entry)
        {
            reason = "no_item";
            return false;
        }
        var price = BuyBackPrice(sim, entry);
        var inv = sim.World.Get<Inventory>(hero)!;
        if (Unaffordable(inv, price) is { } missing)
        {
            reason = "cannot_afford:" + missing;
            return false;
        }
        entry.Item.X = null;
        entry.Item.Y = null;
        if (!AddAll(inv, new List<ItemInstance> { entry.Item }))
        {
            reason = "bag_full";
            return false;
        }
        Pay(sim, hero, inv, price, -1);
        list.Remove(entry);
        sim.Events.Publish(new ItemAcquired(hero, entry.Item.Uid, entry.Item.Base, entry.Item.Count, sim.Service<ItemResolver>().TagsOf(entry.Item), entry.Item.Rarity) { Tick = sim.Clock.Tick });
        sim.Events.Publish(new VendorChanged(hero, vendor) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>What buying back costs: what the vendor paid, times rule <c>inventory.vendor.buy_back_markup</c>.</summary>
    public static Dictionary<string, double> BuyBackPrice(Simulation sim, SoldItem sold) =>
        Scaled(sold.Paid, sim.Content.Rules.Number("inventory.vendor.buy_back_markup", 1));

    /// <summary>The hero's open vendor pane with this vendor, in reach; otherwise why not.</summary>
    private static bool Session(Simulation sim, Entity hero, Entity vendor, out VendorPane pane, out VendorDef def, out string reason)
    {
        pane = null!;
        def = null!;
        if (sim.World.Get<Panes>(hero)?.Vendor is not { } open || open.Source != vendor || sim.Content.Get<VendorDef>(open.Vendor) is not { } vendorDef)
        {
            reason = "not_trading";
            return false;
        }
        if (!Dialogue.DialogueService.InReach(sim, hero, vendor, open.Reach))
        {
            sim.Service<VendorService>().Close(sim, hero, "departed");
            reason = "too_far";
            return false;
        }
        if (!sim.World.Has<Inventory>(hero))
        {
            reason = "no_bag";
            return false;
        }
        pane = open;
        def = vendorDef;
        reason = "";
        return true;
    }

    private static Dictionary<string, double> Scaled(Dictionary<string, double> price, double factor)
    {
        var scaled = new Dictionary<string, double>(price.Count, StringComparer.Ordinal);
        foreach (var (currency, amount) in price)
        {
            scaled[currency] = Math.Round(amount * factor);
        }
        return scaled;
    }

    private static string? Unaffordable(Inventory inv, Dictionary<string, double> price)
    {
        foreach (var (currency, amount) in price)
        {
            if (inv.CurrencyOf(currency) < amount - 1e-9)
            {
                return currency;
            }
        }
        return null;
    }

    private static void Pay(Simulation sim, Entity hero, Inventory inv, Dictionary<string, double> price, int sign)
    {
        foreach (var (currency, amount) in price)
        {
            if (amount <= 0)
            {
                continue;
            }
            inv.AddCurrency(currency, sign * amount);
            sim.Events.Publish(new CurrencyChanged(hero, currency, sign * amount, inv.CurrencyOf(currency)) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Adds every item to the bag, stacking where it can, or none of them.</summary>
    private static bool AddAll(Inventory inv, List<ItemInstance> items)
    {
        var merged = new List<(ItemInstance Into, int Count)>();
        var placed = new List<ItemInstance>();
        var original = new int[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            original[i] = items[i].Count;
        }
        foreach (var item in items)
        {
            var remaining = item.Count;
            if (item.Stack > 1)
            {
                foreach (var existing in inv.Items)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    if (!ReferenceEquals(existing, item) && existing.CanStackWith(item))
                    {
                        var moved = Math.Min(existing.Stack - existing.Count, remaining);
                        existing.Count += moved;
                        remaining -= moved;
                        merged.Add((existing, moved));
                    }
                }
            }
            if (remaining <= 0)
            {
                item.Count = 0;
                continue;
            }
            item.Count = remaining;
            var (w, h) = inv.FootprintOf(item);
            if (!(inv.Bag.FindSpace(w, h, out var x, out var y) && inv.Bag.Place(item, x, y, w, h)))
            {
                foreach (var (into, count) in merged)
                {
                    into.Count -= count;
                }
                foreach (var p in placed)
                {
                    inv.Remove(p);
                }
                for (var i = 0; i < items.Count; i++)
                {
                    items[i].Count = original[i];
                }
                return false;
            }
            placed.Add(item);
        }
        return true;
    }
}
