using Godot;
using HollowWake.Client.World.Appearance;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

/// <summary>
/// A server entity as drawn: a built body from the appearance content (painted geometry or a directional card,
/// chosen through the bodies option and the camera) posed by the shared animator from the state the server
/// sends, a contact mark on the ground, a name and a life bar above it, and smoothing between snapshots.
/// A projectile, area or field the server names a shape for is drawn as that shape (a slab for a projectile, a
/// flat mark for the rest), facing its heading and turned by its spin; a telegraph is a ground mark that fades
/// in, breathes, and fades out when its strike lands. Nothing here decides anything: every pose and flash
/// follows a fact on the wire.
/// </summary>
public partial class EntityView : Node3D
{
    public static bool ShowLegacyCast { get; set; } = true;
    public static bool ConfirmedCastCues { get; set; }
    private const float BarWidth = 1.2f;

    /// <summary>How long a telegraph takes to fade once its strike lands.</summary>
    public const float FadeSeconds = 0.35f;

    private bool _shaped;
    private StandardMaterial3D? _fade;
    private StandardMaterial3D? _fillMaterial;
    private MeshInstance3D _fill = null!;
    private float _fadeAlpha;
    private double _bornAt;
    private double _bornServer;
    private double? _dyingAt;

    /// <summary>The server's clock now as the client estimates it, or null before it knows; marks that end at a server time read it.</summary>
    public static Func<double?>? ServerNow { get; set; }

    /// <summary>The appearance pipeline every view draws with; null draws plain blocks.</summary>
    public static AppearanceService? Appearance { get; set; }

    /// <summary>Where the hero stands, for the reach prop labels show within.</summary>
    public static Func<Vector3?>? HeroPosition { get; set; }

    /// <summary>The entity under the cursor, whose label always shows.</summary>
    public static long HoveredKey { get; set; }

    /// <summary>The self's cast as the interface knows it (mode and progress), richer than the entity state's.</summary>
    public static Func<(string Mode, float Progress)?>? SelfCast { get; set; }

    /// <summary>Where the animator's moments go as sound cues: a footstep on a surface, a swing, a death. Null raises none.</summary>
    public static Action<string, string?, Vector3>? Cue { get; set; }

    /// <summary>The surface tag under a world point (grass, sand, stone ...), for the footstep variant; null when unknown.</summary>
    public static Func<Vector3, string?>? SurfaceAt { get; set; }

    private MeshInstance3D _mesh = null!;
    private Label3D _label = null!;
    private Node3D _overlay = null!;
    private MeshInstance3D _lifeBar = null!;
    private MeshInstance3D _lifeBack = null!;
    private Vector3 _targetPosition;
    private Vector3 _velocity;
    private float _targetYaw;
    private double _lastUpdate;

    // the built body
    private IBodyView? _body;
    private BodyPlan? _plan;
    private AppearanceDef? _look;
    private string _gearKey = "";
    private CardAtlas? _atlas;
    private ContactMark? _contact;
    private readonly VisualState _visual = new();
    private readonly Pose _pose = new();
    private Vector3 _lastDrawn;
    private bool _hasDrawn;
    private float _speed;
    private double _releasedAt = double.NegativeInfinity;
    private double _hitAt = double.NegativeInfinity;
    private float _hitStrength;
    private float _hitFrom = float.NaN;
    private double _diedAt = double.NegativeInfinity;
    private bool _wasCasting;
    private bool _wasDead;
    private bool _settled;
    private string _selfEquipmentKey = "";
    private float _labelAlpha = 1;

    public long Key { get; }
    public EntityState State { get; private set; }
    public bool IsSelf { get; }

    /// <summary>Triangles the body draws, for the measurement line.</summary>
    public int Triangles => _body?.Triangles ?? 0;

    /// <summary>Whether the body is drawn as a card this frame.</summary>
    public bool IsCard => _body is CardNode;

    /// <summary>The drawn body's height, for labels and picking.</summary>
    public float DrawnHeight => _plan?.Height ?? State.Height;

