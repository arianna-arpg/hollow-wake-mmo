using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.World;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>
/// What a player wears as the wire carries it: one entry per worn slot from the item base's class and tags, sorted by
/// slot, on players only; a change of gear is a change the fingerprint sees, a delta carries the list whole, and an
/// empty one clears it.
/// </summary>
public class AppearanceSnapshotTests
{
    private static readonly Lazy<ContentRegistry> Content = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "content", "core")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var registry = ContentRegistry.Load(new[] { Path.Combine(dir!, "content", "core") }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        return registry;
    });

    private static Simulation Arena() => ZoneBuilder.CreateArena(Content.Value, 5);

    private static Entity Body(Simulation sim, bool player, params (string Slot, string Base)[] worn)
    {
        var e = sim.World.Create();
        var at = new Vector3(4, 4, 0);
        sim.World.Add(e, new Transform { Position = at, Radius = 0.4f, Height = 1.8f });
        sim.World.Add(e, new Identity { Name = player ? "Wren" : "Brigand", IsPlayer = player, Pattern = player ? null : "loosed_brigand" });
        var equipment = sim.World.Add(e, new Equipment());
        foreach (var (slot, itemBase) in worn)
        {
            equipment.BySlot[slot] = new ItemInstance { Base = itemBase, Name = itemBase };
        }
        sim.Spatial.Update(e, at);
        return e;
    }

    /// <summary>An entry's slot and class, and its tags as a set, since a base's tags have no order a client reads.</summary>
    private static (string Slot, string Class, HashSet<string> Tags) Parse(string entry)
    {
        var eq = entry.IndexOf('=');
        var words = entry[(eq + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (entry[..eq], words[0], words.Skip(1).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void APlayerCarriesItsWornGearByClassAndTagsSortedBySlot()
    {
        var sim = Arena();
        var wren = Body(sim, player: true, ("main_hand", "short_sword"), ("chest", "leather_jerkin"), ("off_hand", "buckler"));
        var state = Snapshots.Capture(sim, wren);
        Assert.NotNull(state.Appearance);
        var entries = state.Appearance!.Select(Parse).ToList();
        Assert.Equal(new[] { "chest", "main_hand", "off_hand" }, entries.Select(e => e.Slot));
        Assert.Equal(new[] { "armor.chest", "weapon.sword", "shield.buckler" }, entries.Select(e => e.Class));
        Assert.Equal(new HashSet<string> { "light" }, entries[0].Tags);
        Assert.Equal(new HashSet<string> { "melee", "blade", "one_hand" }, entries[1].Tags);
        Assert.Contains("\"appearance\":[\"chest=armor.chest light\",", MessageSerializer.ToJson(new EntitySpawnMsg(state)));
        // the same gear captured again reads the same, whatever order the slots were worn in
        var again = Body(sim, player: true, ("off_hand", "buckler"), ("chest", "leather_jerkin"), ("main_hand", "short_sword"));
        Assert.Equal(state.Appearance, Snapshots.Capture(sim, again).Appearance);

        // nothing worn: a present empty descriptor, distinct from an older server
        var bare = Snapshots.Capture(sim, Body(sim, player: true));
        Assert.NotNull(bare.Appearance);
        Assert.Empty(bare.Appearance!);
        Assert.Contains("\"appearance\":[]", MessageSerializer.ToJson(new EntitySpawnMsg(bare)));
        // a monster's pattern says what it wears; the wire never does
        Assert.Null(Snapshots.Capture(sim, Body(sim, player: false, ("main_hand", "short_sword"))).Appearance);
        // a base content does not know draws nothing
        Assert.Empty(Snapshots.Capture(sim, Body(sim, player: true, ("main_hand", "no_such_item"))).Appearance!);
    }

    [Fact]
    public void AChangeOfGearIsAChangeAndRidesADeltaWhole()
    {
        var sim = Arena();
        var wren = Body(sim, player: true, ("main_hand", "short_sword"));
        var equipment = sim.World.Get<Equipment>(wren)!;
        var before = Snapshots.Fingerprint(sim, wren);
        var first = Snapshots.Capture(sim, wren);

        // a piece put on
        equipment.BySlot["chest"] = new ItemInstance { Base = "iron_cuirass", Name = "Iron Cuirass" };
        var dressed = Snapshots.Fingerprint(sim, wren);
        Assert.NotEqual(before, dressed);
        var state = Snapshots.Capture(sim, wren);
        var delta = Snapshots.Delta(first, state, out var empty);
        Assert.NotNull(delta);
        Assert.False(empty);
        Assert.Equal(state.Appearance, delta!.Appearance);
        Assert.Equal(state.Appearance, delta.ApplyTo(first).Appearance);
        Assert.Null(delta.X);

        // the same gear is no change at all
        Assert.True(Snapshots.Delta(state, Snapshots.Capture(sim, wren), out empty) is { Appearance: null });
        Assert.True(empty);

        // a piece swapped in the same slot
        equipment.BySlot["main_hand"] = new ItemInstance { Base = "hand_axe", Name = "Hand Axe" };
        Assert.NotEqual(dressed, Snapshots.Fingerprint(sim, wren));
        Assert.Contains(Snapshots.Capture(sim, wren).Appearance!, a => a.StartsWith("main_hand=weapon.axe", StringComparison.Ordinal));

        // the last piece off: the full state and delta both preserve an empty list
        equipment.BySlot.Clear();
        var bare = Snapshots.Capture(sim, wren);
        Assert.NotNull(bare.Appearance);
        Assert.Empty(bare.Appearance!);
        var undressed = Snapshots.Delta(state, bare, out empty)!;
        Assert.False(empty);
        Assert.NotNull(undressed.Appearance);
        Assert.Empty(undressed.Appearance!);
        Assert.Contains("\"appearance\":[]", MessageSerializer.ToJson(new EntityDeltaMsg(new List<EntityDelta> { undressed }, 1)));
        Assert.NotNull(undressed.ApplyTo(state).Appearance);
        Assert.Empty(undressed.ApplyTo(state).Appearance!);
        // the fingerprint is a function of what a viewer sees: another body in the same place with the same gear hashes the same
        Assert.Equal(before, Snapshots.Fingerprint(sim, Body(sim, player: true, ("main_hand", "short_sword"))));
    }

    [Fact]
    public void AppearancePresenceSurvivesFullStatesAndDeltas()
    {
        var sim = Arena();
        var bare = Snapshots.Capture(sim, Body(sim, player: true));
        var legacy = bare with { Appearance = null };
        var delta = Snapshots.Delta(legacy, bare, out var empty);
        Assert.NotNull(delta);
        Assert.False(empty);
        Assert.NotNull(delta!.Appearance);
        Assert.Empty(delta.Appearance!);
        Assert.Equal(bare.Appearance, delta.ApplyTo(legacy).Appearance);
        // A delta's absent field means unchanged; returning to absence requires a full state.
        Assert.Null(Snapshots.Delta(bare, legacy, out empty));
        Assert.False(empty);
        Assert.Contains("\"appearance\":[]", MessageSerializer.ToJson(new EntityUpdateMsg(new List<EntityState> { bare }, 1)));
        Assert.DoesNotContain("appearance", MessageSerializer.ToJson(new EntitySpawnMsg(legacy)));
    }
}
