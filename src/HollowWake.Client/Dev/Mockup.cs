using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Maths;
using HollowWake.Protocol;

namespace HollowWake.Client.Dev;

/// <summary>
/// Renders the interface over locally generated terrain with a staged hero, with no server, and saves small
/// screenshots of the cluster and the workspace in each theme, for judging a texture direction:
/// <c>godot --path src/HollowWake.Client -- --mockup DIR [--themes a,b]</c>. The staged states use every field
/// the interface draws (pools before life, reserved mana, statuses, a cast, cooldowns, a dormant glyph, a
/// vendor beside the bag), which a fresh character in play does not yet show.
/// </summary>
public partial class Mockup : Node
{
    private readonly Main _main;
    private readonly string[] _args;
    private string _dir = "mockups";
    private List<string> _themes = new();
    private System.Numerics.Vector3 _at;

    public Mockup(Main main, string[] args)
    {
        _main = main;
        _args = args;
    }

    public override void _Ready()
    {
        for (var i = 0; i + 1 < _args.Length; i++)
        {
            switch (_args[i])
            {
                case "--mockup":
                    _dir = _args[i + 1];
                    break;
                case "--themes":
                    _themes = _args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    break;
            }
        }
        if (_themes.Count == 0)
        {
            _themes = _main.Content.RawOfKind("ui_themes").Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
        _ = Run();
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Run()
    {
        try
        {
            await Stage();
            foreach (var theme in _themes)
            {
                _main.UseTheme(theme);
                Feed();
                await Shots(theme);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"MOCKUP CRASH {ex}");
        }
        GetTree().Quit();
    }

    private async Task Stage()
    {
        await Frame();
        var seed = Rng.Hash64(_main.Content.Rules.Text("world.seed", "aureth"));
        var layout = Core.World.WorldLayout.Default;
        _main.World.ApplyWorldInfo(new WorldInfo(seed, layout.ChunkSize, layout.ZoneChunks, layout.RegionZones, new List<OverlayInfo>(), ""));
        var generator = _main.World.Generator!;
        var (x, y) = generator.StartPoint(generator.StartZone());
        _at = new System.Numerics.Vector3((float)x + 6, (float)y - 4, 0);
        var deadline = Time.GetTicksMsec() / 1000.0 + 90;
        while (Time.GetTicksMsec() / 1000.0 < deadline)
        {
            _main.World.Stream(_at);
            _at.Z = _main.World.HeightAt(_at.X, _at.Y);
            _main.Rig.Follow(Coords.ToGodot(_at.X, _at.Y, _at.Z));
            if (_main.World.BuiltChunks > 0 && _main.World.PendingChunks == 0)
            {
                break;
            }
            await Frame();
        }
        _main.Login.Visible = false;
        GD.Print($"MOCKUP terrain: {_main.World.BuiltChunks} chunks around {_at.X:0}, {_at.Y:0}");
    }

    /// <summary>Feeds the current interface the staged hero.</summary>
    private void Feed()
    {
        var hud = _main.Hud;
        hud.Visible = true;
        hud.Hero.Interface = true;
        hud.InWorld = true;
        // The staged vendor stands beside the hero, so its pane does not depart.
        hud.DistanceTo = _ => 1f;
        hud.Hero.ObserveServerTime(1000, Time.GetTicksMsec() / 1000.0);
        hud.ApplySelf(Self());
        hud.ApplySlots(Slots());
        hud.ApplyInventory(Inventory());
        hud.ApplyPassives(new PassiveStateMsg(new List<string> { "start_kindler", "kd1", "kd2", "inner_a", "inner_b" }, new Dictionary<string, List<string>>(), new Dictionary<string, int> { ["passive"] = 3 }, new List<string> { "star" }, false));
        hud.ApplyPages(new PageStateMsg(new List<PageInfo>
        {
            new("inventory", false, 0),
            new("weaves", false, 1),
            new("passives", false, 3),
            new("reliquary", true, 0) { Reason = "Opens at the shrine in Hollowmere" },
            new("character", false, 0),
        }));
        hud.SetSelfEntity(new EntityState { Key = 1, Name = "Ysolde", Statuses = new List<string> { "burning", "hasted", "regrowth" } });
        hud.SetTarget(new EntityState
        {
            Key = 42,
            Name = "Hob Skulker",
            Level = 7,
            Rarity = "magic",
            Resources = new Dictionary<string, float> { ["life"] = 168, ["ward"] = 30 },
            ResourceMax = new Dictionary<string, float> { ["life"] = 240, ["ward"] = 60 },
            Statuses = new List<string> { "chilled", "bleeding" },
        });
        hud.Log("[color=#a8f0a0]Entered the world at 412, -96 (zone Hollowmere Reach).[/color]");
        hud.Chat("Brannoch", "zone", "Anyone for the barrow at dusk?");
        hud.Chat("Ysolde", "zone", "On my way, bring tide wards.");
        hud.Log("Ysolde hits Hob Skulker for 46 (crit)");
        hud.Notice("Hollowmere Reach", "discovery");
    }

    private static SelfStateMsg Self() => new(
        1, 14, 2210, 3400, 3, new List<string> { "start_kindler", "kd1", "kd2", "inner_a", "inner_b" },
        new Dictionary<string, double>
        {
            ["might"] = 12, ["grace"] = 15, ["will"] = 23, ["vigor"] = 14,
            ["life.max"] = 412, ["mana.max"] = 318, ["stamina.max"] = 100, ["ward.max"] = 80,
            ["crit.chance"] = 0.075, ["spell.power"] = 1.73, ["armor"] = 64, ["evasion"] = 121,
            ["resist.ember"] = 0.31, ["resist.ember.max"] = 0.75, ["resist.tide"] = 0.12, ["resist.tide.max"] = 0.75,
            ["resist.storm"] = -0.05, ["resist.storm.max"] = 0.75, ["move.speed"] = 5.4, ["cast.speed"] = 1.12,
        },
        new Dictionary<string, double> { ["life"] = 286, ["mana"] = 214, ["stamina"] = 72, ["ward"] = 55, ["shield"] = 64, ["poise"] = 18, ["insight"] = 41 },
        new Dictionary<string, double> { ["life"] = 412, ["mana"] = 318, ["stamina"] = 100, ["ward"] = 80, ["shield"] = 120, ["poise"] = 60, ["insight"] = 100 },
        new List<WeaveSlotInfo>(),
        new List<ItemInfo>(),
        40,
        new Dictionary<string, ItemInfo>(),
        new Dictionary<string, double> { ["shards"] = 1284, ["floss"] = 37 },
        new List<QuestInfo> { new("q_hedgerow", "Clear the Hedgerow", "active", new List<string> { "Hobs routed" }, new List<int> { 3 }, new List<int> { 5 }, null, 12) },
        new Dictionary<string, double>(),
        new List<string>())
    {
        Reserved = new Dictionary<string, double> { ["mana"] = 48 },
        Pools = new List<PoolInfo> { new("shield", "recharging"), new("ward", "ready"), new("poise", "broken") { Since = 998, Until = 1006 } },
        Statuses = new List<StatusInfo> { new("burning", 3, 2.4, 4), new("hasted", 1, 8.5, 10), new("regrowth", 2, 5, 12) },
        Cast = new CastInfo(1, "charge", 0.62, 2, 3) { Name = "Ember Bolt", Brim = 0.85 },
    };

    private static WeaveSlotsMsg Slots()
    {
        PreviewLineInfo P(string l, string t) => new(l, t);
        var seats = new List<WeaveSeatInfo>
        {
            new(0, "strike", "strike", "Strike", 3, 2, new List<SeatGlyphInfo> { new(0, "keen", "Keen", 2) }, 0, 0, new List<PreviewLineInfo> { P("Damage", "18 to 26 Stone"), P("Cast time", "0.8 s") })
                { Icon = "strike", Threads = new List<string> { "stone" }, CastMode = "instant", Costs = new Dictionary<string, double> { ["stamina"] = 6 } },
            new(1, "ember_bolt", "bolt", "Ember Bolt", 5, 3, new List<SeatGlyphInfo> { new(0, "fork", "Fork", 3), new(1, "volley", "Volley", 1) { Dormant = "needs a projectile that travels" } }, 0, 0, new List<PreviewLineInfo> { P("Damage", "31 to 47 Ember"), P("Cast time", "1.6 s"), P("Cost", "12 Mana") })
                { Icon = "bolt", Threads = new List<string> { "ember" }, CastMode = "charge", Costs = new Dictionary<string, double> { ["mana"] = 12 } },
            new(2, "tide_nova", "nova", "Tide Nova", 4, 2, new List<SeatGlyphInfo>(), 3.2, 6, new List<PreviewLineInfo> { P("Damage", "22 to 30 Tide"), P("Cooldown", "6 s") })
                { Icon = "nova", Threads = new List<string> { "tide" }, CastMode = "cast", Costs = new Dictionary<string, double> { ["mana"] = 20 } },
            new(3, "storm_beam", "beam", "Storm Beam", 2, 1, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo> { P("Damage", "9 per beat Storm") })
                { Icon = "beam", Threads = new List<string> { "storm" }, CastMode = "channel", Costs = new Dictionary<string, double> { ["mana"] = 4 }, Uses = 2, UsesMax = 3 },
            new(4, "bloom_mend", "mend", "Bloom Mend", 3, 1, new List<SeatGlyphInfo>(), 9.5, 14, new List<PreviewLineInfo> { P("Heal", "64 Bloom") })
                { Icon = "mend", Threads = new List<string> { "bloom" }, CastMode = "cast", Costs = new Dictionary<string, double> { ["mana"] = 26 } },
            new(5, "umbra_dash", "dash", "Umbra Step", 1, 0, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo>())
                { Icon = "dash", Threads = new List<string> { "umbra" }, Dormant = "Needs a dagger in the main hand" },
            new(6, null, null, "", 0, 0, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo>()),
            new(7, null, null, "", 0, 0, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo>()),
        };
        return new WeaveSlotsMsg(seats);
    }

    private static InventoryStateMsg Inventory()
    {
        ItemInfo I(long uid, string name, string rarity, int x, int y, int w, int h, string? slot, string? cls, params string[] lines) =>
            new(uid, name.ToLowerInvariant().Replace(' ', '_'), name, rarity, 1, 12, slot, lines.ToList(), new Dictionary<string, double>()) { X = x, Y = y, W = w, H = h, Class = cls };
        var bag = new List<ItemInfo>
        {
            I(11, "Hedge Sword", "magic", 0, 0, 1, 3, "main_hand", "weapon.sword", "+14% increased Damage", "+6 Might"),
            I(12, "Wardstone Buckler", "rare", 1, 0, 2, 2, "off_hand", "shield", "+42 Armor", "+18 Maximum Ward", "+9% Tide Resistance"),
            I(13, "Kindled Circlet", "unique", 3, 0, 2, 2, "head", "armor.head", "+1 to Ember weave levels", "Burning lasts 30% longer") with { Flavor = "It remembers every fire." },
            I(14, "Fork", "magic", 5, 0, 1, 1, null, "glyph", "Projectiles fork once") with { Glyph = new ItemGlyphInfo("fork", "Fork", 2), Fits = new List<StoneFitInfo> { new(1, "fits"), new(0, "misfit") { Reason = "needs a projectile" } } },
            I(15, "Linen Wraps", "normal", 6, 0, 2, 2, "hands", "armor.hands", "+8 Evasion"),
            I(16, "Shard Pouch", "normal", 8, 0, 1, 1, null, "currency") with { Count = 24 },
            I(17, "Tide Nova Primer", "rare", 3, 2, 1, 2, null, "form") with { Form = new ItemFormInfo("nova", "Nova", 4, 3) },
            I(18, "Copper Ring", "magic", 5, 2, 1, 1, "ring", "jewelry.ring", "+12 Maximum Mana"),
            I(19, "Rusted Greaves", "normal", 6, 2, 2, 2, "feet", "armor.feet", "+11 Armor") with { Requirements = new List<RequirementInfo> { new("Level 16", false) } },
        };
        var worn = new Dictionary<string, ItemInfo>
        {
            ["main_hand"] = I(21, "Ashwood Staff", "rare", 0, 0, 1, 3, "main_hand", "weapon.staff", "+31% increased Weave Power", "+2 to Ember weave levels"),
            ["chest"] = I(22, "Loomwright Robe", "magic", 0, 0, 2, 3, "chest", "armor.chest", "+40 Maximum Life"),
            ["amulet"] = I(23, "Ember Charm", "unique", 0, 0, 1, 1, "amulet", "jewelry.amulet", "Ember hits ignite"),
            ["feet"] = I(24, "Road Boots", "normal", 0, 0, 2, 2, "feet", "armor.feet", "+4% Movement Speed"),
        };
        var slots = new[] { ("main_hand", "Main Hand"), ("off_hand", "Off Hand"), ("head", "Head"), ("chest", "Chest"), ("hands", "Hands"), ("feet", "Feet"), ("belt", "Belt"), ("ring", "Ring"), ("amulet", "Amulet") }
            .Select((s, i) => new EquipSlotInfo(s.Item1, s.Item2, i)).ToList();
        var wallet = new List<CurrencyInfo> { new("shards", "Shards", 1284), new("floss", "Floss", 37), new("residue", "Knot Residue", 4) };
        return new InventoryStateMsg(new BoardInfo(10, 4, bag), slots, worn, wallet, new List<ContainerInfo>());
    }

    private static VendorStateMsg Vendor()
    {
        ItemInfo I(long uid, string name, string rarity, string cls, params string[] lines) =>
            new(uid, name.ToLowerInvariant().Replace(' ', '_'), name, rarity, 1, 10, null, lines.ToList(), new Dictionary<string, double>()) { Class = cls, W = 1, H = 2 };
        return new VendorStateMsg(77, "smith", "Hettie the Smith", new List<VendorStockInfo>
        {
            new("s1", I(101, "Iron Shortsword", "normal", "weapon.sword", "4 to 9 Stone damage"), new Dictionary<string, double> { ["shards"] = 40 }) { Count = 2 },
            new("s2", I(102, "Riveted Cap", "magic", "armor.head", "+22 Armor"), new Dictionary<string, double> { ["shards"] = 95 }),
            new("s3", I(103, "Tidebound Greaves", "rare", "armor.feet", "+18% Tide Resistance"), new Dictionary<string, double> { ["shards"] = 310, ["floss"] = 4 }) { Enabled = false, Reason = "Requires level 16" },
        }, new List<VendorBuyBackInfo> { new(I(104, "Cracked Buckler", "normal", "shield"), new Dictionary<string, double> { ["shards"] = 6 }) },
        new List<VendorQuoteInfo> { new(15, new Dictionary<string, double> { ["shards"] = 9 }), new(18, new Dictionary<string, double> { ["shards"] = 21 }) }, false) { Reach = 6 };
    }

    private async Task Shots(string theme)
    {
        var hud = _main.Hud;
        hud.UseSeat(null);
        Directory.CreateDirectory(_dir);
        await Wait(0.8);
        var cluster = hud.ClusterRect;
        await Save($"{theme}-cluster.png", new Rect2I((int)cluster.X - 20, (int)cluster.Y - 40, (int)cluster.W + 40, (int)cluster.H + 50));

        // The default seat: the vendor and the weaves page share the side with room, bound into one book.
        hud.Workspace.RequestPage("weaves", Via.Key);
        hud.ShowVendor(Vendor());
        await Wait(0.6);
        if (hud.Rects is { } rects)
        {
            var box = rects.Core.Union(rects.PageBook).Union(rects.WorldBook);
            await Save($"{theme}-core-pane.png", new Rect2I((int)box.X - 16, (int)box.Y - 16, (int)box.W + 32, (int)box.H + 32), 0.85f);
        }
        await Save($"{theme}-overview.png", null, 0.5f);

        // The core pane seated in the middle: the page on one side, the world's pane on the other.
        hud.UseSeat(new Seat(0.36f, 0.08f));
        await Wait(0.5);
        if (hud.Rects is { } split)
        {
            var box = split.Core.Union(split.PageBook).Union(split.WorldBook);
            await Save($"{theme}-beside.png", new Rect2I((int)box.X - 16, (int)box.Y - 16, (int)box.W + 32, (int)box.H + 32), 0.6f);
        }
        hud.UseSeat(null);

        hud.Workspace.CloseLeaf("vendor:77");
        hud.Workspace.RequestPage("passives", Via.Key);
        await Wait(0.5);
        if (hud.Rects is { } r2)
        {
            var box = r2.Core.Union(r2.PageBook);
            await Save($"{theme}-passives.png", new Rect2I((int)box.X - 16, (int)box.Y - 16, (int)box.W + 32, (int)box.H + 32), 0.75f);
        }
        hud.Workspace.RequestPage("character", Via.Key);
        await Wait(0.5);
        if (hud.Rects is { } r3)
        {
            var box = r3.Core.Union(r3.PageBook);
            await Save($"{theme}-character.png", new Rect2I((int)box.X - 16, (int)box.Y - 16, (int)box.W + 32, (int)box.H + 32), 0.75f);
        }
        hud.Workspace.CloseCore();
        await Frame();
    }

    private async Task Save(string name, Rect2I? region, float scale = 1f)
    {
        await Frame();
        await Frame();
        var image = GetViewport().GetTexture().GetImage();
        if (region is { } r)
        {
            var clipped = r.Intersection(new Rect2I(0, 0, image.GetWidth(), image.GetHeight()));
            if (clipped.Size.X > 0 && clipped.Size.Y > 0)
            {
                image = image.GetRegion(clipped);
            }
        }
        if (scale < 1f)
        {
            image.Resize((int)(image.GetWidth() * scale), (int)(image.GetHeight() * scale), Image.Interpolation.Lanczos);
        }
        var path = Path.Combine(_dir, name);
        var err = image.SavePng(path);
        GD.Print($"MOCKUP shot {path}: {err}");
    }
}
