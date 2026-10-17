using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Loot;

/// <summary>Gives items to bags, drops them on the ground, and picks them up.</summary>
public sealed class LootService
{
    private readonly ItemGenerator _generator;

    public LootService(ItemGenerator generator)
    {
        _generator = generator;
    }

    public ItemGenerator Generator => _generator;

    public bool Give(Simulation sim, Entity to, ItemInstance item)
    {
        if (!sim.World.TryGet<Inventory>(to, out var inv))
        {
            var pos = sim.World.Get<Transform>(to)?.Position ?? default;
            Drop(sim, item, pos, Entity.None, to);
            return false;
        }
        if (!inv.TryAdd(item))
        {
            var pos = sim.World.Get<Transform>(to)?.Position ?? default;
            Drop(sim, item, pos, to, to);
            return false;
        }
        var tags = _generator.Resolver.TagsOf(item);
        sim.Events.Publish(new ItemAcquired(to, item.Uid, item.Base, item.Count, tags, item.Rarity) { Tick = sim.Clock.Tick });
        return true;
    }

    public void GiveCurrency(Simulation sim, Entity to, string currency, double amount)
    {
        if (!sim.World.TryGet<Inventory>(to, out var inv))
        {
            return;
        }
        inv.AddCurrency(currency, amount);
        sim.Events.Publish(new CurrencyChanged(to, currency, amount, inv.CurrencyOf(currency)) { Tick = sim.Clock.Tick });
    }

    public void GiveTable(Simulation sim, Entity to, string table, int level, Rng rng)
    {
        foreach (var r in _generator.RollLoot(table, level, rng))
        {
            if (r.Item is not null)
            {
                Give(sim, to, r.Item);
            }
            else if (r.Currency is not null)
            {
                GiveCurrency(sim, to, r.Currency, r.Amount);
            }
        }
    }

    public Entity Drop(Simulation sim, ItemInstance item, Vector3 position, Entity owner, Entity from)
    {
        var e = sim.World.Create();
        var rng = sim.RngFor(e, "drop");
        var ang = rng.NextDouble() * Math.PI * 2;
        var pos = sim.Terrain.SnapToGround(position + new Vector3((float)Math.Cos(ang) * 0.8f, (float)Math.Sin(ang) * 0.8f, 0), 2);
        sim.World.Add(e, new Transform { Position = pos, Radius = 0.3f, Height = 0.3f });
        sim.World.Add(e, new Identity { Name = item.Name, Tags = TagSet.Of("drop", "rarity." + item.Rarity), Look = "drop:" + item.Base });
        var ownerSeconds = sim.Content.Rules.Number("items.loot.owner_seconds", 60);
        var expireSeconds = sim.Content.Rules.Number("items.loot.expire_seconds", 600);
        sim.World.Add(e, new LootDrop { Item = item, Owner = owner, OwnerUntil = sim.Clock.Time + ownerSeconds, ExpiresAt = sim.Clock.Time + expireSeconds });
        sim.Spatial.Update(e, pos);
        sim.Events.Publish(new ItemDropped(e, from, item.Base, item.Rarity, pos) { Tick = sim.Clock.Tick });
        return e;
    }

    /// <summary>
    /// Rolls a table and drops what it gives at a position. Stones and codices that a table leaves open lean
    /// toward the forms and glyphs <paramref name="from"/> weaves with (question Q1: drops lean toward the dropper's kit).
    /// </summary>
    public void DropTable(Simulation sim, string table, int level, Vector3 position, Entity owner, Entity from, Rng rng)
    {
        foreach (var r in _generator.RollLoot(table, level, rng, lean: LeanOf(sim, from)))
        {
            if (r.Item is not null)
            {
                Drop(sim, r.Item, position, owner, from);
            }
            else if (r.Currency is not null && !owner.IsNone)
            {
                GiveCurrency(sim, owner, r.Currency, r.Amount);
            }
        }
    }

    /// <summary>The forms and glyphs an entity's weaves are made of, or null when it weaves nothing.</summary>
    private static HashSet<string>? LeanOf(Simulation sim, Entity from)
    {
        if (!sim.World.TryGet<WeaveBook>(from, out var book) || book.Slots.Count == 0)
        {
            return null;
        }
        var lean = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in book.Slots)
        {
            var spec = slot.Spec;
            if (spec.Preset is { } preset && sim.Content.Get<Definitions.WeaveDef>(preset) is { } def)
            {
                lean.Add(def.Form);
                foreach (var glyph in def.Glyphs)
                {
                    lean.Add(glyph.Id);
                }
            }
            if (spec.Form is { } form)
            {
                lean.Add(form);
            }
            foreach (var glyph in spec.Glyphs)
            {
                lean.Add(glyph.Id);
            }
        }
        return lean.Count == 0 ? null : lean;
    }

    public bool PickUp(Simulation sim, Entity who, Entity drop, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<LootDrop>(drop, out var loot) || !sim.World.TryGet<Transform>(drop, out var dt) || !sim.World.TryGet<Transform>(who, out var wt))
        {
            reason = "no_drop";
            return false;
        }
        var reach = sim.Content.Rules.Number("items.loot.pickup_range", 2.5);
        if (wt.GroundDistanceTo(dt.Position) > reach)
        {
            reason = "too_far";
            return false;
        }
        if (!loot.Owner.IsNone && loot.Owner != who && sim.Clock.Time < loot.OwnerUntil)
        {
            reason = "owned";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(who, out var inv) || !inv.TryAdd(loot.Item))
        {
            reason = "bag_full";
            return false;
        }
        var tags = _generator.Resolver.TagsOf(loot.Item);
        sim.Events.Publish(new ItemAcquired(who, loot.Item.Uid, loot.Item.Base, loot.Item.Count, tags, loot.Item.Rarity) { Tick = sim.Clock.Tick });
        Systems.LifetimeSystem.Despawn(sim, drop);
        return true;
    }
}