    public EntityView(EntityState state, bool isSelf)
    {
        Key = state.Key;
        State = state;
        IsSelf = isSelf;
        Name = $"e{state.Key}";
    }

    public override void _Ready()
    {
        _mesh = new MeshInstance3D { Name = "shape", Visible = false };
        AddChild(_mesh);
        _fill = new MeshInstance3D { Name = "fill", Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_fill);
        _label = new Label3D
        {
            Name = "label",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            // Fixed size keeps names legible from the isometric camera 120 units out as well as up close.
            FixedSize = true,
            FontSize = 30,
            PixelSize = 0.0011f,
            OutlineSize = 6,
            NoDepthTest = true,
        };
        AddChild(_label);
        // The overlay is turned to face the camera every frame, so its children are laid out in a
        // screen-aligned space: the life bar hangs from its left edge and scales with the life left.
        _overlay = new Node3D { Name = "overlay" };
        AddChild(_overlay);
        _lifeBack = new MeshInstance3D
        {
            Name = "lifeback",
            Mesh = new QuadMesh { Size = new Vector2(BarWidth + 0.04f, 0.14f) },
            MaterialOverride = Unlit(new Color(0.08f, 0.08f, 0.08f, 0.85f), 0),
        };
        _lifeBar = new MeshInstance3D
        {
            Name = "life",
            Mesh = new QuadMesh { Size = new Vector2(BarWidth, 0.1f), CenterOffset = new Vector3(BarWidth * 0.5f, 0, 0) },
            MaterialOverride = Unlit(new Color(0.85f, 0.2f, 0.2f, 1f), 1),
            Position = new Vector3(-BarWidth * 0.5f, 0, 0.01f),
        };
        _overlay.AddChild(_lifeBack);
        _overlay.AddChild(_lifeBar);
        Rebuild();
        RefreshOverlay();
    }

