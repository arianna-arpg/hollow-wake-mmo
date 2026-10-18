using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Loading;

/// <summary>Reusable loading cover. Work owns a lease; this node owns only presentation and input.</summary>
public partial class LoadingScreen : CanvasLayer
{
    public LoadingSpec Spec { get; private set; }
    public LoadingSession Session { get; } = new();
    public SpiritRun? Run { get; private set; }
    public Func<bool> ReducedMotion { get; set; } = () => false;
    public Action ClearWorldInput { get; set; } = ReleaseActions;
    public bool Active => Session.Active;
    private readonly Dictionary<string, Func<string, uint, SpiritRun>> _activities = new();
    private uint _sequence;
    private Control _root = null!;
    private CrossingView _view = null!;
    private Label _status = null!, _detail = null!;
    private ProgressBar _progress = null!;
    private Button _retry = null!, _cancel = null!;
    private readonly HashSet<Key> _held = new();
    private bool _padQuarantined, _padConfirm;
    private double? _pointer;
    private Control? _previousFocus;
    private SystemFont? _titleFont;
    private Key[] _negativeX = Array.Empty<Key>(), _positiveX = Array.Empty<Key>(), _negativeY = Array.Empty<Key>(), _positiveY = Array.Empty<Key>();
    public bool BlocksWorldInput => Active || _held.Count > 0 || _padQuarantined;

