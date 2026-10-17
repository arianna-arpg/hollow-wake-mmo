using HollowWake.Protocol;

namespace HollowWake.Client.UI;

/// <summary>
/// The latest of every state the server sent about the hero, as the interface reads it: the self state with
/// the cluster's fields, the weave slots, the inventory, the passives and the pages, plus the hero's own entity
/// state for a server that sends no interface states. Timers the server sends as time left (cooldowns,
/// statuses) count down from when they arrived. Nothing here computes a number the server already sent.
/// </summary>
public sealed class HeroState
{
    public SelfStateMsg? Self { get; private set; }
    public WeaveSlotsMsg? Slots { get; private set; }
    public InventoryStateMsg? Inventory { get; private set; }
    public PassiveStateMsg? Passives { get; private set; }
    public PageStateMsg? Pages { get; private set; }
    public EntityState? Entity { get; set; }

    /// <summary>Whether the server granted the interface feature: the self state then carries the cluster's fields.</summary>
    public bool Interface { get; set; }

    /// <summary>Local seconds when each state arrived, for timers counted down from receipt.</summary>
    public double SelfAt { get; private set; }
    public double SlotsAt { get; private set; }

    /// <summary>The server clock as last estimated (server seconds minus local seconds).</summary>
    public double ServerOffset { get; private set; }
    public bool HasServerClock { get; private set; }

    /// <summary>Bumped on every change, so a view can rebuild only when something changed.</summary>
    public int Version { get; private set; }
    public int InventoryVersion { get; private set; }
    public int SlotsVersion { get; private set; }
    public int PassivesVersion { get; private set; }
    public int PagesVersion { get; private set; }

    public void Apply(SelfStateMsg self, double now)
    {
        Self = self;
        SelfAt = now;
        foreach (var w in self.Weaves)
        {
            TrackCooldown(w.Slot, w.ReadyIn, now);
        }
        Version++;
    }

    /// <summary>Each slot's running cooldown as the self state reports it: when it ends (local seconds) and its length.</summary>
    private readonly Dictionary<int, (double ReadyAt, double Span)> _cooldowns = new();

    /// <summary>
    /// Follows a slot's time left from one self state to the next: a later end than the running one starts a new
    /// cooldown whose length is the time left first seen; the same cooldown keeps its length and takes the fresher end.
    /// </summary>
    private void TrackCooldown(int slot, double readyIn, double now)
    {
        if (readyIn <= 0.01)
        {
            _cooldowns.Remove(slot);
            return;
        }
        var readyAt = now + readyIn;
        _cooldowns[slot] = _cooldowns.TryGetValue(slot, out var c) && readyAt <= c.ReadyAt + 0.1
            ? (readyAt, Math.Max(c.Span, readyIn))
            : (readyAt, readyIn);
    }

    public void Apply(WeaveSlotsMsg slots, double now)
    {
        Slots = slots;
        SlotsAt = now;
        SlotsVersion++;
        Version++;
    }

    public void Apply(InventoryStateMsg inventory)
    {
        Inventory = inventory;
        InventoryVersion++;
        Version++;
    }

    public void Apply(PassiveStateMsg passives)
    {
        Passives = passives;
        PassivesVersion++;
        Version++;
    }

    public void Apply(PageStateMsg pages)
    {
        Pages = pages;
        PagesVersion++;
        Version++;
    }

    /// <summary>Learns the server clock from a time the server stamped a message with, smoothing jitter.</summary>
    public void ObserveServerTime(double serverTime, double now)
    {
        var offset = serverTime - now;
        if (!HasServerClock || offset > ServerOffset + 1.0 || offset < ServerOffset - 1.0)
        {
            ServerOffset = offset;
            HasServerClock = true;
            return;
        }
        // A late message reads an older time: keep the largest offset seen, eased toward new readings.
        ServerOffset = offset > ServerOffset ? offset : ServerOffset + (offset - ServerOffset) * 0.02;
    }

    public double ServerNow(double now) => now + ServerOffset;

    public void Clear()
    {
        Self = null;
        Slots = null;
        Inventory = null;
        Passives = null;
        Pages = null;
        Entity = null;
        HasServerClock = false;
        _cooldowns.Clear();
        Version++;
    }

    // ------------------------------------------------------------ the cluster

    public (double Current, double Max) Resource(string id)
    {
        if (Self is { } s && s.Resources.TryGetValue(id, out var cur))
        {
            return (cur, s.ResourceMax.GetValueOrDefault(id));
        }
        if (Entity is { } e && e.Resources.TryGetValue(id, out var ecur))
        {
            return (ecur, e.ResourceMax.GetValueOrDefault(id));
        }
        return (0, 0);
    }

    /// <summary>Whether the hero has a pool at all: it is in the self state with a maximum above zero.</summary>
    public bool HasPool(string id) => Resource(id).Max > 0;

    public double Reserved(string id) => Self?.Reserved?.GetValueOrDefault(id) ?? 0;

    public PoolInfo? Pool(string id) => Self?.Pools?.FirstOrDefault(p => p.Id == id);

    /// <summary>The hero's statuses with time left counted down from receipt; ids alone when the server sends no timers.</summary>
    public IReadOnlyList<StatusInfo> Statuses(double now)
    {
        if (Interface)
        {
            if (Self?.Statuses is not { } list)
            {
                return Array.Empty<StatusInfo>();
            }
            var elapsed = now - SelfAt;
            return list.Select(s => s.Remaining < 0 ? s : s with { Remaining = Math.Max(0, s.Remaining - elapsed) }).ToList();
        }
        if (Entity is { } e)
        {
            return e.Statuses.Select(id => new StatusInfo(id, 1, -1, -1)).ToList();
        }
        return Array.Empty<StatusInfo>();
    }

