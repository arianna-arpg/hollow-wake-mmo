using Godot;
using HollowWake.Protocol;

namespace HollowWake.Client.UI;

/// <summary>Minimal in-world UI: resources, weave bar, target, notices, chat, a character sheet. Placeholder by design.</summary>
public partial class Hud : CanvasLayer
{
    private Label _title = null!;
    private VBoxContainer _bars = null!;
    private readonly Dictionary<string, ProgressBar> _resourceBars = new();
    private HBoxContainer _weaveBar = null!;
    private readonly List<Button> _slots = new();
    private RichTextLabel _chat = null!;
    private LineEdit _chatInput = null!;
    private Label _notice = null!;
    private Label _target = null!;
    private Label _hint = null!;
    private PanelContainer _sheet = null!;
    private RichTextLabel _sheetText = null!;
    private PanelContainer _quests = null!;
    private VBoxContainer _questList = null!;
    private double _noticeUntil;

    public event Action<int>? SlotPressed;
    public event Action<string>? ChatSubmitted;
    public event Action<string, long>? QuestAccepted;
    public event Action<string, long>? QuestTurnedIn;

    public bool ChatFocused => _chatInput.HasFocus();
    public bool SheetVisible => _sheet.Visible;
    public bool QuestsVisible => _quests.Visible;

    public override void _Ready()
    {
        Layer = 10;
        var left = new VBoxContainer { Name = "left" };
        left.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        left.Position = new Vector2(12, 12);
        AddChild(left);
        _title = new Label { Text = "Hollow Wake MMO" };
        left.AddChild(_title);
        _bars = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        left.AddChild(_bars);
        _target = new Label { Text = "" };
        left.AddChild(_target);
        _hint = new Label { Text = "WASD or click to move, Space jumps, 1-8 weaves, F interact, G pick up, V camera, Q/E rotate, C sheet, Enter chat", Modulate = new Color(1, 1, 1, 0.6f) };
        left.AddChild(_hint);

        _notice = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _notice.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _notice.Position = new Vector2(-300, 24);
        _notice.CustomMinimumSize = new Vector2(600, 30);
        _notice.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_notice);

        _weaveBar = new HBoxContainer { Name = "weaves" };
        _weaveBar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _weaveBar.Position = new Vector2(-8 * 96 / 2f, -70);
        AddChild(_weaveBar);
        for (var i = 0; i < 8; i++)
        {
            var slot = i;
            var b = new Button { Text = $"{i + 1}\n-", CustomMinimumSize = new Vector2(92, 56), FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () => SlotPressed?.Invoke(slot);
            _weaveBar.AddChild(b);
            _slots.Add(b);
        }

        var chatBox = new VBoxContainer { Name = "chat" };
        chatBox.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        chatBox.Position = new Vector2(12, -220);
        chatBox.CustomMinimumSize = new Vector2(396, 200);
        AddChild(chatBox);
        _chat = new RichTextLabel { CustomMinimumSize = new Vector2(396, 170), ScrollFollowing = true, BbcodeEnabled = true, FocusMode = Control.FocusModeEnum.None };
        _chat.AddThemeColorOverride("default_color", Colors.White);
        chatBox.AddChild(_chat);
        _chatInput = new LineEdit { PlaceholderText = "Enter to chat" };
        _chatInput.TextSubmitted += text =>
        {
            if (text.Trim().Length > 0)
            {
                ChatSubmitted?.Invoke(text.Trim());
            }
            _chatInput.Text = "";
            _chatInput.ReleaseFocus();
        };
        chatBox.AddChild(_chatInput);

        _sheet = new PanelContainer { Visible = false };
        _sheet.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _sheet.Position = new Vector2(-440, 12);
        _sheet.CustomMinimumSize = new Vector2(428, 640);
        AddChild(_sheet);
        _sheetText = new RichTextLabel { BbcodeEnabled = true, FocusMode = Control.FocusModeEnum.None };
        _sheet.AddChild(_sheetText);

        _quests = new PanelContainer { Visible = false };
        _quests.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _quests.Position = new Vector2(-460, -200);
        _quests.CustomMinimumSize = new Vector2(440, 300);
        AddChild(_quests);
        _questList = new VBoxContainer();
        _quests.AddChild(_questList);
    }

    public void FocusChat() => _chatInput.GrabFocus();

    public void ToggleSheet() => _sheet.Visible = !_sheet.Visible;

    public void SetTitle(string text) => _title.Text = text;

    public void SetTarget(string text) => _target.Text = text;

    public void Notice(string text, string level)
    {
        _notice.Text = text;
        _notice.Modulate = level switch { "levelup" => new Color(1f, 0.85f, 0.3f), "quest" => new Color(0.7f, 1f, 0.7f), "event" => new Color(0.9f, 0.6f, 1f), "discovery" => new Color(0.6f, 0.9f, 1f), "error" => new Color(1f, 0.5f, 0.5f), _ => Colors.White };
        _noticeUntil = Time.GetTicksMsec() / 1000.0 + 4;
        Log($"[color=#aaaaaa]{text}[/color]");
    }

    public void Log(string bbcode) => _chat.AppendText(bbcode + "\n");

    public void Chat(string from, string channel, string text) => Log($"[color=#ffd27a]{Escape(from)}[/color] [{channel}]: {Escape(text)}");

    private static string Escape(string s) => s.Replace("[", "[lb]");

    public override void _Process(double delta)
    {
        if (_notice.Text.Length > 0 && Time.GetTicksMsec() / 1000.0 > _noticeUntil)
        {
            _notice.Text = "";
        }
    }

