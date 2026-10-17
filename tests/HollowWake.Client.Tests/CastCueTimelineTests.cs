using HollowWake.Client.World;
using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

public class CastCueTimelineTests
{
    private static readonly CastVisualsDef Config = ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _).Each<CastVisualsDef>().Single();
    private static CastCueMsg Cue(string phase, long sequence) => new(sequence, 7, phase, 1, 2, "modded", "Custom", "cast",
        new() { "spell", "ember" }, 10, 9, 2, 1, 2, 3, 4, 5, 3, false, "");

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
    public void Content_selects_Flame_and_an_unknown_thread_falls_back()
    {
        var t = new CastCueTimeline(Config);
        Assert.True(t.Receive(Cue("start", 1), 10));
        Assert.Equal("#ffb45c", Assert.Single(t.Active).Color);
        t.Receive(Cue("impact", 2) with { Tags = new() { "unknown" } }, 10);
        Assert.Equal("#e8dcc0", t.Active[1].Color);
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
