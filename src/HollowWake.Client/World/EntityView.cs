using Godot;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

/// <summary>
/// A placeholder body for a server entity: a capsule, box, sphere or cylinder by what it is, a name
/// and a life bar above it, and smoothing between snapshots. Looks are a presentation concern;
/// swapping this for models and animation does not touch anything else.
/// </summary>
public partial class EntityView : Node3D
{
    private const float BarWidth = 1.2f;

    private MeshInstance3D _mesh = null!;
    private MeshInstance3D _canopy = null!;
    private Label3D _label = null!;
    private Node3D _overlay = null!;
    private MeshInstance3D _lifeBar = null!;
    private MeshInstance3D _lifeBack = null!;
    private Vector3 _targetPosition;
    private Vector3 _velocity;
    private float _targetYaw;
    private double _lastUpdate;

    public long Key { get; }
    public EntityState State { get; private set; }
    public bool IsSelf { get; }

    public EntityView(EntityState state, bool isSelf)
    {
        Key = state.Key;
        State = state;
        IsSelf = isSelf;
        Name = $"e{state.Key}";
    }

    public override void _Ready()
    {
        _mesh = new MeshInstance3D { Name = "body" };
        AddChild(_mesh);
        _canopy = new MeshInstance3D { Name = "canopy", Visible = false };
        AddChild(_canopy);
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

    private void Rebuild()
    {
        var s = State;
        var look = s.Look ?? s.Pattern ?? "";
        Mesh mesh;
        var color = ColorFor(s);
        if (look.StartsWith("drop:", StringComparison.Ordinal))
        {
            mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 0.25f };
        }
        else if (look == "projectile" || s.Pattern == "projectile")
        {
            mesh = new SphereMesh { Radius = Mathf.Max(0.15f, s.Radius * 0.6f), Height = Mathf.Max(0.3f, s.Radius * 1.2f) };
        }
        else if (look == "field")
        {
            mesh = new CylinderMesh { TopRadius = s.Radius, BottomRadius = s.Radius, Height = 0.1f };
            color = new Color(color, 0.35f);
        }
        else if (look.StartsWith("poi:", StringComparison.Ordinal))
        {
            mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.6f, Height = 3.5f };
        }
        else if (look == "fount")
        {
            mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 1.0f, Height = s.Height };
        }
        else if (s.Interactable is not null && s.Resources.Count == 0)
        {
            mesh = new BoxMesh { Size = new Vector3(s.Radius * 2, s.Height, s.Radius * 2) };
        }
        else if (s.Resources.Count == 0 && IsTree(look))
        {
            // Trees: a trunk with a round canopy on top, so tall flora reads as a tree and not a pillar.
            var trunk = Mathf.Max(0.25f, s.Radius * 0.6f);
            mesh = new BoxMesh { Size = new Vector3(trunk, s.Height * 0.6f, trunk) };
            var canopy = Mathf.Clamp(s.Height * 0.28f, 0.8f, 4f);
            _canopy.Mesh = new SphereMesh { Radius = canopy, Height = canopy * 2f, RadialSegments = 12, Rings = 6 };
            _canopy.Position = new Vector3(0, s.Height - canopy, 0);
            _canopy.MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f };
            _canopy.Visible = true;
            color = new Color(0.42f, 0.3f, 0.2f);
        }
        else if (s.Resources.Count == 0)
        {
            // other flora and props: a block the size the simulation uses
            mesh = new BoxMesh { Size = new Vector3(Mathf.Max(0.3f, s.Radius * 1.4f), s.Height, Mathf.Max(0.3f, s.Radius * 1.4f)) };
        }
        else
        {
            mesh = new CapsuleMesh { Radius = Mathf.Max(0.2f, s.Radius), Height = Mathf.Max(0.6f, s.Height) };
        }
        if (!(s.Resources.Count == 0 && IsTree(look)))
        {
            _canopy.Visible = false;
        }
        _mesh.Mesh = mesh;
        _mesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            Roughness = 0.8f,
        };
        _mesh.Position = new Vector3(0, mesh is CapsuleMesh ? Mathf.Max(0.6f, s.Height) * 0.5f : mesh is CylinderMesh cyl ? cyl.Height * 0.5f : mesh is BoxMesh box ? box.Size.Y * 0.5f : s.Height * 0.5f, 0);
        _label.Position = new Vector3(0, s.Height + 0.5f, 0);
        _overlay.Position = new Vector3(0, s.Height + 0.25f, 0);
        _overlay.Visible = s.Resources.ContainsKey("life") && s.Interactable is null;
        _label.Visible = s.Resources.Count > 0 || s.Interactable is not null;
    }

    private static bool IsTree(string look) => look.Contains("oak") || look.Contains("pine") || look.Contains("birch") || look.Contains("tree");

    private static Color ColorFor(EntityState s)
    {
        if (s.IsPlayer)
        {
            return new Color(0.35f, 0.75f, 0.95f);
        }
        var look = s.Look ?? s.Pattern ?? "";
        if (look.StartsWith("drop:", StringComparison.Ordinal))
        {
            return s.Rarity switch { "magic" => new Color(0.4f, 0.6f, 1f), "rare" => new Color(1f, 0.83f, 0.3f), "unique" => new Color(0.9f, 0.4f, 0.2f), _ => new Color(0.85f, 0.85f, 0.8f) };
        }
        if (look == "projectile")
        {
            return new Color(1f, 0.6f, 0.2f);
        }
        if (look == "field")
        {
            return new Color(0.5f, 0.3f, 0.7f);
        }
        if (look.StartsWith("poi:", StringComparison.Ordinal))
        {
            return new Color(0.95f, 0.9f, 0.5f);
        }
        if (look == "fount")
        {
            return new Color(0.6f, 0.9f, 1f);
        }
        if (s.Resources.Count == 0)
        {
            return look.Contains("oak") || look.Contains("pine") || look.Contains("birch") || look.Contains("tree") ? new Color(0.2f, 0.45f, 0.2f)
                : look.Contains("boulder") || look.Contains("stone") || look.Contains("vein") ? new Color(0.5f, 0.5f, 0.5f)
                : look.Contains("anvil") || look.Contains("loom") ? new Color(0.6f, 0.4f, 0.25f)
                : new Color(0.35f, 0.55f, 0.3f);
        }
        return s.Faction switch
        {
            "players" => new Color(0.4f, 0.8f, 0.6f),
            "weftmarch_compact" or "lantern_chancery" or "mended_thread" => new Color(0.75f, 0.75f, 0.45f),
            "the_loosed" => new Color(0.8f, 0.3f, 0.3f),
            "tangled" => new Color(0.35f, 0.2f, 0.45f),
            "remnant" => new Color(0.45f, 0.4f, 0.6f),
            _ => s.Rarity switch { "magic" => new Color(0.5f, 0.6f, 1f), "rare" => new Color(1f, 0.8f, 0.3f), "champion" => new Color(1f, 0.55f, 0.25f), _ => new Color(0.7f, 0.5f, 0.35f) },
        };
    }

    public void Apply(EntityState state, bool snap)
    {
        var rebuild = state.Look != State.Look || state.Dead != State.Dead || Math.Abs(state.Height - State.Height) > 0.01f || state.Interactable is null != State.Interactable is null;
        State = state;
        _targetPosition = Coords.ToGodot(state.X, state.Y, state.Z);
        _velocity = Coords.ToGodot(state.VX, state.VY, state.VZ);
        _targetYaw = -state.Yaw;
        _lastUpdate = Time.GetTicksMsec() / 1000.0;
        if (snap)
        {
            GlobalPosition = _targetPosition;
            Rotation = new Vector3(0, _targetYaw + Mathf.Pi / 2, 0);
        }
        if (!IsNodeReady())
        {
            return;
        }
        if (rebuild)
        {
            Rebuild();
        }
        RefreshOverlay();
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
        if (s.Casting is not null)
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
        _mesh.Transparency = s.Dead ? 0.6f : 0f;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var age = (float)(Time.GetTicksMsec() / 1000.0 - _lastUpdate);
        var predicted = _targetPosition + _velocity * Mathf.Min(age, 0.25f);
        var t = 1f - Mathf.Exp(-dt * 12f);
        GlobalPosition = GlobalPosition.Lerp(predicted, t);
        var yaw = Rotation.Y;
        Rotation = new Vector3(0, Mathf.LerpAngle(yaw, _targetYaw + Mathf.Pi / 2, t), 0);
        if (_overlay.Visible)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera is not null)
            {
                _overlay.GlobalBasis = camera.GlobalBasis;
            }
        }
    }
}
