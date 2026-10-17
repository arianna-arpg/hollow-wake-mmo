using System.Globalization;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Interaction;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Items;

/// <summary>What a set of container boards holds when saved: items by board id (each keeps its cell) and stash pages bought.</summary>
public sealed class ContainerSave
{
    public Dictionary<string, List<ItemInstance>> Boards { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Bought { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Containers and stashes (rows I10 and I11). Containers are boards built from unlock rungs whose items may be active
/// on the hero (the reliquary); stashes are pages of boards, some free and the rest bought. Boards belong to the
/// character or the account; both load when a hero enters and save when they leave. Every container is a place the
/// inventory service moves items into and out of, checking reach (a chest, a station), what the board accepts, unique
/// copy limits and, where the container asks for it, the calm a loadout change needs.
/// </summary>
public sealed class ContainerService : InventoryService.IItemPlace
{
    private readonly ContentRegistry _content;

    // The cell of the item last taken out, so a move that cannot finish puts it back where it was.
    private (long Uid, int? X, int? Y)? _lastTaken;

    public ContainerService(ContentRegistry content)
    {
        _content = content;
    }

    public static string PageId(string stash, int page) => stash + ":" + page.ToString(CultureInfo.InvariantCulture);

    /// <summary>Which container or stash page a board id names.</summary>
    public bool TryParse(string board, out ContainerDef? container, out StashDef? stash, out int page)
    {
        container = _content.Get<ContainerDef>(board);
        stash = null;
        page = 0;
        if (container is not null)
        {
            return true;
        }
        var colon = board.LastIndexOf(':');
        if (colon > 0 && int.TryParse(board.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out page) && _content.Get<StashDef>(board[..colon]) is { } s && page >= 1 && page <= s.Pages)
        {
            stash = s;
            return true;
        }
        return false;
    }

    public static string OwnerOf(ContainerDef? container, StashDef? stash) => container?.Owner ?? stash?.Owner ?? "character";

    // ------------------------------------------------------------ loading and saving

    /// <summary>Builds the hero's boards and puts back what the character's and the account's saves hold.</summary>
    public void Load(Simulation sim, Entity hero, ContainerSave? character, ContainerSave? account)
    {
        var containers = sim.World.GetOrAdd<Containers>(hero);
        containers.Boards.Clear();
        containers.Loose.Clear();
        containers.Orphans.Clear();
        containers.Bought.Clear();
        containers.AccountLoaded = account is not null;
        foreach (var save in new[] { character, account })
        {
            if (save is null)
            {
                continue;
            }
            foreach (var (stash, bought) in save.Bought)
            {
                containers.Bought[stash] = bought;
            }
        }
        var scope = new HeroScope(sim, hero);
        foreach (var def in _content.Each<ContainerDef>())
        {
            var (w, h, mask) = ShapeOf(def, scope);
            containers.Boards[def.Id] = new Board(w, h, mask);
        }
        foreach (var stash in _content.Each<StashDef>())
        {
            for (var page = 1; page <= Owned(containers, stash); page++)
            {
                containers.Boards[PageId(stash.Id, page)] = new Board(stash.Width, stash.Height);
            }
        }
        var footprints = sim.Service<ItemResolver>();
        foreach (var save in new[] { character, account })
        {
            if (save is null)
            {
                continue;
            }
            foreach (var (boardId, items) in save.Boards)
            {
                if (containers.Boards.TryGetValue(boardId, out var board))
                {
                    Place(board, footprints, items, Loose(containers, boardId));
                }
                else if (TryParse(boardId, out _, out _, out _))
                {
                    // A stash page not owned (any more): its items wait without a cell until it is bought.
                    Loose(containers, boardId).AddRange(items);
                }
                else if (ReferenceEquals(save, account))
                {
                    // A board content no longer has: the account keeps its items, untouched, until content has it again.
                    containers.Orphans[boardId] = new List<ItemInstance>(items);
                }
                else if (sim.World.TryGet<Inventory>(hero, out var inv))
                {
                    // A character's board content no longer has: its items come back in the bag's overflow.
                    foreach (var item in items)
                    {
                        item.X = null;
                        item.Y = null;
                        inv.Overflow.Add(item);
                    }
                }
            }
        }
        ApplyAll(sim, hero);
    }

    /// <summary>The boards of one owner (character or account) as they would be saved.</summary>
    public ContainerSave Capture(Simulation sim, Entity hero, string owner)
    {
        var save = new ContainerSave();
        if (!sim.World.TryGet<Containers>(hero, out var containers))
        {
            return save;
        }
        foreach (var (boardId, board) in containers.Boards)
        {
            if (!TryParse(boardId, out var def, out var stash, out _) || OwnerOf(def, stash) != owner)
            {
                continue;
            }
            var items = new List<ItemInstance>(board.Placed.Count);
            foreach (var p in board.Placed)
            {
                items.Add(p.Item);
            }
            if (containers.Loose.TryGetValue(boardId, out var loose))
            {
                items.AddRange(loose);
            }
            if (items.Count > 0)
            {
                save.Boards[boardId] = items;
            }
        }
        foreach (var (boardId, loose) in containers.Loose)
        {
            // Items of a page the hero does not have right now go back where they came from.
            if (!containers.Boards.ContainsKey(boardId) && loose.Count > 0 && TryParse(boardId, out var def, out var stash, out _) && OwnerOf(def, stash) == owner)
            {
                save.Boards[boardId] = new List<ItemInstance>(loose);
            }
        }
        if (owner == "account")
        {
            foreach (var (boardId, orphans) in containers.Orphans)
            {
                save.Boards[boardId] = new List<ItemInstance>(orphans);
            }
        }
        foreach (var stash in _content.Each<StashDef>())
        {
            if (stash.Owner == owner && containers.Bought.TryGetValue(stash.Id, out var bought) && bought > 0)
            {
                save.Bought[stash.Id] = bought;
            }
        }
        return save;
    }

    private static List<ItemInstance> Loose(Containers containers, string boardId)
    {
        if (!containers.Loose.TryGetValue(boardId, out var list))
        {
            containers.Loose[boardId] = list = new List<ItemInstance>();
        }
        return list;
    }

    private static void Place(Board board, IFootprints footprints, List<ItemInstance> items, List<ItemInstance> loose)
    {
        var pending = new List<ItemInstance>();
        foreach (var item in items)
        {
            var (w, h) = footprints.Of(item);
            if (item.X is { } x && item.Y is { } y && board.Place(item, x, y, w, h))
            {
                continue;
            }
            pending.Add(item);
        }
        foreach (var item in pending)
        {
            var (w, h) = footprints.Of(item);
            if (!(board.FindSpace(w, h, out var x, out var y) && board.Place(item, x, y, w, h)))
            {
                item.X = null;
                item.Y = null;
                loose.Add(item);
            }
        }
    }

    // ------------------------------------------------------------ shapes, rungs and pages

    /// <summary>A container board's size (every rung's extent) and mask (the cells of the rungs the hero has unlocked).</summary>
    public (int Width, int Height, List<string> Mask) ShapeOf(ContainerDef def, IExprScope hero)
    {
        var width = 1;
        var height = 1;
        foreach (var rung in def.Board)
        {
            height = Math.Max(height, rung.Cells.Count);
            foreach (var row in rung.Cells)
            {
                width = Math.Max(width, row.Length);
            }
        }
        var cells = new char[height][];
        for (var y = 0; y < height; y++)
        {
            cells[y] = new string('.', width).ToCharArray();
        }
        foreach (var rung in def.Board)
        {
            if (!Unlocked(rung, hero))
            {
                continue;
            }
            for (var y = 0; y < rung.Cells.Count; y++)
            {
                var row = rung.Cells[y];
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x] is not ('.' or ' '))
                    {
                        cells[y][x] = 'X';
                    }
                }
            }
        }
        var mask = new List<string>(height);
        foreach (var row in cells)
        {
            mask.Add(new string(row));
        }
        return (width, height, mask);
    }

