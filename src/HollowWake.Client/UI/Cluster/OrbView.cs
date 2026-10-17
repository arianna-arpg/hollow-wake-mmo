using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// One orb of the cluster and the pools drawn on and around it, nearest first: the fill that drains from the
/// top, overlays laid over it from the top (a shield veils the orb and pulses along its edge while it
/// recharges), the reserved cap darkening the top, the etched rim band hugging the orb (ward), and concentric
/// rings around it (poise, insight, stamina). A pool the hero has no maximum in is not drawn. Its tooltip
/// names every pool on the orb with the numbers the server sent.
/// </summary>
public partial class OrbView : Control
{
    private readonly Skin _skin;
    private readonly UiOrb _orb;
    private readonly bool _mirror;
    private HeroState? _hero;
    private ColorRect _liquid = null!;
    private ShaderMaterial _material = null!;
    private OrbLabel _label = null!;
    private ClusterLayout? _layout;
    private double _now;
    private string _tooltip = "";

    public OrbView(Skin skin, UiOrb orb)
    {
        _skin = skin;
        _orb = orb;
        _mirror = orb.Side == "right";
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = Shaders.Orb };
        _material.SetShaderParameter("phase", _mirror ? 2.3f : 0f);
        _liquid = new ColorRect { Name = "liquid", Material = _material, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_liquid);
        _label = new OrbLabel(this) { Name = "label", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_label);
        ApplySkin();
    }

    private void ApplySkin()
    {
        _material.SetShaderParameter("fill_color", _skin.Fill(_orb.Fill));
        _material.SetShaderParameter("deep_color", _skin.Deep(_orb.Fill));
        _material.SetShaderParameter("veil_color", _orb.Overlays.Count > 0 ? _skin.Fill(_orb.Overlays[0]) : Colors.White);
        _material.SetShaderParameter("reserved_color", _skin.State("reserved", new Color(0.03f, 0.02f, 0.04f)));
        _material.SetShaderParameter("empty_color", _skin.S("well").Darkened(0.2f));
        _material.SetShaderParameter("glass_color", _skin.S("glass", Colors.White));
        _material.SetShaderParameter("glass", _skin.Mat("glass", 0.22f));
    }

    /// <summary>Places the orb for this frame's cluster layout and feeds it the hero's pools.</summary>
    public void Update(ClusterLayout layout, Box box, HeroState hero, double now)
    {
        _layout = layout;
        _hero = hero;
        _now = now;
        Position = new Vector2(box.X, box.Y);
        Size = new Vector2(box.W, box.H);
        var r = layout.OrbRadius;
        _liquid.Position = Size / 2 - new Vector2(r, r);
        _liquid.Size = new Vector2(r * 2, r * 2);
        _label.Position = Vector2.Zero;
        _label.Size = Size;

        var (cur, max) = hero.Resource(_orb.Fill);
        _material.SetShaderParameter("fill", max > 0 ? (float)Math.Clamp(cur / max, 0, 1) : 0f);
        _material.SetShaderParameter("reserved", max > 0 ? (float)Math.Clamp(hero.Reserved(_orb.Fill) / max, 0, 1) : 0f);
        var veil = 0f;
        var pulse = 0f;
        foreach (var overlay in _orb.Overlays)
        {
            var (oc, om) = hero.Resource(overlay);
            if (om <= 0)
            {
                continue;
            }
            veil = (float)Math.Clamp(oc / om, 0, 1);
            if (hero.Pool(overlay)?.State == "recharging")
            {
                pulse = 0.5f + 0.5f * Mathf.Sin((float)now * 4.2f);
            }
            break;
        }
        _material.SetShaderParameter("veil", veil);
        _material.SetShaderParameter("pulse", pulse);
        var tip = Tooltip(hero);
        if (tip != _tooltip)
        {
            _tooltip = tip;
            TooltipText = tip;
        }
        QueueRedraw();
        _label.QueueRedraw();
    }

    private string Tooltip(HeroState hero)
    {
        var lines = new List<string>();
        void Line(string pool, bool always)
        {
            var (c, m) = hero.Resource(pool);
            if (m <= 0 && !always)
            {
                return;
            }
            var state = hero.Pool(pool)?.State;
            var reserved = hero.Reserved(pool);
            var text = $"{_skin.Names.ResourceName(pool)}  {Math.Round(c)} / {Math.Round(m)}";
            if (reserved > 0)
            {
                text += $"  ({Math.Round(reserved)} reserved)";
            }
            if (state is not null and not "ready")
            {
                text += $"  {state}";
            }
            lines.Add(text);
        }
        Line(_orb.Fill, true);
        foreach (var p in _orb.Overlays.Concat(_orb.Rim).Concat(_orb.Rings))
        {
            Line(p, false);
        }
        return string.Join("\n", lines);
    }

    public override void _Draw()
    {
        if (_layout is null || _hero is null)
        {
            return;
        }
        var c = Size / 2;
        var r = _layout.OrbRadius;
        var well = _skin.S("well");
        var dark = _skin.S("edge_dark");
        var etch = _skin.S("etch") with { A = _skin.Mat("etch", 0.55f) };

        // A soft shadow under the setting.
        for (var i = 6; i >= 1; i--)
        {
            DrawCircle(c + new Vector2(0, 2), r + _layout.RimWidth + i * 1.5f, new Color(0, 0, 0, 0.05f), true, -1, true);
        }

        // The setting: the orb sits in a bezel the width of the rim band.
        var rimR = _layout.RimRadius;
        var rimW = _layout.RimWidth;
        Bezel(c, rimR, rimW);
        foreach (var pool in _orb.Rim)
        {
            var (cur, max) = _hero.Resource(pool);
            if (max <= 0)
            {
                continue;
            }
            var frac = (float)Math.Clamp(cur / max, 0, 1);
            var fill = _skin.Fill(pool);
            Paint.Arc(this, c, rimR, rimW - 1.5f, frac, fill.Darkened(0.05f), _mirror);
            Paint.Arc(this, c, rimR + rimW * 0.25f, 1.2f, frac, fill.Lightened(0.35f) with { A = 0.8f }, _mirror);
            // Runes cut into the band: dark in gold where the ward stands, faint where it is spent.
            Paint.RuneBand(this, c, rimR, rimW, 24, (frac > 0.02f ? _skin.Deep(pool) : etch) with { A = 0.85f });
            break;
        }

        // The rings, nearest first.
        for (var i = 0; i < _orb.Rings.Count; i++)
        {
            var pool = _orb.Rings[i];
            var (cur, max) = _hero.Resource(pool);
            if (max <= 0)
            {
                continue;
            }
            var radius = _layout.RingRadius(i);
            var width = _layout.RingWidth;
            var state = _hero.Pool(pool)?.State;
            var broken = state is "broken" or "rearming";
            Paint.Track(this, c, radius, width + 2, dark with { A = 0.9f });
            Paint.Track(this, c, radius, width, well with { A = 0.92f });
            var frac = (float)Math.Clamp(cur / max, 0, 1);
            var fill = broken ? _skin.State("broken", new Color(0.45f, 0.45f, 0.45f)) : _skin.Fill(pool);
            if (state == "rearming" && _hero.Pool(pool) is { Since: { } since, Until: { } until } && until > since && _hero.HasServerClock)
            {
                // Re-arming: the ring fills back in dimly over the time the server named.
                var t = (float)Math.Clamp((_hero.ServerNow(_now) - since) / (until - since), 0, 1);
                Paint.Arc(this, c, radius, width - 1, t, _skin.Fill(pool) with { A = 0.35f }, _mirror);
            }
            Paint.Arc(this, c, radius, width - 1, frac, fill, _mirror);
            Paint.Arc(this, c, radius + width * 0.3f, 1f, frac, fill.Lightened(0.4f) with { A = 0.55f }, _mirror);
            Paint.Ticks(this, c, radius - width / 2, radius + width / 2, 15, etch with { A = etch.A * 0.45f });
            if (broken)
            {
                Cracks(c, radius, width);
            }
        }
    }

    private void Bezel(Vector2 c, float radius, float width)
    {
        var dark = _skin.S("edge_dark");
        var edge = _skin.S("edge");
        if (_skin.Brass)
        {
            var bright = edge.Lightened(0.38f);
            for (var i = 0; i < 4; i++)
            {
                var t = i / 3f;
                var col = t < 0.34f ? dark.Lerp(bright, t / 0.34f) : bright.Lerp(edge.Darkened(0.2f), (t - 0.34f) / 0.66f);
                Paint.Track(this, c, radius - width / 2 + width * (i + 0.5f) / 4f, width / 4f + 0.6f, col);
            }
        }
        else
        {
            Paint.Track(this, c, radius, width, edge.Darkened(0.35f));
            DrawArc(c, radius + width / 2 - 0.5f, Mathf.Pi * 1.05f, Mathf.Pi * 1.95f, 48, edge.Lightened(0.1f) with { A = 0.7f }, 1.2f, true);
            DrawArc(c, radius - width / 2 + 0.5f, Mathf.Pi * 0.05f, Mathf.Pi * 0.95f, 48, edge with { A = 0.35f }, 1f, true);
        }
        Paint.Track(this, c, radius + width / 2 + 0.5f, 1.2f, dark);
        Paint.Track(this, c, radius - width / 2 - 0.3f, 1.2f, dark);
    }

    /// <summary>A broken ring cracks: jagged dark cuts across the band at fixed places.</summary>
    private void Cracks(Vector2 c, float radius, float width)
    {
        var dark = _skin.S("edge_dark");
        foreach (var deg in new[] { 28f, 141f, 233f, 312f })
        {
            var a = Mathf.DegToRad(deg - 90);
            var dir = Vector2.FromAngle(a);
            var tan = new Vector2(-dir.Y, dir.X);
            var p0 = c + dir * (radius - width * 0.7f);
            var p1 = c + dir * radius + tan * 2.2f;
            var p2 = c + dir * (radius + width * 0.7f) - tan * 1.4f;
            DrawPolyline(new[] { p0, p1, p2 }, dark, 1.6f, true);
        }
    }

    /// <summary>The numbers on the orb, drawn above the liquid.</summary>
    private sealed partial class OrbLabel : Control
    {
        private readonly OrbView _orb;

        public OrbLabel(OrbView orb) => _orb = orb;

        public override void _Draw()
        {
            if (_orb._hero is null || _orb._layout is null)
            {
                return;
            }
            var (cur, max) = _orb._hero.Resource(_orb._orb.Fill);
            if (max <= 0)
            {
                return;
            }
            var skin = _orb._skin;
            var size = Math.Max(10, (int)(skin.NumeralSize * _orb._layout.Scale));
            var c = Size / 2;
            var y = c.Y + _orb._layout.OrbRadius * 0.52f;
            var text = $"{Math.Round(cur)}";
            Paint.Text(this, skin.Numerals, new Vector2(c.X - 60, y), text, size + 2, new Color(1, 1, 1, 0.95f), HorizontalAlignment.Center, 120);
            Paint.Text(this, skin.Numerals, new Vector2(c.X - 60, y + size), $"{Math.Round(max)}", Math.Max(9, size - 2), new Color(1, 1, 1, 0.6f), HorizontalAlignment.Center, 120, 3);
        }
    }
}
