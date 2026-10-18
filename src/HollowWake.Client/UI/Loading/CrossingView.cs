using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Loading;

/// <summary>Native canvas adapter for the shared crossing model; collision geometry is never inferred from art.</summary>
public partial class CrossingView : Control
{
    public LoadingScreen OwnerScreen { get; init; } = null!;
    private readonly Font _runes = new SystemFont { FontNames = new[] { "Segoe UI Symbol", "Noto Sans Runic", "serif" } };
    private SpiritRun R => OwnerScreen.Run!;
    private SpiritRules C => OwnerScreen.Spec.Rules;
    private SpiritLayout _layout = null!;
    private double _time;
    private float S => (float)_layout.Scale;
    private Color Ink(string name, float alpha = 1) => new Color(OwnerScreen.Spec.Palette[name]) with { A = alpha };
    private Vector2 P(double u, double lane) { var p = _layout.Point(u, lane); return new((float)p.X, (float)p.Y); }
    private static double Hash(double a, double b)
    {
        unchecked { var h=(uint)((int)a*374761393+(int)b*668265263); h=(h^(h>>13))*1274126177; h^=h>>16; return h/4294967296.0; }
    }
    private static double Wrap(double v, double span) => (v % span + span) % span;
    private void Line(double u, double v, double u2, double v2, Color color, float width = 1) => DrawLine(P(u, v), P(u2, v2), color, width, true);
    private void Glow(Vector2 center, float radius, Color color)
    {
        _glow ??= new GradientTexture2D { Width=128, Height=128, Fill=GradientTexture2D.FillEnum.Radial,
            FillFrom=new Vector2(.5f,.5f), FillTo=new Vector2(1,.5f),
            Gradient=new Gradient { Colors=new[] { Colors.White, Colors.Transparent } } };
        DrawTextureRect(_glow, new Rect2(center-new Vector2(radius,radius),new Vector2(radius*2,radius*2)),false,color);
    }
    private GradientTexture2D? _glow;
    public override void _ExitTree()
    {
        _glow?.Gradient?.Dispose(); _glow?.Dispose(); _runes.Dispose();
    }
    private static Vector2[] Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Enumerable.Range(0,13)
        .Select(i => { var t=i/12f;return Mathf.Pow(1-t,3)*a+3*(1-t)*(1-t)*t*b+3*(1-t)*t*t*c+t*t*t*d; }).ToArray();
    private static Vector2[] Quadratic(Vector2 a, Vector2 b, Vector2 c) => Curve(a,a+(b-a)*2/3,c+(b-c)*2/3,c);
    public override void _Draw()
    {
        if (OwnerScreen.Run is null || Size.X <= 0 || Size.Y <= 0) return;
        _layout = new(Size.X, Size.Y, R.Direction, C);
        _time = OwnerScreen.ReducedMotion() ? 0 : R.Time;
        var layers = new Dictionary<string, Action> {
            ["abyss"] = Abyss, ["ribbons"] = Ribbons, ["gates"] = Gates, ["currents"] = Currents,
            ["souls"] = Souls, ["bursts"] = Bursts, ["wisp"] = Wisp,
        };
        foreach (var layer in OwnerScreen.Spec.Layers) if (layers.TryGetValue(layer, out var paint)) paint();
    }
    private void Abyss()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Ink("floor"));
        Glow(P(650, 0), Math.Max(Size.X,Size.Y)*.7f, new Color("#233442") with { A = .65f });
        var motion = OwnerScreen.ReducedMotion() ? 0 : R.Distance * S * .18;
        var camX = R.Direction == "down" ? 0 : R.Direction == "left" ? -motion : motion;
        var camY = R.Direction == "down" ? motion : 0;
        SpiritCanvas.Abyss(this, Size, _time, camX, camY, Ink("abyss"), Glow);
    }
    private void Ribbons()
    {
        for (var i = 0; i < 5; i++)
        {
            var points = new List<Vector2>();
            for (var u = -40; u <= 1040; u += 24) points.Add(P(u, Math.Sin(u * .004 + i * 2 + _time * .1) * 140 + (i - 2) * 54));
            DrawPolyline(points.ToArray(), new Color(i % 2 == 0 ? "#8474bb" : "#78a39d") with { A = .035f }, (18 + i * 7) * S, true);
        }
        foreach (var side in new[] { -1, 1 })
        {
            Line(0, side * 275, 1000, side * 275, Ink("gate", .17f));
            for(var i=0;i<9;i++)
            {
                var u=Wrap(i*142-(OwnerScreen.ReducedMotion()?0:R.Distance)*.26,1280)-100;
                var points=new[]{P(u-10,side*280),P(u+16,side*242),P(u+38,side*236),P(u+61,side*280)};
                DrawColoredPolygon(points,new Color("#354652") with { A=.11f });
                DrawPolyline(points.Append(points[0]).ToArray(),Ink("gate",.11f),1,true);
                Line(u+23,side*251,u+31,side*251,Ink("gate",.19f));
            }
        }
        for (var i = 0; i < 88; i++)
        {
            var u = Wrap(i * 173.7 - (OwnerScreen.ReducedMotion() ? 0 : R.Distance) * (.18 + i % 4 * .09), 1120) - 60;
            var v = Math.Sin(i * 71.2) * 260;
            Line(u, v, u + (OwnerScreen.ReducedMotion() ? 2 : 4 + R.Speed * 4), v, Ink("gate", .1f + i % 5 * .038f));
        }
    }
    private void Gates()
    {
        foreach (var gate in R.Gates)
        {
            var alpha = gate.Resolved ? gate.Hit ? .35f : .17f : .92f;
            var color = Ink(gate.Hit ? "impact" : gate.Openings.Any(o => o.Width < 154) ? "narrowGate" : "gate", alpha);
            foreach (var (low, high) in R.Solids(gate))
            {
                var a = P(gate.U - C.GateThickness / 2, low); var b = P(gate.U + C.GateThickness / 2, high);
                var rect = new Rect2(a, b - a).Abs();
                var mid=(low+high)/2;
                foreach(var (v1,v2,c1,c2) in new[] { (low,mid,Ink("stone",alpha),Ink("stoneDeep",alpha)),(mid,high,Ink("stoneDeep",alpha),Ink("stone",alpha)) })
                    DrawPolygon(new[]{P(gate.U-C.GateThickness/2,v1),P(gate.U+C.GateThickness/2,v1),P(gate.U+C.GateThickness/2,v2),P(gate.U-C.GateThickness/2,v2)},new[]{c1,c1,c2,c2});
                DrawRect(rect, color, false, Math.Max(1, S));
                foreach (var (edge,side) in new[] { (low,1), (high,-1) })
                    if (Math.Abs(edge) < C.HalfWidth) {
                        Line(gate.U - C.GateThickness / 2, edge, gate.U + C.GateThickness / 2, edge, color, 3 * S);
                        var size=Math.Min(14,(high-low)*.26);
                        DrawColoredPolygon(new[]{P(gate.U,edge+side*3),P(gate.U+5,edge+side*size*.65),P(gate.U,edge+side*size),P(gate.U-5,edge+side*size*.65)},color);
                    }
                var span = high - low; var count = span < 38 ? 0 : Math.Max(1, (int)Math.Floor((span - 44) / 27) + 1);
                for (var j = 0; j < count; j++)
                {
                    var v = count == 1 ? (low + high) / 2 : low + 22 + (span - 44) * j / (count - 1);
                    var runes = OwnerScreen.Spec.Runes;
                    var glyph = runes[(int)Math.Floor(Hash(gate.Id * 31 + j, Math.Round(low * 100)) * runes.Length)].ToString();
                    DrawString(_runes, P(gate.U, v) + new Vector2(-8 * S, 6 * S), glyph, fontSize: Math.Max(1, (int)(20 * S)), modulate: color);
                }
            }
        }
    }
    private void Currents()
    {
        foreach (var current in R.Currents.Where(c => !c.Taken))
        {
            Glow(P(current.U, current.Lane), 54 * S, Ink("current", .15f));
            for (var i = 0; i < 5; i++)
            {
                var u = current.U + (i - 2) * 11;
                var alpha = OwnerScreen.ReducedMotion() ? .8f : (float)(.52 + .34 * (.5 + .5 * Math.Sin(_time * 6 - i)));
                DrawPolyline(new[] { P(u - 5, current.Lane - 11), P(u + 6, current.Lane), P(u - 5, current.Lane + 11) }, Ink("current", alpha), 2.4f * S, true);
            }
        }
    }
    private void Souls()
    {
        foreach (var pickup in R.Pickups.Where(p => p.State != "taken"))
        {
            var def = OwnerScreen.Spec.Pickups[pickup.Kind];
            var p = P(pickup.U, pickup.Lane);
            var alpha = pickup.State == "released" ? (float)(.45 * (1 - pickup.Fade / C.PickupFade)) : 1f;
            var phase = _time * def.Flicker + pickup.Id * 2.4;
            var breath = 1 + Math.Sin(phase) * .045 + Math.Sin(phase * 1.7) * .025;
            Glow(p, (float)(def.Radiance * breath) * S, Ink("wisp", alpha * .5f));
            var scale = (float)def.Size * S;
            var curl = (float)Math.Sin(phase * .8) * 2.4f;
            var lick=(float)Math.Sin(phase*1.3)*1.8f;
            // The original cubic soul-flame silhouette stays upright in every travel direction.
            var outline=Curve(new(0,10),new(-12,9),new(-12,-1),new(-7,-10))
                .Concat(Quadratic(new(-7,-10),new(-8,-3),new(-3,-4)))
                .Concat(Curve(new(-3,-4),new(1,-8),new(-5+curl,-17),new(2+curl,-26-lick)))
                .Concat(Curve(new(2+curl,-26-lick),new(-1+curl,-14),new(12,-10),new(9,-1)))
                .Concat(Quadratic(new(9,-1),new(13,-3),new(12,-8)))
                .Concat(Curve(new(12,-8),new(17,5),new(7,11),new(0,10))).ToArray();
            Vector2 At(Vector2 v) => p+new Vector2(v.X,v.Y*(float)breath)*scale;
            DrawPolygon(outline.Select(At).ToArray(),outline.Select(v=>Ink("wisp",alpha*Math.Clamp((v.Y+26)/36,.1f,.94f))).ToArray());
            var heart=Curve(new(0,7),new(-7,5),new(-6,-2),new(-1+curl*.25f,-11))
                .Concat(Curve(new(-1+curl*.25f,-11),new(-2,-3),new(7,-1),new(5,4)))
                .Concat(Quadratic(new(5,4),new(3,8),new(0,7))).ToArray();
            DrawColoredPolygon(heart.Select(At).ToArray(),new Color("#f0f7fa") with { A=alpha });
        }
    }
    private void Bursts()
    {
        if (OwnerScreen.ReducedMotion()) return;
        foreach (var burst in R.Bursts)
        {
            var f = burst.Age / C.BurstSeconds;
            var color = Ink(burst.Kind == "impact" ? "impact" : burst.Kind == "current" ? "current" : burst.Kind == "gate" ? "gate" : "wisp", (float)(1-f));
            DrawArc(P(burst.U, burst.Lane), (float)(18 + f*40)*S, 0, Mathf.Tau, 40, color, 1.3f*S, true);
            for (var i=0;i<9;i++)
            {
                var phase=i*Math.Tau/9; var radius=12+f*55;
                DrawCircle(P(burst.U+Math.Cos(phase)*radius,burst.Lane+Math.Sin(phase)*radius), S, color);
            }
        }
    }
    private void Wisp()
    {
        var color = Ink(R.Dash > 0 ? "current" : "wisp");
        if (!OwnerScreen.ReducedMotion())
        for(var i=18;i>0;i--)
            DrawCircle(P(R.PlayerU-i*(4+R.Speed*2), R.Lane+Math.Sin(_time*4-i*.3)*i*.35),
                (14-i*.55f)*S, color with { A=(1-i/19f)*.28f });
        var p=P(R.PlayerU,R.Lane);
        if(R.Hinder>0) DrawArc(p,27*S,0,Mathf.Tau,40,Ink("impact"),2*S,true);
        if((R.Surge>0||R.Dash>0)&&!OwnerScreen.ReducedMotion())
            foreach(var radius in new[]{30,38}) DrawArc(p,radius*S,(float)_time*3,(float)_time*3+Mathf.Pi*1.4f,35,color,1.2f*S,true);
        var look=OwnerScreen.Spec.Wisp;
        var angle=R.Direction=="down"?Mathf.Pi/2:R.Direction=="left"?Mathf.Pi:0;
        SpiritCanvas.Draw(this, look, p, angle, S, _time, Glow);
    }
}
