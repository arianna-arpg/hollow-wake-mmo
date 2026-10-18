using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
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

    [Theory]
    [InlineData("biped", true)]
    [InlineData("biped", false)]
    [InlineData("wisp", true)]
    [InlineData("wisp", false)]
    [InlineData("quadruped", true)]
    [InlineData("crawler", true)]
    public void Emission_is_visible_during_a_channel_then_returns_to_preparation(string rig, bool swings)
    {
        var motion = new MotionLook(Catalog().Motion);
        var state = new VisualState { Rig = rig, Swings = swings, CastMode = "channel", CastProgress = 0.6f,
            Time = 12, ReducedMotion = true, ReleaseAge = 0.1f };
        var emitted = new Pose();
        Animator.Animate(state, motion, emitted);
        state.Casting = true;
        var channel = new Pose();
        Animator.Animate(state, motion, channel);
        Assert.Equal(emitted.Bones, channel.Bones);
        state.ReleaseAge = 100;
        Animator.Animate(state, motion, channel);
        Assert.False(emitted.Bones.SequenceEqual(channel.Bones));
        var ready = new Pose();
        state.ReleaseAge = float.PositiveInfinity;
        Animator.Animate(state, motion, ready);
        Assert.Equal(ready.Bones, channel.Bones);
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
}
