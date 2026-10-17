using System.Globalization;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Items;

/// <summary>
/// The weave rack (question Q1 of the parity plan): heroes learn codices into seats and socket glyph stones
/// beside them, and every seat builds its weave slot from what it holds. A better codex swaps in place; a stone
/// that does not fit stays socketed but dormant, with its reason; every item leaves the rack back into the bag
/// and nothing is ever destroyed. The rack is also a place the inventory service can move items out of.
/// </summary>
public sealed class RackService : InventoryService.IItemPlace
{
    public const string RackId = "rack";

    /// <summary>The number of seats: rule <c>weaves.max_slots</c>.</summary>
    public static int MaxSeats(Simulation sim) => Math.Max(1, (int)sim.Content.Rules.Number("weaves.max_slots", 8));

    // ------------------------------------------------------------ following the weave book

    /// <summary>
    /// Keeps seats beside the weave slots they built: a slot that moved (the rack was reordered) carries its
    /// seat along, and a slot replaced by a validated set-weave is adopted by the seat whose codex it teaches,
    /// taking its Threads. Cheap; called before every rack read and change.
    /// </summary>
    public void Reconcile(Simulation sim, Entity hero)
    {
        if (!sim.World.TryGet<Rack>(hero, out var rack) || !sim.World.TryGet<WeaveBook>(hero, out var book) || rack.Seats.Count == 0)
        {
            return;
        }
        var count = Math.Max(rack.Seats.Count, book.Slots.Count);
        var placed = new RackSeat?[count];
        var loose = new List<(int Index, RackSeat Seat)>();
        for (var i = 0; i < rack.Seats.Count; i++)
        {
            var seat = rack.Seats[i];
            var at = seat.Bound is null ? -1 : book.Slots.IndexOf(seat.Bound);
            if (at >= 0 && placed[at] is null)
            {
                placed[at] = seat;
            }
            else
            {
                loose.Add((i, seat));
            }
        }
        foreach (var (index, seat) in loose)
        {
            var at = placed[index] is null ? index : Array.IndexOf(placed, null);
            if (at < 0)
            {
                continue;
            }
            placed[at] = seat;
            // A slot rebuilt elsewhere (a set-weave) that still teaches this seat's codex belongs to it again.
            if (at < book.Slots.Count && seat.Codex?.Payload is { } payload && Teaches(sim, payload, book.Slots[at].Spec))
            {
                seat.Bound = book.Slots[at];
                seat.Threads.Clear();
                seat.Threads.AddRange(book.Slots[at].Spec.Threads);
            }
        }
        rack.Seats.Clear();
        foreach (var seat in placed)
        {
            rack.Seats.Add(seat ?? new RackSeat());
        }
        while (rack.Seats.Count > 0 && rack.Seats[^1].IsEmpty && rack.Seats.Count > book.Slots.Count)
        {
            rack.Seats.RemoveAt(rack.Seats.Count - 1);
        }
    }

    /// <summary>Whether a spec is the weave a codex teaches (the stones and Threads aside).</summary>
    public static bool Teaches(Simulation sim, ItemPayload payload, WeaveSpec spec)
    {
        if (payload.Weave is not null)
        {
            return spec.Preset == payload.Weave && (spec.Form is null || spec.Form == sim.Content.Get<WeaveDef>(payload.Weave)?.Form);
        }
        return spec.Preset is null && spec.Form is not null && spec.Form == payload.Form;
    }

