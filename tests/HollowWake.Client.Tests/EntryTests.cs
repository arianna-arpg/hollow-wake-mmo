using System.Text.Json.Nodes;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

public class EntryTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HollowWake.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
    private static ContentRegistry Content() => ContentRegistry.Load([Path.Combine(Root(), "content/core")], out _);
    private static EntrySceneSpec Spec() => EntrySceneSpec.Read(Content().Get<UiEntrySceneDef>("mu")!);
    private static VesselChoice Choice(bool available = true) => new("test", "test", "Test", "", "test", null, available, 0);

    [Fact]
    public void LayoutMatchesPrototypeAcrossSmallHandsAndGrowingRings()
    {
        var spec = Spec(); var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests/fixtures/mu-rings.json")))!;
        foreach (var row in fixture["cases"]!.AsArray())
        {
            var n = row!["count"]!.GetValue<int>(); var ring = SpatialSelection.Ring(n, spec.Radius, spec.SeatGap, spec.ArcFrom, spec.ArcTo);
            Assert.Equal(row["radius"]!.GetValue<double>(), ring.Radius, 8); Assert.Equal(row["closed"]!.GetValue<bool>(), ring.Closed);
            for (var i = 0; i < n; i++) Assert.Equal(row["angles"]![i]!.GetValue<double>(), ring.Angles[i], 8);
            for (var i = 0; i < n; i++) for (var j = i + 1; j < n; j++)
                Assert.True(2 * ring.Radius * Math.Abs(Math.Sin((ring.Angles[i] - ring.Angles[j]) / 2)) >= spec.SeatGap - 1e-7);
        }
    }

    [Fact]
    public void DwellNeedsStillnessAndRearmsOnlyAfterLeaving()
    {
        var model = new SpatialSelection(Spec()); model.SetChoices([Choice()]);
        model.MoveTo(model.Seats[0].X, model.Seats[0].Y);
        for (var i = 0; i < 110; i++) model.Step(1.0 / 60, 0, 0);
        Assert.Equal(-1, model.Opened);
        for (var i = 0; i < 50; i++) model.Step(1.0 / 60, 0, 0);
        Assert.Equal(0, model.Opened); model.Close();
        for (var i = 0; i < 120; i++) model.Step(1.0 / 60, 0, 0);
        Assert.Equal(-1, model.Opened);
        for (var i = 0; i < 60; i++) model.Step(1.0 / 60, 0, 1);
        model.MoveTo(model.Seats[0].X, model.Seats[0].Y);
        for (var i = 0; i < 180; i++) model.Step(1.0 / 60, 0, 0);
        Assert.Equal(0, model.Opened);
    }

    [Fact]
    public void UnavailableVesselsNeverDwellOpenAndSuspensionCancelsTravel()
    {
        var model = new SpatialSelection(Spec()); model.SetChoices([Choice(false)]);
        model.MoveTo(model.Seats[0].X, model.Seats[0].Y);
        for (var i = 0; i < 300; i++) model.Step(1.0 / 60, 0, 0);
        Assert.Equal(-1, model.Opened);
        model.MoveTo(100, 100); var x = model.X; var y = model.Y;
        model.Step(1, 1, 1, suspended: true); model.Step(1.0 / 60, 0, 0);
        Assert.Equal(x, model.X); Assert.Equal(y, model.Y);
        model.Open(0); Assert.Equal(0, model.Opened); // Accessible inspection may explain a refusal.
    }

    [Fact]
    public void TheVoidWrapsAndRosterRefreshInvalidatesTheOpenCard()
    {
        var model = new SpatialSelection(Spec()); model.SetChoices(Enumerable.Range(0, 80).Select(_ => Choice()));
        Assert.True(model.Rim > model.Spec.WrapRadius);
        for (var i = 0; i < 1000 && model.X >= 0; i++) model.Step(.05, 1, 0);
        Assert.True(model.X < 0); Assert.True(Math.Abs(model.X) < model.Rim);
        model.Open(4); model.SetChoices([Choice()]); Assert.Equal(-1, model.Opened); Assert.Equal(0, model.X);
    }

    [Fact]
    public void CurrentAccountsSeeEveryCallingAndExistingLivesRemainResumableAtCapacity()
    {
        var c = Content(); var ids = c.Each<CallingDef>().Select(d => d.Id).ToList();
        var list = new CharacterListMsg([], ids);
        Assert.All(EntryChoices.Build(list, c, Spec()), v => Assert.True(v.Available));
        list = list with { Characters = [new("one", "Wren", ids[0], 3, "0,0")], Entry = new("waking", "mu", "vessels", "mu", false, ids.Select(id => new CallingAvailability(id, true)).ToList()) };
        var choices = EntryChoices.Build(list, c, Spec());
        Assert.True(choices[0].Available); Assert.All(choices.Skip(1), v => Assert.False(v.Available));
    }
}
