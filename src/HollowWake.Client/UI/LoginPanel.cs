using Godot;
using HollowWake.Protocol;

namespace HollowWake.Client.UI;

/// <summary>Connect, log in, pick or create a character. Hidden once in the world.</summary>
public partial class LoginPanel : CanvasLayer
{
    private LineEdit _host = null!;
    private LineEdit _port = null!;
    private LineEdit _account = null!;
    private Button _connect = null!;
    private Label _status = null!;
    private ItemList _characters = null!;
    private LineEdit _newName = null!;
    private OptionButton _calling = null!;
    private Button _create = null!;
    private Button _enter = null!;
    private VBoxContainer _characterBox = null!;
    private List<CharacterSummary> _list = new();

    public event Action<string, int, string>? ConnectRequested;
    public event Action<string, string>? CreateRequested;
    public event Action<string>? EnterRequested;

    public override void _Ready()
    {
        Layer = 20;
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-260, -260);
        panel.CustomMinimumSize = new Vector2(520, 520);
        AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        var title = new Label { Text = "Hollow Wake MMO", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        box.AddChild(title);
        box.AddChild(new Label { Text = "the Weftmarches await", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) });

        var row = new HBoxContainer();
        box.AddChild(row);
        row.AddChild(new Label { Text = "Server" });
        _host = new LineEdit { Text = "127.0.0.1", CustomMinimumSize = new Vector2(220, 0) };
        row.AddChild(_host);
        _port = new LineEdit { Text = "7777", CustomMinimumSize = new Vector2(80, 0) };
        row.AddChild(_port);
        var row2 = new HBoxContainer();
        box.AddChild(row2);
        row2.AddChild(new Label { Text = "Account" });
        _account = new LineEdit { Text = "marcher", CustomMinimumSize = new Vector2(220, 0) };
        row2.AddChild(_account);
        _connect = new Button { Text = "Connect" };
        _connect.Pressed += () => ConnectRequested?.Invoke(_host.Text.Trim(), int.TryParse(_port.Text, out var p) ? p : 7777, _account.Text.Trim());
        row2.AddChild(_connect);
        _status = new Label { Text = "Not connected." };
        box.AddChild(_status);

        _characterBox = new VBoxContainer { Visible = false };
        box.AddChild(_characterBox);
        _characterBox.AddChild(new Label { Text = "Characters" });
        _characters = new ItemList { CustomMinimumSize = new Vector2(480, 160) };
        _characterBox.AddChild(_characters);
        _enter = new Button { Text = "Enter the Weft" };
        _enter.Pressed += () =>
        {
            var selected = _characters.GetSelectedItems();
            if (selected.Length > 0 && selected[0] < _list.Count)
            {
                EnterRequested?.Invoke(_list[selected[0]].Id);
            }
        };
        _characterBox.AddChild(_enter);
        _characterBox.AddChild(new HSeparator());
        var createRow = new HBoxContainer();
        _characterBox.AddChild(createRow);
        _newName = new LineEdit { PlaceholderText = "New character name", CustomMinimumSize = new Vector2(220, 0) };
        createRow.AddChild(_newName);
        _calling = new OptionButton();
        createRow.AddChild(_calling);
        _create = new Button { Text = "Create" };
        _create.Pressed += () =>
        {
            if (_newName.Text.Trim().Length >= 2 && _calling.Selected >= 0)
            {
                CreateRequested?.Invoke(_newName.Text.Trim(), _calling.GetItemText(_calling.Selected));
            }
        };
        createRow.AddChild(_create);
    }

    public string Status => _status.Text;
    public IReadOnlyList<CharacterSummary> Characters => _list;
    public IReadOnlyList<string> Callings { get; private set; } = Array.Empty<string>();

    public void SetStatus(string text) => _status.Text = text;

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
        _characters.Clear();
        foreach (var c in msg.Characters)
        {
            _characters.AddItem($"{c.Name}  ({c.Calling}, level {c.Level}, zone {c.Zone})");
        }
        if (msg.Characters.Count > 0)
        {
            _characters.Select(0);
        }
        _calling.Clear();
        foreach (var calling in msg.Callings)
        {
            _calling.AddItem(calling);
        }
        if (msg.Callings.Count > 0)
        {
            _calling.Select(0);
        }
        _characterBox.Visible = true;
    }
}
