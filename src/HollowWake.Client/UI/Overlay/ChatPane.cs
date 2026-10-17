using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Overlay;

/// <summary>
/// The chat and combat log in the lower left corner: a translucent framed log and the line Enter focuses. It
/// keeps to the corner beside the cluster, and stands above the cluster when the screen is too narrow.
/// </summary>
public partial class ChatPane : Control
{
    private readonly Skin _skin;
    private RichTextLabel _log = null!;
    private LineEdit _input = null!;
    private ColorRect _surface = null!;
    private ShaderMaterial _material = null!;
    private FrameLayer _frame = null!;
    private int _lines;

    public event Action<string>? Submitted;

    public ChatPane(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Pass;
        Theme = skin.Theme;
    }

    public bool Focused => _input.HasFocus();
    public string Text => _log.GetParsedText();

    public override void _Ready()
    {
        _material = _skin.SurfaceMaterial(new Vector2(400, 200), 3.1f, opacity: 0.72f);
        _surface = new ColorRect { Material = _material, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_surface);
        _log = new RichTextLabel
        {
            ScrollFollowing = true,
            BbcodeEnabled = true,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Pass,
            SelectionEnabled = false,
        };
        _log.AddThemeColorOverride("default_color", _skin.S("ink"));
        _log.AddThemeFontOverride("normal_font", _skin.Body);
        _log.AddThemeFontSizeOverride("normal_font_size", _skin.BodySize - 1);
        AddChild(_log);
        _input = new LineEdit { PlaceholderText = "Enter to chat", MaxLength = 400, ContextMenuEnabled = false };
        _input.TextSubmitted += text =>
        {
            if (text.Trim().Length > 0)
            {
                Submitted?.Invoke(text.Trim());
            }
            _input.Text = "";
            _input.ReleaseFocus();
        };
        AddChild(_input);
        _frame = new FrameLayer(_skin);
        AddChild(_frame);
    }

    public void Focus() => _input.GrabFocus();

    public void Log(string bbcode)
    {
        _log.AppendText(bbcode + "\n");
        _lines++;
        var cap = (int)_skin.Metric("chat_lines", 200);
        if (_lines > cap)
        {
            _log.RemoveParagraph(0);
            _lines--;
        }
    }

    public void Place(Vector2 viewport, Box cluster, float scale)
    {
        var edge = _skin.Metric("edge", 14);
        var width = _skin.Metric("chat_width", 420) * Math.Max(0.75f, scale);
        var height = _skin.Metric("chat_height", 200) * Math.Max(0.75f, scale);
        var room = cluster.X - edge * 2;
        float x = edge;
        float bottom = viewport.Y - edge;
        if (room >= 280)
        {
            width = Math.Min(width, room);
        }
        else
        {
            bottom = cluster.Y - edge;
        }
        Position = new Vector2(x, bottom - height);
        Size = new Vector2(width, height);
        _surface.Size = Size;
        _skin.UpdateSurface(_material, Size, 3.1f, opacity: 0.72f);
        var pad = 8f;
        var inputH = _skin.BodySize + 14;
        _log.Position = new Vector2(pad, pad);
        _log.Size = new Vector2(width - pad * 2, height - inputH - pad * 2 - 4);
        _input.Position = new Vector2(pad, height - inputH - pad);
        _input.Size = new Vector2(width - pad * 2, inputH);
        _frame.Fit(Size);
    }
}
