using Godot;

namespace HollowWake.Client.UI.Widgets;

/// <summary>
/// Drawing shared by the interface's custom surfaces: frames in the theme's style (etched stone or engraved
/// brass), wells, rings and arcs, cooldown sweeps, pips and outlined text. Everything takes its colours from
/// the skin, so the same calls draw either texture direction.
/// </summary>
public static class Paint
{
    private const float Tau = Mathf.Tau;

    /// <summary>A soft shadow under a rectangle.</summary>
    public static void Shadow(CanvasItem ci, Rect2 r, float strength, float spread = 8)
    {
        for (var i = (int)spread; i >= 1; i--)
        {
            var a = strength * 0.1f * (1 - i / (spread + 1));
            ci.DrawRect(new Rect2(r.Position + new Vector2(-i * 0.6f, -i * 0.4f + 2), r.Size + new Vector2(i * 1.2f, i * 1.2f)), new Color(0, 0, 0, a));
        }
    }

    /// <summary>The frame of a pane in the theme's style, drawn over its surface.</summary>
    public static void Frame(CanvasItem ci, Skin skin, Rect2 r, bool emphasised = false)
    {
        if (skin.Brass)
        {
            BrassFrame(ci, skin, r, emphasised);
        }
        else
        {
            StoneFrame(ci, skin, r, emphasised);
        }
    }

    private static void Sides(CanvasItem ci, Rect2 r, float inset, Color top, Color left, Color bottom, Color right, float width = 1)
    {
        var a = r.Position + new Vector2(inset, inset);
        var b = new Vector2(r.End.X - inset - 1, r.Position.Y + inset);
        var c = r.End - new Vector2(inset + 1, inset + 1);
        var d = new Vector2(r.Position.X + inset, r.End.Y - inset - 1);
        ci.DrawLine(a, b, top, width);
        ci.DrawLine(a, d, left, width);
        ci.DrawLine(d, c, bottom, width);
        ci.DrawLine(b, c, right, width);
    }

    private static void StoneFrame(CanvasItem ci, Skin skin, Rect2 r, bool emphasised)
    {
        var bevel = Math.Max(1, (int)skin.Mat("bevel", 3));
        var light = skin.S("edge");
        var dark = skin.S("edge_dark");
        var pane = skin.S("pane");
        for (var i = 0; i < bevel; i++)
        {
            var t = (float)i / bevel;
            Sides(ci, r, i, light.Lerp(pane, t * 0.85f), light.Lerp(pane, 0.15f + t * 0.8f), dark.Lerp(pane, t * 0.6f), dark.Lerp(pane, t * 0.6f));
        }
        Sides(ci, r, 0, dark, dark, dark, dark);
        // An incised groove inside the bevel: a dark cut with a lit lower lip.
        var g = bevel + 3;
        var lip = light with { A = 0.35f };
        Sides(ci, r, g, dark with { A = 0.9f }, dark with { A = 0.9f }, lip, lip);
        Sides(ci, r, g + 1, lip, lip, dark with { A = 0.5f }, dark with { A = 0.5f });
        var etch = skin.S("etch") with { A = skin.Mat("etch", 0.5f) * (emphasised ? 1f : 0.75f) };
        foreach (var corner in Corners(r, g + 6))
        {
            Rune(ci, corner, 4.5f, etch);
        }
        if (emphasised)
        {
            Sides(ci, r, 1, skin.S("accent") with { A = 0.55f }, skin.S("accent") with { A = 0.55f }, skin.S("accent") with { A = 0.3f }, skin.S("accent") with { A = 0.3f });
        }
    }

    private static void BrassFrame(CanvasItem ci, Skin skin, Rect2 r, bool emphasised)
    {
        var band = Math.Max(3, (int)skin.Mat("bevel", 4) + 2);
        var brass = skin.S("edge");
        var dark = skin.S("edge_dark");
        var bright = brass.Lightened(0.38f);
        for (var i = 0; i < band; i++)
        {
            var t = (float)i / Math.Max(1, band - 1);
            // across the band: dark rim, bright crest, brass body, dark seat
            var c = t < 0.2f ? dark.Lerp(bright, t / 0.2f) : t < 0.45f ? bright.Lerp(brass, (t - 0.2f) / 0.25f) : brass.Lerp(dark, (t - 0.45f) / 0.55f * 0.7f);
            Sides(ci, r, i, c.Lightened(0.08f), c, c.Darkened(0.12f), c.Darkened(0.06f));
        }
        Sides(ci, r, 0, dark, dark, dark, dark);
        Sides(ci, r, band, dark with { A = 0.85f }, dark with { A = 0.85f }, dark with { A = 0.6f }, dark with { A = 0.6f });
        var etch = skin.S("etch") with { A = skin.Mat("etch", 0.6f) * 0.8f };
        var mid = band * 0.62f;
        Sides(ci, r, mid, etch, etch, etch, etch);
        foreach (var corner in Corners(r, band / 2f))
        {
            Rivet(ci, corner, Math.Max(2.2f, band * 0.42f), brass, dark);
        }
        if (r.Size.X > 220)
        {
            Rivet(ci, new Vector2(r.Position.X + r.Size.X / 2, r.Position.Y + band / 2f), Math.Max(2f, band * 0.36f), brass, dark);
            Rivet(ci, new Vector2(r.Position.X + r.Size.X / 2, r.End.Y - band / 2f), Math.Max(2f, band * 0.36f), brass, dark);
        }
        if (emphasised)
        {
            Sides(ci, r, band + 1, skin.S("accent") with { A = 0.5f }, skin.S("accent") with { A = 0.5f }, skin.S("accent") with { A = 0.35f }, skin.S("accent") with { A = 0.35f });
        }
    }

