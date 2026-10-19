using System.Numerics;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Client.Tests;

public class GroundRayTests
{
    [Theory]
    [InlineData(1.2f)]
    [InlineData(3f)]
    public void VisibleDeckAndGroundUnderItAreDistinct(float height)
    {
        var terrain = Terrain(height);
        Assert.Equal(height, GroundRay.Pick(terrain, new(1.5f, 1.5f, 10), -Vector3.UnitZ, _ => true)!.Value.Z);
        Assert.Equal(0, GroundRay.Pick(terrain, new(1.5f, 1.5f, height / 2), -Vector3.UnitZ, _ => true)!.Value.Z);
        Assert.Null(GroundRay.Pick(terrain, new(1.5f, 1.5f, 10), -Vector3.UnitZ, _ => false));
        Assert.Equal(1, terrain.LoadedChunkCount);
    }

    [Fact]
    public void ObliqueRayHitsOnlyInsideTheDeckCell()
    {
        var terrain = Terrain(3);
        var hit = GroundRay.Pick(terrain, new(.5f, 1.5f, 4), Vector3.Normalize(new(1, 0, -1)), _ => true);
        Assert.Equal(1.5f, hit!.Value.X, 4);
        Assert.Equal(3, hit.Value.Z);
        // Enters the deck cell already below its plane; must not snap upward onto the edge.
        var under = GroundRay.Pick(terrain, new(.9f, 1.5f, 2.9f), Vector3.Normalize(new(.1f, 0, -1)), _ => true);
        Assert.Equal(0, under!.Value.Z);
    }

    [Fact]
    public void UnloadedDataDoesNotInvokeTheProvider()
    {
        var terrain = Terrain(3);
        Assert.Null(GroundRay.Pick(terrain, new(10, 10, 10), -Vector3.UnitZ, _ => true, 12));
        Assert.Equal(1, terrain.LoadedChunkCount);
    }

    private static Terrain Terrain(float height)
    {
        var terrain = new Terrain(new WorldLayout { ChunkSize = 4 }, new Provider(height));
        terrain.GetChunk(new ChunkCoord(0, 0));
        return terrain;
    }

    private sealed class Provider(float height) : IChunkProvider
    {
        public ChunkData Load(ChunkCoord coord)
        {
            Assert.Equal(new ChunkCoord(0, 0), coord);
            var chunk = new ChunkData(coord, 4);
            Array.Fill(chunk.Flags, CellFlags.Walkable);
            chunk.AddLayer(5, new Surface(height, true, TagSet.Of("structure", "bridge")));
            return chunk;
        }
    }
}
