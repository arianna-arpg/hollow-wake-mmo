using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Weaves;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class GroundAimAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("direction")]
    [InlineData("ground")]
    [InlineData("refused")]
    public void GroundResolutionDoesNotGenerateAtAnUnboundedOrRejectedPoint(string kind)
    {
        var a = DeliveryArena.Create();
        var runtime = a.Sim.Service<WeaveRuntime>();
        var book = a.Sim.World.Get<WeaveBook>(a.Caster)!;
        var spec = kind == "direction" ? new WeaveSpec { Preset = "firebolt" } :
            new WeaveSpec { Form = kind == "ground" ? "blast" : "raise", Threads = { "ember" } };
        var slot = book.Add(spec);
        var point = new Vector3(1000000, 10, 0);
        var far = a.Sim.Terrain.Layout.ChunkOf(point.X, point.Y);
        var before = a.Sim.Terrain.LoadedChunkCount;
        Assert.False(a.Sim.Terrain.IsLoaded(far));
        var accepted = runtime.TryBegin(a.Sim, a.Caster, slot, Entity.None, point, Vector2.UnitX, out var reason);
        output.WriteLine($"{kind}: accepted={accepted}, reason={reason}, chunks={before}->{a.Sim.Terrain.LoadedChunkCount}, remote={a.Sim.Terrain.IsLoaded(far)}");
        Assert.Equal(kind != "refused", accepted);
        Assert.False(a.Sim.Terrain.IsLoaded(far));
        if (!accepted)
        {
            Assert.Equal("no_target", reason);
            Assert.Equal(before, a.Sim.Terrain.LoadedChunkCount);
        }
        else
        {
            var limit = kind == "direction" ? a.Sim.Content.Rules.Number("casting.max_aim_range", 40) :
                runtime.Resolve(a.Sim, slot, out _)!.Range(a.Sim.World.Get<Stats>(a.Caster)!);
            Assert.InRange(a.Sim.World.Get<Transform>(a.Caster)!.GroundDistanceTo(book.Casting!.Point), 0, (float)limit + .001f);
            Assert.Equal(Vector2.UnitX, book.Casting.Direction);
        }
    }
}
