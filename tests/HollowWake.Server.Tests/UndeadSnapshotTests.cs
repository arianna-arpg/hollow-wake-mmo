using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Server.World;
using Xunit;

namespace HollowWake.Server.Tests;

public class UndeadSnapshotTests
{
    [Fact]
    public void SummonedAppearanceCarriesOwnerAllegianceAndChangesItsFingerprint()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "content", "core"))) dir = Path.GetDirectoryName(dir);
        var content = ContentRegistry.Load(new[] { Path.Combine(dir!, "content", "core") }, out _);
        var sim = ZoneBuilder.CreateArena(content, 7);
        var factory = sim.Service<PatternFactory>();
        var owner = factory.Spawn(sim, "loosed_kindler", new Vector3(20, 20, 0), new SpawnOptions { Faction = "players" });
        var body = factory.Spawn(sim, "skeleton_warrior", new Vector3(22, 20, 0), new SpawnOptions { Owner = owner });
        Assert.Equal("players", Snapshots.Capture(sim, body).Faction);
        sim.World.Add(body, new Minion { Owner = owner });
        var state = Snapshots.Capture(sim, body);
        Assert.Equal("skeleton_warrior", state.Look);
        Assert.Equal("players", state.Faction);
        var fingerprint = Snapshots.Fingerprint(sim, body);
        sim.World.Get<Identity>(owner)!.Faction = "the_loosed";
        Assert.Equal("the_loosed", Snapshots.Capture(sim, body).Faction);
        Assert.NotEqual(fingerprint, Snapshots.Fingerprint(sim, body));
        Assert.Equal("players", sim.World.Get<Identity>(body)!.Faction);
        Assert.Equal("remnant", content.Require<HollowWake.Core.Definitions.PatternDef>("skeleton_warrior").Faction);
    }
}
