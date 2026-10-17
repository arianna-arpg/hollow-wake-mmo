using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The cluster at the bottom centre: the orbs the layout names (life on the left, mana on the right), the weave
/// bar between them, the cast bar and the status row above it, and the menu button with its tray beside the
/// mana orb. It lays itself out every frame for the viewport and publishes the rectangle it drew, which every
/// pane fits above.
/// </summary>
public partial class ClusterView : Control
{
    private readonly Skin _skin;
    private readonly List<(UiOrb Orb, OrbView View)> _orbs = new();
    private WeaveBar _bar = null!;
    private CastBar _cast = null!;
    private StatusRow _status = null!;
    private MenuButton _menu = null!;
    private MenuTray _tray = null!;

    public ClusterLayout Layout { get; private set; } = null!;

    /// <summary>The rectangle the cluster drew this frame.</summary>
    public Box Drawn => Layout?.Rect ?? Box.Empty;

    public event Action<int>? SlotPressed;
    public event Action? MenuPressed;
    public event Action<string>? TrayPicked;

    public ClusterView(Skin skin)
    {
        _skin = skin;
        Name = "Cluster";
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public MenuTray Tray => _tray;
    public StatusRow Status => _status;

    public override void _Ready()
    {
        foreach (var orb in _skin.L.Orbs)
        {
            var view = new OrbView(_skin, orb) { Name = $"orb_{orb.Fill}" };
            AddChild(view);
            _orbs.Add((orb, view));
        }
        _bar = new WeaveBar(_skin) { Name = "WeaveBar" };
        _bar.SlotPressed += slot => SlotPressed?.Invoke(slot);
        AddChild(_bar);
        _cast = new CastBar(_skin) { Name = "CastBar" };
        AddChild(_cast);
        _status = new StatusRow(_skin) { Name = "StatusRow" };
        AddChild(_status);
        _menu = new MenuButton(_skin) { Name = "MenuButton" };
        _menu.Pressed += () => MenuPressed?.Invoke();
        AddChild(_menu);
        _tray = new MenuTray(_skin) { Name = "MenuTray" };
        _tray.Picked += id => TrayPicked?.Invoke(id);
        AddChild(_tray);
    }

    public void Update(Vector2 viewport, HeroState hero, double now, List<TrayRow> rows, bool trayOpen, string menuKey)
    {
        Layout = ClusterLayout.Compute(_skin.L, viewport.X, viewport.Y, _skin.Metric("scale", 1));
        Position = Vector2.Zero;
        Size = viewport;
        foreach (var (orb, view) in _orbs)
        {
            view.Update(Layout, orb.Side == "right" ? Layout.RightOrb : Layout.LeftOrb, hero, now);
        }
        _bar.Update(Layout, hero, now);
        _cast.Update(Layout, hero, now);
        _status.Update(Layout, hero, now);
        _menu.Update(Layout, MenuFold.Badge(rows), trayOpen, KeyName.Label(menuKey));
        _tray.Visible = trayOpen;
        _tray.Update(Layout, viewport, rows);
    }
}
