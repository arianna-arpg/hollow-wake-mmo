using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HollowWake.Client.UI.Logic;

/// <summary>
/// Reads content entries as the loader leaves them (JSON nodes from YAML: numbers arrive as longs or doubles,
/// quoted scalars as strings). The interface reads its own content and a few names from the rest this way,
/// without binding simulation types.
/// </summary>
public static class Json
{
    public static string Str(JsonNode? node, string fallback = "")
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s))
            {
                return s;
            }
            if (v.GetValueKind() is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return v.ToJsonString();
            }
        }
        return fallback;
    }

    public static string? StrOrNull(JsonNode? node) => node is JsonValue ? Str(node) : null;

    public static double Num(JsonNode? node, double fallback = 0)
    {
        if (node is not JsonValue v)
        {
            return fallback;
        }
        if (v.TryGetValue<double>(out var d))
        {
            return d;
        }
        if (v.TryGetValue<long>(out var l))
        {
            return l;
        }
        if (v.TryGetValue<int>(out var i))
        {
            return i;
        }
        if (v.TryGetValue<float>(out var f))
        {
            return f;
        }
        if (v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return fallback;
    }

    public static bool Bool(JsonNode? node, bool fallback = false) =>
        node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public static List<string> Strings(JsonNode? node)
    {
        var list = new List<string>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var s = Str(item);
                if (s.Length > 0)
                {
                    list.Add(s);
                }
            }
        }
        else if (node is JsonValue)
        {
            // A single value, or a space-separated list as tag sets allow.
            foreach (var s in Str(node).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(s);
            }
        }
        return list;
    }

    public static Dictionary<string, string> StringMap(JsonNode? node)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node is JsonObject obj)
        {
            foreach (var (k, v) in obj)
            {
                map[k] = Str(v);
            }
        }
        return map;
    }

    public static Dictionary<string, double> NumberMap(JsonNode? node)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        if (node is JsonObject obj)
        {
            foreach (var (k, v) in obj)
            {
                map[k] = Num(v);
            }
        }
        return map;
    }

    public static IEnumerable<(string Key, JsonObject Value)> Objects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (k, v) in obj)
            {
                if (v is JsonObject o)
                {
                    yield return (k, o);
                }
            }
        }
    }

    public static IEnumerable<JsonObject> Items(JsonNode? node)
    {
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonObject o)
                {
                    yield return o;
                }
            }
        }
    }
}
