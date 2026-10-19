using System.Text.Json;
using System.Text.Json.Serialization;
using HollowWake.Core.Content;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Server.Tests;

public class SnapshotSerializationTests
{
    [Fact]
    public void GeneratedEntityWriterPreservesTheExistingWireBytesAndReader()
    {
        var reference = new JsonSerializerOptions(ContentJson.Options)
        {
            WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var complete = new EntityState
        {
            Key = 123, Name = "Tusk <&> \"雪\"", Pattern = "test", Look = "look", X = -1.25f, Y = 2.5f, Z = 3,
            Yaw = 0.5f, Radius = 0.4f, Height = 1.8f, HitBody = "human", Faction = "players", Rarity = "rare",
            IsPlayer = true, Level = 4, Resources = new() { ["life"] = 4.5f }, ResourceMax = new() { ["life"] = 20 },
            Flags = new() { "one", "two" }, Statuses = new() { "burning" }, Interactable = "vendor",
            Dead = true, Casting = "Ember Bolt", CastProgress = 0.3f, Grounded = false, VX = 1, VY = 2, VZ = -3,
            Static = true, Shape = "sector", ShapeSize = new() { Radius = 3, Angle = 1, Width = 2, Inner = 0.4f, Curvature = 0.5f },
            Heading = 1.2f, Spin = 0.7f, Until = 20.35, Appearance = new() { "hand=sword" },
        };
        foreach (var state in new[] { new EntityState(), complete, complete with { Static = false, ShapeSize = null, HitBody = null } })
        foreach (Message message in new Message[] { new EntitySpawnMsg(state), new EntityUpdateMsg(new() { state }, 12.5) })
        {
            var expected = JsonSerializer.SerializeToUtf8Bytes(message, reference);
            Assert.Equal(expected, MessageSerializer.Encode(message));
            Assert.Equal(expected, MessageSerializer.Encode(MessageSerializer.Decode(expected)!));
        }
    }

    [Fact]
    public void EntityReaderKeepsCaseInsensitiveKeysAndStringNumbers()
    {
        var message = MessageSerializer.Decode("""{"t":"entity_spawn","state":{"KEY":"42","NAME":"Reader","x":"1.25"}}"""u8);
        var state = Assert.IsType<EntitySpawnMsg>(message).State;
        Assert.Equal(42, state.Key);
        Assert.Equal("Reader", state.Name);
        Assert.Equal(1.25f, state.X);
    }
}