    public LoadingScreen(LoadingSpec spec)
    {
        Spec = spec;
        _activities["spirit_run"] = (direction, seed) => new(Spec, direction, seed);
    }
    public void UseSpec(LoadingSpec spec)
    {
        Session.Close(); Run = null; _root.Free(); _titleFont?.Dispose(); Spec = spec; _Ready();
    }
    public override void _ExitTree() => _titleFont?.Dispose();
    public override void _Ready()
    {
        Key[] Keys(string[] codes) => codes.Select(code => Enum.Parse<Key>(code.StartsWith("Key",StringComparison.Ordinal) ? code[3..] : code[5..])).ToArray();
        _negativeX=Keys(Spec.Controls.NegativeX); _positiveX=Keys(Spec.Controls.PositiveX);
        _negativeY=Keys(Spec.Controls.NegativeY); _positiveY=Keys(Spec.Controls.PositiveY);
        Layer = 100;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, FocusMode = Control.FocusModeEnum.All };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var background = new ColorRect { Color = new Color(Spec.Palette["floor"]), MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _root.AddChild(background);
        _view = new CrossingView { OwnerScreen = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_view);
        var head = new VBoxContainer { Position = new Vector2(48, 32), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(head);
        var brand = new HBoxContainer();
        head.AddChild(brand);
        brand.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        brand.AddChild(new Label { Text = Spec.Text("brand") });
        var title = new Label { Text = Spec.Text("title"), TooltipText = Spec.Text("accessibleTitle") };
        _titleFont = new SystemFont { FontNames = new[] { "Segoe UI Symbol", "Noto Sans Runic", "serif" } };
        title.AddThemeFontOverride("font", _titleFont);
        title.AddThemeFontSizeOverride("font_size", 36); title.AddThemeColorOverride("font_color", new Color(Spec.Palette["wisp"]));
        head.AddChild(title);
        var footer = new VBoxContainer { AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 48, OffsetRight = -48, OffsetTop = -148, OffsetBottom = -24 };
        _root.AddChild(footer);
        var row = new HBoxContainer();
        footer.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        _status = new Label(); _detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detail.AddThemeFontSizeOverride("font_size", 12);
        text.AddChild(_status); text.AddChild(_detail);
        _retry = new Button { Text = Spec.Text("retry"), CustomMinimumSize = new Vector2(90, 36) };
        _cancel = new Button { Text = Spec.Text("cancel"), CustomMinimumSize = new Vector2(90, 36) };
        _retry.Pressed += () => Session.Retry?.Invoke();
        _cancel.Pressed += () => Session.Cancel?.Invoke();
        row.AddChild(_retry); row.AddChild(_cancel);
        _progress = new ProgressBar { CustomMinimumSize = new Vector2(260, 5), ShowPercentage = false, MaxValue = 1 };
        footer.AddChild(_progress);
        footer.AddChild(new Label { Text = Spec.Text("hint") });
        var controls = new Label { Text = Spec.Text("controls") };
        controls.AddThemeFontSizeOverride("font_size", 11); footer.AddChild(controls);
        _root.Visible = false;
    }
    public LoadingSession.Lease Begin(string label, string kind = "entry", Action? cancel = null, string? direction = null)
    {
        _previousFocus ??= GetViewport().GuiGetFocusOwner();
        var seed = unchecked(Spec.Seed + ++_sequence * Spec.SeedStep);
        var rng = new LoadingRng(seed);
        direction ??= kind == "entry" ? "down" : rng.Next() < .5 ? "right" : "left";
        Run = _activities[Spec.Activity](direction, seed);
        _pointer = null; _padQuarantined = true;
        _padConfirm = !InputSetup.IgnorePads && Input.GetConnectedJoypads().Any(p => Input.IsJoyButtonPressed(p, JoyButton.A));
        ClearWorldInput();
        var lease = Session.Begin(label, cancel);
        _root.Visible = true; _root.GrabFocus();
        Refresh();
        return lease;
    }
    public void Close() => Session.Close();
    public async Task Paint()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    public static void ReleaseActions()
    {
        foreach (var action in InputMap.GetActions())
            if (Input.IsActionPressed(action)) Input.ActionRelease(action);
    }
    private bool PadNeutral()
    {
        if (InputSetup.IgnorePads) return true;
        foreach (var pad in Input.GetConnectedJoypads())
        {
            for (var axis = 0; axis < (int)JoyAxis.Max; axis++)
                if (Math.Abs(Input.GetJoyAxis(pad, (JoyAxis)axis)) > Spec.Controls.Deadzone) return false;
            for (var button = 0; button < (int)JoyButton.Max; button++)
                if (Input.IsJoyButtonPressed(pad, (JoyButton)button)) return false;
        }
        return true;
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key)
        {
            var code = key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode;
            if (!key.Pressed) { if (_held.Remove(code)) GetViewport().SetInputAsHandled(); }
            else if (Active) { _held.Add(code); _pointer = null; }
            else if (_held.Contains(code)) { GetViewport().SetInputAsHandled(); return; }
            if (Active && key.Keycode == Key.Tab)
            {
                if (key.Pressed)
                {
                    var buttons = new[] { _retry, _cancel }.Where(b => b.Visible).ToArray();
                    if (buttons.Length == 0) _root.GrabFocus();
                    else
                    {
                        var index = Array.IndexOf(buttons, GetViewport().GuiGetFocusOwner() as Button);
                        var next = index < 0 ? key.ShiftPressed ? buttons.Length - 1 : 0
                            : (index + (key.ShiftPressed ? -1 : 1) + buttons.Length) % buttons.Length;
                        buttons[next].GrabFocus();
                    }
                }
                GetViewport().SetInputAsHandled(); return;
            }
            if (Active && key.Keycode is Key.Enter or Key.Space && GetViewport().GuiGetFocusOwner() is Button focused && _root.IsAncestorOf(focused))
                return; // Only the cover's focused buttons receive GUI activation.
        }
        if (!Active) return;
        // Let the focused cover buttons receive their GUI touch events.
        if (ev is InputEventScreenTouch buttonTouch &&
            ((_retry.Visible && _retry.GetGlobalRect().HasPoint(buttonTouch.Position)) ||
             (_cancel.Visible && _cancel.GetGlobalRect().HasPoint(buttonTouch.Position)))) return;
        if (ev is InputEventMouseMotion motion) Point(motion.Position);
        if (ev is InputEventScreenTouch touch && touch.Pressed) Point(touch.Position);
        if (ev is InputEventScreenDrag drag) Point(drag.Position);
        if (ev is InputEventKey or InputEventJoypadButton or InputEventJoypadMotion or InputEventScreenTouch or InputEventScreenDrag)
            GetViewport().SetInputAsHandled();
    }
    private void Point(Vector2 at)
    {
        if (Run is null) return;
        _pointer = new SpiritLayout(_view.Size.X, _view.Size.Y, Run.Direction, Spec.Rules).Lane(at.X - _view.Position.X, at.Y - _view.Position.Y);
    }
    private void Refresh()
    {
        _status.Text = Session.Label; _detail.Text = Session.Detail;
        _retry.Visible = Session.Retry is not null; _cancel.Visible = Session.Cancel is not null;
        _progress.Indeterminate = Session.Fraction is null;
        _progress.Value = Session.Fraction ?? 0;
    }
    public override void _Process(double delta)
    {
        if (!Active)
        {
            if (_root.Visible)
            {
                _root.Visible = false; Run = null; ClearWorldInput();
                if (GodotObject.IsInstanceValid(_previousFocus) && _previousFocus!.IsInsideTree()) _previousFocus.GrabFocus();
                _previousFocus = null;
            }
            _held.RemoveWhere(k => !Input.IsPhysicalKeyPressed(k));
            if (_padQuarantined && PadNeutral()) _padQuarantined = false;
            return;
        }
        Refresh();
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is not null && focus != _root && !_root.IsAncestorOf(focus)) _root.GrabFocus();
        var size = GetViewport().GetVisibleRect().Size;
        _view.Position = new Vector2(0, size.Y < 480 ? 80 : 108);
        _view.Size = new Vector2(size.X, Math.Max(1, size.Y - (size.Y < 480 ? 225 : 270)));
        var down = Run!.Direction == "down";
        var negative = down ? _negativeX : _negativeY;
        var positive = down ? _positiveX : _positiveY;
        foreach (var k in negative.Concat(positive)) if (Input.IsPhysicalKeyPressed(k)) _held.Add(k);
        double axis = (positive.Any(Input.IsPhysicalKeyPressed) ? 1 : 0) - (negative.Any(Input.IsPhysicalKeyPressed) ? 1 : 0);
        var pads = InputSetup.IgnorePads ? new Godot.Collections.Array<int>() : Input.GetConnectedJoypads();
        if (pads.Count > 0)
        {
            var pad = pads[0];
            var stick = Input.GetJoyAxis(pad, down ? JoyAxis.LeftX : JoyAxis.LeftY);
            var dpad = (Input.IsJoyButtonPressed(pad, down ? JoyButton.DpadRight : JoyButton.DpadDown) ? 1 : 0)
                - (Input.IsJoyButtonPressed(pad, down ? JoyButton.DpadLeft : JoyButton.DpadUp) ? 1 : 0);
            if (Math.Abs(stick) > Spec.Controls.Deadzone || dpad != 0) { axis = dpad != 0 ? dpad : stick; _pointer = null; }
            var confirm = Input.IsJoyButtonPressed(pad, JoyButton.A);
            if (GetWindow().HasFocus() && confirm && !_padConfirm) (Session.Retry ?? Session.Cancel)?.Invoke();
            _padConfirm = confirm;
        }
        if (!Active) return;
        if (GetWindow().HasFocus()) Run!.Step(delta, axis, _pointer);
        _view.QueueRedraw();
    }
}
