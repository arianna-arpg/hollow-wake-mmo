using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

/// <summary>
/// The appearance content and the shared body logic, read the way both clients read them: every look the
/// world spawns has an entry, every family builds a finite body, gear fits its sockets, the animator never
/// produces a non-number, the surfaces and the day blend as content says.
/// </summary>
public class AppearanceTests
{
    private static readonly Lazy<ContentRegistry> Content = new(() => ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _));

    private static AppearanceCatalog Catalog() => new(Content.Value);

    /// <summary>The patterns the world can spawn: those biomes, points of interest, landmarks and summons name.</summary>
    private static IEnumerable<string> SpawnedPatterns()
    {
        var content = Content.Value;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var biome in content.Each<BiomeDef>())
        {
            foreach (var f in biome.Flora)
            {
                ids.Add(f.Pattern);
            }
            foreach (var s in biome.Spawns)
            {
                ids.Add(s.Pattern);
            }
        }
        foreach (var raw in content.RawOfKind("landmarks"))
        {
            if (raw.Object["legend"] is System.Text.Json.Nodes.JsonObject legend)
            {
                foreach (var (_, cell) in legend)
                {
                    var spawn = Json.StrOrNull(cell?["spawn"]);
                    if (spawn is not null)
                    {
                        ids.Add(spawn);
                    }
                }
            }
        }
        // every concrete pattern by its inherited tags, as the browser's test counts them, so both agree
        var catalog = new AppearanceCatalog(content);
        foreach (var pattern in content.Each<PatternDef>())
        {
            var tags = catalog.PatternTags(pattern.Id);
            var concrete = new[] { "summonable", "device", "npc", "prop", "flora", "monster" }.Any(tags.Has) && pattern.Id is not ("device" or "monster");
            if (concrete)
            {
                ids.Add(pattern.Id);
            }
        }
        return ids.Where(id => content.Has<PatternDef>(id)).OrderBy(id => id, StringComparer.Ordinal);
    }

    [Fact]
    public void TheAppearanceContentValidatesClean()
    {
        ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out var report);
        var ours = report.Issues.Where(i => i.Kind is "ui_appearances" or "ui_surfaces" or "ui_world" or "ui_motion").ToList();
        Assert.True(ours.Count == 0, string.Join("\n", ours));
    }

    [Fact]
    public void EverySpawnedPatternHasItsOwnLook()
    {
        var catalog = Catalog();
        var content = Content.Value;
        var missing = new List<string>();
        foreach (var id in SpawnedPatterns())
        {
            var pattern = content.Get<PatternDef>(id)!;
            var look = catalog.Resolve(pattern.Look ?? id, id);
            if (look is null || look.Id.EndsWith("_fallback", StringComparison.Ordinal))
            {
                missing.Add($"{id} -> {look?.Id ?? "nothing"}");
            }
        }
        Assert.True(missing.Count == 0, "patterns drawn by a fallback: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryCallingAndPlaceHasALook()
    {
        var catalog = Catalog();
        foreach (var calling in Content.Value.Each<CallingDef>())
        {
            var look = catalog.Resolve(calling.Look ?? calling.Id, null);
            Assert.True(look is not null && look.Body == "biped", $"calling {calling.Id} draws as {look?.Id ?? "nothing"}");
        }
        foreach (var poi in Content.Value.Each<PoiDef>())
        {
            var look = catalog.Resolve("poi:" + poi.Id, null);
            Assert.True(look is not null && look.Body == "marker", $"place {poi.Id} draws as {look?.Id ?? "nothing"}");
        }
        Assert.Equal("fount", catalog.Resolve("fount", null)?.Body);
        Assert.Equal("orb", catalog.Resolve("projectile", null)?.Body);
        Assert.Equal("drop", catalog.Resolve("drop:short_sword", null)?.Body);
    }

    [Fact]
    public void EveryItemBaseDrawsAsGearOrAParcel()
    {
        var catalog = Catalog();
        foreach (var item in Content.Value.Each<ItemBaseDef>())
        {
            var gear = catalog.GearForItem(item.Id);
            Assert.True(gear is not null, $"item {item.Id} ({item.Class}) has no gear look");
        }
        Assert.Equal("blade", catalog.GearForItem("short_sword")!.Body);
        Assert.Equal("gear_dagger", catalog.GearForItem("dagger")!.Id);
        Assert.Equal("cuirass", catalog.GearForItem("iron_cuirass")!.Body);
        Assert.Equal("robe", catalog.GearForItem("woven_robe")!.Body);
        Assert.Equal("sack", catalog.GearForItem("iron_ore")!.Body);
        Assert.Equal("gear_greaves", catalog.GearForItem("iron_greaves")!.Id);
    }

    [Fact]
    public void LooksResolveByTagsWhenNoEntryNamesThePattern()
    {
        var catalog = Catalog();
        // The howler extends the hob: its own entry wins over the hob's.
        Assert.Equal("hob_howler", catalog.Resolve("hob_howler", "hob_howler")!.Id);
        // A pattern with no entry of its own falls to the best tag match, highest priority first.
        Assert.Equal("beast_fallback", catalog.Resolve("made_up_beast", "made_up_beast", TagSet.Of("beast", "creature"))!.Id);
        Assert.Equal("marker_knot", catalog.Resolve("poi:knot", null)!.Id);
        Assert.Equal("marker", catalog.Resolve("poi:hamlet", null)!.Id);
        Assert.Null(catalog.Resolve("nothing_at_all", "nothing_at_all"));
    }

    [Fact]
    public void EveryEntryBuildsAFiniteBody()
    {
        var catalog = Catalog();
        foreach (var def in catalog.All.Where(d => d.Slot is null))
        {
            var plan = BodyFamilies.Build(def, 12345, 1.8f, 0.45f);
            Assert.True(plan.Parts.Count > 0, $"{def.Id} has no parts");
            Assert.True(plan.Height > 0 && plan.Width > 0, $"{def.Id} has no size");
            Assert.Contains(plan.Bones, b => b.Name == "root");
            foreach (var part in plan.Parts)
            {
                Assert.Contains(plan.Bones, b => b.Name == part.Bone);
                var (positions, normals) = PartGeometry.Build(part);
                Assert.True(positions.Length > 0 && positions.Length == normals.Length, $"{def.Id} part on {part.Bone} has no triangles");
                Assert.All(positions, v => Assert.True(float.IsFinite(v), $"{def.Id} has a non-finite vertex"));
                Assert.All(normals, v => Assert.True(float.IsFinite(v), $"{def.Id} has a non-finite normal"));
            }
            foreach (var bone in plan.Bones.Where(b => b.Parent is not null))
            {
                Assert.Contains(plan.Bones, b => b.Name == bone.Parent);
            }
        }
    }

    [Fact]
    public void GearWearsOntoABipedAndLaysOnTheGround()
    {
        var catalog = Catalog();
        var hob = catalog.ById("hob")!;
        var plan = BodyFamilies.Build(hob, 7, 1.1f, 0.45f);
        var before = plan.Parts.Count;
        var sword = catalog.GearFor("main_hand", "weapon.sword", new[] { "melee", "blade" })!;
        GearFamilies.Wear(plan, sword, "main_hand", 7);
        Assert.True(plan.Parts.Count > before, "the sword added no parts");
        Assert.True(plan.Swings);
        Assert.All(plan.Parts.Skip(before), p => Assert.Equal("arm_r", p.Bone));
        var wand = catalog.GearFor("main_hand", "weapon.wand", new[] { "caster" })!;
        var kindler = BodyFamilies.Build(catalog.ById("kindler")!, 8, 1.8f, 0.4f);
        GearFamilies.Wear(kindler, wand, "main_hand", 8);
        Assert.False(kindler.Swings);
        foreach (var (slot, item) in new[] { ("head", "iron_helm"), ("chest", "iron_cuirass"), ("hands", "hide_gloves"), ("feet", "walking_boots"), ("belt", "cord_belt"), ("off_hand", "buckler") })
        {
            var count = kindler.Parts.Count;
            var look = catalog.ItemLook(item)!.Value;
            var gear = catalog.GearFor(slot, look.Class, look.Tags);
            Assert.True(gear is not null, $"{item} has no gear look for {slot}");
            GearFamilies.Wear(kindler, gear!, slot, 8);
            Assert.True(kindler.Parts.Count > count, $"{item} added nothing");
        }
        var drop = BodyFamilies.Build(catalog.ById("drop")!, 9, 0.3f, 0.3f);
        GearFamilies.Lay(drop, sword, 9);
        Assert.True(drop.Parts.Count > 1);
        Assert.All(drop.Parts, p => Assert.True(p.At.Y > -0.2f && p.At.Y < 0.6f, "a laid piece should lie near the ground"));
    }

    [Fact]
    public void DefaultGearComesFromTheEntryTheGrantsAndTheCalling()
    {
        var catalog = Catalog();
        var brigand = catalog.DefaultGear(catalog.ById("loosed_brigand")!, "loosed_brigand");
        Assert.Equal("weapon.sword", brigand["main_hand"].Class);
        Assert.Equal("armor.chest", brigand["chest"].Class);
        Assert.Contains("light", brigand["chest"].Tags);
        var holdfast = catalog.DefaultGear(catalog.ById("holdfast")!, null);
        Assert.Equal("weapon.sword", holdfast["main_hand"].Class);
        Assert.Equal("shield.buckler", holdfast["off_hand"].Class);
        Assert.True(holdfast.ContainsKey("chest"));
    }

    [Fact]
    public void VariationIsSeededAndBounded()
    {
        var v = new AppearanceVariation { Hue = 0.05, Value = 0.1, Scale = 0.1 };
        Assert.Equal(Variation.Unit(42, 1), Variation.Unit(42, 1));
        Assert.NotEqual(Variation.Unit(42, 1), Variation.Unit(43, 1));
        for (long key = 1; key < 200; key++)
        {
            var s = Variation.ScaleOf(v, key);
            Assert.InRange(s, 0.9f, 1.1f);
            var c = Variation.Jitter(new Rgba(0.5f, 0.3f, 0.2f), v, key);
            Assert.InRange(c.R, 0, 1);
            Assert.InRange(c.G, 0, 1);
            Assert.InRange(c.B, 0, 1);
        }
        var unjittered = Variation.Jitter(new Rgba(0.5f, 0.3f, 0.2f), new AppearanceVariation { Hue = 0, Value = 0, Scale = 0 }, 5);
        Assert.Equal(0.5f, unjittered.R, 4);
        Assert.Equal(0.3f, unjittered.G, 4);
        Assert.Equal(0.2f, unjittered.B, 4);
    }

    [Fact]
    public void TheAnimatorPosesEveryRigInEveryStateWithNumbers()
    {
        var motion = Catalog().Timings;
        var pose = new Pose();
        foreach (var rig in new[] { "biped", "quadruped", "wisp", "crawler", "tree", "knot", "static" })
        {
            foreach (var swings in new[] { true, false })
            {
                var states = new List<VisualState>
                {
                    new() { Rig = rig, Swings = swings, Time = 12.5 },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Speed = 5, WalkPhase = 1.2f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Casting = true, CastProgress = 0.6f, CastMode = "cast" },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Casting = true, CastMode = "channel" },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Casting = true, CastMode = "guard" },
                    new() { Rig = rig, Swings = swings, Time = 12.5, ReleaseAge = 0.05f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, ReleaseAge = 0.3f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, HitAge = 0.05f, HitStrength = 0.8f, HitFrom = 1.2f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Grounded = false, Leaping = true },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Dead = true, DeadFor = 0.3f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, Dead = true, DeadFor = 30f },
                    new() { Rig = rig, Swings = swings, Time = 12.5, ReducedMotion = true, Speed = 3, HitAge = 0.02f },
                };
                foreach (var s in states)
                {
                    Animator.Animate(s, motion, pose);
                    foreach (var (bone, p) in pose.Bones)
                    {
                        Assert.True(float.IsFinite(p.Turn.X + p.Turn.Y + p.Turn.Z + p.Offset.X + p.Offset.Y + p.Offset.Z + p.Scale), $"{rig} {bone} is not finite");
                        Assert.True(p.Scale > 0, $"{rig} {bone} collapsed");
                    }
                    Assert.InRange(pose.Flash, 0, 1);
                    Assert.InRange(pose.Opacity, 0, 1);
                }
            }
        }
        // a swing draws the arm back with the cast and brings it through on release
        var swing = new VisualState { Rig = "biped", Swings = true, Time = 1, Casting = true, CastProgress = 1, CastMode = "cast" };
        Animator.Animate(swing, motion, pose);
        Assert.True(pose["arm_r"].Turn.Y < -30, "the arm should draw back");
        var release = new VisualState { Rig = "biped", Swings = true, Time = 1, ReleaseAge = 0.15f };
        Animator.Animate(release, motion, pose);
        Assert.True(pose["arm_r"].Turn.Y > 30, "the arm should come through");
        // a hit flashes and a corpse falls
        var hit = new VisualState { Rig = "biped", Time = 1, HitAge = 0.01f, HitStrength = 1 };
        Animator.Animate(hit, motion, pose);
        Assert.True(pose.Flash > 0.5f);
        var dead = new VisualState { Rig = "biped", Time = 1, Dead = true, DeadFor = 5 };
        Animator.Animate(dead, motion, pose);
        Assert.True(MathF.Abs(pose["root"].Turn.Y) > 80, "the body should lie down");
        // reduced motion keeps the functional pose and drops the idle sway
        var still = new VisualState { Rig = "biped", Time = 1, ReducedMotion = true };
        Animator.Animate(still, motion, pose);
        Assert.Equal(0, pose["head"].Turn.Z);
    }

    [Fact]
    public void SurfacesColourCellsByTagsAndFlagsWithStableJitter()
    {
        var look = new SurfaceLook(Catalog().Surfaces);
        var meadow = look.Cell(TagSet.Of("grass", "meadow"), CellFlags.Walkable);
        var grass = look.Cell(TagSet.Of("grass"), CellFlags.Walkable);
        Assert.NotEqual(meadow, grass);
        var road = look.Cell(TagSet.Of("grass"), CellFlags.Walkable | CellFlags.Road);
        Assert.NotEqual(grass, road);
        var cliff = look.Cell(TagSet.Of("grass", "cliff"), CellFlags.Walkable);
        Assert.True(cliff.Luminance < grass.Luminance);
        var wall = look.Structure(TagSet.Of("wall", "structure"), CellFlags.Structure);
        Assert.NotNull(wall);
        Assert.Null(wall!.Value.Roof);
        var house = look.Structure(TagSet.Of("house", "roof"), CellFlags.Structure);
        Assert.NotNull(house!.Value.Roof);
        Assert.Null(look.Structure(TagSet.Of("grass"), CellFlags.Walkable));
        Assert.NotEqual(look.Layer(TagSet.Of("bridge")), look.Layer(TagSet.Of("nothing")));
        for (var x = -20; x < 20; x++)
        {
            for (var y = -20; y < 20; y++)
            {
                var j = look.Jitter(x, y, CellFlags.Walkable);
                Assert.InRange(j, 0.8f, 1.2f);
                Assert.Equal(j, look.Jitter(x, y, CellFlags.Walkable));
                Assert.Equal(1, look.Jitter(x, y, CellFlags.Structure));
            }
        }
        Assert.NotEqual(look.Jitter(3, 4, CellFlags.Walkable), look.Jitter(30, 41, CellFlags.Walkable));
    }

    [Fact]
    public void TheDayWrapsAndBlendsContinuously()
    {
        var day = new DayCycle(Catalog().World);
        Assert.Equal(day.TimeOfDay(0), day.TimeOfDay(day.LengthSeconds), 4);
        Assert.InRange(day.TimeOfDay(day.LengthSeconds * 0.5), 0, 1);
        var noon = day.At(0.5f);
        var midnight = day.At(0f);
        Assert.True(noon.SunEnergy > midnight.SunEnergy);
        Assert.True(noon.SunPitch > 30 && midnight.SunPitch == 0);
        Assert.True(noon.Sky.Luminance > midnight.Sky.Luminance);
        Assert.True(midnight.FogDensity > noon.FogDensity);
        SkyState? last = null;
        for (var f = 0f; f <= 1.0001f; f += 0.01f)
        {
            var s = day.At(f);
            if (last is { } l)
            {
                Assert.True(MathF.Abs(s.Sky.R - l.Sky.R) < 0.1f && MathF.Abs(s.SunEnergy - l.SunEnergy) < 0.1f, $"the sky jumps at {f}");
            }
            last = s;
        }
        Assert.Equal(day.At(0f).Sky, day.At(1f).Sky);
    }

    [Fact]
    public void TheDayFollowsTheServerWhenItSaysAndFoldsTheClockOtherwise()
    {
        var day = new DayCycle(Catalog().World);
        Assert.Null(day.Server);
        var folded = day.TimeOfDay(100);
        Assert.Equal(day.LengthSeconds, day.DayLengthSeconds);

        // enter_world: the length and the fraction as of a server time; the fraction advances with the clock and wraps
        day.ObserveServer(600, 0.25, atServerTime: 100);
        Assert.Equal(0.25f, day.TimeOfDay(100), 4);
        Assert.Equal(0.5f, day.TimeOfDay(250), 4);
        Assert.Equal(0.05f, day.TimeOfDay(580), 4);
        Assert.Equal(600f, day.DayLengthSeconds);

        // the self state: a fraction alone keeps the length; no fraction changes nothing
        day.ObserveServer(null, 0.9, atServerTime: 1000);
        Assert.Equal(0.9f, day.TimeOfDay(1000), 4);
        Assert.Equal(600f, day.DayLengthSeconds);
        day.ObserveServer(null, null, atServerTime: 2000);
        Assert.Equal(0.9f, day.TimeOfDay(1000), 4);

        // a server that never says: the clock folded by the content's day again
        day.ForgetServer();
        Assert.Null(day.Server);
        Assert.Equal(folded, day.TimeOfDay(100), 4);
    }

    [Fact]
    public void AStoppedServerDayStaysStillAndAShortDayKeepsItsRate()
    {
        var day = new DayCycle(Catalog().World);
        foreach (var length in new[] { 0.0, -1.0 })
        {
            day.ObserveServer(length, 0.5, 100);
            Assert.Equal(0.5f, day.TimeOfDay(100.25), 4);
            day.ObserveServer(null, 0.5, 200);
            Assert.Equal(0.5f, day.TimeOfDay(200.25), 4);
        }
        day.ObserveServer(0.5, 0.25, 300);
        Assert.Equal(0.75f, day.TimeOfDay(300.25), 4);
    }

    [Fact]
    public void OtherPlayersWearWhatTheWireSays()
    {
        var catalog = Catalog();
        var holdfast = catalog.Resolve("holdfast", null)!;

        // nothing from the server: the Calling's kit
        Assert.Equal("shield.buckler", catalog.WornBy(holdfast, null, null, null)["off_hand"].Class);
        Assert.Empty(catalog.WornBy(holdfast, null, new List<string>(), null));
        Assert.Empty(catalog.WornBy(holdfast, null, new List<string> { "main_hand=weapon.sword" }, new Dictionary<string, ItemInfo>()));

        // the descriptor's slots, classes and tags read as the item's own would, and the class's trailing word is a tag
        var worn = catalog.WornBy(holdfast, null, new List<string> { "chest=armor.chest cloth caster", "main_hand=weapon.wand caster one_hand focus_weapon", "head=armor.head.heavy" }, null);
        Assert.Equal(new[] { "chest", "head", "main_hand" }, worn.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal("robe", catalog.GearFor("chest", worn["chest"].Class, worn["chest"].Tags)!.Body);
        Assert.Equal("wand", catalog.GearFor("main_hand", worn["main_hand"].Class, worn["main_hand"].Tags)!.Body);
        Assert.Equal("gear_helm", catalog.GearFor("head", worn["head"].Class, worn["head"].Tags)!.Id);
        Assert.DoesNotContain("off_hand", worn.Keys);

        // the owner's own equipment wins over the descriptor
        var own = new Dictionary<string, ItemInfo> { ["main_hand"] = new(1, "dagger", "Dagger", "common", 1, 1, "main_hand", new List<string>(), new Dictionary<string, double>()) };
        var self = catalog.WornBy(holdfast, null, new List<string> { "main_hand=weapon.sword melee blade one_hand" }, own);
        Assert.Equal("weapon.dagger", self["main_hand"].Class);
        Assert.Contains("finesse", self["main_hand"].Tags);

        // entries without a slot or a class say nothing
        Assert.Empty(AppearanceCatalog.WornFromDescriptor(new[] { "nonsense", "=weapon.sword", "belt=", "belt=   " }));
    }
}