    public static bool Unlocked(ContainerRungDef rung, IExprScope hero)
    {
        if (rung.Unlock is null)
        {
            return true;
        }
        try
        {
            return rung.Unlock.IsTrue(hero);
        }
        catch (ExprEvalException)
        {
            return false;
        }
    }

    /// <summary>Pages of a stash the hero has: its free pages and those bought, up to its count.</summary>
    public static int Owned(Containers containers, StashDef stash) =>
        Math.Min(stash.Pages, stash.FreePages + (containers.Bought.TryGetValue(stash.Id, out var bought) ? bought : 0));

    /// <summary>The price of a stash page per currency.</summary>
    public Dictionary<string, double> PagePrice(Simulation sim, Entity hero, StashDef stash, int page)
    {
        var scope = new ScopeOverlay(new HeroScope(sim, hero)).Set("page", page);
        var price = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (currency, expr) in stash.PageCost)
        {
            price[currency] = Math.Max(0, Math.Round(expr.Evaluate(scope)));
        }
        return price;
    }

    /// <summary>Buys the next page of a stash: the price from its content, paid from the wallet; the page arrives empty.</summary>
    public bool BuyPage(Simulation sim, Entity hero, string stashId, out string reason)
    {
        if (_content.Get<StashDef>(stashId) is not { } stash)
        {
            reason = "unknown_stash";
            return false;
        }
        var containers = sim.World.GetOrAdd<Containers>(hero);
        var owned = Owned(containers, stash);
        if (owned >= stash.Pages)
        {
            reason = "no_more_pages";
            return false;
        }
        var price = PagePrice(sim, hero, stash, owned + 1);
        if (!sim.World.TryGet<Inventory>(hero, out var inv))
        {
            reason = "no_bag";
            return false;
        }
        foreach (var (currency, amount) in price)
        {
            if (inv.CurrencyOf(currency) < amount - 1e-9)
            {
                reason = "cannot_afford:" + currency;
                return false;
            }
        }
        foreach (var (currency, amount) in price)
        {
            if (amount > 0)
            {
                inv.AddCurrency(currency, -amount);
                sim.Events.Publish(new CurrencyChanged(hero, currency, -amount, inv.CurrencyOf(currency)) { Tick = sim.Clock.Tick });
            }
        }
        containers.Bought[stash.Id] = (containers.Bought.TryGetValue(stash.Id, out var had) ? had : 0) + 1;
        var id = PageId(stash.Id, owned + 1);
        containers.Boards[id] = new Board(stash.Width, stash.Height);
        if (containers.Loose.TryGetValue(id, out var waiting))
        {
            var loose = new List<ItemInstance>();
            Place(containers.Boards[id], sim.Service<ItemResolver>(), waiting, loose);
            containers.Loose[id] = loose;
        }
        sim.Events.Publish(new ContainerChanged(hero, id) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>Re-reads every container's rungs (the hero levelled, a quest opened one) and grows or shrinks its board.</summary>
    public void Refresh(Simulation sim, Entity hero)
    {
        if (!sim.World.TryGet<Containers>(hero, out var containers))
        {
            return;
        }
        var scope = new HeroScope(sim, hero);
        var footprints = sim.Service<ItemResolver>();
        foreach (var def in _content.Each<ContainerDef>())
        {
            var (w, h, mask) = ShapeOf(def, scope);
            if (!containers.Boards.TryGetValue(def.Id, out var board))
            {
                containers.Boards[def.Id] = board = new Board(w, h, mask);
            }
            else if (board.Width != w || board.Height != h || !SameMask(board, mask))
            {
                var displaced = new List<ItemInstance>();
                board.Reshape(w, h, mask, displaced);
                Loose(containers, def.Id).AddRange(displaced);
                sim.Events.Publish(new ContainerChanged(hero, def.Id) { Tick = sim.Clock.Tick });
            }
            if (containers.Loose.TryGetValue(def.Id, out var waiting) && waiting.Count > 0)
            {
                var still = new List<ItemInstance>();
                Place(board, footprints, waiting, still);
                containers.Loose[def.Id] = still;
            }
        }
        ApplyAll(sim, hero);
    }

    private static bool SameMask(Board board, List<string> mask)
    {
        var rows = board.MaskRows();
        if (rows is null)
        {
            foreach (var row in mask)
            {
                if (row.Contains('.'))
                {
                    return false;
                }
            }
            return true;
        }
        return rows.SequenceEqual(mask);
    }

    // ------------------------------------------------------------ reach and activity

    /// <summary>Whether the hero stands at an interactable of the kind (always, when there is no kind).</summary>
    public static bool InReach(Simulation sim, Entity hero, string? at) => at is null || Fonts.Nearest(sim, hero, at) is not null;

    /// <summary>Whether the hero can use a board from where they stand; otherwise why not, as the player reads it.</summary>
    public bool Reachable(Simulation sim, Entity hero, string boardId, out string? reason)
    {
        TryParse(boardId, out var def, out var stash, out _);
        var at = def?.At ?? stash?.At;
        if (at is null || InReach(sim, hero, at))
        {
            reason = null;
            return true;
        }
        reason = sim.Content.Rules.Text("inventory.reach_text", "Walk to a {kind} to use this.").Replace("{kind}", Definition.Humanize(at).ToLowerInvariant(), StringComparison.Ordinal);
        return false;
    }

    /// <summary>Puts every active container's seated items and modifiers on the hero.</summary>
    public void ApplyAll(Simulation sim, Entity hero)
    {
        if (!sim.World.TryGet<Containers>(hero, out var containers))
        {
            return;
        }
        foreach (var def in _content.Each<ContainerDef>())
        {
            if (def.Active && containers.Boards.TryGetValue(def.Id, out var board))
            {
                Apply(sim, hero, def, board);
            }
        }
    }

    /// <summary>
    /// The container's lines on the hero, under source <c>container:{id}</c>: every seated item's wearer lines, and the
    /// container's own modifiers while it holds anything, with gauge <c>container.{id}.cells</c> at the cells it covers.
    /// </summary>
    public void Apply(Simulation sim, Entity hero, ContainerDef def, Board board)
    {
        if (!sim.World.TryGet<Stats>(hero, out var stats))
        {
            return;
        }
        var source = "container:" + def.Id;
        stats.Attributes.RemoveSource(source);
        var resolver = sim.Service<ItemResolver>();
        var cells = 0;
        foreach (var p in board.Placed)
        {
            cells += p.Width * p.Height;
            foreach (var m in resolver.WearerModifiers(p.Item))
            {
                stats.Attributes.Add(m with { Source = source });
            }
        }
        if (board.Placed.Count > 0 && def.Modifiers.Count > 0)
        {
            stats.Attributes.AddRange(def.Modifiers, source);
        }
        stats.SetGauge("container." + def.Id + ".cells", cells);
    }

    // ------------------------------------------------------------ a place items move into and out of

    public bool Handles(string container) => TryParse(container, out _, out _, out _);

    public bool Find(Simulation sim, Entity hero, long uid, out ItemInstance? item, out InventoryService.Location location)
    {
        item = null;
        location = default;
        if (!sim.World.TryGet<Containers>(hero, out var containers))
        {
            return false;
        }
        foreach (var (boardId, board) in containers.Boards)
        {
            foreach (var p in board.Placed)
            {
                if (p.Item.Uid == uid)
                {
                    item = p.Item;
                    location = new InventoryService.Location(boardId, null);
                    return true;
                }
            }
        }
        foreach (var (boardId, loose) in containers.Loose)
        {
            foreach (var l in loose)
            {
                if (l.Uid == uid)
                {
                    item = l;
                    location = new InventoryService.Location(boardId, "loose");
                    return true;
                }
            }
        }
        return false;
    }

    public bool Take(Simulation sim, Entity hero, ItemInstance item, InventoryService.Location from, out string reason)
    {
        if (!TryParse(from.Container, out var def, out _, out _) || !sim.World.TryGet<Containers>(hero, out var containers))
        {
            reason = "no_item";
            return false;
        }
        if (!Reachable(sim, hero, from.Container, out _))
        {
            reason = "out_of_reach";
            return false;
        }
        if (def is { Calm: true } && !LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        int? x = item.X, y = item.Y;
        var removed = containers.Boards.TryGetValue(from.Container, out var board) && board.Remove(item);
        _lastTaken = (item.Uid, x, y);
        if (!removed && containers.Loose.TryGetValue(from.Container, out var loose))
        {
            removed = loose.Remove(item);
        }
        if (!removed)
        {
            reason = "no_item";
            return false;
        }
        if (def is { Active: true } && board is not null)
        {
            Apply(sim, hero, def, board);
        }
        sim.Events.Publish(new ContainerChanged(hero, from.Container) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    public bool Put(Simulation sim, Entity hero, ItemInstance item, string container, int x, int y, out string reason)
    {
        if (!TryParse(container, out var def, out var stash, out _))
        {
            reason = "unknown_container";
            return false;
        }
        var containers = sim.World.GetOrAdd<Containers>(hero);
        if (!containers.Boards.TryGetValue(container, out var board))
        {
            reason = "page_locked";
            return false;
        }
        if (!Reachable(sim, hero, container, out _))
        {
            reason = "out_of_reach";
            return false;
        }
        var accepts = def?.Accepts ?? stash!.Accepts;
        var resolver = sim.Service<ItemResolver>();
        if (!accepts.IsEmpty && !resolver.TagsOf(item).HasAny(accepts))
        {
            reason = "not_accepted";
            return false;
        }
        if (def is { Calm: true } && !LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        if (CopyLimitReached(sim, board, item))
        {
            reason = "copy_limit";
            return false;
        }
        var (w, h) = resolver.Of(item);
        if (!board.Place(item, x, y, w, h))
        {
            reason = "no_room";
            return false;
        }
        if (def is { Active: true })
        {
            Apply(sim, hero, def, board);
        }
        sim.Events.Publish(new ContainerChanged(hero, container) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>A unique whose <c>max_per_container</c> copies already sit on the board (row I5).</summary>
    private static bool CopyLimitReached(Simulation sim, Board board, ItemInstance item)
    {
        if (item.Unique is null || sim.Content.Get<UniqueDef>(item.Unique)?.MaxPerContainer is not { } max)
        {
            return false;
        }
        var copies = 0;
        foreach (var p in board.Placed)
        {
            if (p.Item.Unique == item.Unique)
            {
                copies++;
            }
        }
        return copies >= max;
    }

    public void Return(Simulation sim, Entity hero, ItemInstance item, InventoryService.Location from)
    {
        var containers = sim.World.GetOrAdd<Containers>(hero);
        if (_lastTaken is { } taken && taken.Uid == item.Uid)
        {
            // Back to the cell it left, when that is still free.
            item.X = taken.X;
            item.Y = taken.Y;
            _lastTaken = null;
        }
        if (containers.Boards.TryGetValue(from.Container, out var board))
        {
            var footprints = sim.Service<ItemResolver>();
            var loose = new List<ItemInstance>();
            Place(board, footprints, new List<ItemInstance> { item }, loose);
            if (loose.Count > 0)
            {
                Loose(containers, from.Container).AddRange(loose);
            }
            if (TryParse(from.Container, out var def, out _, out _) && def is { Active: true })
            {
                Apply(sim, hero, def, board);
            }
            return;
        }
        Loose(containers, from.Container).Add(item);
    }

    // ------------------------------------------------------------ pages of the core pane

    /// <summary>One page per container or stash of a kind: sealed with the reason when it cannot be reached from here.</summary>
    public void Pages(Simulation sim, Entity hero, string kind, List<PageView> into, string pageId, int pips)
    {
        foreach (var def in _content.Each<ContainerDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Kind == kind)
            {
                var reachable = Reachable(sim, hero, def.Id, out var reason);
                into.Add(new PageView(pageId, !reachable, reason, pips, def.Id));
            }
        }
        if (kind == "stash")
        {
            foreach (var stash in _content.Each<StashDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                var first = PageId(stash.Id, 1);
                var reachable = Reachable(sim, hero, first, out var reason);
                into.Add(new PageView(pageId, !reachable, reason, pips, first));
            }
        }
    }
}
