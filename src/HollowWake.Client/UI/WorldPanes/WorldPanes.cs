using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Core;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.WorldPanes;

/// <summary>A quest giver's offers and the quests ready to hand in, with accept and turn-in buttons.</summary>
public partial class QuestOfferPane : LeafView
{
    private VBoxContainer _list = null!;
    private QuestOffersMsg? _offers;
    private bool _dirty;

    public QuestOfferPane(LeafContext context) : base(context)
    {
        Name = "QuestOfferPane";
    }

    public override float PreferredWidth => Skin.Metric("page_width", 460) * 0.9f;

    public int Buttons { get; private set; }

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
    }

    public void Show(QuestOffersMsg offers)
    {
        _offers = offers;
        _dirty = true;
    }

    public event Action? Answered;

    public override void Refresh()
    {
        if (!_dirty || _offers is null || _list is null)
        {
            return;
        }
        _dirty = false;
        Clear(_list);
        var offers = _offers;
        var buttons = 0;
        void Entry(QuestInfo q, string verb, Message intent)
        {
            var box = new VBoxContainer();
            box.AddChild(MakeButton($"{verb}: {q.Name}  (level {q.Level})", () =>
            {
                Ctx.Send(intent);
                Answered?.Invoke();
            }));
            foreach (var o in q.Objectives)
            {
                box.AddChild(MakeLabel("   " + o, Skin.S("ink_muted"), Skin.BodySize - 1, wrap: true));
            }
            _list.AddChild(box);
            buttons++;
        }
        foreach (var q in offers.Offers)
        {
            Entry(q, "Accept", new AcceptQuestMsg(q.Id, offers.Giver));
        }
        foreach (var q in offers.TurnIns)
        {
            Entry(q, "Turn in", new TurnInQuestMsg(q.Id, offers.Giver));
        }
        if (buttons == 0)
        {
            _list.AddChild(MakeLabel("Nothing for you right now.", Skin.S("ink_muted")));
        }
        Buttons = buttons;
    }
}

/// <summary>
/// A conversation: who speaks, the line, and the choices that can be taken (with why not for those that
/// cannot). Choosing sends the line's id with the choice; the server runs the conversation and answers with
/// the next line, or ends it. A line can open a vendor or a station in the same book.
/// </summary>
public partial class DialoguePane : LeafView
{
    private VBoxContainer _column = null!;
    private DialogueStateMsg? _state;
    private bool _dirty;

    public DialoguePane(LeafContext context) : base(context)
    {
        Name = "DialoguePane";
    }

    public override float PreferredWidth => Skin.Metric("page_width", 460);

    public DialogueStateMsg? State => _state;

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_column);
    }

    public void Show(DialogueStateMsg state)
    {
        _state = state;
        _dirty = true;
    }

    public override void Refresh()
    {
        if (!_dirty || _state is null || _column is null)
        {
            return;
        }
        _dirty = false;
        Clear(_column);
        var s = _state;
        _column.AddChild(MakeLabel(s.Speaker, Skin.S("accent"), Skin.TitleSize, font: Skin.Title));
        var line = MakeLabel(s.Line, Skin.S("ink"), Skin.BodySize + 1, wrap: true);
        line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _column.AddChild(line);
        _column.AddChild(new HSeparator());
        var n = 1;
        foreach (var choice in s.Choices)
        {
            var c = choice;
            var button = MakeButton($"{n}. {c.Text}", () => Ctx.Send(new ChooseDialogueMsg(s.Conversation, c.Id) { Node = s.Node }), c.Enabled, c.Enabled ? null : c.Reason);
            button.Alignment = HorizontalAlignment.Left;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _column.AddChild(button);
            n++;
        }
    }
}

/// <summary>
/// A vendor: the stock with the price of one unit per currency and the units left, what the hero sold and can
/// buy back, and the prices the vendor pays (shown on bag items' cards while the vendor is open). Buying,
/// selling (drop a bag item here) and buying back are one intent each; the bag updates through the inventory
/// state.
/// </summary>
public partial class VendorPane : LeafView, IDropTarget
{
    private VBoxContainer _column = null!;
    private VendorStateMsg? _state;
    private bool _dirty;
    private bool _dropHover;

    public VendorPane(LeafContext context) : base(context)
    {
        Name = "VendorPane";
    }

    public VendorStateMsg? State => _state;
    public int StockRows { get; private set; }

