using HollowWake.Client.World;
using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

public class CastCueTimelineTests
{
    private static readonly CastVisualsDef Config = ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _).Each<CastVisualsDef>().Single();
    private static CastCueMsg Cue(string phase, long sequence) => new(sequence, 7, phase, 1, 2, "modded", "Custom", "cast",
        new() { "spell", "ember" }, 10, 9, 2, 1, 2, 3, 4, 5, 3, false, "");

    [Fact]
    public void Body_lifecycle_is_validated_once_even_without_cosmetic_profiles()
    {
        var t = new CastCueTimeline(Config with { Profiles = new() });
        var accepted = new List<CastCueMsg>();
        t.Lifecycle += accepted.Add;
        var release = Cue("release", 1) with { Fired = true };
        Assert.False(t.Receive(release, 10)); // No art selected, but emission remains a fact.
        Assert.Equal(release, Assert.Single(accepted));
        t.Receive(release, 10);
        t.Receive(Cue("release", 2) with { Fired = true }, 20); // Stale.
        t.Receive(Cue("release", 3) with { Fired = true, CastId = 0 }, 10);
        t.Receive(Cue("release", 4) with { Fired = true, Outcome = true }, 10);
        t.Receive(Cue("unknown", 5), 10);
        Assert.Single(accepted);
        foreach (var phase in new[] { "start", "sync", "end", "interrupt", "impact", "release" })
            t.Receive(Cue(phase, 6 + accepted.Count), 10);
        Assert.Equal(new[] { "release", "start", "sync", "end", "interrupt", "impact", "release" }, accepted.Select(e => e.Phase));
        Assert.Single(accepted, e => e.Phase == "release" && e.Fired);
        t.Clear();
        t.Receive(release, 10);
        Assert.Equal(2, accepted.Count(e => e.Phase == "release" && e.Fired));
    }

    [Fact]
    public void Every_current_preset_has_shared_cast_presentation_and_new_effect_families_need_a_coverage_decision()
    {
        var core = ThemeContentTests.FindContentDir();
        var content = ContentRegistry.Load(new[] { core }, out var report);
        Assert.Empty(report.Errors);
        var config = CastVisualThemes.Resolve(Config, content.Each<ThreadDef>());
        var compiler = ZoneBuilder.CreateArena(content, 1).Service<WeaveCompiler>();
        var presets = content.Each<WeaveDef>().ToArray();
        Assert.NotEmpty(presets);
        foreach (var preset in presets)
        {
            Assert.True(compiler.TryCompile(new WeaveSpec { Preset = preset.Id }, out var weave, out var error, GlyphFit.Dormant), error);
            foreach (var phase in new[] { "start", "release", "impact", "end", "interrupt" })
            {
                var t = new CastCueTimeline(config);
                Assert.True(t.Receive(Cue(phase, 1) with { Weave = weave!.Key, Name = weave.Name,
                    Tags = weave.Tags.Items.ToList(), Mechanisms = weave.Mechanisms.Items.ToList(), Fired = true }, 10), preset.Id + ": " + phase);
                Assert.All(t.Active, cue => Assert.NotEmpty(CastVisualPrimitives.Build(cue.Style)));
            }
        }
        // Baseline coverage is explicit, including missing families. Adding a new handler cannot silently skip that review.
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(core, "..", "..", "tests", "fixtures", "cast-visuals", "coverage.json")));
        var classified = manifest.RootElement.GetProperty("families").EnumerateObject()
            .Where(f => f.Value.TryGetProperty("current_effects", out _))
            .SelectMany(f => f.Value.GetProperty("current_effects").Deserialize<string[]>()!).ToArray();
        Assert.Equal(classified.Length, classified.Distinct().Count());
        Assert.Equal(EffectRegistry.Shared.Types, classified.Order(StringComparer.Ordinal));
        Assert.Equal(manifest.RootElement.GetProperty("primitives").Deserialize<string[]>(), CastVisualPrimitives.Kinds);
    }

    [Fact]
    public void A_declared_outcome_with_no_matching_specialization_keeps_its_neutral_fallback()
    {
        var config = Config with { Profiles = Config.Profiles.Select(p => p.Id == "restoration" ? p with { Match = new() { "unmatched" } } : p).ToList() };
        var t = new CastCueTimeline(config);
        Assert.True(t.Receive(Cue("heal", 1) with { Outcome = true, CastId = 0, Tags = new() { "life" } }, 10));
        Assert.Equal("neutral", Assert.Single(t.Active).Profile);
    }

    [Fact]
    public void Shared_content_only_cases_select_identical_recipes_without_skill_dispatch()
    {
        var core = ThemeContentTests.FindContentDir();
        var fixtures = Path.GetFullPath(Path.Combine(core, "..", "..", "tests", "fixtures", "cast-visuals"));
        var content = ContentRegistry.Load(new[] { core, Path.Combine(fixtures, "content") }, out var report);
        Assert.Empty(report.Errors);
        var config = content.Each<CastVisualsDef>().Single();
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "cases.json")));
        foreach (var example in cases.RootElement.EnumerateArray())
        {
            var t = new CastCueTimeline(config);
            var e = Cue(example.GetProperty("phase").GetString()!, 1) with
            {
                Weave = example.GetProperty("weave").GetString()!,
                Name = example.GetProperty("name").GetString()!,
                Tags = example.GetProperty("tags").EnumerateArray().Select(x => x.GetString()!).ToList(),
                Fired = example.TryGetProperty("fired", out var fired) && fired.GetBoolean()
            };
            Assert.True(t.Receive(e, 10), example.GetProperty("id").GetString());
            var actual = Assert.Single(t.Active);
            var expectedPhase = example.GetProperty("expected_phase").GetString()!;
            var expected = config.Profiles.Single(p => p.Id == example.GetProperty("expected_profile").GetString());
            Assert.Equal(expectedPhase, actual.Phase);
            Assert.Equal(expected.Color, actual.Color);
            Assert.Same(expected.Phases[expectedPhase], actual.Style);
        }
    }

    [Fact]
    public void Content_selects_a_Thread_profile_and_an_unknown_thread_falls_back()
    {
        var t = new CastCueTimeline(Config);
        Assert.True(t.Receive(Cue("start", 1), 10));
        Assert.Equal("#ffb45c", Assert.Single(t.Active).Color);
        t.Receive(Cue("impact", 2) with { Tags = new() { "unknown" } }, 10);
        Assert.Equal("#e8dcc0", t.Active[1].Color);
    }

    [Fact]
    public void Shared_composition_and_primitive_fixtures_cover_new_Threads_supports_and_outcomes()
    {
        var core = ThemeContentTests.FindContentDir();
        var fixture = Path.GetFullPath(Path.Combine(core, "..", "..", "tests", "fixtures", "cast-visuals", "composition.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(fixture));
        var content = ContentRegistry.Load(new[] { core }, out _);
        var extra = doc.RootElement.GetProperty("extra_threads").Deserialize<List<ThreadDef>>(ContentJson.Options)!;
        var config = CastVisualThemes.Resolve(Config, content.Each<ThreadDef>().Concat(extra));
        foreach (var example in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var outcome = example.TryGetProperty("outcome", out var value) && value.GetBoolean();
            var e = Cue(example.GetProperty("phase").GetString()!, 1) with
            {
                CastId = outcome ? 0 : 7, Outcome = outcome, Weave = "a_new_composed_skill", Name = "Arbitrary name",
                Tags = example.GetProperty("tags").Deserialize<List<string>>()!,
                Mechanisms = example.GetProperty("mechanisms").Deserialize<List<string>>()!,
            };
            var t = new CastCueTimeline(config);
            Assert.True(t.Receive(e, 10), example.GetProperty("name").GetString());
            Assert.Equal(example.GetProperty("profiles").Deserialize<string[]>(), t.Active.Select(c => c.Profile).Order(StringComparer.Ordinal));
        }
        foreach (var primitive in doc.RootElement.GetProperty("primitives").EnumerateArray())
        {
            var p = primitive.Deserialize<CastVisualPhase>(ContentJson.Options)!;
            var actual = CastVisualPrimitives.Build(p).SelectMany(v => v).ToArray();
            var expected = primitive.GetProperty("expected").Deserialize<double[][]>()!.SelectMany(v => v).ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++) Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, 0.000001);
        }
        var limited = new CastCueTimeline(config with { MaxLayersPerEvent = 2 });
        limited.Receive(Cue("release", 1) with { Tags = new() { "ember", "tide" }, Mechanisms = new() { "area", "projectile" } }, 10);
        Assert.Equal(2, limited.Active.Count);
    }

    [Fact]
    public void Interrupted_emission_keeps_later_contact_and_each_world_reset_restarts_the_watermark()
    {
        var t = new CastCueTimeline(Config);
        for (var world = 0; world < 3; world++)
        {
            t.Clear(); // the same entry point called on disconnect and WorldEnter by the client
            Assert.True(t.Receive(Cue("start", 1), 10));
            t.Receive(Cue("release", 2), 10);
            t.Receive(Cue("interrupt", 3) with { Fired = true }, 10);
            Assert.Contains(t.Active, c => c.Phase == "release");
            t.Receive(Cue("impact", 4) with { ServerTime = 11 }, 11);
            t.Advance(11);
            Assert.Equal("impact", Assert.Single(t.Active).Phase);
            Assert.False(t.Receive(Cue("release", 2), 11));
        }
    }

    [Fact]
    public void Events_are_deduplicated_bounded_and_expired()
    {
        var t = new CastCueTimeline(Config with { MaxEffects = 2 });
        Assert.True(t.Receive(Cue("release", 1), 10));
        Assert.False(t.Receive(Cue("release", 1), 10));
        Assert.False(t.Receive(Cue("impact", 2), 20));
        for (var i = 3; i < 10; i++) t.Receive(Cue("release", i), 10);
        Assert.Equal(2, t.Active.Count);
        t.Advance(11);
        Assert.Empty(t.Active);
    }

    [Theory]
    [InlineData("end")]
    [InlineData("interrupt")]
    public void Unfired_endings_never_imply_a_successful_release(string phase)
    {
        var t = new CastCueTimeline(Config);
        t.Receive(Cue("start", 1), 10);
        t.Receive(Cue(phase, 2), 10);
        var cancel = Assert.Single(t.Active);
        Assert.Equal("cancel", cancel.Phase);
        Assert.Equal("#e8dcc0", cancel.Color);
    }

    [Fact]
    public void Late_join_only_reconstructs_preparation_and_new_zone_resets_sequence()
    {
        var t = new CastCueTimeline(Config);
        t.Receive(Cue("sync", 99), 10);
        Assert.Equal("start", Assert.Single(t.Active).Phase);
        t.Advance(13);
        Assert.Empty(t.Active);
        t.Clear();
        Assert.True(t.Receive(Cue("start", 1), 10));
    }

    [Fact]
    public void Delayed_contact_survives_cast_end_but_not_target_despawn()
    {
        var t = new CastCueTimeline(Config);
        t.Receive(Cue("start", 1), 10);
        t.Receive(Cue("end", 2) with { Fired = true }, 10);
        Assert.Equal("recovery", Assert.Single(t.Active).Phase);
        t.Receive(Cue("impact", 3) with { ServerTime = 11 }, 11);
        t.Advance(11);
        Assert.Equal("impact", Assert.Single(t.Active).Phase);
        t.RemoveEntity(2);
        Assert.Empty(t.Active);
    }
}
