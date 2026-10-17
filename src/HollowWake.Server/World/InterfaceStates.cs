using System.Globalization;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using HollowWake.Server.Config;

namespace HollowWake.Server.World;

/// <summary>
/// The states the interface draws (docs/design/ui.md), for clients that negotiated
/// <see cref="ProtocolInfo.InterfaceFeature"/>: the cluster's fields on the self state, and the inventory,
/// weave slot, passive and page states. Each state has a fingerprint over what it reads, so the snapshot
/// builder sends it only when it changed. Nothing here mutates the simulation.
/// </summary>
public static class InterfaceStates
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------ the cluster

    /// <summary>The self state with the cluster's fields: reserved amounts, pools before life, statuses, the cast, charges and stamps.</summary>
    public static SelfStateMsg WithCluster(Simulation sim, Entity e, SelfStateMsg self)
    {
        var stats = sim.World.Require<Stats>(e);
        return self with
        {
            Reserved = Reserved(stats),
            Pools = Pools(sim, stats),
            Statuses = StatusesOf(sim, e),
            Cast = CastOf(sim, e),
            Charges = ChargesOf(sim, e, stats),
            Stamps = StampsOf(sim, stats),
        };
    }

    /// <summary>Folds what <see cref="WithCluster"/> reads into a self-state fingerprint, quantized as the self state is.</summary>
    public static ulong ClusterFingerprint(Simulation sim, Entity e, ulong seed, SnapshotSettings settings)
    {
        var h = new FingerprintHash(seed);
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return h.Value;
        }
        foreach (var (res, _) in stats.Resources)
        {
            h.Add((long)Math.Round(stats.Gauge("reserved." + res) * 10));
        }
        foreach (var pool in PoolIds(sim))
        {
            h.Add(PoolState(stats, pool, out _));
        }
        if (sim.World.TryGet<Statuses>(e, out var statuses))
        {
            var step = Math.Max(1e-3, settings.SelfSeconds);
            foreach (var s in statuses.Active)
            {
                h.Add(s.Id);
                h.Add(s.Stacks);
                // The end time, not the time left, so a status counting down does not change the fingerprint.
                h.Add(double.IsFinite(s.Remaining) ? (long)Math.Round((sim.Clock.Time + s.Remaining) / step) : -1);
            }
        }
        if (sim.World.TryGet<WeaveBook>(e, out var book) && book.Casting is { } casting)
        {
            h.Add(casting.StartedAt);
            h.Add(casting.Stage);
            h.Add(casting.Channeling);
            h.Add((long)(CastProgress(sim, casting) * Math.Max(1, settings.CastSteps)));
        }
        if (sim.World.TryGet<Charges>(e, out var charges))
        {
            foreach (var (id, count) in charges.Counts)
            {
                h.Add(id);
                h.Add(count);
            }
        }
        return h.Value;
    }

    private static Dictionary<string, double>? Reserved(Stats stats)
    {
        Dictionary<string, double>? reserved = null;
        foreach (var (res, _) in stats.Resources)
        {
            var amount = stats.Gauge("reserved." + res);
            if (amount > 0)
            {
                (reserved ??= new Dictionary<string, double>(StringComparer.Ordinal))[res] = Math.Round(amount, 1);
            }
        }
        return reserved;
    }

    /// <summary>The pools hits reach before life, from rule <c>combat.pools</c>.</summary>
    private static IEnumerable<string> PoolIds(Simulation sim)
    {
        foreach (var id in sim.Content.Rules.Strings("combat.pools"))
        {
            if (id == "life")
            {
                yield break;
            }
            yield return id;
        }
    }

    /// <summary>
    /// A pool's state. The pool's own mechanics mark it with a flag <c>pool.{id}.{state}</c> (broken, rearming or
    /// recharging) and stamp when the state began; without a flag a spent pool reads broken, a partial one
    /// recharging and a full one ready. A pool the hero has no maximum in is not shown.
    /// </summary>
    private static string PoolState(Stats stats, string pool, out bool shown)
    {
        var max = stats.ResourceMax(pool);
        shown = max > 0;
        foreach (var state in PoolStates)
        {
            if (stats.HasFlag("pool." + pool + "." + state))
            {
                return state;
            }
        }
        var current = stats.Resource(pool);
        return current <= 0 ? "broken" : current < max - 1e-6 ? "recharging" : "ready";
    }

    private static readonly string[] PoolStates = { "broken", "rearming", "recharging" };

    private static List<PoolInfo>? Pools(Simulation sim, Stats stats)
    {
        List<PoolInfo>? pools = null;
        foreach (var id in PoolIds(sim))
        {
            var state = PoolState(stats, id, out var shown);
            if (!shown)
            {
                continue;
            }
            var since = stats.Recent.TryGetValue("pool." + id + "." + state, out var at) ? Math.Round(at, 2) : (double?)null;
            (pools ??= new List<PoolInfo>()).Add(new PoolInfo(id, state) { Since = since });
        }
        return pools;
    }

    private static List<StatusInfo>? StatusesOf(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Statuses>(e, out var statuses) || statuses.Active.Count == 0)
        {
            return null;
        }
        var list = new List<StatusInfo>(statuses.Active.Count);
        foreach (var s in statuses.Active)
        {
            var remaining = double.IsFinite(s.Remaining) ? Math.Round(Math.Max(0, s.Remaining), 2) : -1;
            var duration = double.IsFinite(s.Total) ? Math.Round(s.Total, 2) : -1;
            var merged = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Id == s.Id)
                {
                    // Independent instances of one status show as one icon with every stack and the longest timer.
                    var known = list[i];
                    list[i] = new StatusInfo(s.Id, known.Stacks + s.Stacks, Math.Max(known.Remaining, remaining), Math.Max(known.Duration, duration));
                    merged = true;
                    break;
                }
            }
            if (!merged)
            {
                list.Add(new StatusInfo(s.Id, s.Stacks, remaining, duration));
            }
        }
        return list;
    }

    private static double CastProgress(Simulation sim, CastState casting) =>
        casting.Duration <= 0 ? 0 : Math.Clamp((sim.Clock.Time - casting.StartedAt) / casting.Duration, 0, 1);

    private static CastInfo? CastOf(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<WeaveBook>(e, out var book) || book.Casting is not { } casting)
        {
            return null;
        }
        var slot = book.Slots.IndexOf(casting.Slot);
        var compiled = casting.Slot.Compiled;
        var mode = compiled is null ? "cast" : ModeName(compiled.CastMode);
        return new CastInfo(slot, mode, Math.Round(CastProgress(sim, casting), 2), casting.Stage, 0)
        {
            Name = compiled?.Name,
            Held = casting.Held > 0 ? Math.Round(casting.Held, 2) : null,
            Duration = casting.Duration > 0 ? Math.Round(casting.Duration, 2) : null,
        };
    }

    public static string ModeName(CastMode mode) => mode switch
    {
        CastMode.Instant => "instant",
        CastMode.Channel => "channel",
        CastMode.Charge => "charge",
        _ => "cast",
    };

    private static List<ChargeInfo>? ChargesOf(Simulation sim, Entity e, Stats stats)
    {
        if (!sim.World.TryGet<Charges>(e, out var charges) || charges.Counts.Count == 0)
        {
            return null;
        }
        var list = new List<ChargeInfo>(charges.Counts.Count);
        foreach (var (id, count) in charges.Counts)
        {
            var maxAttribute = "charge." + id + ".max";
            var max = sim.Content.Attributes.Contains(maxAttribute) ? stats.Get(maxAttribute) : 0;
            list.Add(new ChargeInfo(id, count, Math.Round(max, 2)));
        }
        return list;
    }

    private static Dictionary<string, double>? StampsOf(Simulation sim, Stats stats)
    {
        Dictionary<string, double>? stamps = null;
        var now = sim.Clock.Time;
        foreach (var (key, at) in stats.Recent)
        {
            if (now - at <= stats.RecentWindow)
            {
                (stamps ??= new Dictionary<string, double>(StringComparer.Ordinal))[key] = Math.Round(at, 2);
            }
        }
        return stamps;
    }

    // ------------------------------------------------------------ inventory

    public static InventoryStateMsg Inventory(Simulation sim, Entity e)
    {
        var resolver = sim.Service<ItemResolver>();
        var stats = sim.World.Get<Stats>(e);
        var inv = sim.World.Get<Inventory>(e);
        var eq = sim.World.Get<Equipment>(e);
        var bag = inv is null ? new BoardInfo(1, 1, new List<ItemInfo>()) : BoardOf(sim, resolver, stats, inv.Bag);
        var containers = new List<ContainerInfo>();
        if (inv is not null && inv.Overflow.Count > 0)
        {
            // Items the bag holds without a cell show as a board of their own until there is room for them.
            var overflow = new List<ItemInfo>(inv.Overflow.Count);
            for (var i = 0; i < inv.Overflow.Count; i++)
            {
                var (w, h) = inv.FootprintOf(inv.Overflow[i]);
                overflow.Add(Card(sim, resolver, stats, inv.Overflow[i]) with { X = 0, Y = i, W = w, H = h });
            }
            containers.Add(new ContainerInfo(InventoryService.Overflow, "Overflow", "overflow", new BoardInfo(1, Math.Max(1, inv.Overflow.Count), overflow), true)
            {
                Reason = "Make room in the bag for these.",
                Owner = "character",
            });
        }
        var slots = new List<EquipSlotInfo>();
        foreach (var def in sim.Content.Each<SlotDef>().OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal))
        {
            string? occupiedBy = null;
            if (eq is not null && eq.OccupiedBy.TryGetValue(def.Id, out var by))
            {
                occupiedBy = by;
            }
            slots.Add(new EquipSlotInfo(def.Id, def.DisplayName, def.Order) { OccupiedBy = occupiedBy });
        }
        var equipment = new Dictionary<string, ItemInfo>(StringComparer.Ordinal);
        if (eq is not null)
        {
            foreach (var (slot, item) in eq.BySlot)
            {
                equipment[slot] = Card(sim, resolver, stats, item);
            }
        }
        var wallet = new List<CurrencyInfo>();
        foreach (var def in sim.Content.Each<CurrencyDef>().OrderBy(c => c.Order).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            wallet.Add(new CurrencyInfo(def.Id, def.DisplayName, inv?.CurrencyOf(def.Id) ?? 0));
        }
        return new InventoryStateMsg(bag, slots, equipment, wallet, containers);
    }

    /// <summary>A board as the wire draws it: its shape and mask, and every item with its cell and footprint.</summary>
    public static BoardInfo BoardOf(Simulation sim, ItemResolver resolver, Stats? stats, Board board)
    {
        var items = new List<ItemInfo>(board.Placed.Count);
        foreach (var p in board.Placed)
        {
            items.Add(Card(sim, resolver, stats, p.Item) with { X = p.X, Y = p.Y, W = p.Width, H = p.Height });
        }
        return new BoardInfo(board.Width, board.Height, items) { Mask = board.MaskRows() };
    }

    /// <summary>An item's full card: the self state's fields plus its footprint, class, requirements and payload.</summary>
    public static ItemInfo Card(Simulation sim, ItemResolver resolver, Stats? stats, ItemInstance item)
    {
        var b = resolver.BaseOf(item);
        List<RequirementInfo>? requirements = null;
        foreach (var (attr, required) in b.Requirements)
        {
            var name = attr == "level" ? "Level" : sim.Content.Attributes.Get(attr)?.DisplayName ?? Definition.Humanize(attr);
            var have = stats is null ? 0 : attr == "level" ? stats.Level : stats.Get(attr);
            (requirements ??= new List<RequirementInfo>()).Add(new RequirementInfo($"{name} {required.ToString("0.##", Invariant)}", have >= required));
        }
        string? flavor = null;
        if (item.Unique is not null && sim.Content.TryGet<UniqueDef>(item.Unique, out var unique) && unique is not null && unique.Flavor.Length > 0)
        {
            flavor = unique.Flavor;
        }
        return Snapshots.Item(resolver, item) with
        {
            W = Math.Max(1, b.Width),
            H = Math.Max(1, b.Height),
            Class = b.Class,
            Icon = b.Icon,
            Quality = item.Quality > 0 ? Math.Round(item.Quality, 3) : null,
            Flavor = flavor,
            Locked = item.Locked ? true : null,
            Requirements = requirements,
        };
    }

    public static ulong InventoryFingerprint(Simulation sim, Entity e)
    {
        var h = new FingerprintHash(3);
        if (sim.World.TryGet<Inventory>(e, out var inv))
        {
            Snapshots.AddBag(ref h, inv);
        }
        if (sim.World.TryGet<Equipment>(e, out var eq))
        {
            foreach (var (slot, item) in eq.BySlot)
            {
                h.Add(slot);
                Snapshots.AddItem(ref h, item);
            }
        }
        // Requirements read the hero's attributes: a level or an attribute bought can meet one.
        if (sim.World.TryGet<Stats>(e, out var stats))
        {
            h.Add(stats.Level);
            h.Add(stats.Passives.Count);
        }
        return h.Value;
    }

    // ------------------------------------------------------------ weave slots

    public static WeaveSlotsMsg WeaveSlots(Simulation sim, Entity e)
    {
        var stats = sim.World.Require<Stats>(e);
        var book = sim.World.Get<WeaveBook>(e);
        var runtime = sim.Service<WeaveRuntime>();
        var compiler = sim.Service<WeaveCompiler>();
        var count = Math.Max(book?.Slots.Count ?? 0, (int)sim.Content.Rules.Number("weaves.max_slots", 8));
        var seats = new List<WeaveSeatInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var slot = book is not null && i < book.Slots.Count ? book.Slots[i] : null;
            if (slot is null || (slot.Spec.Preset is null && slot.Spec.Form is null))
            {
                seats.Add(new WeaveSeatInfo(i, null, null, "", 0, 0, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo>()));
                continue;
            }
            seats.Add(Seat(sim, stats, book!, runtime, compiler, i, slot));
        }
        return new WeaveSlotsMsg(seats);
    }

    private static WeaveSeatInfo Seat(Simulation sim, Stats stats, WeaveBook book, WeaveRuntime runtime, WeaveCompiler compiler, int index, WeaveSlot slot)
    {
        var key = slot.Key;
        var compiled = runtime.Resolve(sim, slot, out var error);
        string? dormant = null;
        if (compiled is null)
        {
            // A strict compile failed: compile with misfits dormant so the slot still shows what it holds and why.
            compiler.TryCompile(slot.Spec, out compiled, out var dormantError, GlyphFit.Dormant);
            dormant = compiled is null ? dormantError ?? error : error;
        }
        var glyphs = new List<SeatGlyphInfo>();
        if (compiled is not null)
        {
            var socket = 0;
            foreach (var glyphRef in slot.Spec.Glyphs)
            {
                var def = sim.Content.Get<GlyphDef>(glyphRef.Id);
                var reason = (string?)null;
                foreach (var d in compiled.Dormant)
                {
                    if (d.Ref.Id == glyphRef.Id)
                    {
                        reason = d.Reason;
                        break;
                    }
                }
                glyphs.Add(new SeatGlyphInfo(socket, glyphRef.Id, def?.DisplayName ?? glyphRef.Id, glyphRef.Level ?? def?.Level ?? 1) { Dormant = reason });
                socket++;
            }
        }
        var readyIn = book.ReadyAt.TryGetValue(key, out var at) ? Math.Max(0, at - sim.Clock.Time) : 0;
        var preview = new List<PreviewLineInfo>();
        double cooldown = 0;
        Dictionary<string, double>? costs = null;
        if (compiled is not null)
        {
            cooldown = compiled.Cooldown(stats);
            costs = runtime.Costs(compiled, stats);
            preview.Add(new PreviewLineInfo("Cast time", Seconds(runtime.CastDuration(compiled, stats))));
            if (cooldown > 0)
            {
                preview.Add(new PreviewLineInfo("Cooldown", Seconds(cooldown)));
            }
            foreach (var (resource, cost) in costs)
            {
                preview.Add(new PreviewLineInfo("Cost", $"{cost.ToString("0.#", Invariant)} {sim.Content.Get<ResourceDef>(resource)?.DisplayName ?? resource}"));
            }
            preview.Add(new PreviewLineInfo("Range", compiled.Range(stats).ToString("0.#", Invariant)));
        }
        var icon = slot.Spec.Preset is { } preset ? sim.Content.Get<WeaveDef>(preset)?.Icon : null;
        return new WeaveSeatInfo(index, key, compiled?.Form.Id ?? slot.Spec.Form, compiled?.Name ?? key, compiled?.Level ?? slot.Spec.Level,
            compiled?.Form.MaxGlyphs ?? 0, glyphs, Math.Round(readyIn, 2), Math.Round(cooldown, 2), preview)
        {
            Icon = icon ?? compiled?.Look,
            Dormant = dormant,
            Threads = compiled is null ? null : compiled.Threads.Select(t => t.Id).ToList(),
            CastMode = compiled is null ? null : ModeName(compiled.CastMode),
            Targeting = compiled?.Targeting.ToString().ToLowerInvariant(),
            Costs = costs,
        };
    }

    private static string Seconds(double s) => s.ToString("0.##", Invariant) + " s";

    public static ulong WeaveSlotsFingerprint(Simulation sim, Entity e, int previewVersion)
    {
        var h = new FingerprintHash(4);
        h.Add(previewVersion);
        if (sim.World.TryGet<WeaveBook>(e, out var book))
        {
            foreach (var slot in book.Slots)
            {
                h.Add(slot.Key);
                h.Add((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slot));
            }
            // Cooldowns by when they end, so a sweep counting down does not resend the slots.
            foreach (var (key, at) in book.ReadyAt)
            {
                if (at > sim.Clock.Time)
                {
                    h.Add(key);
                    h.Add(at);
                }
            }
        }
        return h.Value;
    }

    // ------------------------------------------------------------ passives

    public static PassiveStateMsg Passives(Simulation sim, Entity e)
    {
        var stats = sim.World.Require<Stats>(e);
        var progression = sim.Service<ProgressionService>();
        var points = new Dictionary<string, int>(StringComparer.Ordinal) { ["passive"] = progression.AvailablePoints(sim, stats) };
        return new PassiveStateMsg(new List<string>(stats.Passives), new Dictionary<string, List<string>>(StringComparer.Ordinal), points, new List<string> { "star" }, false);
    }

    public static ulong PassiveFingerprint(Simulation sim, Entity e)
    {
        var h = new FingerprintHash(5);
        if (sim.World.TryGet<Stats>(e, out var stats))
        {
            h.Add(stats.Level);
            h.Add(stats.PassivePointsSpent);
            foreach (var p in stats.Passives)
            {
                h.Add(p);
            }
        }
        return h.Value;
    }

    // ------------------------------------------------------------ pages

    public static PageStateMsg Pages(Simulation sim, Entity e)
    {
        var stats = sim.World.Require<Stats>(e);
        var unspent = Math.Max(0, sim.Service<ProgressionService>().AvailablePoints(sim, stats));
        return new PageStateMsg(new List<PageInfo>
        {
            new("inventory", false, 0),
            new("weaves", false, 0),
            new("passives", false, unspent),
            new("character", false, 0),
        });
    }

    public static ulong PageFingerprint(Simulation sim, Entity e)
    {
        var h = new FingerprintHash(6);
        if (sim.World.TryGet<Stats>(e, out var stats))
        {
            h.Add(Math.Max(0, sim.Service<ProgressionService>().AvailablePoints(sim, stats)));
        }
        return h.Value;
    }
}