    public override IEnumerable<IDropTarget> DropTargets => new IDropTarget[] { this };

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_column);
    }

    public void Show(VendorStateMsg state)
    {
        _state = state;
        _dirty = true;
    }

    /// <summary>What the vendor pays for one bag item, as text, or null.</summary>
    public string? QuoteFor(long uid)
    {
        var quote = _state?.SellPrices.FirstOrDefault(q => q.Item == uid);
        return quote is null ? null : "Sells for " + Price(quote.Price);
    }

    private string Price(Dictionary<string, double> price) => string.Join(", ", price.Select(p => $"{Math.Round(p.Value)} {Skin.Names.CurrencyName(p.Key)}"));

    public override void Refresh()
    {
        if (!_dirty || _state is null || _column is null)
        {
            return;
        }
        _dirty = false;
        Clear(_column);
        var s = _state;
        _column.AddChild(MakeLabel("For sale", Skin.S("accent"), Skin.TitleSize - 1, font: Skin.Title));
        var rows = 0;
        foreach (var stock in s.Stock)
        {
            var st = stock;
            var row = new HBoxContainer();
            row.AddChild(new ItemSwatch(Skin, Ctx.Items, st.Item) { CustomMinimumSize = new Vector2(36, 36) });
            var name = MakeLabel(st.Item.Name + (st.Count is { } count ? $"  ({count} left)" : ""), Skin.Rarity(st.Item.Rarity));
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(name);
            row.AddChild(MakeLabel(Price(st.Price), Skin.S("ink"), font: Skin.Numerals));
            var enabled = st.Enabled != false;
            row.AddChild(MakeButton("Buy", () => Ctx.Send(new BuyMsg(s.Source, st.Id, 1)), enabled, enabled ? null : st.Reason));
            _column.AddChild(row);
            rows++;
        }
        if (s.Stock.Count == 0)
        {
            _column.AddChild(MakeLabel("Nothing on the shelf.", Skin.S("ink_muted")));
        }
        StockRows = rows;
        if (s.BuyBack.Count > 0)
        {
            _column.AddChild(new HSeparator());
            _column.AddChild(MakeLabel("Buy back", Skin.S("accent"), Skin.TitleSize - 1, font: Skin.Title));
            foreach (var back in s.BuyBack)
            {
                var b = back;
                var row = new HBoxContainer();
                row.AddChild(new ItemSwatch(Skin, Ctx.Items, b.Item) { CustomMinimumSize = new Vector2(36, 36) });
                var name = MakeLabel(b.Item.Name, Skin.Rarity(b.Item.Rarity));
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                row.AddChild(MakeLabel(Price(b.Price), Skin.S("ink"), font: Skin.Numerals));
                row.AddChild(MakeButton("Buy back", () => Ctx.Send(new BuyBackMsg(s.Source, b.Item.Uid))));
                _column.AddChild(row);
            }
        }
        _column.AddChild(new HSeparator());
        _column.AddChild(MakeLabel("Drop an item from the bag here to sell it.", Skin.S("ink_muted"), Skin.BodySize - 1, wrap: true));
    }

    public bool Covers(Vector2 point) => IsVisibleInTree() && GetGlobalRect().HasPoint(point);

    public void Hover(Carry carry, Vector2 point)
    {
        if (!_dropHover)
        {
            _dropHover = true;
            QueueRedraw();
        }
    }

    public void Leave()
    {
        if (_dropHover)
        {
            _dropHover = false;
            QueueRedraw();
        }
    }

    public bool Drop(Carry carry, Vector2 point)
    {
        Leave();
        if (_state is null || carry.From.Container != "bag")
        {
            return false;
        }
        Ctx.Send(new SellMsg(_state.Source, carry.Item.Uid));
        return true;
    }

    public override void _Draw()
    {
        if (_dropHover)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Skin.State("fit", Colors.Green), false, 2);
        }
    }
}

/// <summary>A crafting station: its recipes with what they need against what the hero has, and a craft button each.</summary>
public partial class StationPane : LeafView
{
    private VBoxContainer _column = null!;
    private StationStateMsg? _state;
    private bool _dirty;

    public StationPane(LeafContext context) : base(context)
    {
        Name = "StationPane";
    }

    public StationStateMsg? State => _state;

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_column);
    }

    public void Show(StationStateMsg state)
    {
        _state = state;
        _dirty = true;
    }

    public override void Refresh()
    {
        if (!_dirty || _state is null || _column is null)
        {
            return;
        }
        _dirty = false;
        Clear(_column);
        var s = _state;
        if (s.Recipes.Count == 0)
        {
            _column.AddChild(MakeLabel("Nothing can be made here yet.", Skin.S("ink_muted")));
        }
        foreach (var recipe in s.Recipes)
        {
            var r = recipe;
            var head = new HBoxContainer();
            head.AddChild(new ItemSwatch(Skin, Ctx.Items, r.Output) { CustomMinimumSize = new Vector2(36, 36) });
            var name = MakeLabel(r.Name, Skin.Rarity(r.Output.Rarity), font: Skin.Title);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            head.AddChild(MakeButton("Craft", () => Ctx.Send(new CraftStartMsg(r.Id, s.Source)), r.Craftable, r.Craftable ? null : r.Reason));
            _column.AddChild(head);
            foreach (var input in r.Inputs)
            {
                _column.AddChild(MakeLabel($"   {input.Text}  {input.Have}/{input.Need}", input.Have >= input.Need ? Skin.S("ink") : Skin.S("bad", Skin.S("ink_muted")), Skin.BodySize - 1));
            }
            if (r.Cost is { Count: > 0 } cost)
            {
                _column.AddChild(MakeLabel("   Costs " + string.Join(", ", cost.Select(p => $"{Math.Round(p.Value)} {Skin.Names.CurrencyName(p.Key)}")), Skin.S("ink_muted"), Skin.BodySize - 1));
            }
        }
    }
}

/// <summary>An item tile in a list, with its card on hover.</summary>
public partial class ItemSwatch : Control
{
    private readonly Skin _skin;
    private readonly IItemHost _host;
    private readonly ItemInfo _item;

    public ItemSwatch(Skin skin, IItemHost host, ItemInfo item)
    {
        _skin = skin;
        _host = host;
        _item = item;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            _host.HoverItem(_item, GetGlobalMousePosition(), "pane");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _host.HoverItem(null, Vector2.Zero, "pane");
        }
    }

    public override void _Draw() => ItemArt.Tile(this, _skin, new Rect2(Vector2.Zero, Size).Grow(-2), _item);
}
