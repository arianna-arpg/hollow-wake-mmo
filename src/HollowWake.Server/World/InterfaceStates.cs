using System.Globalization;
using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
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
            h.Add((long)Math.Round(ReservedOf(stats, res) * 10));
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
            var steps = Math.Max(1, settings.CastSteps);
            var progress = casting.Mode is CastMode.Charge or CastMode.Overcharge ? Math.Clamp(casting.Fill, 0, 1) : CastProgress(sim, casting);
            h.Add((long)(progress * steps));
            h.Add((long)(casting.Brim * steps));
            if (casting.Mode == CastMode.Guard && GuardPool(sim, e, casting) is { } pool)
            {
                h.Add((long)(pool * steps));
            }
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
            var amount = ReservedOf(stats, res);
            if (amount > 0)
            {
                (reserved ??= new Dictionary<string, double>(StringComparer.Ordinal))[res] = Math.Round(amount, 1);
            }
        }
        return reserved;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ReservedNames = new(StringComparer.Ordinal);

    /// <summary>
    /// What toggles and contracts hold back of a resource: attribute <c>{resource}.reserved</c>, which they raise
    /// with flat modifiers while they run (and which the resource's maximum already discounts).
    /// </summary>
    private static double ReservedOf(Stats stats, string resource) => stats.Get(ReservedNames.GetOrAdd(resource, static r => r + ".reserved"));

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
        var weave = casting.Weave ?? casting.Slot.Compiled;
        // A charge-up or an overcharge fills as it is held; a bar, a channel or a guard runs its length.
        var progress = casting.Mode is CastMode.Charge or CastMode.Overcharge ? Math.Clamp(casting.Fill, 0, 1) : CastProgress(sim, casting);
        return new CastInfo(slot, ModeName(casting.Mode), Math.Round(progress, 2), casting.Stage, casting.Stages)
        {
            Name = weave?.Name,
            Brim = casting.Brim > 0 ? Math.Round(casting.Brim, 2) : null,
            Held = casting.Held > 0 ? Math.Round(casting.Held, 2) : null,
            Duration = casting.Duration > 0 ? Math.Round(casting.Duration, 2) : null,
            Pool = casting.Mode == CastMode.Guard ? GuardPool(sim, e, casting) : null,
        };
    }

    /// <summary>The fraction of a raised guard's pool left, from the bearer's guard while it is the one this cast holds.</summary>
    private static double? GuardPool(Simulation sim, Entity e, CastState casting)
    {
        if (!sim.World.TryGet<Guard>(e, out var guard) || !ReferenceEquals(guard.Cast, casting))
        {
            return null;
        }
        return guard.MaxPool <= 0 ? 0 : Math.Round(Math.Clamp(guard.Pool / guard.MaxPool, 0, 1), 2);
    }

    public static string ModeName(CastMode mode) => mode switch
    {
        CastMode.Instant => "instant",
        CastMode.Channel => "channel",
        CastMode.Charge => "charge",
        CastMode.Overcharge => "overcharge",
        CastMode.Guard => "guard",
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
        sim.Service<RackService>().Reconcile(sim, e);
        var bag = inv is null ? new BoardInfo(1, 1, new List<ItemInfo>()) : BoardOf(sim, resolver, stats, inv.Bag, e);
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
        if (sim.World.TryGet<Containers>(e, out var boards) && sim.TryService<ContainerService>(out var service))
        {
            AddContainers(sim, e, resolver, stats, boards, service, containers);
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

    /// <summary>
    /// The hero's container boards: each container with its rungs, then every page of every stash (pages not yet
    /// bought arrive locked with their price), and the items a board holds without a cell as a board of their own.
    /// </summary>
    private static void AddContainers(Simulation sim, Entity e, ItemResolver resolver, Stats? stats, Containers boards, ContainerService service, List<ContainerInfo> into)
    {
        var scope = new HeroScope(sim, e);
        foreach (var def in sim.Content.Each<ContainerDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (!boards.Boards.TryGetValue(def.Id, out var board))
            {
                continue;
            }
            var reachable = service.Reachable(sim, e, def.Id, out var reason);
            var rungs = new List<RungInfo>(def.Board.Count);
            for (var i = 0; i < def.Board.Count; i++)
            {
                var rung = def.Board[i];
                rungs.Add(new RungInfo(i, ContainerService.Unlocked(rung, scope), rung.Cells) { Requirement = rung.Requirement.Length > 0 ? rung.Requirement : null });
            }
            into.Add(new ContainerInfo(def.Id, def.DisplayName, def.Kind, BoardOf(sim, resolver, stats, board) with { Rungs = rungs }, reachable)
            {
                Reason = reason,
                Owner = def.Owner,
                Active = def.Active ? true : null,
            });
            AddLoose(sim, resolver, stats, boards, def.Id, def.Owner, into);
        }
        foreach (var stash in sim.Content.Each<StashDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var reachable = service.Reachable(sim, e, ContainerService.PageId(stash.Id, 1), out var reason);
            var owned = ContainerService.Owned(boards, stash);
            for (var page = 1; page <= stash.Pages; page++)
            {
                var id = ContainerService.PageId(stash.Id, page);
                if (page <= owned && boards.Boards.TryGetValue(id, out var board))
                {
                    into.Add(new ContainerInfo(id, stash.DisplayName, "stash", BoardOf(sim, resolver, stats, board), reachable)
                    {
                        Reason = reason,
                        Owner = stash.Owner,
                        Of = stash.Id,
                        Page = page,
                    });
                }
                else
                {
                    into.Add(new ContainerInfo(id, stash.DisplayName, "stash", new BoardInfo(stash.Width, stash.Height, new List<ItemInfo>()), reachable)
                    {
                        Reason = reason,
                        Owner = stash.Owner,
                        Of = stash.Id,
                        Page = page,
                        Locked = true,
                        Price = service.PagePrice(sim, e, stash, page),
                    });
                }
                AddLoose(sim, resolver, stats, boards, id, stash.Owner, into);
            }
        }
    }

    /// <summary>Items a container board holds without a cell, as a column board of their own (<c>{board}:loose</c>) to take them out of.</summary>
    private static void AddLoose(Simulation sim, ItemResolver resolver, Stats? stats, Containers boards, string board, string owner, List<ContainerInfo> into)
    {
        if (!boards.Loose.TryGetValue(board, out var loose) || loose.Count == 0)
        {
            return;
        }
        var items = new List<ItemInfo>(loose.Count);
        var y = 0;
        var width = 1;
        foreach (var item in loose)
        {
            var (w, h) = resolver.Of(item);
            items.Add(Card(sim, resolver, stats, item) with { X = 0, Y = y, W = w, H = h });
            y += h;
            width = Math.Max(width, w);
        }
        into.Add(new ContainerInfo(board + ":loose", "Overflow", "overflow", new BoardInfo(width, Math.Max(1, y), items), true)
        {
            Reason = "Make room on the board for these.",
            Owner = owner,
            Of = board,
        });
    }

    /// <summary>A board as the wire draws it: its shape and mask, and every item with its cell and footprint.</summary>
    /// <param name="fitsFor">For the hero's own bag: loose stones list how they fit the hero's rack.</param>
    public static BoardInfo BoardOf(Simulation sim, ItemResolver resolver, Stats? stats, Board board, Entity? fitsFor = null)
    {
        var items = new List<ItemInfo>(board.Placed.Count);
        foreach (var p in board.Placed)
        {
            var card = Card(sim, resolver, stats, p.Item) with { X = p.X, Y = p.Y, W = p.Width, H = p.Height };
            items.Add(fitsFor is { } hero ? WithFits(sim, hero, card, p.Item) : card);
        }
        return new BoardInfo(board.Width, board.Height, items) { Mask = board.MaskRows() };
    }

    /// <summary>An item's full card: the self state's fields plus its footprint, class, requirements and payload.</summary>
    public static ItemInfo Card(Simulation sim, ItemResolver resolver, Stats? stats, ItemInstance item)
    {
        var b = resolver.BaseOf(item);
        List<RequirementInfo>? requirements = null;
        void Require(string attr, double required)
        {
            var have = stats is null ? 0 : attr == "level" ? stats.Level : stats.Get(attr);
            (requirements ??= new List<RequirementInfo>()).Add(new RequirementInfo(WeaveItems.RequirementText(sim.Content, attr, required), have >= required));
        }
        foreach (var (attr, required) in b.Requirements)
        {
            Require(attr, required);
        }
        foreach (var (attr, required) in WeaveItems.RequirementsOf(sim.Content, item))
        {
            Require(attr, required);
        }
        string? flavor = null;
        if (item.Unique is not null && sim.Content.TryGet<UniqueDef>(item.Unique, out var unique) && unique is not null && unique.Flavor.Length > 0)
        {
            flavor = unique.Flavor;
        }
        ItemGlyphInfo? glyph = null;
        ItemFormInfo? form = null;
        if (item.Payload is { Glyph: { } glyphId } stone)
        {
            var def = sim.Content.Get<GlyphDef>(glyphId);
            List<string>? rows = null;
            if (stone.Rows is { Count: > 0 } && def is not null)
            {
                foreach (var axis in def.Axes)
                {
                    if (stone.Rows.TryGetValue(axis.Id, out var rowId) && axis.Rows.Find(r => r.Id == rowId) is { } row)
                    {
                        (rows ??= new List<string>()).Add(row.Text.Length > 0 ? row.Text : Definition.Humanize(row.Id));
                    }
                }
            }
            glyph = new ItemGlyphInfo(glyphId, def?.DisplayName ?? Definition.Humanize(glyphId), Math.Max(1, stone.Level)) { Rows = rows };
        }
        else if (item.Payload is { } codex && WeaveItems.IsCodex(item))
        {
            form = new ItemFormInfo(codex.Weave ?? codex.Form!, WeaveItems.TeachingName(sim.Content, codex), Math.Max(1, codex.Level), WeaveItems.SocketsOf(sim.Content, item)) { Weave = codex.Weave };
        }
        List<SocketInfo>? sockets = null;
        if (item.Sockets is { } held)
        {
            sockets = new List<SocketInfo>(held.Count);
            for (var i = 0; i < held.Count; i++)
            {
                sockets.Add(new SocketInfo(i) { Stone = held[i] is { } s ? Card(sim, resolver, stats, s) : null });
            }
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
            Sockets = sockets,
            Requirements = requirements,
            Glyph = glyph,
            Form = form,
        };
    }

    /// <summary>A loose stone's card with how it fits every learned seat of the rack.</summary>
    private static ItemInfo WithFits(Simulation sim, Entity e, ItemInfo card, ItemInstance item)
    {
        if (!WeaveItems.IsStone(item) || !sim.World.TryGet<Rack>(e, out var rack))
        {
            return card;
        }
        var service = sim.Service<RackService>();
        List<StoneFitInfo>? fits = null;
        for (var slot = 0; slot < rack.Seats.Count; slot++)
        {
            if (rack.Seats[slot].Codex is null)
            {
                continue;
            }
            var (fit, reason) = service.FitOf(sim, e, item, slot);
            (fits ??= new List<StoneFitInfo>()).Add(new StoneFitInfo(slot, fit) { Reason = reason });
        }
        return card with { Fits = fits };
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
        if (sim.World.TryGet<Containers>(e, out var boards))
        {
            AddContainers(ref h, sim, e, boards);
        }
        return h.Value;
    }

    private static void AddContainers(ref FingerprintHash h, Simulation sim, Entity e, Containers boards)
    {
        foreach (var (id, board) in boards.Boards)
        {
            h.Add(id);
            h.Add(board.Width);
            h.Add(board.Height);
            h.Add(board.UsableCells);
            var placed = board.Placed;
            for (var i = 0; i < placed.Count; i++)
            {
                h.Add(placed[i].X);
                h.Add(placed[i].Y);
                Snapshots.AddItem(ref h, placed[i].Item);
            }
        }
        foreach (var (id, loose) in boards.Loose)
        {
            h.Add(id);
            foreach (var item in loose)
            {
                Snapshots.AddItem(ref h, item);
            }
        }
        foreach (var (stash, bought) in boards.Bought)
        {
            h.Add(stash);
            h.Add(bought);
        }
        // Walking up to a chest (or away) changes what the hero can reach.
        if (sim.TryService<ContainerService>(out _))
        {
            foreach (var def in sim.Content.Each<ContainerDef>())
            {
                if (def.At is not null)
                {
                    h.Add(ContainerService.InReach(sim, e, def.At));
                }
            }
            foreach (var stash in sim.Content.Each<StashDef>())
            {
                h.Add(ContainerService.InReach(sim, e, stash.At));
            }
        }
    }

    // ------------------------------------------------------------ weave slots

    public static WeaveSlotsMsg WeaveSlots(Simulation sim, Entity e)
    {
        var stats = sim.World.Require<Stats>(e);
        var book = sim.World.Get<WeaveBook>(e);
        var runtime = sim.Service<WeaveRuntime>();
        sim.Service<RackService>().Reconcile(sim, e);
        var rack = sim.World.Get<Rack>(e);
        var count = Math.Max(Math.Max(book?.Slots.Count ?? 0, rack?.Seats.Count ?? 0), RackService.MaxSeats(sim));
        var seats = new List<WeaveSeatInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var slot = book is not null && i < book.Slots.Count ? book.Slots[i] : null;
            var seat = rack is not null && i < rack.Seats.Count ? rack.Seats[i] : null;
            if (slot is null || (slot.Spec.Preset is null && slot.Spec.Form is null))
            {
                seats.Add(Empty(sim, i, seat));
                continue;
            }
            seats.Add(Seat(sim, e, stats, book!, runtime, i, slot, seat));
        }
        return new WeaveSlotsMsg(seats);
    }

    /// <summary>A slot with no weave: still drawn, with any codex or stones its seat holds.</summary>
    private static WeaveSeatInfo Empty(Simulation sim, int index, RackSeat? seat) =>
        new(index, null, null, "", 0, seat?.Codex is { } codex ? WeaveItems.SocketsOf(sim.Content, codex) : 0, StonesOf(sim, seat, null), 0, 0, new List<PreviewLineInfo>())
        {
            Item = seat?.Codex?.Uid,
        };

    private static WeaveSeatInfo Seat(Simulation sim, Entity e, Stats stats, WeaveBook book, WeaveRuntime runtime, int index, WeaveSlot slot, RackSeat? seat)
    {
        var key = slot.Key;
        var compiled = runtime.Resolve(sim, slot, out var error);
        var preview = new List<PreviewLineInfo>();
        double cooldown = 0;
        Dictionary<string, double>? costs = null;
        string? dormant = compiled is null ? error : null;
        if (compiled is not null)
        {
            cooldown = compiled.Cooldown(stats);
            if (cooldown > 0)
            {
                cooldown /= Math.Max(0.1, stats.Attributes.Get("cooldown.recovery", stats, compiled.Tags, compiled.Modifiers));
            }
            costs = runtime.Costs(compiled, stats);
            foreach (var line in WeavePreviews.Lines(sim, e, compiled))
            {
                preview.Add(new PreviewLineInfo(line.Label, line.Text));
            }
        }
        if (dormant is null && seat?.Codex is { } codex && WeaveItems.Unmet(WeaveItems.RequirementsOf(sim.Content, codex), stats) is { } unmet)
        {
            dormant = "Requires " + WeaveItems.RequirementText(sim.Content, unmet.Attribute, unmet.Value);
        }
        var readyIn = book.ReadyAt.TryGetValue(key, out var at) ? Math.Max(0, at - sim.Clock.Time) : 0;
        var icon = slot.Spec.Preset is { } preset ? sim.Content.Get<WeaveDef>(preset)?.Icon : null;
        List<SeatGlyphInfo> glyphs;
        int sockets;
        if (seat?.Codex is { } learned)
        {
            glyphs = StonesOf(sim, seat, slot);
            sockets = WeaveItems.SocketsOf(sim.Content, learned);
        }
        else
        {
            // A weave bound without the rack (a monster's, a tool's, free composition): its glyphs in socket order.
            glyphs = new List<SeatGlyphInfo>();
            for (var socket = 0; socket < slot.Spec.Glyphs.Count; socket++)
            {
                var glyphRef = slot.Spec.Glyphs[socket];
                var def = sim.Content.Get<GlyphDef>(glyphRef.Id);
                glyphs.Add(new SeatGlyphInfo(socket, glyphRef.Id, def?.DisplayName ?? glyphRef.Id, glyphRef.Level ?? def?.Level ?? 1) { Dormant = DormantReason(slot, glyphRef.Id), Riding = IsRiding(slot, glyphRef.Id) ? true : null });
            }
            sockets = glyphs.Count;
        }
        var usesMax = 0;
        var uses = compiled?.Casting.Uses is null ? -1 : runtime.Uses(sim, e, slot, out usesMax);
        var stacks = compiled is null ? 0 : runtime.Stacks(sim, slot);
        var (treeSpent, treePoints) = compiled is null ? (0, 0) : runtime.TreePoints(sim, slot);
        return new WeaveSeatInfo(index, key, compiled?.Form.Id ?? slot.Spec.Form, compiled?.Name ?? key, compiled?.Level ?? slot.Spec.Level,
            sockets, glyphs, Math.Round(readyIn, 2), Math.Round(cooldown, 2), preview)
        {
            Icon = icon ?? compiled?.Look,
            Preset = slot.Spec.Preset,
            Dormant = dormant,
            Item = seat?.Codex?.Uid,
            Threads = compiled is null ? null : compiled.Threads.Select(t => t.Id).ToList(),
            CastMode = compiled is null ? null : ModeName(compiled.CastMode),
            Targeting = compiled?.Targeting.ToString().ToLowerInvariant(),
            Costs = costs,
            Uses = uses >= 0 ? uses : null,
            UsesMax = uses >= 0 ? usesMax : null,
            Stacks = stacks > 0 ? stacks : null,
            Converted = compiled is null ? null : ConvertedFace(sim, e, runtime, slot, compiled),
            Meta = compiled is null ? null : MetaFace(sim, runtime, slot),
            Active = IsActive(slot) ? true : null,
            Tree = slot.Spec.Tree.Count > 0 ? new List<string>(slot.Spec.Tree) : null,
            DormantNodes = DormantNodesOf(compiled),
            TreeSpent = compiled?.Form.Tree is null ? null : treeSpent,
            TreePoints = compiled?.Form.Tree is null ? null : treePoints,
        };
    }

    /// <summary>Whether a socketed glyph rides to the weave's crew (<see cref="CompiledWeave.Riding"/>).</summary>
    private static bool IsRiding(WeaveSlot? slot, string glyph)
    {
        if (slot?.Compiled is not { Riding.Count: > 0 } compiled)
        {
            return false;
        }
        foreach (var rider in compiled.Riding)
        {
            if (rider.Id == glyph)
            {
                return true;
            }
        }
        return false;
    }

    private static List<DormantNodeInfo>? DormantNodesOf(CompiledWeave? compiled)
    {
        if (compiled is not { DormantNodes.Count: > 0 })
        {
            return null;
        }
        var list = new List<DormantNodeInfo>(compiled.DormantNodes.Count);
        foreach (var node in compiled.DormantNodes)
        {
            list.Add(new DormantNodeInfo(node.Id, node.Reason));
        }
        return list;
    }

    /// <summary>The face the slot shows instead of its own weave while a convert or a combo step holds, or null.</summary>
    private static FaceInfo? ConvertedFace(Simulation sim, Entity e, WeaveRuntime runtime, WeaveSlot slot, CompiledWeave compiled)
    {
        if (compiled.Casting.Convert is null && compiled.Casting.Chain is null)
        {
            return null;
        }
        return runtime.FaceOf(sim, e, slot) is { } face && !ReferenceEquals(face, compiled) ? new FaceInfo(face.Name) { Icon = face.Look, Weave = face.Spec.Preset, Form = face.Form.Id } : null;
    }

    /// <summary>The slot's second ability: the first meta face, labelled by content or by the weave it casts.</summary>
    private static FaceInfo? MetaFace(Simulation sim, WeaveRuntime runtime, WeaveSlot slot)
    {
        var metas = runtime.MetasOf(slot);
        if (metas.Count == 0)
        {
            return null;
        }
        var meta = metas[0];
        var def = sim.Content.Get<WeaveDef>(meta.Weave);
        return new FaceInfo(meta.Label.Length > 0 ? meta.Label : def?.DisplayName ?? Definition.Humanize(meta.Weave)) { Icon = def?.Icon };
    }

    /// <summary>A slot whose toggle or contract runs (a sustained summon, an aura).</summary>
    private static bool IsActive(WeaveSlot slot) =>
        slot.HasState && (WeaveRuntime.IsToggledOn(slot) || slot.StateOr(Core.Minions.MinionContract.SlotState, 0) > 0);

    /// <summary>The stones a seat holds, each at its socket, with why it is dormant when it is.</summary>
    private static List<SeatGlyphInfo> StonesOf(Simulation sim, RackSeat? seat, WeaveSlot? slot)
    {
        var glyphs = new List<SeatGlyphInfo>();
        if (seat is null)
        {
            return glyphs;
        }
        for (var socket = 0; socket < seat.Stones.Count; socket++)
        {
            if (seat.Stones[socket]?.Payload is not { Glyph: { } id } payload)
            {
                continue;
            }
            var def = sim.Content.Get<GlyphDef>(id);
            glyphs.Add(new SeatGlyphInfo(socket, id, def?.DisplayName ?? id, Math.Max(1, payload.Level))
            {
                Dormant = slot is null ? null : DormantReason(slot, id),
                Item = seat.Stones[socket]!.Uid,
                Riding = IsRiding(slot, id) ? true : null,
            });
        }
        return glyphs;
    }

    private static string? DormantReason(WeaveSlot slot, string glyph)
    {
        foreach (var d in slot.Dormant)
        {
            if (d.Ref.Id == glyph)
            {
                return d.Reason;
            }
        }
        return null;
    }

    public static ulong WeaveSlotsFingerprint(Simulation sim, Entity e, int previewVersion)
    {
        // What the slots hold and how they compiled. Cooldowns are left out on purpose: casting starts one every few
        // seconds in a fight, the self state already counts each down, and this message carries the full duration.
        var h = new FingerprintHash(4);
        h.Add(previewVersion);
        if (sim.World.TryGet<WeaveBook>(e, out var book))
        {
            var runtime = sim.Service<WeaveRuntime>();
            foreach (var slot in book.Slots)
            {
                h.Add(slot.Key);
                h.Add((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slot));
                h.Add(slot.Dormant.Count);
                h.Add(IsActive(slot));
                h.Add(slot.Spec.Tree.Count);
                // Whole use charges, self stacks and the face a convert or a combo step puts on the slot.
                if (slot.Compiled is { } compiled)
                {
                    if (compiled.Casting.Uses is not null)
                    {
                        h.Add(runtime.Uses(sim, e, slot, out var max));
                        h.Add(max);
                    }
                    h.Add(runtime.Stacks(sim, slot));
                    h.Add(compiled.TreeSpent);
                    h.Add(compiled.TreePoints);
                    h.Add(compiled.DormantNodes.Count);
                    h.Add(compiled.Riding.Count);
                    if (compiled.Casting.Convert is not null || compiled.Casting.Chain is not null)
                    {
                        h.Add((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(runtime.FaceOf(sim, e, slot)));
                    }
                }
            }
        }
        if (sim.World.TryGet<Rack>(e, out var rack))
        {
            foreach (var seat in rack.Seats)
            {
                h.Add(seat.Codex?.Uid ?? 0);
                h.Add(seat.Codex?.Payload?.Level ?? 0);
                foreach (var stone in seat.Stones)
                {
                    h.Add(stone?.Uid ?? 0);
                    h.Add(stone?.Payload?.Level ?? 0);
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
        var state = sim.World.Get<PassiveState>(e);
        var points = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var currency in progression.Currencies(sim))
        {
            points[currency] = progression.Points(sim, e, currency);
        }
        var picks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (state is not null)
        {
            foreach (var (node, options) in state.Picks)
            {
                picks[node] = new List<string>(options);
            }
        }
        var realms = new List<string> { ProgressionService.MainRealm };
        foreach (var realm in sim.Content.Each<PassiveRealmDef>().OrderBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            if (progression.RealmOpen(sim, e, realm.Id))
            {
                realms.Add(realm.Id);
            }
        }
        List<GraftInfo>? grafts = null;
        foreach (var owned in PassiveGrafts.Of(sim, e))
        {
            var glyph = sim.Content.Get<GlyphDef>(owned.Glyph.Id);
            (grafts ??= new List<GraftInfo>()).Add(new GraftInfo(owned.Source, owned.Glyph.Id, glyph?.DisplayName ?? Definition.Humanize(owned.Glyph.Id), Math.Max(1, owned.Glyph.Level))
            {
                Slot = state is not null && state.Grafts.TryGetValue(owned.Source, out var slot) ? slot : null,
            });
        }
        var font = Core.Interaction.Fonts.InReach(sim, e);
        return new PassiveStateMsg(new List<string>(stats.Passives), picks, points, realms, font is not null)
        {
            Font = font?.Key,
            RefundCost = font is null ? null : progression.RefundCostText(sim, e, new PassiveNodeDef { Id = "", Cost = 1 }),
            Grafts = grafts,
        };
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
        if (sim.World.TryGet<PassiveState>(e, out var state))
        {
            h.Add(state.Refunds);
            foreach (var (node, picks) in state.Picks)
            {
                h.Add(node);
                h.Add(picks.Count);
            }
            foreach (var (currency, points) in state.Granted)
            {
                h.Add(currency);
                h.Add(points);
            }
            h.Add(state.OpenRealms.Count);
            foreach (var (source, slot) in state.Grafts)
            {
                h.Add(source);
                h.Add(slot);
            }
        }
        // Standing at a font turns refunds on.
        h.Add(Core.Interaction.Fonts.InReach(sim, e)?.Key ?? -1);
        return h.Value;
    }

    // ------------------------------------------------------------ world panes

    /// <summary>The hero's current line, or an ended state for the speaker when the conversation is over.</summary>
    public static DialogueStateMsg Dialogue(Simulation sim, Entity hero, Entity speaker, string conversation)
    {
        if (sim.Service<Core.Dialogue.DialogueService>().View(sim, hero) is { } view && view.Source == speaker && view.Conversation == conversation)
        {
            var choices = new List<DialogueChoiceInfo>(view.Choices.Count);
            foreach (var c in view.Choices)
            {
                choices.Add(new DialogueChoiceInfo(c.Id, c.Text, c.Enabled) { Reason = c.Reason });
            }
            return new DialogueStateMsg(view.Source.Key, view.Conversation, view.Speaker, view.Line, choices, false) { Node = view.Node, Reach = Math.Round(view.Reach, 2) };
        }
        return Ended(sim, speaker, conversation);
    }

    public static DialogueStateMsg Ended(Simulation sim, Entity speaker, string conversation) =>
        new(speaker.Key, conversation, sim.World.Get<Identity>(speaker)?.Name ?? "", "", new List<DialogueChoiceInfo>(), true);

    /// <summary>The hero's open vendor pane: stock with prices and readiness, buy-back, and what the vendor pays per bag item.</summary>
    public static VendorStateMsg Vendor(Simulation sim, Entity hero)
    {
        var panes = sim.World.Get<Panes>(hero);
        if (panes?.Vendor is not { } pane || sim.Content.Get<VendorDef>(pane.Vendor) is not { } def)
        {
            return new VendorStateMsg(0, "", "", new List<VendorStockInfo>(), new List<VendorBuyBackInfo>(), new List<VendorQuoteInfo>(), true);
        }
        var resolver = sim.Service<ItemResolver>();
        var stats = sim.World.Get<Stats>(hero);
        var vendors = sim.Service<VendorService>();
        var stock = new List<VendorStockInfo>();
        foreach (var offer in vendors.Offers(sim, hero))
        {
            stock.Add(new VendorStockInfo(offer.Offer.Id, Card(sim, resolver, stats, offer.Offer.Item), new Dictionary<string, double>(offer.Offer.Price, StringComparer.Ordinal))
            {
                Count = offer.Remaining,
                Enabled = offer.Enabled ? null : false,
                Reason = offer.Reason,
            });
        }
        var buyBack = new List<VendorBuyBackInfo>();
        if (panes.BuyBack.TryGetValue(pane.Source.Key, out var sold))
        {
            for (var i = sold.Count - 1; i >= 0; i--)
            {
                buyBack.Add(new VendorBuyBackInfo(Card(sim, resolver, stats, sold[i].Item), VendorService.BuyBackPrice(sim, sold[i])));
            }
        }
        var quotes = new List<VendorQuoteInfo>();
        if (sim.World.TryGet<Inventory>(hero, out var inv))
        {
            foreach (var item in inv.Items)
            {
                if (vendors.SellPrice(sim, hero, def, item) is { } price)
                {
                    quotes.Add(new VendorQuoteInfo(item.Uid, price));
                }
            }
        }
        return new VendorStateMsg(pane.Source.Key, def.Id, def.DisplayName, stock, buyBack, quotes, false) { Reach = Math.Round(pane.Reach, 2) };
    }

    public static VendorStateMsg VendorClosed(Simulation sim, Entity vendor) =>
        new(vendor.Key, "", sim.World.Get<Identity>(vendor)?.Name ?? "", new List<VendorStockInfo>(), new List<VendorBuyBackInfo>(), new List<VendorQuoteInfo>(), true);

    /// <summary>The hero's open station pane: the recipes it offers with inputs, output cards and readiness.</summary>
    public static StationStateMsg Station(Simulation sim, Entity hero, Entity station, Core.Tags.TagSet tags)
    {
        var resolver = sim.Service<ItemResolver>();
        var stats = sim.World.Get<Stats>(hero);
        var recipes = new List<RecipeInfo>();
        foreach (var r in sim.Service<Core.Crafting.StationService>().Recipes(sim, hero, station, tags))
        {
            var inputs = new List<RecipeInputInfo>(r.Inputs.Count);
            foreach (var input in r.Inputs)
            {
                inputs.Add(new RecipeInputInfo(input.Text, input.Have, input.Need));
            }
            recipes.Add(new RecipeInfo(r.Recipe.Id, r.Recipe.DisplayName, inputs, Card(sim, resolver, stats, r.Output), r.Craftable)
            {
                Reason = r.Reason,
                Cost = r.Recipe.Currency.Count == 0 ? null : new Dictionary<string, double>(r.Recipe.Currency, StringComparer.Ordinal),
            });
        }
        var reach = sim.World.Get<Panes>(hero)?.Station is { } pane && pane.Source == station ? Math.Round(pane.Reach, 2) : (double?)null;
        List<StationServiceInfo>? services = null;
        foreach (var quote in StationServices.For(sim, hero, station))
        {
            (services ??= new List<StationServiceInfo>()).Add(new StationServiceInfo(quote.Def.Id, quote.Def.DisplayName)
            {
                Item = quote.Target.Item,
                Slot = quote.Target.Slot,
                Node = quote.Target.Node,
                Price = quote.Price.Count == 0 ? null : quote.Price,
                Enabled = quote.Enabled,
                Reason = quote.Reason,
            });
        }
        return new StationStateMsg(station.Key, sim.World.Get<Identity>(station)?.Name ?? "", recipes, false) { Tags = tags.IsEmpty ? null : tags.ToList(), Reach = reach, Services = services };
    }

    public static StationStateMsg StationClosed(Simulation sim, Entity station) =>
        new(station.Key, sim.World.Get<Identity>(station)?.Name ?? "", new List<RecipeInfo>(), true);

    // ------------------------------------------------------------ pages

    /// <summary>The pages the hero has, from the ui_pages content evaluated over the hero.</summary>
    public static PageStateMsg Pages(Simulation sim, Entity e)
    {
        var pages = PageGates.For(sim, e);
        var list = new List<PageInfo>(pages.Count);
        foreach (var p in pages)
        {
            list.Add(new PageInfo(p.Id, p.Sealed, p.Pips) { Reason = p.Reason, Container = p.Container });
        }
        return new PageStateMsg(list);
    }

    /// <summary>The pages as they stand: which exist, which are sealed, their pips. Evaluates the gates; no state is kept.</summary>
    public static ulong PageFingerprint(Simulation sim, Entity e)
    {
        var h = new FingerprintHash(6);
        foreach (var p in PageGates.For(sim, e))
        {
            h.Add(p.Id);
            h.Add(p.Sealed);
            h.Add(p.Pips);
            h.Add(p.Container);
        }
        return h.Value;
    }
}