    /// <summary>The cast in progress: the self state's cast, else (without the interface feature) the hero's entity state's.</summary>
    public CastInfo? Cast
    {
        get
        {
            if (Interface)
            {
                return Self?.Cast;
            }
            if (Entity is { Casting: { Length: > 0 } name } e)
            {
                return new CastInfo(-1, "cast", e.CastProgress, 0, 0) { Name = name };
            }
            return null;
        }
    }

    // ------------------------------------------------------------ the weave bar and the rack

    /// <summary>A seat as the bar and the rack draw it, from the weave slot state or, without it, the self state.</summary>
    public sealed record Seat(int Slot, string? Key, string Name, string? Icon, IReadOnlyList<string> Threads, double ReadyAt, double Cooldown,
        IReadOnlyDictionary<string, double> Costs, string? Dormant, int? Uses, int? UsesMax, FaceInfo? Converted, FaceInfo? Meta, WeaveSeatInfo? Info)
    {
        public bool Empty => Key is null;

        public double ReadyIn(double now) => Math.Max(0, ReadyAt - now);
    }

    public IReadOnlyList<Seat> Seats(int count)
    {
        var seats = new List<Seat>(count);
        if (Slots is { } slots)
        {
            foreach (var s in slots.Seats.OrderBy(s => s.Slot))
            {
                var (readyAt, span) = Timer(s.Slot, SlotsAt + s.ReadyIn, s.Cooldown);
                seats.Add(new Seat(s.Slot, s.Key, s.Name, s.Icon, s.Threads ?? new List<string>(), readyAt, span,
                    s.Costs ?? new Dictionary<string, double>(), s.Dormant, s.Uses, s.UsesMax, s.Converted, s.Meta, s));
            }
        }
        else if (Self is { } self)
        {
            foreach (var w in self.Weaves)
            {
                var (readyAt, span) = Timer(w.Slot, SelfAt + w.ReadyIn, 0);
                seats.Add(new Seat(w.Slot, w.Key, w.Name, w.Look, Array.Empty<string>(), readyAt, span, w.Costs, null, null, null, null, null, null));
            }
        }
        for (var i = seats.Count; i < count; i++)
        {
            seats.Add(new Seat(i, null, "", null, Array.Empty<string>(), 0, 0, new Dictionary<string, double>(), null, null, null, null, null, null));
        }
        return seats;
    }

    /// <summary>
    /// A seat's cooldown: the self state's time left (sent every tick) when it reports the slot, else the weave
    /// slot state's; its length is the seat's cooldown when that covers the time left, else the length followed.
    /// </summary>
    private (double ReadyAt, double Span) Timer(int slot, double fallbackAt, double cooldown)
    {
        if (Self?.Weaves.Any(w => w.Slot == slot) != true)
        {
            return (fallbackAt, cooldown > 0 ? cooldown : Math.Max(0, fallbackAt - SlotsAt));
        }
        if (!_cooldowns.TryGetValue(slot, out var c))
        {
            return (SelfAt, cooldown);
        }
        return (c.ReadyAt, cooldown > 0 && cooldown >= c.ReadyAt - SelfAt - 0.05 ? cooldown : c.Span);
    }

    // ------------------------------------------------------------ items

    /// <summary>The bag as a board: the inventory state's, or the self state's list laid out one cell each.</summary>
    public BoardInfo? Bag(int columns)
    {
        if (Inventory is { } inv)
        {
            return inv.Bag;
        }
        if (Self is not { } self)
        {
            return null;
        }
        columns = Math.Max(1, columns);
        var items = new List<ItemInfo>(self.Bag.Count);
        for (var i = 0; i < self.Bag.Count; i++)
        {
            items.Add(self.Bag[i] with { X = i % columns, Y = i / columns, W = 1, H = 1 });
        }
        return new BoardInfo(columns, Math.Max(1, (Math.Max(self.BagCapacity, self.Bag.Count) + columns - 1) / columns), items);
    }

    public IReadOnlyDictionary<string, ItemInfo> Equipment => Inventory?.Equipment ?? (IReadOnlyDictionary<string, ItemInfo>?)Self?.Equipment ?? new Dictionary<string, ItemInfo>();

    public ItemInfo? FindItem(long uid)
    {
        var bag = Inventory?.Bag.Items ?? Self?.Bag;
        var item = bag?.FirstOrDefault(i => i.Uid == uid);
        if (item is not null)
        {
            return item;
        }
        foreach (var worn in Equipment.Values)
        {
            if (worn.Uid == uid)
            {
                return worn;
            }
        }
        if (Inventory is { } inv)
        {
            foreach (var c in inv.Containers)
            {
                if (c.Board.Items.FirstOrDefault(i => i.Uid == uid) is { } found)
                {
                    return found;
                }
            }
        }
        return null;
    }

    /// <summary>The wallet: the inventory state's, or the self state's currency map with names from content.</summary>
    public IReadOnlyList<CurrencyInfo> Wallet(Func<string, string> nameOf)
    {
        if (Inventory is { } inv)
        {
            return inv.Wallet;
        }
        return Self?.Currency.Select(kv => new CurrencyInfo(kv.Key, nameOf(kv.Key), kv.Value)).ToList() ?? (IReadOnlyList<CurrencyInfo>)Array.Empty<CurrencyInfo>();
    }

    // ------------------------------------------------------------ passives and pages

    public IReadOnlyCollection<string> Allocated => (IReadOnlyCollection<string>?)Passives?.Allocated ?? Self?.Passives ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public int Points(string currency) => Passives?.Points.GetValueOrDefault(currency) ?? (currency == "passive" ? Self?.PassivePoints ?? 0 : 0);
}
