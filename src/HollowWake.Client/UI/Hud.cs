using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Cluster;
using HollowWake.Client.UI.Core;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Overlay;
using HollowWake.Client.UI.Pages;
using HollowWake.Client.UI.WorldPanes;
using HollowWake.Protocol;

namespace HollowWake.Client.UI;

/// <summary>
/// The interface of docs/design/ui.md: the cluster at the bottom centre, the target plate, notices and combat
/// text, the chat in the lower left corner, and the workspace: the core pane (the inventory) with its ribbon
/// rail, the development pages docked beside it, and the panes the world opens, bound into books by the book
/// rule. It draws what the server sent and raises intents through <see cref="Send"/>; it never folds a number.
/// Everything it looks like comes from the theme content.
/// </summary>
public partial class Hud : CanvasLayer, IItemHost
{
    private readonly UiContent _ui;
    private readonly ContentNames _names;
    private readonly Dictionary<string, LeafView> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Leaf> _leafState = new(StringComparer.Ordinal);
    private readonly HashSet<string> _quietClose = new(StringComparer.Ordinal);
    private readonly List<(string Label, Action Act)> _menuActions = new();
    private Control _root = null!;
    private ClusterView _cluster = null!;
    private TargetPlate _plate = null!;
    private Notices _notices = null!;
    private ChatPane _chat = null!;
    private CorePane _core = null!;
    private BookView _pageBook = null!;
    private BookView _worldBook = null!;
    private ItemCard _card = null!;
    private CarryGhost _ghost = null!;
    private PopupMenu _popup = null!;
    private LeafContext _leafContext = null!;
    private Carry? _carry;
    private bool _carryMoved;
    private IDropTarget? _hoverTarget;
    private Seat? _seat;
    private double _now;
    private WorkspaceRects? _rects;
    private List<TrayRow> _rows = new();

    public Skin Skin { get; }
    public HeroState Hero { get; } = new();
    public Workspace Workspace { get; }

    /// <summary>Every intent the interface raises; the client sends it to the server.</summary>
    public event Action<Message>? Send;

    /// <summary>A weave slot clicked on the bar; the client casts it as its key would.</summary>
    public event Action<int>? SlotPressed;

    /// <summary>The hero's head on screen this frame, for combat text; null when not on screen.</summary>
    public Func<Vector2?>? HeroScreen { get; set; }

    /// <summary>The ground distance from the hero to an entity, or null when the client does not know it.</summary>
    public Func<long, float?>? DistanceTo { get; set; }

    public Hud(UiContent ui, ContentNames names)
    {
        _ui = ui;
        _names = names;
        Skin = new Skin(ui, names);
        Workspace = Workspace.For(ui);
        Workspace.WorldClosed += OnWorldClosed;
    }

    public override void _Ready()
    {
        Layer = 10;
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Skin.Theme };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _leafContext = new LeafContext { Skin = Skin, Hero = Hero, Items = this, Send = Raise, Menu = OpenMenu };

        _chat = new ChatPane(Skin) { Name = "Chat" };
        _chat.Submitted += text => Raise(new ChatMsg("zone", text));
        _root.AddChild(_chat);
        _cluster = new ClusterView(Skin);
        _cluster.SlotPressed += slot => SlotPressed?.Invoke(slot);
        _cluster.MenuPressed += () => Workspace.TrayOpen = !Workspace.TrayOpen;
        _cluster.TrayPicked += id =>
        {
            Workspace.TrayOpen = false;
            Workspace.RequestPage(id, Via.Menu);
        };
        _root.AddChild(_cluster);
        _plate = new TargetPlate(Skin) { Name = "TargetPlate" };
        _root.AddChild(_plate);
        _notices = new Notices(Skin) { Name = "Notices" };
        _root.AddChild(_notices);

