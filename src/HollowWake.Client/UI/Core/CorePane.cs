using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>
/// The core pane: the inventory and the hub of the workspace. The equipment doll and the hero's level beside
/// it, the bag board below, the ribbon rail on the edge where pages dock with the wallet's chips. It drags by
/// its header and remembers its seat; its pages move with it.
/// </summary>
public partial class CorePane : Control
{
    private readonly Skin _skin;
    private readonly IItemHost _host;
    private Pane _pane = null!;
    private RailView _rail = null!;
    private DollView _doll = null!;
    private BoardView _bag = null!;
    private Summary _summary = null!;
    private bool _railLeft = true;

    public event Action? CloseRequested;
    public event Action<Vector2>? DragMoved;
    public event Action? DragEnded;
    public event Action<string>? RibbonPressed;

    public CorePane(Skin skin, IItemHost host, string title)
    {
        _skin = skin;
        _host = host;
        Name = "CorePane";
        MouseFilter = MouseFilterEnum.Ignore;
        _pane = new Pane(skin, title, closable: true, draggable: true) { Name = "frame" };
    }

    public BoardView Bag => _bag;
    public DollView Doll => _doll;
    public RailView Rail => _rail;
    public float RailWidth => _skin.Metric("rail", 46);

    public override void _Ready()
    {
        AddChild(_pane);
        _pane.CloseRequested += () => CloseRequested?.Invoke();
        _pane.DragMoved += d => DragMoved?.Invoke(d);
        _pane.DragEnded += () => DragEnded?.Invoke();
        _rail = new RailView(_skin) { Name = "rail" };
        _rail.RibbonPressed += id => RibbonPressed?.Invoke(id);
        AddChild(_rail);
        var column = new VBoxContainer { Name = "content", MouseFilter = MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 10);
        _pane.Body.AddChild(column);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        top.AddThemeConstantOverride("separation", 14);
        column.AddChild(top);
        _doll = new DollView(_host) { Name = "doll" };
        top.AddChild(_doll);
        _summary = new Summary(_skin) { Name = "summary", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        top.AddChild(_summary);
        _bag = new BoardView(_host, "bag") { Name = "bag" };
        column.AddChild(_bag);
    }

    /// <summary>The size the pane wants for this state: the rail, the doll and summary row, and the bag board.</summary>
    public Vector2 Desired(HeroState hero, float scale)
    {
        var cell = _skin.Metric("cell", 36) * scale;
        var dollCell = _skin.Metric("doll_cell", 50) * scale;
        var columns = (int)_skin.Metric("bag_columns", 10);
        var bag = hero.Bag(columns);
        var boardSize = _bag.SizeFor(bag, cell);
        var dollW = 3 * dollCell + 2 * Math.Max(4, dollCell * 0.12f);
        var dollH = 4 * dollCell + 3 * Math.Max(4, dollCell * 0.12f);
        var inset = (_skin.Brass ? _skin.Mat("bevel", 4) + 4 : _skin.Mat("bevel", 3) + 3) + 6;
        var bodyW = Math.Max(boardSize.X, dollW + 14 + 150);
        var width = RailWidth + bodyW + inset * 2;
        var height = _pane.HeaderHeight + inset * 2 + dollH + 10 + boardSize.Y + 4;
        return new Vector2(width, height);
    }

    /// <summary>Places the pane, its rail on the side where pages dock, and fills it from the hero's state.</summary>
    public void Show(Box box, bool railLeft, HeroState hero, IReadOnlyList<EquipSlotInfo> slots, IEnumerable<TrayRow> rows, IReadOnlyList<CurrencyInfo> wallet, bool badges, bool chips, float scale)
    {
        Position = new Vector2(box.X, box.Y);
        Size = new Vector2(box.W, box.H);
        _railLeft = railLeft;
        var rail = RailWidth;
        _rail.Position = new Vector2(railLeft ? 0 : box.W - rail, 0);
        _rail.Size = new Vector2(rail, box.H);
        _pane.Position = new Vector2(railLeft ? rail - 2 : 0, 0);
        _pane.Size = new Vector2(box.W - rail + 2, box.H);
        _rail.Show(rows, wallet, badges, chips);
        var cell = _skin.Metric("cell", 36) * scale;
        var dollCell = _skin.Metric("doll_cell", 50) * scale;
        _doll.Show(slots, hero.Equipment, _skin.L, dollCell);
        _bag.Show(hero.Bag((int)_skin.Metric("bag_columns", 10)), cell);
        _summary.Show(hero);
    }

    /// <summary>The hero's level, experience and unspent points beside the doll.</summary>
    private sealed partial class Summary : Control
    {
        private readonly Skin _skin;
        private SelfStateMsg? _self;
        private int _points;
        private List<SheetRow> _rows = new();

        public Summary(Skin skin) => _skin = skin;

        public void Show(HeroState hero)
        {
            _self = hero.Self;
            _points = hero.Points("passive");
            // The sheet's first section (the core attributes) stands beside the doll.
            var first = _skin.Ui.Sheet.FirstOrDefault(s => s.Kind == "attributes");
            _rows = first is null || hero.Self is null
                ? new List<SheetRow>()
                : SheetModel.Build(new[] { first }, _skin.Names, hero.Self.Attributes, Array.Empty<QuestLine>())[0].Rows;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_self is not { } s)
            {
                return;
            }
            var ink = _skin.S("ink");
            var muted = _skin.S("ink_muted");
            DrawString(_skin.Title, new Vector2(0, _skin.TitleSize + 4), $"Level {s.Level}", HorizontalAlignment.Left, Size.X, _skin.TitleSize, ink);
            var bar = new Rect2(0, _skin.TitleSize + 14, Math.Max(40, Size.X - 4), 10);
            Paint.Well(this, _skin, bar, _skin.S("well"));
            var frac = s.XpToNext > 0 ? (float)Math.Clamp(s.Xp / s.XpToNext, 0, 1) : 0f;
            DrawRect(new Rect2(bar.Position + new Vector2(2, 2), new Vector2((bar.Size.X - 4) * frac, bar.Size.Y - 4)), _skin.S("accent"));
            DrawString(_skin.Numerals, new Vector2(0, bar.End.Y + _skin.NumeralSize + 4), $"{Math.Round(s.Xp)} / {Math.Round(s.XpToNext)} experience", HorizontalAlignment.Left, Size.X, _skin.NumeralSize - 1, muted);
            var y = bar.End.Y + _skin.NumeralSize * 2 + 14;
            if (_points > 0)
            {
                DrawString(_skin.Body, new Vector2(0, y), $"{_points} passive point{(_points == 1 ? "" : "s")} to spend", HorizontalAlignment.Left, Size.X, _skin.BodySize - 1, _skin.S("accent"));
                y += _skin.BodySize + 6;
            }
            foreach (var row in _rows)
            {
                DrawString(_skin.Body, new Vector2(0, y), row.Label, HorizontalAlignment.Left, Size.X * 0.6f, _skin.BodySize - 1, muted);
                DrawString(_skin.Numerals, new Vector2(0, y), row.Value, HorizontalAlignment.Right, Size.X - 6, _skin.NumeralSize, ink);
                y += _skin.BodySize + 4;
            }
        }
    }
}