    /// <summary>
    /// Builds weave slot <paramref name="slot"/> from its seat: the codex's weave with the seat's stones in socket
    /// order. The weave runtime compiles it with misfits dormant, so a stone that does not fit never stops the
    /// weave, and the slot carries the dormant stones' reasons. An empty seat leaves an empty slot.
    /// </summary>
    public void Rebuild(Simulation sim, Entity hero, int slot)
    {
        var rack = sim.World.GetOrAdd<Rack>(hero);
        var book = sim.World.GetOrAdd<WeaveBook>(hero);
        var seat = rack.SeatAt(slot);
        while (book.Slots.Count <= slot)
        {
            book.Slots.Add(new WeaveSlot { Spec = new WeaveSpec() });
        }
        var spec = WeaveItems.SpecOf(seat) ?? new WeaveSpec();
        // The weave's tree stays with the seat while it still teaches the same weave (a stone socketed, a better copy
        // learned, the hero entering the world); a different weave starts with none.
        if (book.Slots[slot].Spec is { Tree.Count: > 0 } previous && seat.Codex?.Payload is { } payload && Teaches(sim, payload, previous))
        {
            spec = spec with { Tree = new List<string>(previous.Tree) };
        }
        var weaveSlot = new WeaveSlot { Spec = spec };
        book.Slots[slot] = weaveSlot;
        seat.Bound = weaveSlot;
        if (spec.Preset is not null || spec.Form is not null)
        {
            sim.Service<WeaveRuntime>().Resolve(sim, weaveSlot, out _);
        }
    }

    // ------------------------------------------------------------ learning and socketing

