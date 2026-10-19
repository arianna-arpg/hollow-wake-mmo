using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

public partial class CastForecasts : Node3D
{
    public CastForecastTimeline Timeline { get; } = new();
    private readonly ClientWorld _world;
    private readonly CastForecastStyleDef _style;
    private readonly CanvasLayer _overlay = new() { Layer = 20 };
    private readonly Label _warning = new() { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
    private sealed class Drawn
    {
        public CastForecastRegion? Region;
        public bool Committed;
        public List<MeshInstance3D> Meshes = new();
        public required PanelContainer Panel;
        public required Label Label;
        public required ProgressBar Bar;
    }
    private readonly Dictionary<long, Drawn> _drawn = new();
    public CastForecasts(ClientWorld world, CastForecastStyleDef style)
    {
        _world = world; _style = style;
        _overlay.AddChild(_warning); AddChild(_overlay);
    }
    public void Clear(string? epoch = null) { Timeline.Reset(epoch); Clean(); _warning.Text = ""; }
    private void Clean()
    {
        var ids = Timeline.Active.Select(c => c.CastId).ToHashSet();
        foreach (var (id, v) in _drawn.ToArray()) if (!ids.Contains(id))
        {
            foreach (var mesh in v.Meshes) mesh.QueueFree();
            v.Panel.QueueFree(); _drawn.Remove(id);
        }
    }
    private StandardMaterial3D Material(string color, int priority, bool billboard = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(color),
        NoDepthTest = true, DisableReceiveShadows = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
        RenderPriority = priority,
    };
    private Drawn Timing()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(_style.UnderlayColor, .7f),
            ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", (int)_style.FontSize);
        var bar = new ProgressBar { MaxValue = 1, Step = 0, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2((float)_style.BarWidth, (float)_style.BarHeight) };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(_style.UnderlayColor) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(_style.Color) });
        column.AddChild(label); column.AddChild(bar); panel.AddChild(column); _overlay.AddChild(panel);
        return new Drawn { Panel = panel, Label = label, Bar = bar };
    }
    public void Advance(double now)
    {
        Clean(); var stale = Timeline.Stale(now);
        _warning.Text = Timeline.Omitted > 0 ? _style.Labels["overflow"] + $" ({Timeline.Omitted})" : "";
        _warning.Size = new Vector2(GetViewport().GetVisibleRect().Size.X, 30);
        _warning.Position = new Vector2(0, GetViewport().GetVisibleRect().Size.Y * .18f);
        foreach (var cast in Timeline.Active)
        {
            if (!_world.Entities.TryGetValue(cast.Source, out var body)) continue;
            if (!_drawn.TryGetValue(cast.CastId, out var v))
            {
                v = Timing();
                _drawn.Add(cast.CastId, v);
            }
            var region = !stale && CastForecastTimeline.SegmentsFor(cast.Region, _style.Segments, _style.MaxSegments, _style.MaxChordError) > 0 ? cast.Region : null;
            if (!CastForecastGeometry.SameMesh(v.Region, v.Committed, region, cast.DirectionCommitted))
            {
                foreach (var mesh in v.Meshes) mesh.QueueFree();
                v.Meshes = region is null ? new() : Boundary(region, cast.DirectionCommitted); v.Region = region; v.Committed = cast.DirectionCommitted;
            }
            var caption = CastForecastTimeline.Caption(cast, now, stale);
            if (caption == "primary" && region is null) caption = "unavailable";
            var coverage = caption is "primary" or "unavailable" or "stale" ? "" : _style.Labels[region is not null ? "primary" : "unavailable"];
            v.Label.Text = string.Join(" · ", new[] { cast.Name, _style.Labels[caption], coverage }.Where(s => s.Length > 0));
            v.Label.Modulate = new Color(region is not null ? _style.Color : _style.UnknownColor);
            var position = Coords.ToGodot(cast.X, cast.Y, cast.Z + body.State.Height + (float)_style.LabelHeight);
            var camera = GetViewport().GetCamera3D();
            v.Bar.Value = stale ? 0 : CastForecastTimeline.Progress(cast, now);
            v.Panel.Visible = !body.State.Dead && camera is not null && !camera.IsPositionBehind(position);
            v.Panel.ResetSize();
            if (camera is not null) v.Panel.Position = camera.UnprojectPosition(position) - v.Panel.Size * new Vector2(.5f, 1);
            foreach (var mesh in v.Meshes)
            {
                mesh.Visible = !body.State.Dead;
                if (region is not null) { mesh.Position = Coords.ToGodot(region.X, region.Y, region.Z + (float)_style.Lift); mesh.Rotation = new Vector3(0, region.Heading, 0); }
            }
        }
    }
    private List<MeshInstance3D> Boundary(CastForecastRegion region, bool committed)
    {
        var parts = CastForecastGeometry.Build(region, committed, _style);
        return new() { Lines(parts, region, _style.Stroke * _style.UnderlayScale, _style.UnderlayColor, 9),
            Lines(parts, region, _style.Stroke, region.ViewerEligibility == "candidate" ? _style.CandidateColor : _style.Color, 10) };
    }
    private MeshInstance3D Lines(List<(System.Numerics.Vector3 A, System.Numerics.Vector3 B)> parts, CastForecastRegion region, double width, string color, int priority)
    {
        var lines = new ImmediateMesh(); lines.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (localA, localB) in parts)
        {
            var a = Coords.ToGodot(localA.X, localA.Y, localA.Z); var b = Coords.ToGodot(localB.X, localB.Y, localB.Z);
            var d = b - a; if (d.IsZeroApprox()) continue;
            var axis = Math.Abs(d.Y) > .99f * d.Length() ? Vector3.Right : Vector3.Up;
            var offset = d.Cross(axis).Normalized() * (float)(width / 2);
            var corners = new[] { a - offset, a + offset, b + offset, b - offset };
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 }) lines.SurfaceAddVertex(corners[i]);
        }
        lines.SurfaceEnd();
        var mesh = new MeshInstance3D { Mesh = lines, MaterialOverride = Material(color, priority),
            Position = Coords.ToGodot(region.X, region.Y, region.Z + (float)_style.Lift), Rotation = new Vector3(0, region.Heading, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh); return mesh;
    }
}