    private static IEnumerable<Vector2> Corners(Rect2 r, float inset)
    {
        yield return r.Position + new Vector2(inset, inset);
        yield return new Vector2(r.End.X - inset, r.Position.Y + inset);
        yield return r.End - new Vector2(inset, inset);
        yield return new Vector2(r.Position.X + inset, r.End.Y - inset);
    }

    /// <summary>A small diamond rune cut into a surface.</summary>
    public static void Rune(CanvasItem ci, Vector2 at, float size, Color color)
    {
        var pts = new[] { at + new Vector2(0, -size), at + new Vector2(size, 0), at + new Vector2(0, size), at + new Vector2(-size, 0), at + new Vector2(0, -size) };
        ci.DrawPolyline(pts, color, 1f, true);
        ci.DrawLine(at + new Vector2(0, -size * 0.45f), at + new Vector2(0, size * 0.45f), color, 1f, true);
    }

    public static void Rivet(CanvasItem ci, Vector2 at, float radius, Color brass, Color dark)
    {
        ci.DrawCircle(at, radius + 0.8f, dark, true, -1, true);
        ci.DrawCircle(at, radius, brass.Darkened(0.12f), true, -1, true);
        ci.DrawCircle(at + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.45f, brass.Lightened(0.55f), true, -1, true);
    }

    /// <summary>An inset well (a slot, a cell): dark floor, shadowed top-left, lit bottom-right lip.</summary>
    public static void Well(CanvasItem ci, Skin skin, Rect2 r, Color floor, bool hot = false)
    {
        ci.DrawRect(r, floor);
        var dark = skin.S("edge_dark");
        var light = skin.S("edge");
        ci.DrawLine(r.Position, new Vector2(r.End.X, r.Position.Y), dark with { A = 0.95f }, 2);
        ci.DrawLine(r.Position, new Vector2(r.Position.X, r.End.Y), dark with { A = 0.95f }, 2);
        ci.DrawLine(new Vector2(r.Position.X, r.End.Y - 0.5f), r.End - new Vector2(0, 0.5f), light with { A = 0.4f }, 1);
        ci.DrawLine(new Vector2(r.End.X - 0.5f, r.Position.Y), r.End - new Vector2(0.5f, 0), light with { A = 0.4f }, 1);
        if (hot)
        {
            ci.DrawRect(r.Grow(-1), skin.S("accent") with { A = 0.9f }, false, 2);
        }
    }

    /// <summary>A band around a circle from the top, clockwise for a positive fraction and counter-clockwise for a negative one.</summary>
    public static void Arc(CanvasItem ci, Vector2 center, float radius, float width, float fraction, Color color, bool mirror = false)
    {
        if (Math.Abs(fraction) <= 0.0005f)
        {
            return;
        }
        var start = -Mathf.Pi / 2;
        var sweep = Math.Clamp(fraction, 0, 1) * Tau * (mirror ? -1 : 1);
        var points = Math.Max(8, (int)(Math.Abs(sweep) / Tau * 120));
        ci.DrawArc(center, radius, start, start + sweep, points, color, width, true);
    }

    /// <summary>A full track for a ring.</summary>
    public static void Track(CanvasItem ci, Vector2 center, float radius, float width, Color color) =>
        ci.DrawArc(center, radius, 0, Tau, 128, color, width, true);

    /// <summary>Radial ticks etched across a band, every <paramref name="stepDegrees"/>.</summary>
    public static void Ticks(CanvasItem ci, Vector2 center, float inner, float outer, float stepDegrees, Color color, float width = 1)
    {
        for (var a = 0f; a < 360f; a += stepDegrees)
        {
            var dir = Vector2.FromAngle(Mathf.DegToRad(a - 90));
            ci.DrawLine(center + dir * inner, center + dir * outer, color, width, true);
        }
    }