    private static StandardMaterial3D Unlit(Color c, int priority) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = priority,
    };

    /// <summary>Whether the body is a telegraph mark, which lingers to fade out when it goes.</summary>
    public bool IsTelegraph => _fade is not null;

    /// <summary>
    /// The size the mouse picks, matching what is drawn rather than the simulation radius (a marker's radius is its
    /// whole place); null never under the cursor (fields, projectiles, shapes, marks).
    /// </summary>
    public (float Radius, float Height)? PickSize { get; private set; }

    /// <summary>The telegraph's strike landed (the world let it go): the mark fades out and then frees itself.</summary>
    public void Dying()
    {
        _released = true;
        _dyingAt ??= Time.GetTicksMsec() / 1000.0;
    }

    /// <summary>The world no longer holds this view; it frees itself once faded.</summary>
    private bool _released;

    /// <summary>The body's turn about the vertical: a shape faces its heading (else the yaw) plus its spin; a body faces its yaw, forward along its x.</summary>
    private float Facing(EntityState s) => _shaped ? (s.Heading ?? s.Yaw) + (s.Spin ?? 0) : s.Yaw;

    /// <summary>A shaped body: the delivery's shape as a slab (a projectile) or a flat mark (an area, a field, a telegraph).</summary>
    private void BuildShape(EntityState s, string look)
    {
        PickSize = null;
        DropBody();
        var size = s.ShapeSize;
        var spec = UI.Logic.ShapeOutline.SpecOf(s.Shape ?? "circle", size?.Radius, size?.Angle, size?.Width, size?.Inner, Mathf.Max(0.1f, s.Radius), size?.Curvature);
        var telegraph = look == "telegraph";
        var projectile = !telegraph && (look == "projectile" || s.Pattern == "projectile");
        var thickness = projectile ? Mathf.Clamp(s.Height * 0.4f, 0.12f, 0.4f) : 0f;
        _mesh.Visible = true;
        _mesh.Mesh = ShapeMesh.Build(UI.Logic.ShapeOutline.RegionOf(spec), thickness);
        var color = telegraph ? new Color(1f, 0.35f, 0.21f) : ColorFor(s);
        var alpha = telegraph ? 0.5f : projectile ? 1f : 0.35f;
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(color, telegraph ? 0 : alpha),
            Transparency = alpha < 1 || telegraph ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            ShadingMode = telegraph ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            Roughness = 0.8f,
        };
        _mesh.MaterialOverride = material;
        _mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _fade = telegraph ? material : null;
        _fadeAlpha = alpha;
        _mesh.Position = new Vector3(0, projectile ? Mathf.Max(0.1f, s.Height * 0.5f) - thickness / 2 : telegraph ? 0.07f : 0.05f, 0);
        // A telegraph's fill grows from its anchor to the whole mark as the strike nears.
        _fill.Visible = telegraph;
        if (telegraph)
        {
            _fillMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(color, 0),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                RenderPriority = 1,
            };
            _fill.Mesh = _mesh.Mesh;
            _fill.MaterialOverride = _fillMaterial;
            _fill.Position = _mesh.Position + new Vector3(0, 0.01f, 0);
        }
        else
        {
            _fillMaterial = null;
        }
        _label.Position = new Vector3(0, s.Height + 0.5f, 0);
        _overlay.Visible = false;
        RefreshLabel();
    }

    private void DropBody()
    {
        _body?.Release();
        _body = null;
        _plan = null;
        _atlas = null;
        if (_contact is not null)
        {
            _contact.QueueFree();
            _contact = null;
        }
    }

    private void Rebuild()
    {
        var s = State;
        var look = s.Look ?? s.Pattern ?? "";
        _shaped = s.Shape is not null || look == "telegraph";
        if (_shaped)
        {
            BuildShape(s, look);
            return;
        }
        _fade = null;
        _fill.Visible = false;
        _mesh.Visible = false;
        DropBody();
        var service = Appearance;
        _look = service?.LookFor(s) ?? new AppearanceDef { Id = "block", Body = "block" };
        if (service is not null)
        {
            _plan = service.PlanFor(s, _look, IsSelf, out _gearKey);
            _selfEquipmentKey = IsSelf ? EquipmentKey(service) : "";
        }
        else
        {
            _plan = BodyFamilies.Build(_look, s.Key, s.Height, s.Radius);
            _gearKey = _look.Id;
        }
        _visual.Rig = _plan.Rig;
        _visual.Swings = _plan.Swings;
        _visual.Height = _plan.Height;
        _visual.Hover = _plan.Hover;
        _visual.Key = s.Key;
        var geometry = new BodyNode(_plan);
        AddChild(geometry);
        geometry.SetShadows(service?.Shadows ?? true);
        _body = geometry;
        // the body stands at the hover height, the shape is picked by the drawn size
        var isDrop = look.StartsWith("drop:", StringComparison.Ordinal);
        PickSize = _look.Body is "orb" ? null : (Mathf.Max(0.3f, _plan.Width * 0.5f), Mathf.Max(0.3f, _plan.Height + _plan.Hover));
        var creature = s.Resources.Count > 0;
        if (creature || isDrop || s.Interactable is not null || _plan.Rig == "tree")
        {
            var relation = service?.Catalog.RelationOf(s.Faction) ?? "neutral";
            var contact = service?.Motion.Contact ?? new ContactLook();
            var style = !creature || IsSelf ? "plain" : relation is "hostile" ? contact.Hostile : relation is "friendly" or "allied" ? contact.Friendly : contact.Neutral;
            if (style != "none")
            {
                var size = isDrop ? 0.9f : Mathf.Clamp(Mathf.Max(_plan.Width, 0.5f) * (_plan.Rig == "tree" ? 0.5f : 1.25f), 0.5f, 4f);
                _contact = new ContactMark(size, ContactMark.StyleOf(style));
                AddChild(_contact);
                _contact.SetFade(creature ? 1f : 0.55f);
            }
        }
        _label.Position = new Vector3(0, _plan.Height + _plan.Hover + 0.45f, 0);
        _overlay.Position = new Vector3(0, _plan.Height + _plan.Hover + 0.22f, 0);
        _overlay.Visible = creature && s.Interactable is null;
        RefreshLabel();
    }

    private static string EquipmentKey(AppearanceService service)
    {
        var equipment = service.SelfEquipment?.Invoke();
        return equipment is null ? "" : string.Join(",", equipment.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}:{e.Value.Base}"));
    }

    /// <summary>The self's worn gear changed: its body is built again with it.</summary>
    public void RefreshEquipment()
    {
        if (!IsSelf || _shaped || Appearance is null || !IsNodeReady())
        {
            return;
        }
        if (EquipmentKey(Appearance) != _selfEquipmentKey)
        {
            Rebuild();
        }
    }

    /// <summary>Whether interactable props wear their names (the names over props option); creatures always do.</summary>
    public static bool PropLabels { get; set; } = true;

    /// <summary>How near the hero a prop must be for its name to show, in world units; zero shows them all.</summary>
    public static float PropLabelReach { get; set; } = 7f;

    public void RefreshLabel()
    {
        if (_label is null)
        {
            return;
        }
        var s = State;
        _label.Visible = s.Resources.Count > 0 || (s.Interactable is not null && PropLabels);
    }

    private static Color ColorFor(EntityState s)
    {
        if (s.IsPlayer)
        {
            return new Color(0.35f, 0.75f, 0.95f);
        }
        var look = s.Look ?? s.Pattern ?? "";
        if (look == "field")
        {
            return new Color(0.5f, 0.3f, 0.7f);
        }
        if (look.Length > 0 && look[0] == '#' && Color.HtmlIsValid(look))
        {
            // a flight without a shape of its own wears its Thread's colour
            return Color.FromHtml(look);
        }
        return s.Faction switch
        {
            "players" => new Color(0.4f, 0.8f, 0.6f),
            "the_loosed" => new Color(0.8f, 0.3f, 0.3f),
            "tangled" => new Color(0.35f, 0.2f, 0.45f),
            "remnant" => new Color(0.45f, 0.4f, 0.6f),
            _ => new Color(1f, 0.6f, 0.2f),
        };
    }

    public void Apply(EntityState state, bool snap)
    {
        var rebuild = state.Look != State.Look || Math.Abs(state.Height - State.Height) > 0.01f || state.Interactable is null != State.Interactable is null
            || state.Shape != State.Shape || state.ShapeSize != State.ShapeSize || state.Faction != State.Faction;
        var now = ServerNow?.Invoke() ?? Time.GetTicksMsec() / 1000.0;
        if (!ConfirmedCastCues && state.Casting is null && State.Casting is not null)
        {
            _releasedAt = now;
        }
        if (state.Dead && !State.Dead)
        {
            _diedAt = now;
        }
        else if (!state.Dead && State.Dead)
        {
            _diedAt = double.NegativeInfinity;
        }
        if (snap && state.Dead)
        {
            // a corpse first seen dead has lain there a while
            _diedAt = Math.Min(_diedAt, now - 5);
        }
        State = state;
        _targetPosition = Coords.ToGodot(state.X, state.Y, state.Z);
        _velocity = Coords.ToGodot(state.VX, state.VY, state.VZ);
        _lastUpdate = Time.GetTicksMsec() / 1000.0;
        if (snap)
        {
            _bornAt = _lastUpdate;
            _bornServer = ServerNow?.Invoke() ?? 0;
        }
        if (IsNodeReady() && rebuild)
        {
            Rebuild();
        }
        _targetYaw = Facing(state);
        if (snap)
        {
            GlobalPosition = _targetPosition;
            Rotation = new Vector3(0, _targetYaw, 0);
            _lastDrawn = _targetPosition;
            _hasDrawn = true;
        }
        if (!IsNodeReady())
        {
            return;
        }
        RefreshOverlay();
    }

    /// <summary>A blow landed on this body: the animator flinches it away from where it came and flashes it.</summary>
    public void Hit(double amount, Vector3? fromGodot)
    {
        var now = ServerNow?.Invoke() ?? Time.GetTicksMsec() / 1000.0;
        _hitAt = now;
        Cue?.Invoke("hit", _plan?.Rig, GlobalPosition);
        var max = State.ResourceMax.TryGetValue("life", out var m) && m > 0 ? m : 100f;
        _hitStrength = Mathf.Clamp((float)(amount / max) * 4f, 0.25f, 1f);
        if (fromGodot is { } from)
        {
            var d = from - GlobalPosition;
            var toSource = MathF.Atan2(-d.Z, d.X);
            _hitFrom = Mathf.Wrap(toSource - Rotation.Y, -MathF.PI, MathF.PI);
        }
        else
        {
            _hitFrom = float.NaN;
        }
    }

    /// <summary>Validated lifecycle facts drive body motion even when no cosmetic layer is selected.</summary>
    public void ObserveCastCue(CastCueMsg cue)
    {
        if (cue.Phase is "start" or "sync") _releasedAt = double.NegativeInfinity;
        if (cue.Phase != "release" || !cue.Fired) return;
        _releasedAt = cue.ServerTime;
        Cue?.Invoke("swing", _plan?.Swings == true ? "weapon" : _plan?.Rig, GlobalPosition);
    }

    /// <summary>Legacy-server fallback only; a later hit must not replay a confirmed release.</summary>
    public void Struck()
    {
        if (ConfirmedCastCues) return;
        if (!_wasCasting)
        {
            _releasedAt = ServerNow?.Invoke() ?? Time.GetTicksMsec() / 1000.0;
        }
        Cue?.Invoke("swing", _plan?.Swings == true ? "weapon" : _plan?.Rig, GlobalPosition);
    }

    private void RefreshOverlay()
    {
        var s = State;
        var title = s.Name;
        if (s.Level > 0 && s.Resources.Count > 0 && !s.IsPlayer)
        {
            title = $"{s.Name} [{s.Level}]";
        }
        if (s.Dead)
        {
            title += " (dead)";
        }
        if (ShowLegacyCast && s.Casting is not null)
        {
            title += $"  {s.Casting} {(int)(s.CastProgress * 100)}%";
        }
        if (s.Statuses.Count > 0)
        {
            title += "\n" + string.Join(" ", s.Statuses);
        }
        _label.Text = title;
        _label.Modulate = s.Dead ? new Color(0.6f, 0.6f, 0.6f) : s.IsPlayer ? new Color(0.7f, 0.9f, 1f) : s.Rarity switch { "magic" => new Color(0.5f, 0.6f, 1f), "rare" => new Color(1f, 0.85f, 0.4f), "champion" => new Color(1f, 0.6f, 0.3f), _ => Colors.White };
        if (s.Resources.TryGetValue("life", out var life) && s.ResourceMax.TryGetValue("life", out var max) && max > 0)
        {
            var frac = Mathf.Clamp(life / max, 0f, 1f);
            _lifeBar.Scale = new Vector3(Mathf.Max(0.001f, frac), 1, 1);
        }
        if (_shaped)
        {
            _mesh.Transparency = s.Dead ? 0.6f : 0f;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var now = Time.GetTicksMsec() / 1000.0;
        if (_fade is not null)
        {
            // A telegraph fades in, breathes while its strike is coming, its fill growing from the anchor to the
            // whole mark as the server time nears its until, and fades out once it lands or its time is up.
            var life = (float)(now - _bornAt);
            var pulse = UI.Motion.Reduced ? 1f : 0.78f + 0.22f * Mathf.Sin(life * 6f);
            var fadeIn = Mathf.Min(1f, life / 0.35f);
            var serverNow = ServerNow?.Invoke();
            var progress = 1f;
            if (State.Until is { } until && serverNow is { } sn && until - _bornServer > 0.01)
            {
                progress = (float)Math.Clamp((sn - _bornServer) / (until - _bornServer), 0, 1);
                if (_dyingAt is null && sn > until + 0.1)
                {
                    _dyingAt = now;
                }
            }
            var fadeOut = _dyingAt is { } dying ? Mathf.Max(0f, 1f - (float)(now - dying) / FadeSeconds) : 1f;
            _fade.AlbedoColor = _fade.AlbedoColor with { A = _fadeAlpha * 0.55f * fadeIn * fadeOut * pulse };
            if (_fillMaterial is not null)
            {
                var grown = Mathf.Max(0.02f, progress);
                _fill.Scale = new Vector3(grown, 1, grown);
                _fillMaterial.AlbedoColor = _fillMaterial.AlbedoColor with { A = _fadeAlpha * (0.45f + 0.4f * progress) * fadeIn * fadeOut };
            }
            if (_released && _dyingAt is { } gone && now - gone > FadeSeconds)
            {
                QueueFree();
                return;
            }
        }
        if (_dyingAt is not null)
        {
            return;
        }
        var age = (float)(now - _lastUpdate);
        var predicted = _targetPosition + _velocity * Mathf.Min(age, 0.25f);
        var t = 1f - Mathf.Exp(-dt * 12f);
        GlobalPosition = GlobalPosition.Lerp(predicted, t);
        var yaw = Rotation.Y;
        Rotation = new Vector3(0, Mathf.LerpAngle(yaw, _targetYaw, t), 0);
        if (_overlay.Visible)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera is not null)
            {
                _overlay.GlobalBasis = camera.GlobalBasis;
            }
        }
        if (_body is not null && _plan is not null)
        {
            AnimateBody(dt);
        }
        PlaceLabel(dt);
    }

    /// <summary>Feeds the animator the body's state and applies the pose; swaps the representation when the camera asks.</summary>
    private void AnimateBody(float dt)
    {
        var service = Appearance;
        var s = State;
        var serverNow = ServerNow?.Invoke() ?? Time.GetTicksMsec() / 1000.0;
        // a settled static prop (a rock, a chest, a marker) keeps its pose until something happens to it
        if (_plan!.Rig == "static" && !s.Dead && serverNow - _hitAt > 1 && _settled)
        {
            ChooseRepresentation(service);
            return;
        }
        _settled = _plan.Rig == "static";
        // the walk cycle follows the distance the drawn body moved
        var drawn = GlobalPosition;
        if (_hasDrawn)
        {
            var moved = drawn - _lastDrawn;
            moved.Y = 0;
            var distance = moved.Length();
            var speed = dt > 1e-4f ? distance / dt : 0;
            _speed += (speed - _speed) * Mathf.Min(1f, dt * 10f);
            var stride = (service?.Motion.Walk("stride", 1.5f) ?? 1.5f) * Mathf.Clamp(_plan!.Height / 1.8f, 0.4f, 2.5f);
            var before = _visual.WalkPhase;
            _visual.WalkPhase = Mathf.PosMod(_visual.WalkPhase + distance / Mathf.Max(0.2f, stride) * MathF.PI * 2, MathF.PI * 2);
            // a foot plants each half cycle: the footstep cue, with the surface underfoot as its variant
            if (_speed > 0.5f && s.Grounded && !s.Dead && _plan.Rig is "biped" or "quadruped" && (int)(before / MathF.PI) != (int)(_visual.WalkPhase / MathF.PI))
            {
                Cue?.Invoke("footstep", SurfaceAt?.Invoke(drawn), drawn);
            }
            // a lean into acceleration, read from the drawn motion
            var forward = new Vector3(Mathf.Cos(Rotation.Y), 0, -Mathf.Sin(Rotation.Y));
            var along = dt > 1e-4f ? moved.Dot(forward) / dt : 0;
            var target = Mathf.Clamp((along - _speed) * 2f, -8f, 8f);
            _visual.LeanForward += (target - _visual.LeanForward) * Mathf.Min(1f, dt * 6f);
        }
        _lastDrawn = drawn;
        _hasDrawn = true;
        _visual.Speed = _speed;
        _visual.Time = serverNow;
        _visual.Grounded = s.Grounded;
        _visual.Leaping = s.Flags.Contains("leaping");
        _visual.Dashing = s.Flags.Contains("dashing");
        _visual.Dead = s.Dead;
        _visual.DeadFor = s.Dead ? (float)Math.Max(0, serverNow - _diedAt) : 0;
        if (s.Dead && !_wasDead)
        {
            Cue?.Invoke("death", _plan!.Rig, drawn);
        }
        _wasDead = s.Dead;
        var casting = s.Casting is { Length: > 0 };
        var mode = "cast";
        var progress = s.CastProgress;
        if (IsSelf && SelfCast?.Invoke() is { } own)
        {
            casting = true;
            mode = own.Mode;
            progress = own.Progress;
        }
        _visual.Casting = casting;
        _visual.CastMode = mode;
        _visual.CastProgress = progress;
        _wasCasting = casting;
        _visual.ReleaseAge = (float)Math.Max(0, serverNow - _releasedAt);
        _visual.HitAge = (float)Math.Max(0, serverNow - _hitAt);
        _visual.HitStrength = _hitStrength;
        _visual.HitFrom = _hitFrom;
        _visual.ReducedMotion = UI.Motion.Reduced;
        Animator.Animate(_visual, service?.Motion ?? new MotionLook(new MotionLookDef { Id = "none" }), _pose);
        ChooseRepresentation(service);
        _body!.Apply(_pose);
        _contact?.SetFade((s.Resources.Count > 0 ? 1f : 0.55f) * _pose.Opacity);
    }

    /// <summary>Geometry or a card, as the bodies option and the camera decide; a card waits for its atlas.</summary>
    private void ChooseRepresentation(AppearanceService? service)
    {
        if (service is null || _plan is null || _look is null)
        {
            return;
        }
        var isCard = _body is CardNode;
        var wantCard = service.WantsCard(_look.Card, isCard, _plan.Height);
        if (wantCard && !isCard)
        {
            var look = _look;
            var state = State;
            _atlas ??= service.AtlasFor(_gearKey, () =>
            {
                var plain = look with { Variation = new AppearanceVariation { Hue = 0, Value = 0, Scale = 0 } };
                return service.PlanFor(state with { Key = 0 }, plain, IsSelf, out _);
            });
            if (_atlas.Ready)
            {
                _body!.Release();
                _body = new CardNode(_plan, _atlas, _plan.Height / Mathf.Max(0.1f, _atlas.Height));
                AddChild((Node)_body);
            }
        }
        else if (!wantCard && isCard)
        {
            _body!.Release();
            var geometry = new BodyNode(_plan);
            AddChild(geometry);
            geometry.SetShadows(service.Shadows);
            _body = geometry;
        }
    }

    /// <summary>A creature's name always shows; a prop's shows under the cursor or within reach of the hero, fading in and out.</summary>
    private void PlaceLabel(float dt)
    {
        var s = State;
        if (s.Resources.Count > 0 || s.Interactable is null || !PropLabels)
        {
            return;
        }
        var near = HoveredKey == Key;
        if (!near && PropLabelReach <= 0)
        {
            near = true;
        }
        else if (!near && HeroPosition?.Invoke() is { } hero)
        {
            var d = hero - GlobalPosition;
            d.Y = 0;
            near = d.Length() <= PropLabelReach;
        }
        var target = near ? 1f : 0f;
        _labelAlpha += (target - _labelAlpha) * Mathf.Min(1f, dt * 6f);
        var shown = _labelAlpha > 0.02f;
        _label.Visible = shown;
        if (shown)
        {
            _label.Modulate = _label.Modulate with { A = _labelAlpha };
            _label.OutlineModulate = _label.OutlineModulate with { A = _labelAlpha };
        }
    }

    public void SetShadows(bool on) => _body?.SetShadows(on);
}
