using Godot;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

/// <summary>Depth-tested body-seated cosmetic lines, selected and shaped entirely by shared content recipes.</summary>
public partial class CastVisuals : Node3D
{
    public CastCueTimeline Timeline { get; }
    private readonly ClientWorld _world;
    private readonly Dictionary<CastCueTimeline.Cue, MeshInstance3D> _drawn = new();
    public CastVisuals(ClientWorld world, CastVisualsDef config)
    {
        _world = world;
        Timeline = new(config);
        Timeline.Lifecycle += cue =>
        {
            if (_world.Entities.TryGetValue(cue.Source, out var body)) body.ObserveCastCue(cue);
        };
    }
    public void Receive(CastCueMsg cue, double now) => Timeline.Receive(cue, now);
    public void Clear() { Timeline.Clear(); Clean(); }
    private void Clean()
    {
        foreach (var (cue, mesh) in _drawn.ToArray())
            if (!Timeline.Active.Contains(cue)) { mesh.QueueFree(); _drawn.Remove(cue); }
    }
    public void Advance(double now, bool reduced)
    {
        Timeline.Advance(now);
        Clean();
        foreach (var cue in Timeline.Active)
        {
            var e = cue.Event;
            var style = cue.Style;
            _world.Entities.TryGetValue(cue.Phase == "impact" || e.Outcome ? e.Target : e.Source, out var anchor);
            if (anchor is null || (anchor.State.Dead && cue.Phase != "impact" && !style.ShowOnDead))
            {
                if (_drawn.TryGetValue(cue, out var hidden)) hidden.Visible = false;
                continue;
            }
            if (!_drawn.TryGetValue(cue, out var mesh))
            {
                var lines = new ImmediateMesh();
                lines.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                foreach (var s in CastVisualPrimitives.Build(style))
                {
                    var a = Coords.ToGodot(s[0], s[1], s[2]);
                    var b = Coords.ToGodot(s[3], s[4], s[5]);
                    var direction = (b - a).Normalized();
                    if (direction.IsZeroApprox()) continue;
                    var axis = Math.Abs(direction.Y) < 0.9 ? Vector3.Up : Vector3.Back;
                    var side = direction.Cross(axis).Normalized() * (float)(style.Stroke / 2);
                    var other = direction.Cross(side).Normalized() * (float)(style.Stroke / 2);
                    foreach (var offset in new[] { side, other })
                    {
                        var corners = new[] { a - offset, a + offset, b + offset, b - offset };
                        foreach (var i in new[] { 0, 1, 2, 0, 2, 3 }) lines.SurfaceAddVertex(corners[i]);
                    }
                }
                lines.SurfaceEnd();
                var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(cue.Color),
                    NoDepthTest = false, DisableReceiveShadows = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                mesh = new MeshInstance3D { Mesh = lines, MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(mesh); _drawn.Add(cue, mesh);
            }
            var age = Math.Max(0, now - e.ServerTime);
            var progress = cue.Phase == "start" ? 0 : Math.Min(1, age / style.Seconds);
            mesh.Position = (cue.Phase == "impact" ? Coords.ToGodot(e.X, e.Y, e.Z) : anchor.GlobalPosition)
                + Vector3.Up * (float)(anchor.State.Height * style.Height);
            mesh.Scale = Vector3.One * (float)(style.Size * (reduced ? 1 : 1 + style.Expand * progress));
            var yaw = e.AimX == e.X && e.AimY == e.Y ? anchor.State.Yaw : Math.Atan2(e.AimY - e.Y, e.AimX - e.X);
            mesh.Rotation = new Vector3(0, (float)(yaw + (reduced ? 0 : age * style.Spin)), 0);
            var color = new Color(cue.Color) { A = (float)(style.Opacity * (reduced || cue.Phase == "start" ? 1 : 1 - progress)) };
            ((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor = color;
            mesh.Visible = true;
        }
    }
}