        _pageBook = new BookView(Skin, "PageBook");
        _worldBook = new BookView(Skin, "WorldBook");
        foreach (var book in new[] { _pageBook, _worldBook })
        {
            book.TabSelected += id => Workspace.SelectTab(id);
            book.CloseRequested += id => Workspace.CloseLeaf(id);
            _root.AddChild(book);
            book.Visible = false;
        }
        _core = new CorePane(Skin, this, _ui.CorePage?.Name ?? "Inventory") { Visible = false };
        _core.CloseRequested += () => Workspace.CloseCore();
        _core.RibbonPressed += id => Workspace.RequestPage(id, Via.Ribbon);
        _core.DragMoved += delta => MoveSeat(delta);
        _core.DragEnded += SaveSeat;
        _root.AddChild(_core);

        _card = new ItemCard(Skin) { Name = "ItemCard" };
        _root.AddChild(_card);
        _ghost = new CarryGhost(Skin) { Name = "Carry" };
        _root.AddChild(_ghost);
        _popup = new PopupMenu { Name = "QuickActions", Theme = Skin.Theme };
        _popup.IdPressed += id =>
        {
            if (id >= 0 && id < _menuActions.Count)
            {
                _menuActions[(int)id].Act();
            }
        };
        AddChild(_popup);
        LoadSeat();
        BindKeys();
    }

    private void Raise(Message message) => Send?.Invoke(message);

    private static double Clock => Time.GetTicksMsec() / 1000.0;

    // ------------------------------------------------------------ messages in

    public void ApplySelf(SelfStateMsg self) => Hero.Apply(self, Clock);
    public void ApplySlots(WeaveSlotsMsg slots) => Hero.Apply(slots, Clock);
    public void ApplyInventory(InventoryStateMsg inventory) => Hero.Apply(inventory);
    public void ApplyPassives(PassiveStateMsg passives) => Hero.Apply(passives);
    public void ApplyPages(PageStateMsg pages) => Hero.Apply(pages);

    public void SetSelfEntity(EntityState? state) => Hero.Entity = state;

    /// <summary>The hovered entity, for the target line and plate.</summary>
    public void SetTarget(EntityState? target) => _plate.Show(target, _root.Size, _cluster.Layout?.Scale ?? 1);

    public string TargetLine => _plate.Line;

    public void Notice(string text, string level)
    {
        _notices.Notice(text, level, Clock);
        Log($"[color=#{Skin.Notice(level).ToHtml(false)}]{Escape(text)}[/color]");
    }

    public void Log(string bbcode) => _chat.Log(bbcode);

    public void Chat(string from, string channel, string text) =>
        Log($"[color=#{Skin.S("accent").ToHtml(false)}]{Escape(from)}[/color] [{Escape(channel)}]: {Escape(text)}");

    public static string Escape(string s) => s.Replace("[", "[lb]");

    /// <summary>Combat text above the hero: hits it lands, hits it takes, heals.</summary>
    public void CombatText(string kind, double amount, bool crit, bool dealtBySelf, bool takenBySelf)
    {
        var text = kind switch
        {
            "damage" => Math.Round(amount).ToString(),
            "heal" => "+" + Math.Round(amount),
            "evade" => "evaded",
            "block" => "blocked",
            _ => "",
        };
        if (text.Length == 0)
        {
            return;
        }
        var color = kind == "heal" ? Skin.Fill("stamina").Lightened(0.2f)
            : takenBySelf ? Skin.Fill("life").Lightened(0.25f)
            : crit ? Skin.S("accent").Lightened(0.2f)
            : Colors.White;
        _notices.Float(crit ? text + "!" : text, color, Clock, crit);
    }

    public void ShowQuestOffers(QuestOffersMsg offers)
    {
        var id = $"quest:{offers.Giver}";
        var view = (QuestOfferPane)ViewFor(id, "quest_offer");
        view.Show(offers);
        Workspace.OpenWorld(id, "quest_offer", "Quests", offers.Giver);
    }

    public void ShowDialogue(DialogueStateMsg state)
    {
        var id = $"dialogue:{state.Source}";
        if (state.Ended)
        {
            CloseQuietly(id);
            return;
        }
        var view = (DialoguePane)ViewFor(id, "dialogue");
        view.Show(state);
        Workspace.OpenWorld(id, "dialogue", state.Speaker, state.Source, (float)(state.Reach ?? 0));
    }

    public void ShowVendor(VendorStateMsg state)
    {
        var id = $"vendor:{state.Source}";
        if (state.Closed)
        {
            CloseQuietly(id);
            return;
        }
        var view = (VendorPane)ViewFor(id, "vendor");
        view.Show(state);
        Workspace.OpenWorld(id, "vendor", state.Name, state.Source, (float)(state.Reach ?? 0));
    }

    public void ShowStation(StationStateMsg state)
    {
        var id = $"station:{state.Source}";
        if (state.Closed)
        {
            CloseQuietly(id);
            return;
        }
        var view = (StationPane)ViewFor(id, "station");
        view.Show(state);
        Workspace.OpenWorld(id, "station", state.Name, state.Source, (float)(state.Reach ?? 0));
    }

    private void CloseQuietly(string id)
    {
        if (Workspace.HasWorld(id))
        {
            _quietClose.Add(id);
            Workspace.CloseWorld(id);
        }
    }

    private void OnWorldClosed(Leaf leaf)
    {
        var quiet = _quietClose.Remove(leaf.Id);
        if (_views.Remove(leaf.Id, out var view))
        {
            if (!quiet)
            {
                switch (view)
                {
                    case DialoguePane { State: { } d }:
                        Raise(new CloseDialogueMsg(d.Conversation));
                        break;
                    case VendorPane { State: { } v }:
                        Raise(new ClosePaneMsg("vendor", v.Source));
                        break;
                    case StationPane { State: { } s }:
                        Raise(new ClosePaneMsg("station", s.Source));
                        break;
                }
            }
            _pageBook.Forget(view);
            _worldBook.Forget(view);
            view.GetParent()?.RemoveChild(view);
            view.QueueFree();
        }
    }

    // ------------------------------------------------------------ the leaves

    private LeafView ViewFor(string id, string kind)
    {
        if (_views.TryGetValue(id, out var existing))
        {
            return existing;
        }
        LeafView view = kind switch
        {
            "weaves" => new WeavesPage(_leafContext),
            "passives" => new PassivesPage(_leafContext),
            "character" => new CharacterPage(_leafContext),
            "reliquary" => new ContainerPage(_leafContext, c => c.Kind == "reliquary", "The reliquary has not opened to you."),
            "quest_offer" => new QuestOfferPane(_leafContext),
            "dialogue" => new DialoguePane(_leafContext),
            "vendor" => new VendorPane(_leafContext),
            "station" => new StationPane(_leafContext),
            _ => ContainerPageFor(id),
        };
        if (view is QuestOfferPane quest)
        {
            quest.Answered += () => Workspace.CloseWorld(id);
        }
        _views[id] = view;
        return view;
    }

    /// <summary>A container page: the boards of the container the page state names for it (a stash's pages share it).</summary>
    private LeafView ContainerPageFor(string pageId)
    {
        var container = Hero.Pages?.Pages.FirstOrDefault(p => p.Id == pageId)?.Container ?? pageId;
        return new ContainerPage(_leafContext, c => c.Id == container || c.Of == container, "Nothing is stored here.");
    }

    private LeafView ViewOf(Leaf leaf) => ViewFor(leaf.Id, leaf.Kind);

    /// <summary>The pages the menu and the rail list: content pages, then container pages the page state names.</summary>
    private List<UiPage> AllPages()
    {
        var pages = new List<UiPage>(_ui.Pages);
        if (Hero.Pages is { } served)
        {
            foreach (var p in served.Pages)
            {
                if (p.Container is { } container && pages.All(x => x.Id != p.Id))
                {
                    var name = Hero.Inventory?.Containers.FirstOrDefault(c => c.Id == container || c.Of == container)?.Name ?? ContentNames.Humanize(p.Id);
                    pages.Add(new UiPage(p.Id, name, "", "chest", 100 + pages.Count, "ribbon", name));
                }
            }
        }
        return pages;
    }

    private Dictionary<string, PageStatus>? Served()
    {
        if (Hero.Pages is not { } served)
        {
            return null;
        }
        var map = new Dictionary<string, PageStatus>(StringComparer.Ordinal);
        foreach (var page in _ui.Pages)
        {
            map[page.Id] = new PageStatus(false, null, 0);
        }
        foreach (var p in served.Pages)
        {
            map[p.Id] = new PageStatus(true, p.Sealed ? p.Reason ?? "Sealed here" : null, p.Pips);
        }
        return map;
    }

    // ------------------------------------------------------------ the frame

    public override void _Process(double delta)
    {
        _now = Clock;
        _leafContext.Now = _now;
        var viewport = _root.Size;
        if (viewport.X < 10 || viewport.Y < 10)
        {
            return;
        }
        var pages = AllPages();
        var served = Served();
        _rows = MenuFold.Fold(pages, served, id => id == "passives" ? Hero.Points("passive") : 0, id => Workspace.IsShown(id) || (id == _ui.CorePage?.Id && Workspace.CoreOpen));
        _cluster.Update(viewport, Hero, _now, _rows, Workspace.TrayOpen, _ui.Layout.Keys.GetValueOrDefault("menu", ""));
        _notices.Update(viewport, HeroScreen?.Invoke(), _now);
        _chat.Place(viewport, _cluster.Drawn, _cluster.Layout.Scale);
        if (_plate.Visible)
        {
            _plate.Show(_plate.Target, viewport, _cluster.Layout.Scale);
        }

        // Departure: a world pane closes when the hero walks out of reach of what opened it.
        if (DistanceTo is { } distance)
        {
            Workspace.Depart(distance, Skin.Metric("departure_reach", 6));
        }
        LayoutWorkspace(viewport);
        _ghost.Follow(_carryMoved || _carry?.Lifted == true ? _carry : null, _root.GetLocalMousePosition(), Skin.Metric("cell", 36) * _cluster.Layout.Scale);
    }

    private void LayoutWorkspace(Vector2 viewport)
    {
        var book = Workspace.Book;
        if (!Workspace.CoreOpen)
        {
            _core.Visible = false;
            _pageBook.Hide2();
            _worldBook.Hide2();
            _rects = null;
            return;
        }
        var scale = Math.Min(1f, _cluster.Layout.Scale * 1.1f);
        var desired = _core.Desired(Hero, scale);
        var pageFront = book.FrontWithin(LeafGroup.Page);
        var worldFront = book.FrontWithin(LeafGroup.World);
        var pageWidth = pageFront is null ? Skin.Metric("page_width", 460) : ViewOf(pageFront).PreferredWidth;
        var worldWidth = worldFront is null ? Skin.Metric("page_width", 460) : ViewOf(worldFront).PreferredWidth;
        var ribbons = _rows.Count(r => r.Page.OnRibbon && r.State != TrayState.Hidden);
        var wallet = Hero.Wallet(_names.CurrencyName);
        var rects = PaneLayout.Compute(new PaneLayout.Input(
            new Box(0, 0, viewport.X, viewport.Y),
            _cluster.Drawn,
            _seat,
            desired.X,
            desired.Y,
            pageWidth,
            worldWidth,
            Skin.Metric("edge", 14),
            book.Within(LeafGroup.Page).Count > 0,
            book.Within(LeafGroup.World).Count > 0,
            ribbons,
            RailView.RibbonHeight(Skin),
            RailView.BadgeHeight,
            wallet.Count,
            RailView.ChipHeight,
            RailView.Top));
        _rects = rects;
        book.Merged = rects.Merged;
        _core.Visible = true;
        var slots = Hero.Inventory?.Slots ?? _names.Slots.Select(s => new EquipSlotInfo(s.Id, s.Name, s.Order)).ToList();
        _core.Show(rects.Core, rects.PagesLeft, Hero, slots, _rows, wallet, rects.RailBadges, rects.RailChips, scale);

        if (rects.Merged)
        {
            ShowBook(_pageBook, rects.PageBook, book.Leaves, book.Front(LeafGroup.Page));
            _worldBook.Hide2();
        }
        else
        {
            ShowBook(_pageBook, rects.PageBook, book.Within(LeafGroup.Page), pageFront);
            ShowBook(_worldBook, rects.WorldBook, book.Within(LeafGroup.World), worldFront);
        }
    }

    private void ShowBook(BookView view, Box box, IReadOnlyList<Leaf> leaves, Leaf? front)
    {
        if (front is null || leaves.Count == 0 || box.IsEmpty)
        {
            view.Hide2();
            return;
        }
        var leafView = ViewOf(front);
        view.Show(box, leaves, front, leafView, front.Group == LeafGroup.World);
        leafView.Refresh();
    }

    // ------------------------------------------------------------ the core pane's seat

    private const string SeatFile = "user://interface.cfg";

    private void MoveSeat(Vector2 delta)
    {
        if (_rects is not { } r)
        {
            return;
        }
        var view = new Box(0, 0, _root.Size.X, _root.Size.Y);
        _seat = Seat.Of(r.Core.Moved(delta.X, delta.Y), view);
    }

    private void SaveSeat()
    {
        if (_seat is not { } s)
        {
            return;
        }
        var cfg = new ConfigFile();
        cfg.Load(SeatFile);
        cfg.SetValue("core_pane", "x", s.FX);
        cfg.SetValue("core_pane", "y", s.FY);
        cfg.Save(SeatFile);
    }

    private void LoadSeat()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SeatFile) == Error.Ok && cfg.HasSectionKey("core_pane", "x"))
        {
            _seat = new Seat((float)cfg.GetValue("core_pane", "x").AsDouble(), (float)cfg.GetValue("core_pane", "y").AsDouble());
        }
    }

    /// <summary>Seats the core pane for this session without remembering it (scripted shots).</summary>
    public void UseSeat(Seat? seat) => _seat = seat;

    /// <summary>Puts the core pane back on its default seat.</summary>
    public void ResetSeat()
    {
        _seat = null;
        var cfg = new ConfigFile();
        if (cfg.Load(SeatFile) == Error.Ok && cfg.HasSection("core_pane"))
        {
            cfg.EraseSection("core_pane");
            cfg.Save(SeatFile);
        }
    }

    // ------------------------------------------------------------ keys

    private void BindKeys()
    {
        foreach (var page in _ui.Pages)
        {
            InputSetup.BindName("page_" + page.Id, page.Key);
        }
        InputSetup.BindName("ui_menu_tray", _ui.Layout.Keys.GetValueOrDefault("menu", ""));
        InputSetup.BindName("ui_book_next", _ui.Layout.Keys.GetValueOrDefault("book_next", ""));
        InputSetup.BindName("ui_book_previous", _ui.Layout.Keys.GetValueOrDefault("book_previous", ""));
    }

    public bool ChatFocused => _chat.Focused;

    public void FocusChat() => _chat.Focus();

    /// <summary>
    /// The interface's keys, before the world's: Escape cancels a carry, folds the tray, closes the front pane
    /// and the core pane last; Tab walks a book or opens the tray; page keys request their pages. True when the
    /// interface took the event.
    /// </summary>
    public bool HandleInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return false;
        }
        if (@event.IsActionPressed("cancel"))
        {
            if (_carry is not null)
            {
                CancelCarry();
                return true;
            }
            if (_popup.Visible)
            {
                _popup.Hide();
                return true;
            }
            return Workspace.Escape() != EscapeStep.Nothing;
        }
        var shiftTab = key.Keycode == Key.Tab && key.ShiftPressed;
        if (shiftTab || @event.IsActionPressed("ui_book_previous", exactMatch: true))
        {
            if (Workspace.Book.CanWalk)
            {
                Workspace.Book.Walk(-1);
                return true;
            }
            return false;
        }
        if (@event.IsActionPressed("ui_book_next", exactMatch: true) && Workspace.Book.CanWalk)
        {
            Workspace.Book.Walk(1);
            return true;
        }
        if (@event.IsActionPressed("ui_menu_tray", exactMatch: true))
        {
            Workspace.TrayOpen = !Workspace.TrayOpen;
            return true;
        }
        foreach (var page in AllPages())
        {
            if (page.Key.Length > 0 && @event.IsActionPressed("page_" + page.Id, exactMatch: true))
            {
                var row = _rows.FirstOrDefault(r => r.Page.Id == page.Id);
                if (row is null || row.State == TrayState.Open)
                {
                    Workspace.RequestPage(page.Id, Via.Key);
                }
                else if (row.State == TrayState.Sealed)
                {
                    Notice($"{page.Name}: {row.Reason}", "error");
                }
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------ items: hover, carry, quick actions

    Skin IItemHost.Skin => Skin;

    public Carry? Carrying => _carry is { } c && (_carryMoved || c.Lifted) ? c : null;

    public void HoverItem(ItemInfo? item, Vector2 at, string container)
    {
        if (item is null)
        {
            _card.Hide2();
            return;
        }
        ItemInfo? worn = null;
        if (container != "equipment" && item.Slot is { } slot && Hero.Equipment.TryGetValue(slot, out var w))
        {
            worn = w;
        }
        var extra = new List<string>();
        if (container == "bag")
        {
            foreach (var vendor in _views.Values.OfType<VendorPane>())
            {
                if (vendor.QuoteFor(item.Uid) is { } quote)
                {
                    extra.Add(quote);
                }
            }
        }
        var seats = Hero.Seats((int)Skin.Metric("slots", 8));
        _card.ShowFor(item, _root.GetLocalMousePosition(), _root.Size, worn, extra, i => i < seats.Count && !seats[i].Empty ? seats[i].Name : $"Seat {i + 1}");
    }

    public void PressItem(ItemInfo item, CarrySource from, Vector2 at)
    {
        _carry = new Carry { Item = item, From = from, PressAt = _root.GetLocalMousePosition() };
        _carryMoved = false;
        _card.Hide2();
    }

    public void QuickActions(ItemInfo item, string container, Vector2 at)
    {
        var actions = new List<(string, Action)>();
        if (container == "equipment")
        {
            if (item.Slot is { } worn)
            {
                actions.Add(("Unequip", () => Raise(new UnequipMsg(worn))));
            }
        }
        else if (container == "bag")
        {
            if (item.Slot is not null)
            {
                actions.Add(("Equip", () => Raise(new EquipMsg(item.Uid, item.Slot))));
            }
            foreach (var vendor in _views.Values.OfType<VendorPane>())
            {
                if (vendor.State is { } v && vendor.QuoteFor(item.Uid) is { } quote)
                {
                    actions.Add(($"Sell to {v.Name} ({quote.Replace("Sells for ", "")})", () => Raise(new SellMsg(v.Source, item.Uid))));
                }
            }
        }
        if (container is "bag" or "equipment" && item.Locked != true)
        {
            actions.Add(("Drop", () => Raise(new MoveItemMsg(item.Uid, "world", 0, 0))));
        }
        OpenMenu(_root.GetLocalMousePosition(), actions);
    }

    private void OpenMenu(Vector2 at, IReadOnlyList<(string Label, Action Act)> actions)
    {
        if (actions.Count == 0)
        {
            return;
        }
        _menuActions.Clear();
        _menuActions.AddRange(actions);
        _popup.Clear();
        for (var i = 0; i < actions.Count; i++)
        {
            _popup.AddItem(actions[i].Label, i);
        }
        _popup.Position = new Vector2I((int)at.X, (int)at.Y);
        _popup.ResetSize();
        _popup.Popup();
    }

    public void SendMove(long uid, string container, int x, int y, string? slot = null) =>
        Raise(new MoveItemMsg(uid, container, x, y) { Slot = slot });

    private IEnumerable<IDropTarget> DropTargets()
    {
        // Front-most first: the books, then the core pane's doll and bag.
        foreach (var book in new[] { _worldBook, _pageBook })
        {
            if (book.Visible && book.Shown is { } view)
            {
                foreach (var t in view.DropTargets)
                {
                    yield return t;
                }
            }
        }
        if (_core.Visible)
        {
            yield return _core.Doll;
            yield return _core.Bag;
        }
    }

    private void CancelCarry()
    {
        _hoverTarget?.Leave();
        _hoverTarget = null;
        _carry = null;
        _carryMoved = false;
    }

    private void DropCarry(Vector2 point)
    {
        if (_carry is not { } carry)
        {
            return;
        }
        var global = _root.GetGlobalTransform() * point;
        foreach (var target in DropTargets())
        {
            if (target.Covers(global))
            {
                target.Drop(carry, global);
                CancelCarry();
                return;
            }
        }
        // Over a pane but no target: nothing happens. Over the world: the item is dropped there.
        var hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is null || hovered == _root || !_root.IsAncestorOf(hovered))
        {
            SendMove(carry.Item.Uid, "world", 0, 0);
        }
        CancelCarry();
    }

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion when _carry is { } carry:
            {
                var p = _root.GetLocalMousePosition();
                if (!_carryMoved && carry.Pressed && p.DistanceTo(carry.PressAt) > Skin.Metric("drag_threshold", 6))
                {
                    _carryMoved = true;
                }
                if (_carryMoved || carry.Lifted)
                {
                    var global = _root.GetGlobalTransform() * p;
                    var target = DropTargets().FirstOrDefault(t => t.Covers(global));
                    if (target != _hoverTarget)
                    {
                        _hoverTarget?.Leave();
                        _hoverTarget = target;
                    }
                    target?.Hover(carry, global);
                }
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button when _carry is { } carry:
            {
                var p = _root.GetLocalMousePosition();
                if (!button.Pressed && carry.Pressed)
                {
                    carry.Pressed = false;
                    if (_carryMoved)
                    {
                        DropCarry(p);
                    }
                    else
                    {
                        // A click without a drag lifts the item; the next click drops it.
                        carry.Lifted = true;
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if (button.Pressed && carry.Lifted)
                {
                    DropCarry(p);
                    GetViewport().SetInputAsHandled();
                }
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } when _carry is not null:
                CancelCarry();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true } press when Workspace.TrayOpen:
            {
                var p = _root.GetLocalMousePosition();
                var tray = _cluster.Tray;
                var inTray = tray.Visible && new Rect2(tray.Position, tray.Size).HasPoint(p);
                var inButton = _cluster.Layout is { } l && l.Menu.Contains(p.X, p.Y);
                if (!inTray && !inButton)
                {
                    Workspace.TrayOpen = false;
                }
                _ = press;
                break;
            }
        }
    }

    // ------------------------------------------------------------ for scripted checks

    public bool CoreOpen => Workspace.CoreOpen;
    public bool IsShown(string pageId) => Workspace.IsShown(pageId);
    public bool SheetVisible => Workspace.IsShown("character");
    public bool QuestsVisible => Workspace.Book.Within(LeafGroup.World).Any(l => l.Kind == "quest_offer");
    public IReadOnlyList<TrayRow> TrayRows => _rows;
    public IEnumerable<string> RibbonPages => _rows.Where(r => r.Page.OnRibbon && r.State == TrayState.Open).Select(r => r.Page.Id);
    public bool HasWorldPane(string kind) => Workspace.Book.Within(LeafGroup.World).Any(l => l.Kind == kind);
    public LeafView? ViewOfLeaf(string id) => _views.GetValueOrDefault(id);
    public T? FrontView<T>() where T : LeafView => _views.Values.OfType<T>().FirstOrDefault();
    public Box ClusterRect => _cluster.Drawn;
    public WorkspaceRects? Rects => _rects;
    public int BagItems => Hero.Bag((int)Skin.Metric("bag_columns", 10))?.Items.Count ?? 0;

    /// <summary>Answers the quest offer through its own button path, for scripted checks.</summary>
    public void HideQuests()
    {
        foreach (var leaf in Workspace.Book.Within(LeafGroup.World).Where(l => l.Kind == "quest_offer").ToList())
        {
            Workspace.CloseWorld(leaf.Id);
        }
    }
}
