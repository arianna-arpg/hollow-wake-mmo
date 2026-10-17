using System.Globalization;
using System.Text.Json.Nodes;

namespace HollowWake.Core.Content;

/// <summary>
/// Reads numbers from JSON nodes regardless of how they were created. A node built from YAML holds a
/// long for <c>4</c> and a double for <c>4.5</c>; a node built in code may hold an int; a node parsed
/// from the wire holds a JsonElement. <see cref="JsonValue.TryGetValue{T}"/> only converts the last,
/// so every numeric read goes through here.
/// </summary>
public static class JsonNum
{
    public static bool TryDouble(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue v)
        {
            return false;
        }
        if (v.TryGetValue<double>(out value))
        {
            return true;
        }
        if (v.TryGetValue<long>(out var l))
        {
            value = l;
            return true;
        }
        if (v.TryGetValue<int>(out var i))
        {
            value = i;
            return true;
        }
        if (v.TryGetValue<float>(out var f))
        {
            value = f;
            return true;
        }
        if (v.TryGetValue<decimal>(out var m))
        {
            value = (double)m;
            return true;
        }
        if (v.TryGetValue<string>(out var s))
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        try
        {
            return double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        catch
        {
            return false;
        }
    }

    public static double Of(JsonNode? node, double fallback) => TryDouble(node, out var v) ? v : fallback;

    public static int IntOf(JsonNode? node, int fallback) => TryDouble(node, out var v) ? (int)Math.Round(v) : fallback;

    public static bool BoolOf(JsonNode? node, bool fallback) => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public static string? StringOf(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
