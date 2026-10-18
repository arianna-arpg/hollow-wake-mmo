using Godot;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Client.UI.Entry;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Appearance;

namespace HollowWake.Client.UI;

/// <summary>Connect, log in, pick or create a character, in a pane of the interface's theme. Hidden once in the world.</summary>
public partial class LoginPanel : CanvasLayer
{
    private Skin? _skin;
    private Pane _pane = null!;
    private LineEdit _host = null!;
    private LineEdit _port = null!;
    private LineEdit _account = null!;
    private Button _connect = null!;
    private Label _status = null!;
    private Control _connection = null!;
    private AccountEntryView? _entry;
    private string? _entryId;
    public EntryScene? Entry => _entry as EntryScene;
    public ContentRegistry Content { get; init; } = null!;
    public AppearanceService Appearance { get; init; } = null!;
    public Func<bool> Suspended { get; init; } = () => false;
    public string? CreationRequestId => _entry?.Pending;
    public event Action? LeaveRequested;
    private List<CharacterSummary> _list = new();

    public event Action<string, int, string>? ConnectRequested;
    public event Action<string, string>? CreateRequested;
    public event Action<string>? EnterRequested;
    public event Action? OptionsRequested;

    /// <summary>The server the fields start with; the launcher passes it as <c>--host</c> and <c>--port</c>.</summary>
    public string DefaultHost { get; init; } = LaunchOptions.DefaultHost;
    public int DefaultPort { get; init; } = LaunchOptions.DefaultPort;

    /// <summary>The look of the panel: the interface's skin.</summary>
    public Skin? Skin
    {
        get => _skin;
        init => _skin = value;
    }

    public override void _Ready()
    {
        Layer = 20;
        var size = new Vector2(540, 560);
        if (_skin is { } skin)
        {
            _pane = new Pane(skin, "Hollow Wake MMO", closable: false) { Name = "frame" };
        }
        else
        {
            _pane = null!;
        }
        Control host;
        if (_pane is not null)
        {
            _pane.SetAnchorsPreset(Control.LayoutPreset.Center);
            _pane.Position = -size / 2;
            _pane.Size = size;
            AddChild(_pane);
            host = _pane.Body;
        }
        else
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            panel.Position = -size / 2;
            panel.CustomMinimumSize = size;
            AddChild(panel);
            host = panel;
        }
        _connection = _pane is not null ? _pane : host;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        host.AddChild(box);
        box.AddChild(new TextureRect {
            Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        var muted = _skin?.S("ink_muted") ?? new Color(1, 1, 1, 0.7f);
        var tagline = new Label { Text = "the Weftmarches await", HorizontalAlignment = HorizontalAlignment.Center };
        tagline.AddThemeColorOverride("font_color", muted);
        box.AddChild(tagline);

        var row = new HBoxContainer();
        box.AddChild(row);
        row.AddChild(new Label { Text = "Server", CustomMinimumSize = new Vector2(70, 0) });
        _host = new LineEdit { Text = DefaultHost, CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(_host);
        _port = new LineEdit { Text = DefaultPort.ToString(), CustomMinimumSize = new Vector2(80, 0) };
        row.AddChild(_port);
        var row2 = new HBoxContainer();
        box.AddChild(row2);
        row2.AddChild(new Label { Text = "Account", CustomMinimumSize = new Vector2(70, 0) });
        _account = new LineEdit { Text = "marcher", CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row2.AddChild(_account);
        _connect = new Button { Text = "Connect", FocusMode = Control.FocusModeEnum.None };
        _connect.Pressed += () => ConnectRequested?.Invoke(_host.Text.Trim(), int.TryParse(_port.Text, out var p) ? p : 7777, _account.Text.Trim());
        row2.AddChild(_connect);
        var options = new Button { Text = "Options", FocusMode = Control.FocusModeEnum.None };
        options.Pressed += () => OptionsRequested?.Invoke();
        row2.AddChild(options);
        _status = new Label { Text = "Not connected.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeColorOverride("font_color", muted);
        box.AddChild(_status);

    }

    public string Status => _status.Text;
    public IReadOnlyList<CharacterSummary> Characters => _list;
    public IReadOnlyList<string> Callings { get; private set; } = Array.Empty<string>();

    public void SetStatus(string text) { _status.Text = text; _entry?.Error(text); }

    // Drive the panel from code (the scripted smoke run does); these raise the same events as the buttons.
    public void ConnectTo(string host, int port, string account)
    {
        _host.Text = host;
        _port.Text = port.ToString();
        _account.Text = account;
        ConnectRequested?.Invoke(host, port, account);
    }

    public void Create(string name, string calling) => CreateRequested?.Invoke(name, calling);

    public void Enter(string id) => EnterRequested?.Invoke(id);

    public void ShowCharacters(CharacterListMsg msg)
    {
        _list = msg.Characters;
        Callings = msg.Callings;
        var stage = Content.Each<EntryFlowDef>().Single(f => f.Default).Stages[^1];
        var kind = msg.Entry?.Kind ?? stage.Kind;
        var definition = Content.Get<UiEntrySceneDef>(msg.Entry?.Presentation ?? stage.Presentation);
        if (definition is null) { Reset(); SetStatus("This entry stage needs a compatible client."); return; }
        var id = kind + ":" + definition.Id;
        if (_entry is null || _entryId != id)
        {
            _entry?.QueueFree();
            _entry = EntryViews.Create(kind, definition, Content, Appearance); _entryId = id;
            if (_entry is null) { Reset(); SetStatus("This entry activity needs a compatible client."); return; }
            _entry.Theme = _skin?.Theme;
            _entry.Suspended = () => !Visible || Suspended();
            _entry.CreateRequested += (name, calling) => CreateRequested?.Invoke(name, calling);
            _entry.EnterRequested += id => EnterRequested?.Invoke(id);
            _entry.LeaveRequested += () => LeaveRequested?.Invoke();
            _entry.OptionsRequested += () => OptionsRequested?.Invoke();
            AddChild(_entry);
        }
        _connection.Hide();
        _entry.ShowCharacters(msg);
    }
    public void FocusCharacter(string id) => _entry?.FocusCharacter(id);
    public void Reset()
    {
        _entry?.QueueFree(); _entry = null; _entryId = null; _connection.Show(); _list = new(); Callings = Array.Empty<string>();
    }
}
