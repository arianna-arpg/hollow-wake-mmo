using Godot;
using HollowWake.Client.World.Logic;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

public partial class HitBodyDebug : Node3D
{
    private string _key = "";
    private MeshInstance3D? _lines;
    private Label3D? _label;
    public void Update(EntityState state, IReadOnlyDictionary<string, HitBodyInfo> catalog)
    {
        var body = state.HitBody is null ? HitBodyLines.Legacy : catalog.GetValueOrDefault(state.HitBody);
        var key = $"{state.HitBody}:{state.Radius}:{state.Height}:{body?.GetHashCode()}";
        if (_key != key)
        {
            _key = key; _lines?.QueueFree(); _label?.QueueFree();
            var mesh = new ImmediateMesh();
            var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true };
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
            mesh.SurfaceSetColor(new Color(.2f, 1, 1));
            if (body is not null) foreach (var p in HitBodyLines.Build(body, state.Radius, state.Height)) mesh.SurfaceAddVertex(Coords.ToGodot(p));
            mesh.SurfaceSetColor(new Color(1, .75f, .1f));
            for (var i = 0; i < 32; i++) for (var j = 0; j < 2; j++)
            {
                var a = (i + j) * Mathf.Tau / 32;
                mesh.SurfaceAddVertex(Coords.ToGodot(Mathf.Cos(a) * state.Radius, Mathf.Sin(a) * state.Radius, .01f));
            }
            mesh.SurfaceEnd();
            _lines = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_lines);
            _label = new Label3D { Text = body is null || !HitBodyLines.Supported(body) ? "Hit body unavailable" : "Hit body / clearance",
                Position = new Vector3(0, state.Height + .12f, 0), FontSize = 20, PixelSize = .006f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(.2f, 1, 1) };
            AddChild(_label);
        }
        GlobalPosition = Coords.ToGodot(state.X, state.Y, state.Z);
        GlobalRotation = new Vector3(0, state.Yaw, 0);
    }
}
