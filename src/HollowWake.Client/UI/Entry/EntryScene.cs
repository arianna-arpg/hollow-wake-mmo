using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Appearance;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using HollowWake.Protocol;
using HollowWake.Core.Definitions;
using HollowWake.Client.UI.Loading;

namespace HollowWake.Client.UI.Entry;

/// <summary>Native adapter for a spatial-choice activity, using the same body plans and bindings as the world.</summary>
public partial class EntryScene(EntrySceneSpec spec, ContentRegistry content, AppearanceService appearance) : AccountEntryView
{
    public SpatialSelection Model { get; } = new(spec);
    private string? _pending, _notice;
    public override string? Pending => _pending;
    private SubViewport _viewport = null!;
    private Node3D _scene = null!;
    private Camera3D _camera = null!;
    private readonly List<BodyNode?> _bodies = new();
    private readonly List<Label> _names = new();
    private readonly List<Button> _choices = new();
    private EntrySpirit _spirit = null!;
    private HFlowContainer _roster = null!;
    private VBoxContainer _footer = null!;
    private Control _header = null!, _veil = null!;
    private PanelContainer _card = null!;
    private VBoxContainer _cardBody = null!;
    private Label _status = null!, _title = null!;
    private ProgressBar _progress = null!;
    private int _shown = -1, _request;
    private bool _wasActive, _movementArmed;
    private Control? _previousFocus;
    private GradientTexture2D? _background;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); FocusMode = FocusModeEnum.All; MouseFilter = MouseFilterEnum.Stop;
        _background = new GradientTexture2D { Width = 512, Height = 512, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new(.5f, .58f), FillTo = new(1, .58f), Gradient = new Gradient { Colors = [new Color(spec.Palette["glow"]), new Color(spec.Palette["floor"])] } };
        var background = new TextureRect { Texture = _background, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var surface = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(surface); surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _viewport = new SubViewport { TransparentBg = true, OwnWorld3D = true, HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        surface.AddChild(_viewport); _scene = new Node3D(); _viewport.AddChild(_scene);
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height, Far = 200 };
        _scene.AddChild(_camera);
        _scene.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Colors.Transparent, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(.55f, .58f, .66f), AmbientLightEnergy = 1 } });
        _scene.AddChild(new DirectionalLight3D { RotationDegrees = new(-55, -30, 0), LightEnergy = 1 });
        _spirit = new EntrySpirit { Model = Model, Camera = _camera, Spec = spec,
            Look = LoadingSpec.FromMeta(content.Get<UiLoadingScreenDef>(spec.WispSource)!.Meta!).Wisp with { Radius = (float)spec.WispRadius },
            ReducedMotion = () => appearance.ReducedMotion };
        AddChild(_spirit); _spirit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _header = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_header);
        _title = new Label { Text = spec.Labels["brand"] + "\n" + spec.Labels["title"], Position = new(0, 0) };
        _title.AddThemeColorOverride("font_color", new Color(spec.Palette["ink"])); _header.AddChild(_title);
        var actions = new HBoxContainer(); _header.AddChild(actions); actions.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight); actions.GrowHorizontal = GrowDirection.Begin;
        actions.AddChild(Button(spec.Labels["options"], () => { Model.Suspend(); RequestOptions(); }));
        actions.AddChild(Button(spec.Labels["disconnect"], () => RequestLeave()));
        _footer = new VBoxContainer(); _footer.AddThemeConstantOverride("separation", 8); AddChild(_footer);
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center }; _footer.AddChild(_status);
        _progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new(spec.Layout["progressWidth"], spec.Layout["progressHeight"]), SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; _footer.AddChild(_progress);
        _footer.AddChild(new Label { Text = spec.Labels["prompt"], HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, spec.Layout["rosterHeight"]), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _footer.AddChild(scroll);
        _roster = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = FlowContainer.AlignmentMode.Center }; scroll.AddChild(_roster);
        _veil = new ColorRect { Color = new Color(0, 0, 0, .45f), Visible = false, MouseFilter = MouseFilterEnum.Stop }; AddChild(_veil); _veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop }; AddChild(_card);
        var style = new StyleBoxFlat { BgColor = new Color(spec.Palette["panel"]), BorderColor = new Color(spec.Palette["border"]) }; style.SetBorderWidthAll(1); style.SetContentMarginAll(spec.Layout["cardPadding"]); _card.AddThemeStyleboxOverride("panel", style);
        _cardBody = new VBoxContainer(); _cardBody.AddThemeConstantOverride("separation", (int)spec.Layout["cardGap"]); _card.AddChild(_cardBody);
        GuiInput += GroundInput;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text }; button.Pressed += action; return button;
    }
    public override void ShowCharacters(CharacterListMsg list)
    {
        // A delayed reply for a dismissed card must not disturb a newer request or its card.
        if (Pending is not null && list.RequestId is not null && list.RequestId != Pending) return;
        _notice = null;
        var wake = Pending is not null && list.RequestId == Pending ? list.CreatedCharacterId : null;
        _pending = null;
        foreach (var b in _bodies) b?.QueueFree(); _bodies.Clear();
        foreach (var n in _names) n.QueueFree(); _names.Clear();
        foreach (var b in _choices) { _roster.RemoveChild(b); b.QueueFree(); } _choices.Clear();
        Model.SetChoices(EntryChoices.Build(list, content, spec));
        foreach (var (seat, index) in Model.Seats.Select((s, i) => (s, i)))
        {
            var body = Body(seat.Choice.Look, index + 1, spec.BodyHeight);
            if (body is not null) body.Position = new((float)(seat.X / spec.Units), 0, (float)(seat.Y / spec.Units));
            _bodies.Add(body);
            var label = new Label { Text = seat.Choice.Name, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeColorOverride("font_color", new Color(seat.Choice.Available ? spec.Palette["accent"] : spec.Palette["muted"])); AddChild(label); MoveChild(label, _footer.GetIndex()); _names.Add(label);
            var b = Button(seat.Choice.Name, () => { Model.Open(index); UpdateCard(); }); b.TooltipText = seat.Choice.Description;
            _roster.AddChild(b); _choices.Add(b);
        }
        _shown = -1; _card.Hide(); _veil.Hide(); _status.Text = Model.Seats.Count == 0 ? spec.Labels["empty"] : "";
        if (IsVisibleInTree() && !Suspended()) GrabFocus();
        if (wake is not null && list.Characters.Any(c => c.Id == wake)) RequestEnter(wake);
    }
    private BodyNode? Body(string look, long key, double height)
    {
        var def = appearance.Catalog.Resolve(look, look, HollowWake.Core.Tags.TagSet.Of("humanoid")); if (def is null) return null;
        var plan = appearance.PlanFor(new EntityState { Look = look, Pattern = look, Key = key, Height = (float)height, Radius = (float)spec.BodyWidth }, def, false, out _);
        var body = new BodyNode(plan); _scene.AddChild(body); body.Apply(new Pose { Opacity = (float)spec.ApparitionOpacity }); return body;
    }
    public override void FocusCharacter(string id)
    {
        var i = Model.Seats.FindIndex(s => s.Choice.Character == id); if (i >= 0 && Model.Opened < 0 && IsVisibleInTree() && !Suspended()) _choices[i].GrabFocus();
    }
    public override void Error(string text)
    {
        _notice = text; _status.Text = text;
        if (Pending is null) return;
        _pending = null; _shown = -1; UpdateCard();
    }
    private void CloseCard() { _notice = null; _status.Text = ""; _pending = null; Model.Close(); UpdateCard(); }
    private void UpdateCard()
    {
        if (_shown == Model.Opened) return;
        _shown = Model.Opened; _movementArmed = false;
        if (_shown >= 0 && !_card.Visible) _previousFocus = GetViewport().GuiGetFocusOwner();
        foreach (var child in _cardBody.GetChildren()) { _cardBody.RemoveChild(child); child.QueueFree(); }
        _card.Visible = _veil.Visible = _shown >= 0;
        if (_shown < 0) { if (IsInstanceValid(_previousFocus)) _previousFocus?.GrabFocus(); else GrabFocus(); return; }
        var choice = Model.Seats[_shown].Choice;
        var title = new Label { Text = choice.Name, HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", (int)spec.Layout["cardTitleSize"]); _cardBody.AddChild(title);
        _cardBody.AddChild(new Label { Text = choice.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center });
        var field = new LineEdit { Name = "MuName", PlaceholderText = spec.Labels["name"], MaxLength = 24 };
        if (choice.Character is null && choice.Available) _cardBody.AddChild(field);
        var wake = Button(choice.Character is not null ? spec.Labels["wake"] : spec.Labels["create"], () => {});
        wake.Name = "MuWake";
        wake.Disabled = !choice.Available || Pending is not null; _cardBody.AddChild(wake);
        void Wake()
        {
            if (!choice.Available || Pending is not null) return;
            _notice = null; _status.Text = "";
            if (choice.Character is not null) { RequestEnter(choice.Character); return; }
            if (field.Text.Trim().Length < 2) { field.GrabFocus(); return; }
            _pending = "entry-" + ++_request; wake.Disabled = true; field.Editable = false; _status.Text = spec.Labels["pending"];
            RequestCreate(field.Text.Trim(), choice.Calling);
        }
        wake.Pressed += Wake; field.TextSubmitted += _ => Wake();
        var close = Button(spec.Labels["back"], CloseCard); _cardBody.AddChild(close);
        var controls = _cardBody.GetChildren().OfType<Control>().Where(c => c.FocusMode == FocusModeEnum.All && c is not Button { Disabled: true }).ToArray();
        for (var i = 0; i < controls.Length; i++)
        {
            var previous = controls[i].GetPathTo(controls[(i + controls.Length - 1) % controls.Length]);
            var next = controls[i].GetPathTo(controls[(i + 1) % controls.Length]);
            controls[i].FocusPrevious = controls[i].FocusNeighborTop = controls[i].FocusNeighborLeft = previous;
            controls[i].FocusNext = controls[i].FocusNeighborBottom = controls[i].FocusNeighborRight = next;
        }
        if (controls.Length > 0) controls[0].GrabFocus();
        if (field.GetParent() is null) field.Free();
    }
    private void GroundInput(InputEvent e)
    {
        if (Suspended() || Model.Opened >= 0) return;
        Vector2? point = e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse ? mouse.Position
            : e is InputEventScreenTouch { Pressed: true } touch ? touch.Position : null;
        if (point is not { } p) return;
        var origin = _camera.ProjectRayOrigin(p); var direction = _camera.ProjectRayNormal(p);
        if (Math.Abs(direction.Y) < .001) return;
        var world = origin + direction * (-origin.Y / direction.Y); Model.MoveTo(world.X * spec.Units, world.Z * spec.Units); GrabFocus(); AcceptEvent();
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (Suspended() || !IsVisibleInTree()) return;
        if (e.IsActionPressed("ui_cancel") && Model.Opened >= 0) { CloseCard(); GetViewport().SetInputAsHandled(); }
    }
    public override void _Process(double dt)
    {
        var active = IsVisibleInTree() && !Suspended();
        _viewport.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (!active) { Model.Suspend(); _wasActive = _movementArmed = false; return; }
        if (!_wasActive) Model.Suspend(); _wasActive = true;
        var typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        double Held(string action) => Input.IsActionPressed(action) ? 1 : 0;
        var dx = Held("move_right") - Held("move_left"); var dy = Held("move_back") - Held("move_forward");
        if (dx == 0 && dy == 0 && Model.Opened < 0) _movementArmed = true;
        Model.Step(dt, dx, dy, typing || !GetWindow().HasFocus() || !_movementArmed);
        UpdateCard();
        _header.Position = new(24, 20); _header.Size = new(Math.Max(0, Size.X - 48), 90);
        _title.AddThemeFontSizeOverride("font_size", Size.X < 600 ? 20 : 28);
        _footer.Size = new(Math.Max(0, Size.X - 32), 0); _footer.Position = new(16, Math.Max(110, Size.Y - _footer.Size.Y - 16));
        _card.Size = new(Math.Min(spec.Layout["cardWidth"], Math.Max(250, Size.X - 32)), 0); _card.Position = (Size - _card.Size) / 2;
        _camera.Size = (float)(spec.CameraSpan / spec.Units);
        var x = (float)(Model.X / spec.Units); var z = (float)(Model.Y / spec.Units);
        _camera.Position = new(x, (float)spec.CameraHeight, z + (float)spec.CameraOffset); _camera.LookAt(new Vector3(x, 0, z - (float)spec.CameraLead));
        for (var i = 0; i < Model.Seats.Count; i++)
        {
            var s = Model.Seats[i]; if (_bodies[i] is { } body) body.Rotation = new(0, (float)-s.Yaw, 0);
            var point = _camera.UnprojectPosition(new((float)(s.X / spec.Units), (float)spec.BodyHeight + .2f, (float)(s.Y / spec.Units)));
            _names[i].Position = point - new Vector2(_names[i].Size.X / 2, _names[i].Size.Y);
        }
        _spirit.Time += Math.Min(dt, spec.MaxFrame); _spirit.QueueRedraw();
        _progress.Value = Model.Progress; _progress.Visible = Model.Engaged >= 0 && Model.Opened < 0;
        if (_notice is null && Model.Engaged >= 0 && Model.Opened < 0) _status.Text = Model.Seats[Model.Engaged].Choice.Name;
    }
    public override void _ExitTree() { _background?.Gradient?.Dispose(); _background?.Dispose(); }
}
