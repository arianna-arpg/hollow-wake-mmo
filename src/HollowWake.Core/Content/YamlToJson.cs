using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace HollowWake.Core.Content;

/// <summary>
/// Converts YAML documents into <see cref="JsonNode"/> trees so YAML and JSON content go through one
/// binding path. Plain scalars become numbers, booleans or null where they parse as such; quoted
/// scalars always stay strings, so <c>"20"</c> is text and <c>20</c> is a number.
/// </summary>
public static class YamlToJson
{
    public static IReadOnlyList<JsonNode?> ParseDocuments(string yamlText)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yamlText);
        stream.Load(reader);
        var result = new List<JsonNode?>();
        foreach (var doc in stream.Documents)
        {
            result.Add(Convert(doc.RootNode));
        }
        return result;
    }

    public static JsonNode? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
            {
                var obj = new JsonObject();
                foreach (var kv in map.Children)
                {
                    var key = kv.Key is YamlScalarNode ks ? ks.Value ?? "" : kv.Key.ToString() ?? "";
                    obj[key] = Convert(kv.Value);
                }
                return obj;
            }
            case YamlSequenceNode seq:
            {
                var arr = new JsonArray();
                foreach (var child in seq.Children)
                {
                    arr.Add(Convert(child));
                }
                return arr;
            }
            case YamlScalarNode scalar:
                return ConvertScalar(scalar);
            default:
                return null;
        }
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var v = scalar.Value;
        if (v is null)
        {
            return null;
        }
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
        {
            return JsonValue.Create(v);
        }
        switch (v)
        {
            case "":
            case "~":
            case "null":
            case "Null":
            case "NULL":
                return null;
            case "true":
            case "True":
            case "TRUE":
                return JsonValue.Create(true);
            case "false":
            case "False":
            case "FALSE":
                return JsonValue.Create(false);
        }
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return JsonValue.Create(l);
        }
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
        {
            return JsonValue.Create(hex);
        }
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !v.Contains('_'))
        {
            // Keep things like "1e5" numeric but avoid turning words such as "Infinity" into numbers.
            if (!double.IsInfinity(d) && !double.IsNaN(d))
            {
                return JsonValue.Create(d);
            }
        }
        return JsonValue.Create(v);
    }
}