    /// <summary>
    /// Runes cut along a band: each a stem across the band with one or two branches, varied by position so the
    /// band reads as script rather than a dashed line.
    /// </summary>
    public static void RuneBand(CanvasItem ci, Vector2 center, float radius, float width, int count, Color color)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.DegToRad(360f * i / count - 90 + 360f / count / 2);
            var dir = Vector2.FromAngle(angle);
            var tangent = new Vector2(-dir.Y, dir.X);
            var inner = center + dir * (radius - width * 0.38f);
            var outer = center + dir * (radius + width * 0.38f);
            ci.DrawLine(inner, outer, color, 1.1f, true);
            var mid = (inner + outer) / 2;
            var branch = width * 0.32f;
            switch (i % 4)
            {
                case 0:
                    ci.DrawLine(mid, mid + (tangent + dir) * branch * 0.7f, color, 1f, true);
                    break;
                case 1:
                    ci.DrawLine(outer, outer + (tangent - dir) * branch * 0.7f, color, 1f, true);
                    ci.DrawLine(mid, mid + (tangent - dir) * branch * 0.7f, color, 1f, true);
                    break;
                case 2:
                    ci.DrawLine(mid - tangent * branch * 0.5f, mid + tangent * branch * 0.5f, color, 1f, true);
                    break;
                default:
                    ci.DrawLine(inner, inner + (-tangent + dir) * branch * 0.7f, color, 1f, true);
                    break;
            }
        }
    }

    /// <summary>A pie from the top, clockwise, covering a fraction of a rectangle (a cooldown's remaining part).</summary>
    public static void Sweep(CanvasItem ci, Rect2 r, float fraction, Color color)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0.001f)
        {
            return;
        }
        var center = r.GetCenter();
        var radius = r.Size.Length();
        var steps = Math.Max(4, (int)(fraction * 64));
        var pts = new List<Vector2>(steps + 2) { center };
        var start = -Mathf.Pi / 2;
        // The remaining part runs clockwise from where the sweep has reached back to the top.
        var from = start + (1 - fraction) * Tau;
        for (var i = 0; i <= steps; i++)
        {
            var a = from + (start + Tau - from) * i / steps;
            var p = center + Vector2.FromAngle(a) * radius;
            pts.Add(new Vector2(Math.Clamp(p.X, r.Position.X, r.End.X), Math.Clamp(p.Y, r.Position.Y, r.End.Y)));
        }
        ci.DrawColoredPolygon(pts.ToArray(), color);
    }

    /// <summary>A row of pips, filled for those held.</summary>
    public static void Pips(CanvasItem ci, Vector2 rightTop, int held, int max, float radius, Color on, Color off)
    {
        for (var i = 0; i < max; i++)
        {
            var p = rightTop + new Vector2(-i * (radius * 2.6f) - radius, radius);
            ci.DrawCircle(p, radius + 0.8f, new Color(0, 0, 0, 0.7f), true, -1, true);
            ci.DrawCircle(p, radius, i < held ? on : off, true, -1, true);
        }
    }

    /// <summary>Text with a dark outline, for numbers on orbs and slots.</summary>
    public static void Text(CanvasItem ci, Font font, Vector2 baseline, string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1, int outline = 4)
    {
        if (outline > 0)
        {
            ci.DrawStringOutline(font, baseline, text, align, width, size, outline, new Color(0, 0, 0, 0.85f));
        }
        ci.DrawString(font, baseline, text, align, width, size, color);
    }

    /// <summary>A badge: a coloured disc with a count.</summary>
    public static void Badge(CanvasItem ci, Skin skin, Vector2 center, string text, float radius = 9)
    {
        if (text.Length == 0)
        {
            return;
        }
        var w = Math.Max(radius * 2, skin.Numerals.GetStringSize(text, HorizontalAlignment.Left, -1, (int)(radius * 1.25f)).X + 8);
        var r = new Rect2(center - new Vector2(w / 2, radius), new Vector2(w, radius * 2));
        var badge = skin.State("badge", new Color(0.7f, 0.15f, 0.12f));
        ci.DrawRect(r.Grow(1), new Color(0, 0, 0, 0.75f));
        ci.DrawRect(r, badge);
        ci.DrawRect(r, badge.Lightened(0.3f), false, 1);
        ci.DrawString(skin.Numerals, new Vector2(r.Position.X, center.Y + radius * 0.45f), text, HorizontalAlignment.Center, r.Size.X, (int)(radius * 1.25f), Colors.White);
    }
}
