using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The hero's statuses in a row above the cast bar: each a glyph tinted by the Thread whose ailment it is, its
/// stacks, and a sweep for the time left against its full duration (counted down from when the self state
/// arrived). The tooltip names each status from content.
/// </summary>
public partial class StatusRow : Control
{
    private readonly Skin _skin;
    private IReadOnlyList<StatusInfo> _statuses = Array.Empty<StatusInfo>();
    private float _icon;
    private double _now;
    private string _tip = "";

    public StatusRow(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Pass;
    }

    public int Count => _statuses.Count;

    public void Update(ClusterLayout layout, HeroState hero, double now)
    {
        // Statuses content marks hidden (the pool states the orbs draw) stay out of the row.
        _statuses = hero.Statuses(now).Where(s => !_skin.Names.LookOf(s.Id).Hidden).ToList();
        _now = now;
        _icon = layout.StatusIcon;
        var width = _statuses.Count * (_icon + 4);
        Position = new Vector2(layout.Status.CenterX - width / 2, layout.Status.Y);
        Size = new Vector2(Math.Max(1, width), layout.Status.H);
        var tip = string.Join("\n", _statuses.Select(s => $"{_skin.Names.StatusName(s.Id)}{(s.Stacks > 1 ? " x" + s.Stacks : "")}{(s.Remaining >= 0 ? $"  {s.Remaining:0.0}s" : "")}"));
        if (tip != _tip)
        {
            _tip = tip;
            TooltipText = tip;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < _statuses.Count; i++)
        {
            var s = _statuses[i];
            var r = new Rect2(i * (_icon + 4), 0, _icon, _icon);
            Paint.Well(this, _skin, r, _skin.S("well") with { A = 0.92f });
            var look = _skin.Names.LookOf(s.Id);
            var (glyph, tint) = _skin.StatusFace(s.Id);
            if (look.Kind is "buff" or "debuff")
            {
                DrawRect(r.Grow(-1), _skin.S(look.Kind == "buff" ? "good" : "bad") with { A = 0.85f }, false, 1.5f);
            }
            var alpha = look.Cue == "pulse" && !Motion.Reduced ? 0.6f + 0.4f * Mathf.Sin((float)_now * 5f) : 1f;
            DrawTextureRect(_skin.Icon(glyph, (int)(_icon * 0.78f)), r.Grow(-_icon * 0.11f), false, tint with { A = alpha });
            if (s.Duration > 0 && s.Remaining >= 0)
            {
                var spent = 1 - (float)Math.Clamp(s.Remaining / s.Duration, 0, 1);
                Paint.Sweep(this, r.Grow(-1), spent, new Color(0, 0, 0, 0.5f));
            }
            if (s.Stacks > 1)
            {
                Paint.Text(this, _skin.Numerals, new Vector2(r.Position.X, r.End.Y - 2), s.Stacks.ToString(), Math.Max(9, (int)(_icon * 0.4f)), Colors.White, HorizontalAlignment.Right, _icon - 2, 3);
            }
        }
    }
}