    /// <summary>
    /// Learns a codex from the bag into a seat. A codex already there swaps back to the bag (a better copy swaps
    /// in place); stones stay socketed while the new codex has sockets for them and the rest return to the bag.
    /// Refused, with nothing changed, when the bag has no room for what comes out.
    /// </summary>
    public bool Learn(Simulation sim, Entity hero, long codexUid, int slot, out string reason)
    {
        Reconcile(sim, hero);
        if (slot < 0 || slot >= MaxSeats(sim))
        {
            reason = "bad_slot";
            return false;
        }
        if (!FromBag(sim, hero, codexUid, out var inv, out var codex))
        {
            reason = "no_item";
            return false;
        }
        if (!WeaveItems.IsCodex(codex) || WeaveItems.FormOf(sim.Content, codex.Payload!) is not { } form)
        {
            reason = "not_a_codex";
            return false;
        }
        if (!Gates(sim, hero, codex, out reason))
        {
            return false;
        }
        var seat = sim.World.GetOrAdd<Rack>(hero).SeatAt(slot);
        var sockets = WeaveItems.SocketsOf(sim.Content, codex);
        int? cx = codex.X, cy = codex.Y;
        var fromOverflow = inv.Overflow.Contains(codex);
        inv.Remove(codex);
        var outgoing = new List<ItemInstance>();
        if (seat.Codex is { } old)
        {
            outgoing.Add(old);
        }
        for (var i = sockets; i < seat.Stones.Count; i++)
        {
            if (seat.Stones[i] is { } extra)
            {
                outgoing.Add(extra);
            }
        }
        if (!Stow(inv, outgoing, cx, cy))
        {
            Restore(inv, codex, cx, cy, fromOverflow);
            reason = "bag_full";
            return false;
        }
        var sameTeaching = seat.Codex?.Payload is { } previous && previous.Form == codex.Payload!.Form && previous.Weave == codex.Payload.Weave;
        seat.Codex = codex;
        if (seat.Stones.Count > sockets)
        {
            seat.Stones.RemoveRange(sockets, seat.Stones.Count - sockets);
        }
        if (!sameTeaching)
        {
            seat.Threads.Clear();
            if (codex.Payload!.Form is not null)
            {
                seat.Threads.AddRange(DefaultThreads(sim, hero, form));
            }
        }
        Rebuild(sim, hero, slot);
        sim.Events.Publish(new FormLearned(hero, slot, codex.Uid) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>Takes a seat's codex and stones back into the bag, the codex at a cell when one is given.</summary>
    public bool Unlearn(Simulation sim, Entity hero, int slot, int? x, int? y, out string reason)
    {
        Reconcile(sim, hero);
        if (!sim.World.TryGet<Rack>(hero, out var rack) || slot < 0 || slot >= rack.Seats.Count || rack.Seats[slot].Codex is null)
        {
            reason = "not_learned";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(hero, out var inv))
        {
            reason = "no_bag";
            return false;
        }
        if (!LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        var seat = rack.Seats[slot];
        var outgoing = new List<ItemInstance> { seat.Codex! };
        foreach (var stone in seat.Stones)
        {
            if (stone is not null)
            {
                outgoing.Add(stone);
            }
        }
        if (!Stow(inv, outgoing, x, y))
        {
            reason = "bag_full";
            return false;
        }
        var codexUid = seat.Codex!.Uid;
        seat.Codex = null;
        seat.Stones.Clear();
        seat.Threads.Clear();
        Rebuild(sim, hero, slot);
        sim.Events.Publish(new FormUnlearned(hero, slot, codexUid) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>
    /// Sockets a stone from the bag into a seat's socket. A stone already there goes back to the bag; a stone
    /// that does not fit the weave stays socketed but dormant, with its reason.
    /// </summary>
    public bool Socket(Simulation sim, Entity hero, long stoneUid, int slot, int socket, out string reason)
    {
        Reconcile(sim, hero);
        if (!sim.World.TryGet<Rack>(hero, out var rack) || slot < 0 || slot >= rack.Seats.Count || rack.Seats[slot].Codex is not { } codex)
        {
            reason = "not_learned";
            return false;
        }
        var sockets = WeaveItems.SocketsOf(sim.Content, codex);
        if (sockets == 0)
        {
            reason = "no_sockets";
            return false;
        }
        if (socket < 0 || socket >= sockets)
        {
            reason = "no_socket";
            return false;
        }
        if (!FromBag(sim, hero, stoneUid, out var inv, out var stone))
        {
            reason = "no_item";
            return false;
        }
        if (!WeaveItems.IsStone(stone) || !sim.Content.Has<GlyphDef>(stone.Payload!.Glyph!))
        {
            reason = "not_a_stone";
            return false;
        }
        if (!Gates(sim, hero, stone, out reason))
        {
            return false;
        }
        var seat = rack.Seats[slot];
        while (seat.Stones.Count < sockets)
        {
            seat.Stones.Add(null);
        }
        int? sx = stone.X, sy = stone.Y;
        var fromOverflow = inv.Overflow.Contains(stone);
        inv.Remove(stone);
        if (seat.Stones[socket] is { } previous && !Stow(inv, new List<ItemInstance> { previous }, sx, sy))
        {
            Restore(inv, stone, sx, sy, fromOverflow);
            reason = "bag_full";
            return false;
        }
        seat.Stones[socket] = stone;
        Rebuild(sim, hero, slot);
        sim.Events.Publish(new StoneSocketed(hero, slot, socket, stone.Uid) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>Takes the stone out of a seat's socket into the bag, at a cell when one is given.</summary>
    public bool Unsocket(Simulation sim, Entity hero, int slot, int socket, int? x, int? y, out string reason)
    {
        Reconcile(sim, hero);
        if (!sim.World.TryGet<Rack>(hero, out var rack) || slot < 0 || slot >= rack.Seats.Count)
        {
            reason = "not_learned";
            return false;
        }
        var seat = rack.Seats[slot];
        if (socket < 0 || socket >= seat.Stones.Count || seat.Stones[socket] is not { } stone)
        {
            reason = "empty_socket";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(hero, out var inv))
        {
            reason = "no_bag";
            return false;
        }
        if (!LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        if (!Stow(inv, new List<ItemInstance> { stone }, x, y))
        {
            reason = "bag_full";
            return false;
        }
        seat.Stones[socket] = null;
        Rebuild(sim, hero, slot);
        sim.Events.Publish(new StoneUnsocketed(hero, slot, socket, stone.Uid) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    // ------------------------------------------------------------ fit

    /// <summary>How a loose stone fits one seat: <c>fits</c>, <c>misfit</c> with the reason, <c>full</c> or <c>no_sockets</c>.</summary>
    public (string Fit, string? Reason) FitOf(Simulation sim, Entity hero, ItemInstance stone, int slot)
    {
        var rack = sim.World.Get<Rack>(hero);
        if (rack is null || slot >= rack.Seats.Count || rack.Seats[slot].Codex is not { } codex || WeaveItems.SpecOf(rack.Seats[slot]) is not { } spec)
        {
            return ("no_sockets", null);
        }
        var sockets = WeaveItems.SocketsOf(sim.Content, codex);
        if (sockets == 0)
        {
            return ("no_sockets", null);
        }
        var used = 0;
        foreach (var s in rack.Seats[slot].Stones)
        {
            if (s is not null)
            {
                used++;
            }
        }
        if (used >= sockets)
        {
            return ("full", null);
        }
        if (!WeaveItems.IsStone(stone))
        {
            return ("misfit", "not a glyph stone");
        }
        var glyphs = new List<GlyphRef>(spec.Glyphs) { WeaveItems.RefOf(stone) };
        if (!sim.Service<WeaveCompiler>().TryCompile(spec with { Glyphs = glyphs }, out var compiled, out var error, GlyphFit.Dormant) || compiled is null)
        {
            return ("misfit", error);
        }
        foreach (var d in compiled.Dormant)
        {
            if (d.Ref.Id == stone.Payload!.Glyph)
            {
                return ("misfit", d.Reason);
            }
        }
        return ("fits", null);
    }

    // ------------------------------------------------------------ the rack as a place items move out of

    public bool Handles(string container) => container == RackId;

    public bool Find(Simulation sim, Entity hero, long uid, out ItemInstance? item, out InventoryService.Location location)
    {
        item = null;
        location = default;
        if (!sim.World.TryGet<Rack>(hero, out var rack))
        {
            return false;
        }
        for (var i = 0; i < rack.Seats.Count; i++)
        {
            var seat = rack.Seats[i];
            if (seat.Codex?.Uid == uid)
            {
                item = seat.Codex;
                location = new InventoryService.Location(RackId, i.ToString(CultureInfo.InvariantCulture) + ":codex");
                return true;
            }
            for (var s = 0; s < seat.Stones.Count; s++)
            {
                if (seat.Stones[s]?.Uid == uid)
                {
                    item = seat.Stones[s];
                    location = new InventoryService.Location(RackId, i.ToString(CultureInfo.InvariantCulture) + ":" + s.ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }
        }
        return false;
    }

    public bool Take(Simulation sim, Entity hero, ItemInstance item, InventoryService.Location from, out string reason)
    {
        if (!LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        if (!TryParse(from, out var slot, out var socket) || !sim.World.TryGet<Rack>(hero, out var rack) || slot >= rack.Seats.Count)
        {
            reason = "no_item";
            return false;
        }
        var seat = rack.Seats[slot];
        if (socket < 0)
        {
            // The codex leaves with its stones only through unlearn, which has room for both; here it leaves alone
            // when it holds none.
            foreach (var stone in seat.Stones)
            {
                if (stone is not null)
                {
                    reason = "unlearn_first";
                    return false;
                }
            }
            seat.Codex = null;
            seat.Threads.Clear();
        }
        else
        {
            seat.Stones[socket] = null;
        }
        Rebuild(sim, hero, slot);
        reason = "";
        return true;
    }

    public bool Put(Simulation sim, Entity hero, ItemInstance item, string container, int x, int y, out string reason)
    {
        // The rack takes items through learning and socketing, which say which seat and socket.
        reason = "use_learn_or_socket";
        return false;
    }

    public void Return(Simulation sim, Entity hero, ItemInstance item, InventoryService.Location from)
    {
        if (!TryParse(from, out var slot, out var socket))
        {
            return;
        }
        var seat = sim.World.GetOrAdd<Rack>(hero).SeatAt(slot);
        if (socket < 0)
        {
            seat.Codex = item;
        }
        else
        {
            while (seat.Stones.Count <= socket)
            {
                seat.Stones.Add(null);
            }
            seat.Stones[socket] = item;
        }
        Rebuild(sim, hero, slot);
    }

    private static bool TryParse(InventoryService.Location location, out int slot, out int socket)
    {
        slot = -1;
        socket = -1;
        if (location.Slot is not { } text)
        {
            return false;
        }
        var colon = text.IndexOf(':');
        if (colon < 0 || !int.TryParse(text.AsSpan(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
        {
            return false;
        }
        var rest = text[(colon + 1)..];
        return rest == "codex" || int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out socket);
    }

    // ------------------------------------------------------------ helpers

    /// <summary>A codex or stone in the hero's bag (on the board or without a cell).</summary>
    private static bool FromBag(Simulation sim, Entity hero, long uid, out Inventory inv, out ItemInstance item)
    {
        item = null!;
        if (!sim.World.TryGet<Inventory>(hero, out inv!))
        {
            return false;
        }
        var found = inv.Find(uid);
        if (found is null)
        {
            return false;
        }
        item = found;
        return true;
    }

    /// <summary>Requirements of what the item holds, then the calm a loadout change needs.</summary>
    private static bool Gates(Simulation sim, Entity hero, ItemInstance item, out string reason)
    {
        if (sim.World.TryGet<Stats>(hero, out var stats) && WeaveItems.Unmet(WeaveItems.RequirementsOf(sim.Content, item), stats) is { } unmet)
        {
            reason = "requires:" + unmet.Attribute;
            return false;
        }
        return LoadoutGate.Allows(sim, hero, out reason);
    }

    /// <summary>
    /// Puts items into the bag, the first at a cell when one is given and it fits there, the rest in the first
    /// space that fits; all or nothing.
    /// </summary>
    internal static bool Stow(Inventory inv, List<ItemInstance> items, int? x, int? y)
    {
        var stowed = new List<ItemInstance>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var placed = i == 0 && x is { } px && y is { } py && inv.TryAddAt(item, px, py);
            if (!placed)
            {
                var (w, h) = inv.FootprintOf(item);
                placed = inv.Bag.FindSpace(w, h, out var fx, out var fy) && inv.Bag.Place(item, fx, fy, w, h);
            }
            if (!placed)
            {
                foreach (var s in stowed)
                {
                    inv.Remove(s);
                }
                return false;
            }
            stowed.Add(item);
        }
        return true;
    }

    internal static void Restore(Inventory inv, ItemInstance item, int? x, int? y, bool overflow)
    {
        if (!overflow && x is { } px && y is { } py && inv.TryAddAt(item, px, py))
        {
            return;
        }
        var (w, h) = inv.FootprintOf(item);
        if (!(inv.Bag.FindSpace(w, h, out var fx, out var fy) && inv.Bag.Place(item, fx, fy, w, h)))
        {
            inv.Overflow.Add(item);
        }
    }

    /// <summary>The Threads a freshly learned form starts with: the hero's attuned Threads, as many as the form needs.</summary>
    private static List<string> DefaultThreads(Simulation sim, Entity hero, FormDef form)
    {
        var attuned = WeaveOwnership.Attuned(sim, hero);
        var take = Math.Min(attuned.Count, Math.Max(form.MinThreads, Math.Min(1, form.MaxThreads)));
        return attuned.GetRange(0, take);
    }
}

/// <summary>
/// Whether the hero may change their weave loadout now (question Q11): rule <c>casting.swap_requires</c> is an
/// expression over the hero (for example no hits taken or dealt of late), waived where the zone is safe.
/// Without the rule, loadouts change freely.
/// </summary>
public static class LoadoutGate
{
    public static bool Allows(Simulation sim, Entity hero, out string reason)
    {
        reason = "";
        if (!sim.Content.Rules.Has("casting.swap_requires") || !sim.World.TryGet<Stats>(hero, out var stats))
        {
            return true;
        }
        if (sim.Generator is not null && sim.Zone is { } zone && sim.Generator.Zone(zone).Safe)
        {
            return true;
        }
        if (sim.Content.Rules.Expr("casting.swap_requires", "1").IsTrue(stats))
        {
            return true;
        }
        reason = "not_calm";
        return false;
    }
}