    public void ApplySelf(SelfStateMsg self, string zoneName)
    {
        _title.Text = $"Level {self.Level}  xp {self.Xp:0}/{self.XpToNext:0}  points {self.PassivePoints}   {zoneName}";
        foreach (var (res, value) in self.Resources)
        {
            if (!_resourceBars.TryGetValue(res, out var bar))
            {
                bar = new ProgressBar { CustomMinimumSize = new Vector2(260, 18), ShowPercentage = false };
                var style = new StyleBoxFlat { BgColor = res switch { "life" => new Color(0.75f, 0.2f, 0.2f), "mana" => new Color(0.2f, 0.45f, 0.85f), "stamina" => new Color(0.25f, 0.65f, 0.3f), "ward" => new Color(0.9f, 0.75f, 0.2f), _ => Colors.Gray } };
                bar.AddThemeStyleboxOverride("fill", style);
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = res, CustomMinimumSize = new Vector2(64, 0) });
                row.AddChild(bar);
                _bars.AddChild(row);
                _resourceBars[res] = bar;
            }
            var max = self.ResourceMax.GetValueOrDefault(res, 1);
            bar.MaxValue = Math.Max(1, max);
            bar.Value = value;
            bar.TooltipText = $"{value:0}/{max:0}";
        }
        for (var i = 0; i < _slots.Count; i++)
        {
            var w = i < self.Weaves.Count ? self.Weaves[i] : null;
            if (w is null)
            {
                _slots[i].Text = $"{i + 1}\n-";
                _slots[i].Disabled = true;
                continue;
            }
            _slots[i].Disabled = false;
            var cd = w.ReadyIn > 0.05 ? $" ({w.ReadyIn:0.0}s)" : "";
            var cost = string.Join(" ", w.Costs.Select(kv => $"{kv.Value:0}{kv.Key[0]}"));
            _slots[i].Text = $"{i + 1} {w.Name}{cd}\n{cost}";
        }
        var sb = new System.Text.StringBuilder();
        sb.Append($"[b]Character[/b]  level {self.Level}\n");
        foreach (var (k, v) in self.Attributes.OrderBy(kv => kv.Key))
        {
            if (k.Contains(".max") || k.StartsWith("resist.") || k.StartsWith("pen.") || k.StartsWith("convert.") || k.StartsWith("extra.") || k.StartsWith("weapon."))
            {
                continue;
            }
            sb.Append($"{k}: {v:0.##}\n");
        }
        sb.Append("\n[b]Resistances[/b]\n");
        foreach (var (k, v) in self.Attributes.Where(kv => kv.Key.StartsWith("resist.") && !kv.Key.EndsWith(".max")).OrderBy(kv => kv.Key))
        {
            sb.Append($"{k[7..]}: {v * 100:0}%\n");
        }
        sb.Append("\n[b]Equipment[/b]\n");
        foreach (var (slot, item) in self.Equipment)
        {
            sb.Append($"{slot}: [color=#{RarityColor(item.Rarity)}]{Escape(item.Name)}[/color]\n");
        }
        sb.Append($"\n[b]Bag[/b] {self.Bag.Count}/{self.BagCapacity}\n");
        foreach (var item in self.Bag.Take(20))
        {
            sb.Append($"[color=#{RarityColor(item.Rarity)}]{Escape(item.Name)}[/color]{(item.Count > 1 ? " x" + item.Count : "")}\n");
        }
        sb.Append("\n[b]Currency[/b] " + string.Join(", ", self.Currency.Select(kv => $"{kv.Key} {kv.Value:0}")) + "\n");
        sb.Append("\n[b]Quests[/b]\n");
        foreach (var q in self.Quests)
        {
            sb.Append($"{Escape(q.Name)} [{q.State}]\n");
            for (var i = 0; i < q.Objectives.Count; i++)
            {
                sb.Append($"   {Escape(q.Objectives[i])}: {q.Counts[i]}/{q.Required[i]}\n");
            }
        }
        _sheetText.Text = sb.ToString();
    }

    private static string RarityColor(string rarity) => rarity switch { "magic" => "6c9cff", "rare" => "ffd34d", "unique" => "e8622a", _ => "d0d0d0" };

    public void ShowQuestOffers(QuestOffersMsg offers)
    {
        foreach (var child in _questList.GetChildren())
        {
            child.QueueFree();
        }
        _questList.AddChild(new Label { Text = "Quests" });
        foreach (var q in offers.Offers)
        {
            var b = new Button { Text = $"Accept: {q.Name} [L{q.Level}]", FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () =>
            {
                QuestAccepted?.Invoke(q.Id, offers.Giver);
                _quests.Visible = false;
            };
            _questList.AddChild(b);
        }
        foreach (var q in offers.TurnIns)
        {
            var b = new Button { Text = $"Turn in: {q.Name}", FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () =>
            {
                QuestTurnedIn?.Invoke(q.Id, offers.Giver);
                _quests.Visible = false;
            };
            _questList.AddChild(b);
        }
        if (offers.Offers.Count == 0 && offers.TurnIns.Count == 0)
        {
            _questList.AddChild(new Label { Text = "Nothing for you right now." });
        }
        var close = new Button { Text = "Close", FocusMode = Control.FocusModeEnum.None };
        close.Pressed += () => _quests.Visible = false;
        _questList.AddChild(close);
        _quests.Visible = true;
    }

    public void HideQuests() => _quests.Visible = false;
}
