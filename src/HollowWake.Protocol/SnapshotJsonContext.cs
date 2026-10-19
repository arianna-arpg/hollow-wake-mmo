using System.Text.Json;
using System.Text.Json.Serialization;

namespace HollowWake.Protocol;

// The frequently repeated entity payload has only wire primitives. Generate its writer while
// retaining the existing extensible Message serializer and content converters for other payloads.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EntityState))]
internal partial class SnapshotJsonContext : JsonSerializerContext;

internal sealed class SnapshotStateConverter(JsonSerializerOptions readOptions) : JsonConverter<EntityState>
{
    public override EntityState? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<EntityState>(ref reader, readOptions);

    public override void Write(Utf8JsonWriter writer, EntityState value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, SnapshotJsonContext.Default.EntityState);
}
