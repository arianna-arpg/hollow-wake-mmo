using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Content;

/// <summary>
/// JSON conventions for every definition: snake_case keys, enums as snake_case strings, tag sets as
/// arrays or space-separated strings, expressions as strings or numbers, typed specs as objects with a
/// <c>type</c> key.
/// </summary>
public static class ContentJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };
        o.Converters.Add(new TagSetConverter());
        o.Converters.Add(new ExprConverter());
        o.Converters.Add(new TypedSpecConverter());
        o.Converters.Add(new WeaveSpecConverter());
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        return o;
    }

    public static T Bind<T>(JsonNode node) => node.Deserialize<T>(Options) ?? throw new JsonException($"Could not bind {typeof(T).Name}.");

    public static JsonNode ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, Options) ?? new JsonObject();

    private static readonly Dictionary<Type, HashSet<string>> KnownKeys = new();

    /// <summary>Keys present in the object that the target type does not declare. Catches typos in content.</summary>
    public static IEnumerable<string> UnknownKeys(Type type, JsonObject obj)
    {
        HashSet<string> known;
        lock (KnownKeys)
        {
            if (!KnownKeys.TryGetValue(type, out known!))
            {
                known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                    {
                        continue;
                    }
                    var name = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name);
                    known.Add(name);
                }
                KnownKeys[type] = known;
            }
        }
        foreach (var kv in obj)
        {
            if (!known.Contains(kv.Key))
            {
                yield return kv.Key;
            }
        }
    }

    // ------------------------------------------------------------ converters

    private sealed class TagSetConverter : JsonConverter<TagSet>
    {
        public override TagSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return TagSet.Empty;
                case JsonTokenType.String:
                    return TagSet.Parse(reader.GetString());
                case JsonTokenType.StartArray:
                {
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            list.Add(reader.GetString()!);
                        }
                        else
                        {
                            throw new JsonException("Tags must be strings.");
                        }
                    }
                    return TagSet.From(list);
                }
                default:
                    throw new JsonException("A tag set is a string or an array of strings.");
            }
        }

        public override void Write(Utf8JsonWriter writer, TagSet value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var t in value)
            {
                writer.WriteStringValue(t);
            }
            writer.WriteEndArray();
        }
    }

    private sealed class ExprConverter : JsonConverter<Expr>
    {
        public override Expr? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.Number:
                    return Expr.Constant(reader.GetDouble());
                case JsonTokenType.True:
                    return Expr.Constant(1);
                case JsonTokenType.False:
                    return Expr.Constant(0);
                case JsonTokenType.String:
                {
                    var s = reader.GetString()!;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    {
                        return Expr.Constant(d);
                    }
                    try
                    {
                        return Expr.Parse(s);
                    }
                    catch (ExprParseException ex)
                    {
                        throw new JsonException($"Bad expression \"{s}\": {ex.Message}");
                    }
                }
                default:
                    throw new JsonException("An expression is a number or a formula string.");
            }
        }

        public override void Write(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
        {
            if (value.IsConstant)
            {
                writer.WriteNumberValue(value.ConstantValue);
            }
            else
            {
                writer.WriteStringValue(value.Source);
            }
        }
    }

    /// <summary>
    /// A typed spec is written as <c>{ type: damage, amount: 10 }</c>; everything but <c>type</c>
    /// becomes the params object. A bare string <c>"heal"</c> is a spec with no params.
    /// </summary>
    private sealed class TypedSpecConverter : JsonConverter<TypedSpec>
    {
        public override TypedSpec? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                return new TypedSpec { Type = reader.GetString()!, Params = new JsonObject() };
            }
            var node = JsonNode.Parse(ref reader) as JsonObject ?? throw new JsonException("A typed spec is an object with a 'type' key.");
            var type = node["type"]?.GetValue<string>() ?? throw new JsonException("Typed spec is missing 'type'.");
            node.Remove("type");
            return new TypedSpec { Type = type, Params = node };
        }

        public override void Write(Utf8JsonWriter writer, TypedSpec value, JsonSerializerOptions options)
        {
            var obj = new JsonObject { ["type"] = value.Type };
            foreach (var kv in value.Params)
            {
                obj[kv.Key] = kv.Value?.DeepClone();
            }
            obj.WriteTo(writer, options);
        }
    }

    /// <summary>A weave spec is either a preset id string or a composition object.</summary>
    private sealed class WeaveSpecConverter : JsonConverter<WeaveSpec>
    {
        public override WeaveSpec? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                return new WeaveSpec { Preset = reader.GetString() };
            }
            var node = JsonNode.Parse(ref reader) as JsonObject ?? throw new JsonException("A weave is a preset id or an object.");
            return new WeaveSpec
            {
                Preset = node["preset"]?.GetValue<string>(),
                Form = node["form"]?.GetValue<string>(),
                Threads = node["threads"] is JsonArray ta ? ta.Select(n => n!.GetValue<string>()).ToList() : new List<string>(),
                Glyphs = node["glyphs"] is JsonArray ga ? ga.Select(n => n!.GetValue<string>()).ToList() : new List<string>(),
                Level = JsonNum.IntOf(node["level"], 1),
                Name = node["name"]?.GetValue<string>(),
            };
        }

        public override void Write(Utf8JsonWriter writer, WeaveSpec value, JsonSerializerOptions options)
        {
            if (value.Preset is not null && value.Form is null && value.Glyphs.Count == 0 && value.Level == 1)
            {
                writer.WriteStringValue(value.Preset);
                return;
            }
            writer.WriteStartObject();
            if (value.Preset is not null)
            {
                writer.WriteString("preset", value.Preset);
            }
            if (value.Form is not null)
            {
                writer.WriteString("form", value.Form);
            }
            writer.WritePropertyName("threads");
            JsonSerializer.Serialize(writer, value.Threads, options);
            writer.WritePropertyName("glyphs");
            JsonSerializer.Serialize(writer, value.Glyphs, options);
            writer.WriteNumber("level", value.Level);
            if (value.Name is not null)
            {
                writer.WriteString("name", value.Name);
            }
            writer.WriteEndObject();
        }
    }
}
