using System.Text.Json.Nodes;
using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Pages;

/// <summary>What the Options pane remembers across a restyle: the page, the search, and the changes not yet confirmed.</summary>
public sealed class OptionsState
{
    public string Page { get; set; } = "";
    public string Search { get; set; } = "";

    /// <summary>The value each changed entry had when the pane opened, for Escape to undo.</summary>
    public Dictionary<string, JsonNode?> Originals { get; } = new(StringComparer.Ordinal);

    /// <summary>Entries changed this visit that take effect after a restart.</summary>
    public HashSet<string> NeedRestart { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The Options pane (docs/design/options.md), drawn from the ui_options content: a column of pages, the selected
/// page's groups with their entries as rows, a search over every page, resets for a row, a group and a page,
/// and the restart notice. Live entries take effect as the control moves; Escape undoes the changes not yet
/// confirmed, and Done confirms them. Key rows capture the next press, show conflicts within the action's
/// context and swap or clear the other binding.
/// </summary>
public partial class OptionsPane : LeafView
{
    private readonly Settings _settings;
    private readonly OptionsState _state;
    private readonly Func<string, double?> _facts;
    private readonly List<Row> _rows = new();
    private VBoxContainer _pageList = null!;
    private VBoxContainer _list = null!;
    private LineEdit _search = null!;
    private Label _notice = null!;
    private ScrollContainer _scroll = null!;
    private bool _dirty = true;
    private bool _ownChange;
    private (string Action, int Slot)? _capturing;
    private Button? _captureButton;
    private HBoxContainer? _conflict;

    /// <summary>Raised when the player confirms (Done); the interface closes the pane.</summary>
    public event Action? Confirmed;

    public OptionsPane(LeafContext context, Settings settings, OptionsState state, Func<string, double?> facts) : base(context)
    {
        _settings = settings;
        _state = state;
        _facts = facts;
        Name = "OptionsPane";
    }

    public override float PreferredWidth => Skin.Metric("options_width", 760);

    public bool Capturing => _capturing is not null;
    public string SelectedPage => _state.Page;
    public int RowCount => _rows.Count;

    private IEnumerable<OptionPage> Pages => _settings.Catalog.Pages.Where(p => GateExpr.Holds(p.Gate, Fact));

    private double? Fact(string name)
    {
        if (name.StartsWith("option.", StringComparison.Ordinal))
        {
            var id = name["option.".Length..];
            return _settings.Catalog.Entry(id)?.Kind switch
            {
                "toggle" => _settings.Toggle(id) ? 1 : 0,
                "slider" => _settings.Number(id),
                _ => _settings.Value(id) is null ? 0 : 1,
            };
        }
        return _facts(name);
    }

    public override void _Ready()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        AddChild(row);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
        row.AddChild(left);
        _pageList = new VBoxContainer();
        left.AddChild(_pageList);
        left.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        left.AddChild(MakeButton("Reset page", () =>
        {
            if (_state.Page.Length > 0)
            {
                Track(() => _settings.ResetPage(_state.Page));
            }
        }, tip: "Every entry on this page to its default"));
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(right);
        var top = new HBoxContainer();
        right.AddChild(top);
        _search = new LineEdit { PlaceholderText = "Search every page", Text = _state.Search, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClearButtonEnabled = true };
        _search.TextChanged += text =>
        {
            _state.Search = text;
            _dirty = true;
        };
        top.AddChild(_search);
        top.AddChild(MakeButton("Done", () => Confirmed?.Invoke(), tip: "Keep the changes and close"));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(_scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 4);
        _scroll.AddChild(_list);
        _notice = MakeLabel("", Skin.S("accent"), Skin.BodySize - 1, wrap: true);
        right.AddChild(_notice);
        _settings.Changed += OnChanged;
        if (_state.Page.Length == 0 || Pages.All(p => p.Id != _state.Page))
        {
            _state.Page = Pages.FirstOrDefault()?.Id ?? "";
        }
    }

    public override void _ExitTree() => _settings.Changed -= OnChanged;

    private void OnChanged(OptionEntry entry)
    {
        if (!_ownChange)
        {
            _dirty = true;
        }
        foreach (var r in _rows)
        {
            r.Refresh();
        }
    }

    /// <summary>Runs a change the pane makes, remembering each changed entry's original for Escape.</summary>
    private void Track(Action change, string? only = null)
    {
        var before = _settings.Catalog.Entries.Keys.ToDictionary(id => id, id => _settings.Value(id)?.DeepClone(), StringComparer.Ordinal);
        _ownChange = only is not null;
        try
        {
            change();
        }
        finally
        {
            _ownChange = false;
        }
        foreach (var (id, was) in before)
        {
            if (!JsonNode.DeepEquals(was, _settings.Value(id)))
            {
                _state.Originals.TryAdd(id, was);
                if (_settings.Catalog.Entry(id)?.Applies == "restart")
                {
                    _state.NeedRestart.Add(id);
                }
            }
        }
        UpdateNotice();
        if (only is null)
        {
            _dirty = true;
        }
    }

    private void UpdateNotice()
    {
        var restart = _state.NeedRestart.Select(id => _settings.Catalog.Entry(id)?.Name ?? id).ToList();
        var parts = new List<string>();
        if (_state.Originals.Count > 0)
        {
            parts.Add("Escape undoes the changes; Done keeps them.");
        }
        if (restart.Count > 0)
        {
            parts.Add($"After a restart: {string.Join(", ", restart)}.");
        }
        _notice.Text = string.Join(" ", parts);
    }

    /// <summary>Escape: a key capture ends, then unconfirmed changes are undone; with neither, the pane may close.</summary>
    public override bool OnEscape()
    {
        if (_capturing is not null)
        {
            EndCapture();
            return true;
        }
        if (_state.Originals.Count > 0)
        {
            var originals = _state.Originals.ToList();
            _state.Originals.Clear();
            foreach (var (id, value) in originals)
            {
                _settings.Set(id, value?.DeepClone());
            }
            _state.NeedRestart.Clear();
            _dirty = true;
            UpdateNotice();
            return true;
        }
        return false;
    }

    /// <summary>Confirms the visit's changes: nothing is left to undo.</summary>
    public void Confirm()
    {
        _state.Originals.Clear();
        UpdateNotice();
    }

    public override void Refresh()
    {
        if (!_dirty || _list is null)
        {
            return;
        }
        _dirty = false;
        RebuildPages();
        RebuildRows();
        UpdateNotice();
    }

    private void RebuildPages()
    {
        Clear(_pageList);
        foreach (var page in Pages)
        {
            var id = page.Id;
            var button = MakeButton(page.Name, () =>
            {
                _state.Page = id;
                _state.Search = "";
                _search.Text = "";
                _dirty = true;
            }, tip: page.Description);
            button.ToggleMode = true;
            button.ButtonPressed = id == _state.Page && _state.Search.Length == 0;
            button.Alignment = HorizontalAlignment.Left;
            _pageList.AddChild(button);
        }
    }

    private void RebuildRows()
    {
        EndCapture();
        Clear(_list);
        _rows.Clear();
        var query = _state.Search.Trim();
        var pages = query.Length > 0 ? Pages.ToList() : Pages.Where(p => p.Id == _state.Page).ToList();
        foreach (var page in pages)
        {
            foreach (var group in page.Groups)
            {
                var entries = group.Entries
                    .Where(e => GateExpr.Holds(e.Gate, Fact))
                    .Where(e => query.Length == 0 || e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (entries.Count == 0)
                {
                    continue;
                }
                var head = new HBoxContainer();
                var title = MakeLabel(query.Length > 0 ? $"{page.Name}: {group.Name}" : group.Name, Skin.S("accent"), Skin.TitleSize - 2, font: Skin.Title);
                title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                head.AddChild(title);
                var (pageId, groupId) = (page.Id, group.Id);
                head.AddChild(MakeButton("Reset group", () => Track(() => _settings.ResetGroup(pageId, groupId)), tip: $"Every entry of {group.Name} to its default"));
                _list.AddChild(head);
                if (group.Description.Length > 0)
                {
                    _list.AddChild(MakeLabel(group.Description, Skin.S("ink_muted"), Skin.BodySize - 2, wrap: true));
                }
                foreach (var entry in entries)
                {
                    var row = new Row(this, entry);
                    _list.AddChild(row.Root);
                    _rows.Add(row);
                }
                _list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
            }
        }
        if (_rows.Count == 0)
        {
            _list.AddChild(MakeLabel(query.Length > 0 ? $"Nothing matches \"{query}\"." : "Nothing to set here.", Skin.S("ink_muted")));
        }
    }

    // ------------------------------------------------------------ key capture

    private void BeginCapture(string action, int slot, Button button)
    {
        EndCapture();
        _capturing = (action, slot);
        _captureButton = button;
        button.Text = slot == 2 ? "Press a button" : "Press a key";
    }

    private void EndCapture()
    {
        _capturing = null;
        if (_captureButton is not null && IsInstanceValid(_captureButton))
        {
            _dirty = true;
        }
        _captureButton = null;
        if (_conflict is not null && IsInstanceValid(_conflict))
        {
            _conflict.QueueFree();
        }
        _conflict = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (_capturing is not { } capture)
        {
            return;
        }
        string? name = null;
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key when capture.Slot < 2:
                if (key.Keycode == Key.Escape)
                {
                    EndCapture();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (key.Keycode is Key.Delete or Key.Backspace)
                {
                    name = "";
                    break;
                }
                if (key.Keycode is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta)
                {
                    return;
                }
                name = KeyName.Compose(OS.GetKeycodeString(key.Keycode), key.ShiftPressed, key.CtrlPressed, key.AltPressed);
                break;
            case InputEventJoypadButton { Pressed: true } pad when capture.Slot == 2:
                name = InputSetup.PadName(pad.ButtonIndex);
                break;
            case InputEventJoypadMotion motion when capture.Slot == 2 && Math.Abs(motion.AxisValue) > 0.6f:
                name = InputSetup.PadName(motion.Axis, motion.AxisValue);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when capture.Slot == 2:
                EndCapture();
                GetViewport().SetInputAsHandled();
                return;
        }
        if (name is null)
        {
            return;
        }
        GetViewport().SetInputAsHandled();
        Bind(capture.Action, capture.Slot, name);
    }

    /// <summary>Binds a key, or asks what to do with the action that already holds it.</summary>
    internal void Bind(string action, int slot, string name)
    {
        var conflicts = name.Length == 0 ? new List<InputAction>() : _settings.Conflicts(action, name, pad: slot == 2).ToList();
        if (conflicts.Count == 0)
        {
            Track(() => _settings.Rebind(action, slot, name, swap: false));
            EndCapture();
            return;
        }
        var owner = _rows.FirstOrDefault(r => r.Entry.Action == action);
        var others = string.Join(", ", conflicts.Select(c => c.Name));
        _conflict = new HBoxContainer();
        _conflict.AddChild(MakeLabel($"{KeyName.Label(name)} is also bound to {others}.", Skin.S("bad", Skin.S("accent"))));
        _conflict.AddChild(MakeButton("Swap", () =>
        {
            Track(() => _settings.Rebind(action, slot, name, swap: true));
            EndCapture();
        }, tip: "The other action takes this action's old key"));
        _conflict.AddChild(MakeButton("Clear theirs", () =>
        {
            Track(() => _settings.Rebind(action, slot, name, swap: false));
            EndCapture();
        }, tip: "The other action loses the key"));
        _conflict.AddChild(MakeButton("Cancel", EndCapture));
        _capturing = null;
        if (owner is not null)
        {
            var index = owner.Root.GetIndex();
            _list.AddChild(_conflict);
            _list.MoveChild(_conflict, index + 1);
        }
        else
        {
            _list.AddChild(_conflict);
        }
    }

    /// <summary>The conflict prompt's buttons, for scripted checks.</summary>
    public IReadOnlyList<Button> ConflictButtons => _conflict is not null && IsInstanceValid(_conflict) ? _conflict.GetChildren().OfType<Button>().ToList() : Array.Empty<Button>();

    /// <summary>A row by entry id, for scripted checks.</summary>
    public Control? RowOf(string entryId) => _rows.FirstOrDefault(r => r.Entry.Id == entryId)?.Root;

    // ------------------------------------------------------------ rows

    private sealed class Row
    {
        private readonly OptionsPane _pane;
        private readonly Button _reset;
        private readonly Label _reason;
        private readonly List<Control> _controls = new();
        private readonly List<Button> _keys = new();

        public OptionEntry Entry { get; }
        public HBoxContainer Root { get; }

        public Row(OptionsPane pane, OptionEntry entry)
        {
            _pane = pane;
            Entry = entry;
            var skin = pane.Skin;
            Root = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, TooltipText = entry.Description };
            Root.AddThemeConstantOverride("separation", 10);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddChild(pane.MakeLabel(entry.Name, skin.S("ink")));
            if (entry.Description.Length > 0 && entry.Kind != "key")
            {
                var d = pane.MakeLabel(entry.Description, skin.S("ink_muted"), skin.BodySize - 3, wrap: true);
                d.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                text.AddChild(d);
            }
            _reason = pane.MakeLabel("", skin.S("bad", skin.S("ink_muted")), skin.BodySize - 3, wrap: true);
            text.AddChild(_reason);
            Root.AddChild(text);
            var control = Control();
            control.CustomMinimumSize = new Vector2(entry.Kind == "key" ? 300 : 240, 0);
            Root.AddChild(control);
            _reset = pane.MakeButton("Reset", () => pane.Track(() => pane._settings.Reset(Entry.Id)), tip: "Back to the default");
            Root.AddChild(_reset);
            Refresh();
        }

        private Settings S => _pane._settings;

        private Control Control()
        {
            var e = Entry;
            switch (e.Kind)
            {
                case "toggle":
                {
                    var check = new CheckButton { ButtonPressed = S.Toggle(e.Id), FocusMode = FocusModeEnum.None };
                    check.Toggled += on => _pane.Track(() => S.Set(e.Id, JsonValue.Create(on)), e.Id);
                    _controls.Add(check);
                    return check;
                }
                case "choice":
                {
                    var option = new OptionButton { FocusMode = FocusModeEnum.None };
                    var current = S.Text(e.Id);
                    for (var i = 0; i < e.Choices.Count; i++)
                    {
                        option.AddItem(e.Choices[i].Name, i);
                        if (e.Choices[i].Id == current)
                        {
                            option.Select(i);
                        }
                    }
                    option.ItemSelected += index =>
                    {
                        if (index >= 0 && index < e.Choices.Count)
                        {
                            _pane.Track(() => S.Set(e.Id, JsonValue.Create(e.Choices[(int)index].Id)), e.Id);
                        }
                    };
                    _controls.Add(option);
                    return option;
                }
                case "slider":
                {
                    var box = new HBoxContainer();
                    var slider = new HSlider { MinValue = e.Min, MaxValue = e.Max, Step = e.Step, Value = S.Number(e.Id), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None };
                    var value = _pane.MakeLabel(Format(S.Number(e.Id)), _pane.Skin.S("ink"), font: _pane.Skin.Numerals);
                    value.CustomMinimumSize = new Vector2(64, 0);
                    slider.ValueChanged += v =>
                    {
                        value.Text = Format(v);
                        _pane.Track(() => S.Set(e.Id, JsonValue.Create(Math.Round(v, 4))), e.Id);
                    };
                    box.AddChild(slider);
                    box.AddChild(value);
                    _controls.Add(slider);
                    return box;
                }
                case "key":
                {
                    var box = new HBoxContainer();
                    var binding = S.Bindings(e.Action ?? "");
                    for (var slot = 0; slot < 3; slot++)
                    {
                        var s = slot;
                        var held = binding.Slot(slot);
                        var button = _pane.MakeButton(held.Length == 0 ? "-" : slot == 2 ? held : KeyName.Label(held), () => { }, tip: slot switch { 0 => "Primary key", 1 => "Second key", _ => "Controller" });
                        button.CustomMinimumSize = new Vector2(92, 0);
                        button.Pressed += () => _pane.BeginCapture(e.Action ?? "", s, button);
                        box.AddChild(button);
                        _keys.Add(button);
                        _controls.Add(button);
                    }
                    return box;
                }
                case "color":
                {
                    var picker = new ColorPickerButton { Color = Rgba.TryParse(S.Text(e.Id), out var c) ? new Color(c.R, c.G, c.B, c.A) : Colors.White, FocusMode = FocusModeEnum.None };
                    picker.ColorChanged += col => _pane.Track(() => S.Set(e.Id, JsonValue.Create("#" + col.ToHtml(col.A < 0.999f))), e.Id);
                    _controls.Add(picker);
                    return picker;
                }
                default:
                {
                    var line = new LineEdit { Text = S.Text(e.Id), MaxLength = e.MaxLength };
                    line.TextSubmitted += t => _pane.Track(() => S.Set(e.Id, JsonValue.Create(t)), e.Id);
                    line.FocusExited += () => _pane.Track(() => S.Set(e.Id, JsonValue.Create(line.Text)), e.Id);
                    _controls.Add(line);
                    return line;
                }
            }
        }

        private string Format(double v)
        {
            var text = Math.Round(v, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return Entry.Unit.Length == 0 ? text : Entry.Unit.Length <= 2 ? text + Entry.Unit : $"{text} {Entry.Unit}";
        }

        /// <summary>Greys the row when its requirement fails and shows the reset while it differs from the default.</summary>
        public void Refresh()
        {
            if (!GodotObject.IsInstanceValid(Root))
            {
                return;
            }
            var requires = GateExpr.Holds(Entry.Requires, _pane.Fact);
            foreach (var c in _controls)
            {
                switch (c)
                {
                    case BaseButton b:
                        b.Disabled = !requires;
                        break;
                    case Godot.Slider r:
                        r.Editable = requires;
                        break;
                    case LineEdit l:
                        l.Editable = requires;
                        break;
                }
            }
            _reason.Text = requires ? "" : Entry.RequiresReason;
            _reason.Visible = !requires && Entry.RequiresReason.Length > 0;
            _reset.Visible = !S.IsDefault(Entry.Id);
            if (Entry.Kind == "key" && Entry.Action is { } action)
            {
                var b = S.Bindings(action);
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i] != _pane._captureButton)
                    {
                        var held = b.Slot(i);
                        _keys[i].Text = held.Length == 0 ? "-" : i == 2 ? held : KeyName.Label(held);
                    }
                }
            }
        }
    }

    internal new Label MakeLabel(string text, Color? color = null, int? size = null, bool wrap = false, Font? font = null) => base.MakeLabel(text, color, size, wrap, font);

    internal new Button MakeButton(string text, Action pressed, bool enabled = true, string? tip = null) => base.MakeButton(text, pressed, enabled, tip);

    internal new Skin Skin => base.Skin;
}
