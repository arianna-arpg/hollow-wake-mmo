using Godot;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Overlay;

/// <summary>
/// Notices at the top centre under the target plate, in the colour of their level, fading after a while; and
/// combat text rising above the hero: the hero's hits, the hits it takes and its heals.
/// </summary>
public partial class Notices : Control
{
    private readonly Skin _skin;
    private readonly List<Floater> _floaters = new();
    private string _notice = "";
    private string _level = "info";
    private double _noticeUntil;
    private double _noticeAt;
    private double _now;
    private Vector2 _anchor;
    private bool _hasAnchor;

    private sealed record Floater(string Text, Color Color, double At, float Drift, bool Big, float Weight);

    public Notices(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public string Current => _now < _noticeUntil ? _notice : "";

    /// <summary>How long a notice stays (the notice time option).</summary>
    public double Seconds { get; set; } = 4;

    public void Notice(string text, string level, double now)
    {
        _notice = text;
        _level = level;
        _noticeAt = now;
        _noticeUntil = now + Seconds;
    }

    /// <summary>Text rising above the hero.</summary>
    /// <summary>A number rising over the hero; weight (zero to one) is how heavy the blow was for its target, which sizes and pops it.</summary>
    public void Float(string text, Color color, double now, bool big = false, float weight = 0)
    {
        var drift = (float)((_floaters.Count % 5) - 2) * 9f;
        _floaters.Add(new Floater(text, color, now, drift, big, Math.Clamp(weight, 0, 1)));
        if (_floaters.Count > 24)
        {
            _floaters.RemoveAt(0);
        }
    }

    /// <summary>Where the hero's head is on screen this frame, or nowhere.</summary>
    public void Update(Vector2 viewport, Vector2? hero, double now)
    {
        _now = now;
        Position = Vector2.Zero;
        Size = viewport;
        _hasAnchor = hero.HasValue;
        _anchor = hero ?? Vector2.Zero;
        var life = _skin.Metric("combat_text_seconds", 1.3f);
        _floaters.RemoveAll(f => now - f.At > life);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_now < _noticeUntil && _notice.Length > 0)
        {
            var fadeIn = (float)Math.Clamp((_now - _noticeAt) / 0.2, 0, 1);
            var fadeOut = (float)Math.Clamp((_noticeUntil - _now) / 0.6, 0, 1);
            var color = _skin.Notice(_level) with { A = Math.Min(fadeIn, fadeOut) };
            Paint.Text(this, _skin.Title, new Vector2(0, 118), _notice, _skin.TitleSize + 5, color, HorizontalAlignment.Center, Size.X, 6);
        }
        if (!_hasAnchor)
        {
            return;
        }
        var life = _skin.Metric("combat_text_seconds", 1.3f);
        foreach (var f in _floaters)
        {
            var t = (float)((_now - f.At) / life);
            // a heavy blow rises slower and lands with a pop; a light one drifts off quickly
            var rise = 60f - 20f * f.Weight;
            var pop = Motion.Reduced ? 1f : 1f + 0.5f * Math.Max(0f, 1f - t / 0.12f) * (0.4f + f.Weight);
            var p = _anchor + new Vector2(f.Drift + t * f.Drift * 0.6f, -24 - t * rise);
            var size = (f.Big ? _skin.TitleSize + 6 : _skin.NumeralSize + 3) * (1f + 0.7f * f.Weight) * pop;
            Paint.Text(this, _skin.Numerals, p - new Vector2(80, 0), f.Text, (int)MathF.Round(size), f.Color with { A = 1 - t * t }, HorizontalAlignment.Center, 160, 5);
        }
    }
}
