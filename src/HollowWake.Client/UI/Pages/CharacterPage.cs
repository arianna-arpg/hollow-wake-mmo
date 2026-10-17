using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Pages;

/// <summary>
/// The character popout: the sheet by the sections content lists, each row labelled and described by its
/// attribute definition with the value the self state carries, resistances by Thread in the Thread's colour,
/// and the quest log. It rebuilds when the self state changes.
/// </summary>
public partial class CharacterPage : LeafView
{
    private VBoxContainer _list = null!;
    private int _version = -1;

    public CharacterPage(LeafContext context) : base(context)
    {
        Name = "CharacterPage";
    }

    public override float PreferredWidth => Skin.Metric("page_width", 460) * 0.8f;

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_list);
    }

    /// <summary>Every row's label and value, for scripted checks.</summary>
    public int Rows { get; private set; }

    public override void Refresh()
    {
        if (Hero.Self is not { } self)
        {
            return;
        }
        var signature = Signature(self);
        if (signature == _version)
        {
            return;
        }
        _version = signature;
        Clear(_list);
        var quests = self.Quests.Select(q => new QuestLine(q.Name, q.State,
            q.Objectives.Select((o, i) => $"{o}: {(i < q.Counts.Count ? q.Counts[i] : 0)}/{(i < q.Required.Count ? q.Required[i] : 0)}").ToList())).ToList();
        var sections = SheetModel.Build(Skin.Ui.Sheet, Skin.Names, self.Attributes, quests);
        var rows = 0;
        foreach (var section in sections)
        {
            if (section.Rows.Count == 0 && section.Kind != "quests")
            {
                continue;
            }
            var head = MakeLabel(section.Name, Skin.S("accent"), Skin.TitleSize - 1, font: Skin.Title);
            _list.AddChild(head);
            if (section.Rows.Count == 0)
            {
                _list.AddChild(MakeLabel("Nothing yet.", Skin.S("ink_muted")));
            }
            foreach (var row in section.Rows)
            {
                var line = new HBoxContainer { TooltipText = row.Description, MouseFilter = MouseFilterEnum.Pass };
                var color = row.Thread is { } thread ? Skin.Thread(thread) : Skin.S("ink");
                var label = MakeLabel(row.Label, section.Kind == "quests" ? Skin.S("ink") : color);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                line.AddChild(label);
                line.AddChild(MakeLabel(row.Value, section.Kind == "quests" ? Skin.S("ink_muted") : Skin.S("ink"), font: Skin.Numerals));
                _list.AddChild(line);
                if (section.Kind == "quests" && row.Description.Length > 0)
                {
                    var objectives = MakeLabel(row.Description, Skin.S("ink_muted"), Skin.BodySize - 2, wrap: true);
                    objectives.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    _list.AddChild(objectives);
                }
                else if (row.Description.Length > 0 && row.Description.Length <= 80)
                {
                    _list.AddChild(MakeLabel("  " + row.Description, Skin.S("ink_muted"), Skin.BodySize - 3, wrap: true));
                }
                rows++;
            }
            _list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        Rows = rows;
    }

    /// <summary>What the sheet shows, hashed, so it rebuilds only when an attribute or a quest changed.</summary>
    private static int Signature(Protocol.SelfStateMsg self)
    {
        var h = new HashCode();
        foreach (var (k, v) in self.Attributes)
        {
            h.Add(k);
            h.Add(v);
        }
        foreach (var q in self.Quests)
        {
            h.Add(q.Id);
            h.Add(q.State);
            foreach (var c in q.Counts)
            {
                h.Add(c);
            }
        }
        return h.ToHashCode();
    }
}
