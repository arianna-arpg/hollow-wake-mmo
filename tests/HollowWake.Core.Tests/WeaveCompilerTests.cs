using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The weave compiler: glyph levels, level bonuses, rolled rows, patches, hooks, probes, dormant glyphs and cast scopes.</summary>
public class WeaveCompilerTests
{
    /// <summary>The core content plus test glyphs and forms, loaded once for this class.</summary>
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        registry
            .With(new GlyphDef
            {
                Id = "t_levels",
                PerLevel = { new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 0.1 } },
                LevelBonus = 1,
                LevelBonusPerLevel = 0.5,
            })
            .With(new GlyphDef { Id = "t_speed_a", Patches = { Patch("projectile", "{\"speed\": 30}", PatchMode.First) } })
            .With(new GlyphDef { Id = "t_speed_b", Patches = { Patch("projectile", "{\"speed\": 40}", PatchMode.First) } })
            .With(new GlyphDef { Id = "t_default", Patches = { Patch("field", "{\"interval\": 0.1, \"look\": \"t\"}", PatchMode.Default) } })
            .With(new GlyphDef { Id = "t_append", Patches = { Patch("projectile", "{\"on_hit\": [{\"type\": \"log\", \"text\": \"appended\"}]}", PatchMode.Append) } })
            .With(new GlyphDef { Id = "t_wrap", Patches = { Wrap("area", PatchWhere.Root, "{\"type\": \"repeat\", \"times\": 2, \"interval\": 0.1}", null) } })
            .With(new GlyphDef { Id = "t_wrap_once_a", Patches = { Wrap("area", PatchWhere.Root, "{\"type\": \"repeat\", \"times\": 2}", PatchMode.First) } })
            .With(new GlyphDef { Id = "t_wrap_once_b", Patches = { Wrap("area", PatchWhere.Root, "{\"type\": \"repeat\", \"times\": 5}", PatchMode.First) } })
            .With(new GlyphDef
            {
                Id = "t_hook",
                Hooks =
                {
                    ["on_hit"] = new List<TypedSpec> { Spec("{\"type\": \"log\", \"text\": \"hooked\"}") },
                    ["on_cast"] = new List<TypedSpec> { Spec("{\"type\": \"log\", \"text\": \"cast hook\"}") },
                },
            })
            .With(new GlyphDef { Id = "t_needs_cooldown", RequiresMechanisms = TagSet.Of("cooldown") })
            .With(new GlyphDef { Id = "t_levy", Modifiers = { new Modifier { Attribute = "cooldown.added", Form = ModifierForm.Flat, Value = 2 } } })
            .With(new GlyphDef { Id = "t_needs_burning", RequiresMechanisms = TagSet.Of("affliction:burning") })
            .With(new GlyphDef { Id = "t_excl_a", AddsTags = TagSet.Of("t_a"), Excludes = TagSet.Of("t_b") })
            .With(new GlyphDef { Id = "t_excl_b", AddsTags = TagSet.Of("t_b"), Excludes = TagSet.Of("t_a") })
            .With(new GlyphDef
            {
                Id = "t_rolled",
                Axes =
                {
                    new GlyphAxis
                    {
                        Id = "edge",
                        Rows =
                        {
                            new GlyphRow { Id = "keen", Modifiers = { new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 0.05 } } },
                            new GlyphRow { Id = "heavy", Modifiers = { new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 0.2 } } },
                        },
                    },
                },
            })
            .With(new FormDef { Id = "t_channel", CastMode = CastMode.Channel, Effects = { Spec("{\"type\": \"log\", \"text\": \"pulse\"}") }, MinThreads = 0 })
            .With(new FormDef { Id = "t_charge", CastMode = CastMode.Charge, Effects = { Spec("{\"type\": \"log\", \"text\": \"release\"}") }, MinThreads = 0 })
            // A preset with tags, a declared mechanism and casting behaviors of its own, between its form and its glyphs.
            .With(new WeaveDef
            {
                Id = "t_preset",
                Form = "bolt",
                Threads = { "ember" },
                Tags = TagSet.Of("curse", "javelin"),
                Mechanisms = TagSet.Of("t_declared"),
                Uses = new UsesSpec { Max = 2 },
                Gates = { HollowWake.Core.Expressions.Expr.Parse("life_pct > 0.1") },
                MetaAction = new MetaSpec { Weave = "ember_blast", Label = "Burst" },
                EquipModifiers = { new Modifier { Attribute = "armor", Form = ModifierForm.Flat, Value = 3 } },
            })
            .With(new GlyphDef { Id = "t_bank", Uses = new UsesSpec { Max = 5 } })
            .With(new GlyphDef { Id = "t_needs_meta", RequiresMechanisms = TagSet.Of("meta") })
            .With(new GlyphDef { Id = "t_needs_curse", Requires = TagSet.Of("curse") })
            .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
            .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    });

    private static ContentRegistry Registry => Loaded.Value;

    private static WeaveCompiler Compiler() => new(Registry, EffectRegistry.CreateDefault());

    private static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    private static EffectPatch Patch(string type, string set, PatchMode mode) =>
        new() { Type = type, Set = (JsonObject)JsonNode.Parse(set)!, Mode = mode };

    private static EffectPatch Wrap(string type, PatchWhere where, string wrap, PatchMode? mode) =>
        new() { Type = type, Where = where, Wrap = Spec(wrap), Mode = mode };

    private static WeaveSpec Bolt(params GlyphRef[] glyphs) => new() { Form = "bolt", Threads = { "ember" }, Glyphs = glyphs.ToList() };

    private static JsonObject Root(CompiledWeave weave, int index = 0) => WeaveGraph.ToNode(weave.Specs[index]);

    [Fact]
    public void GlyphReferencesParseLevelsAndRows()
    {
        var plain = GlyphRef.Parse("fork");
        Assert.Equal("fork", plain.Id);
        Assert.Null(plain.Level);
        Assert.Equal("fork", plain.ToString());
        var leveled = GlyphRef.Parse("fork@3");
        Assert.Equal(3, leveled.Level);
        Assert.Equal("fork@3", leveled.ToString());
        var spec = JsonSerializer.Deserialize<WeaveSpec>("{\"form\": \"bolt\", \"threads\": [\"ember\"], \"glyphs\": [\"fork\", \"needle@2\", {\"id\": \"t_rolled\", \"level\": 4, \"rows\": {\"edge\": \"heavy\"}}]}", ContentJson.Options)!;
        Assert.Equal(new[] { "fork", "needle@2", "t_rolled@4{edge=heavy}" }, spec.Glyphs.Select(g => g.ToString()));
        var roundTrip = JsonSerializer.Deserialize<WeaveSpec>(JsonSerializer.Serialize(spec, ContentJson.Options), ContentJson.Options)!;
        Assert.Equal(spec.ToString(), roundTrip.ToString());
    }

    [Fact]
    public void PerLevelScalesByGlyphLevelAndLevelBonusRaisesTheWeave()
    {
        var compiler = Compiler();
        var one = compiler.Compile(Bolt("t_levels"));
        Assert.DoesNotContain(one.Modifiers, m => m.Source == "glyph:t_levels");
        Assert.Equal(2, one.Level);
        var five = compiler.Compile(Bolt("t_levels@5"));
        var perLevel = Assert.Single(five.Modifiers, m => m.Source == "glyph:t_levels");
        Assert.Equal(0.4, perLevel.Value, 9);
        // 1 (spec) + 1 (level_bonus) + floor(0.5 * 4) (per level above the first).
        Assert.Equal(4, five.Level);
        Assert.Equal(3, five.LevelBonus);
        Assert.Equal(1, compiler.Compile(Bolt("fork@5")).Level);
    }

    [Fact]
    public void RolledRowsFoldTheirModifiersAndDefaultToTheFirstRow()
    {
        var compiler = Compiler();
        var canonical = compiler.Compile(Bolt("t_rolled"));
        Assert.Contains(canonical.Modifiers, m => m.Attribute == "crit.chance" && m.Source == "glyph:t_rolled");
        var heavy = compiler.Compile(Bolt(new GlyphRef { Id = "t_rolled", Rows = new Dictionary<string, string> { ["edge"] = "heavy" } }));
        Assert.Contains(heavy.Modifiers, m => m.Attribute == "damage" && m.Value == 0.2);
        Assert.DoesNotContain(heavy.Modifiers, m => m.Attribute == "crit.chance");
        Assert.False(compiler.TryCompile(Bolt(new GlyphRef { Id = "t_rolled", Rows = new Dictionary<string, string> { ["edge"] = "none" } }), out _, out var error));
        Assert.Contains("no row 'none'", error);
    }

    [Fact]
    public void FirstModePatchesLetTheFirstGlyphInSocketOrderWin()
    {
        var compiler = Compiler();
        Assert.Equal(30, JsonNum.Of(Root(compiler.Compile(Bolt("t_speed_a")))["speed"], 0));
        Assert.Equal(30, JsonNum.Of(Root(compiler.Compile(Bolt("t_speed_a", "t_speed_b")))["speed"], 0));
        Assert.Equal(40, JsonNum.Of(Root(compiler.Compile(Bolt("t_speed_b", "t_speed_a")))["speed"], 0));
    }

    [Fact]
    public void DefaultModePatchesOnlyFillWhatTheFormLacks()
    {
        var field = Compiler().Compile(new WeaveSpec { Form = "field", Threads = { "umbra" }, Glyphs = { "t_default" } });
        var node = Root(field);
        Assert.Equal(0.5, JsonNum.Of(node["interval"], 0));
        Assert.Equal("t", JsonNum.StringOf(node["look"]));
    }

    [Fact]
    public void AppendModePatchesConcatenateLists()
    {
        var compiler = Compiler();
        var plain = ((JsonArray)Root(compiler.Compile(Bolt()))["on_hit"]!).Count;
        var appended = (JsonArray)Root(compiler.Compile(Bolt("t_append")))["on_hit"]!;
        Assert.Equal(plain + 1, appended.Count);
        Assert.Equal("appended", JsonNum.StringOf(appended[^1]!["text"]));
    }

    [Fact]
    public void WrapPatchesWrapRootNodesAndFirstModeWrapsOnce()
    {
        var compiler = Compiler();
        var nova = new WeaveSpec { Form = "nova", Threads = { "storm" }, Glyphs = { "t_wrap" } };
        var wrapped = compiler.Compile(nova);
        var root = Root(wrapped);
        Assert.Equal("repeat", WeaveGraph.TypeOf(root));
        Assert.Equal("area", WeaveGraph.TypeOf((JsonObject)((JsonArray)root["effects"]!)[0]!));
        Assert.Equal("repeat", wrapped.Effects[0].Type);

        var twice = Root(compiler.Compile(new WeaveSpec { Form = "nova", Threads = { "storm" }, Glyphs = { "t_wrap", "t_wrap" } }));
        Assert.Equal("repeat", WeaveGraph.TypeOf((JsonObject)((JsonArray)twice["effects"]!)[0]!));

        var once = Root(compiler.Compile(new WeaveSpec { Form = "nova", Threads = { "storm" }, Glyphs = { "t_wrap_once_a", "t_wrap_once_b" } }));
        Assert.Equal(2, JsonNum.Of(once["times"], 0));
        Assert.Equal("area", WeaveGraph.TypeOf((JsonObject)((JsonArray)once["effects"]!)[0]!));
    }

    [Fact]
    public void HooksReachEveryNodeExposingThemAndOnCastJoinsTheRoot()
    {
        var weave = Compiler().Compile(Bolt("t_hook"));
        var projectile = Root(weave);
        var onHit = (JsonArray)projectile["on_hit"]!;
        Assert.Contains(onHit, n => JsonNum.StringOf(n!["text"]) == "hooked");
        // The chain inside the projectile's on-hit list exposes on_hit too.
        var chain = onHit.OfType<JsonObject>().Single(n => WeaveGraph.TypeOf(n) == "chain");
        Assert.Contains((JsonArray)chain["effects"]!, n => JsonNum.StringOf(n!["text"]) == "hooked");
        Assert.Equal("log", weave.Specs[^1].Type);
        Assert.Equal("cast hook", JsonNum.StringOf(weave.Specs[^1].Params["text"]));

        // The hook runs when the projectile lands.
        var sim = ZoneBuilder.CreateArena(Registry, 3);
        var log = new ListSimLog();
        sim.Log = log;
        var factory = sim.Service<PatternFactory>();
        var caster = factory.Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var target = factory.Spawn(sim, "boar", new Vector3(13, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(caster);
        sim.World.Remove<Behavior>(target);
        var slot = sim.World.Get<WeaveBook>(caster)!.Add(Bolt("t_hook"));
        Assert.True(sim.Service<WeaveRuntime>().TryBegin(sim, caster, slot, target, new Vector3(13, 10, 0), Vector2.Zero, out var reason), reason);
        sim.RunSeconds(3);
        Assert.Contains(log.Lines, l => l.Contains("[effect] hooked"));
        Assert.Contains(log.Lines, l => l.Contains("[effect] cast hook"));
    }

    [Fact]
    public void ProbesSelfLiftGatesThroughCooldownAdded()
    {
        var compiler = Compiler();
        Assert.False(compiler.TryCompile(Bolt("t_needs_cooldown"), out _, out var error));
        Assert.Contains("t_needs_cooldown", error);
        var dormant = compiler.Compile(Bolt("t_needs_cooldown"), GlyphFit.Dormant);
        Assert.Single(dormant.Dormant);
        Assert.False(dormant.Mechanisms.Has("cooldown"));
        // A glyph levying a cooldown lifts the gate, whichever socket it sits in.
        foreach (var spec in new[] { Bolt("t_levy", "t_needs_cooldown"), Bolt("t_needs_cooldown", "t_levy") })
        {
            var lifted = compiler.Compile(spec);
            Assert.True(lifted.Fits);
            Assert.True(lifted.Mechanisms.Has("cooldown"));
            Assert.False(lifted.DeclaredMechanisms.Has("cooldown"));
        }
    }

    [Fact]
    public void ParameterizedProbesReadTheWeavesAilments()
    {
        var compiler = Compiler();
        var ember = compiler.Compile(Bolt("t_needs_burning"));
        Assert.True(ember.Fits);
        Assert.True(ember.Mechanisms.Has("affliction:burning"));
        Assert.True(ember.HasMechanism("affliction:burning"));
        Assert.False(ember.HasMechanism("affliction:chilled"));
        var tide = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "tide" }, Glyphs = { "t_needs_burning" } }, GlyphFit.Dormant);
        var misfit = Assert.Single(tide.Dormant);
        Assert.Equal("t_needs_burning", misfit.Glyph.Id);
        Assert.Contains("affliction:burning", misfit.Reason);
    }

    [Fact]
    public void PresetsCarryTagsMechanismsAndBehaviorsBetweenFormAndGlyphs()
    {
        var compiler = Compiler();
        var weave = compiler.Compile(new WeaveSpec { Preset = "t_preset" });
        Assert.True(weave.Tags.Has("curse"));
        Assert.True(weave.Tags.Has("javelin"));
        Assert.True(weave.Tags.Has("preset.t_preset"));
        Assert.True(weave.Tags.Has("form.bolt"));
        Assert.True(weave.DeclaredMechanisms.Has("t_declared"));
        Assert.True(weave.Mechanisms.Has("meta"));
        Assert.Equal(2, weave.Casting.Uses!.Max);
        Assert.Single(weave.Casting.Gates);
        Assert.Equal("ember_blast", Assert.Single(weave.Casting.Metas).Weave);
        Assert.Equal("armor", Assert.Single(weave.Casting.EquipModifiers).Attribute);

        // A glyph's bank wins over the preset's; the preset's tags and meta lift glyph gates.
        var banked = compiler.Compile(new WeaveSpec { Preset = "t_preset", Glyphs = { "t_bank", "t_needs_meta", "t_needs_curse" } });
        Assert.True(banked.Fits);
        Assert.Equal(5, banked.Casting.Uses!.Max);
        Assert.Equal(3, banked.GlyphRefs.Count);

        // Without the preset the same form carries none of it.
        var bare = compiler.Compile(Bolt());
        Assert.False(bare.Tags.Has("curse"));
        Assert.Null(bare.Casting.Uses);
        Assert.Empty(bare.Casting.Metas);
        var misfit = compiler.Compile(Bolt("t_needs_curse"), GlyphFit.Dormant);
        Assert.Single(misfit.Dormant);
    }

    [Fact]
    public void ValidationChecksAPresetsBehaviorsLikeAGlyphs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-preset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "weaves.yaml"), string.Join("\n",
                "kind: weaves",
                "entries:",
                "  - id: bad_trigger",
                "    form: bolt",
                "    threads: [ember]",
                "    trigger: { on: sneeze }",
                "  - id: bad_meta",
                "    form: bolt",
                "    threads: [ember]",
                "    meta_action: { weave: no_such_weave, label: x }",
                "  - id: good_preset",
                "    form: bolt",
                "    threads: [ember]",
                "    tags: [curse]",
                "    uses: { max: 2, recharge: 3 }",
                "    gates: ['life_pct > 0.1']",
                ""));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Select(e => $"{e.Id}: {e.Message}").ToList();
            Assert.Contains(errors, e => e.StartsWith("bad_trigger") && e.Contains("sneeze"));
            Assert.Contains(errors, e => e.StartsWith("bad_meta") && e.Contains("no_such_weave"));
            Assert.DoesNotContain(errors, e => e.StartsWith("good_preset"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MisfitsStayDormantWithTheirReasonAndContributeNothing()
    {
        var compiler = Compiler();
        var strike = new WeaveSpec { Form = "strike", Glyphs = { "fork", "whetted" } };
        Assert.False(compiler.TryCompile(strike, out _, out var error));
        var weave = compiler.Compile(strike, GlyphFit.Dormant);
        var dormant = Assert.Single(weave.Dormant);
        Assert.Equal("fork", dormant.Ref.Id);
        Assert.Equal(error, dormant.Reason);
        Assert.Equal(new[] { "whetted" }, weave.Glyphs.Select(g => g.Id));
        Assert.DoesNotContain(weave.Modifiers, m => m.Source == "glyph:fork");
        Assert.False(weave.Tags.Has("glyph.fork"));
    }

    [Fact]
    public void WhereGlyphsExcludeEachOtherTheEarlierSocketStaysActive()
    {
        var compiler = Compiler();
        var ab = compiler.Compile(Bolt("t_excl_a", "t_excl_b"), GlyphFit.Dormant);
        Assert.Equal(new[] { "t_excl_a" }, ab.Glyphs.Select(g => g.Id));
        Assert.Equal("t_excl_b", Assert.Single(ab.Dormant).Glyph.Id);
        var ba = compiler.Compile(Bolt("t_excl_b", "t_excl_a"), GlyphFit.Dormant);
        Assert.Equal(new[] { "t_excl_b" }, ba.Glyphs.Select(g => g.Id));
        Assert.Equal("t_excl_a", Assert.Single(ba.Dormant).Glyph.Id);
    }

    [Fact]
    public void CastScopeTagsFollowTheCastModeAndBaseTime()
    {
        var compiler = Compiler();
        Assert.Equal(CastScopes.Timed, compiler.Compile(new WeaveSpec { Preset = "strike" }).CastScope);
        Assert.Equal(CastScopes.Timed, compiler.Compile(new WeaveSpec { Preset = "ember_bolt" }).CastScope);
        Assert.Equal(CastScopes.Instant, compiler.Compile(new WeaveSpec { Preset = "lumen_ward" }).CastScope);
        Assert.Equal(CastScopes.Instant, compiler.Compile(new WeaveSpec { Preset = "quick_dash" }).CastScope);
        var channel = compiler.Compile(new WeaveSpec { Form = "t_channel" });
        Assert.Equal(CastScopes.Channel, channel.CastScope);
        Assert.True(channel.Tags.Has("cast.channel"));
        Assert.True(compiler.Compile(new WeaveSpec { Form = "t_charge" }).Tags.Has("cast.held"));
    }

    [Fact]
    public void ValidationRejectsPatchesHooksAndProbesItCannotCheck()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-wp1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "glyphs.yaml"), string.Join("\n",
                "kind: glyphs",
                "entries:",
                "  - id: bad_param",
                "    patches: [ { type: projectile, set: { wobble: 3 } } ]",
                "  - id: bad_wrapper",
                "    patches: [ { type: area, wrap: { type: damage } } ]",
                "  - id: bad_hook",
                "    hooks: { on_sneeze: [ { type: log, text: x } ] }",
                "  - id: bad_probe",
                "    requires_mechanisms: [\"affliction:no_such_status\", \"cooldown:soon\", \"nothing:here\"]",
                ""));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Select(e => $"{e.Id}: {e.Message}").ToList();
            Assert.Contains(errors, e => e.StartsWith("bad_param") && e.Contains("wobble"));
            Assert.Contains(errors, e => e.StartsWith("bad_wrapper") && e.Contains("not a wrapper"));
            Assert.Contains(errors, e => e.StartsWith("bad_hook") && e.Contains("on_sneeze"));
            Assert.Contains(errors, e => e.StartsWith("bad_probe") && e.Contains("no_such_status"));
            Assert.Contains(errors, e => e.StartsWith("bad_probe") && e.Contains("cooldown:soon"));
            Assert.Contains(errors, e => e.StartsWith("bad_probe") && e.Contains("nothing:here"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EveryPresetFitsWithEveryGlyphActive()
    {
        var compiler = Compiler();
        foreach (var preset in CoreContent.Registry.Each<WeaveDef>())
        {
            var weave = compiler.Compile(new WeaveSpec { Preset = preset.Id }, GlyphFit.Dormant);
            Assert.True(weave.Fits, $"{preset.Id}: {string.Join("; ", weave.Dormant.Select(d => d.Reason))}");
        }
    }
}
