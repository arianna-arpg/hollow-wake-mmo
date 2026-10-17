using System.Text.Json;
using System.Text.Json.Nodes;

namespace HollowWake.Core.Content;

/// <summary>One definition as it was read from disk, before binding: its kind, id, raw object and where it came from.</summary>
public sealed record RawDefinition(string Kind, string Id, JsonObject Object, string Origin);

/// <summary>
/// Reads content layers from disk into raw definitions. A layer is a directory of YAML or JSON files;
/// layers load in order and later layers override, patch or remove entries from earlier ones. That is
/// the whole modding model: core content is layer zero, an expansion is a layer, a server's tuning is
/// a layer, a player-made mod is a layer.
/// </summary>
public static class ContentLoader
{
    public static readonly string[] Extensions = { ".yaml", ".yml", ".json" };

    /// <summary>Reads every layer directory in order and merges entries by (kind, id).</summary>
    public static IReadOnlyDictionary<string, Dictionary<string, RawDefinition>> LoadRaw(IEnumerable<string> layerDirectories, ValidationReport report)
    {
        var merged = new Dictionary<string, Dictionary<string, RawDefinition>>(StringComparer.Ordinal);
        foreach (var dir in layerDirectories)
        {
            if (!Directory.Exists(dir))
            {
                report.Error("layer", dir, "Content layer directory does not exist.");
                continue;
            }
            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            foreach (var file in files)
            {
                foreach (var doc in ReadDocuments(file, report))
                {
                    ApplyDocument(doc, file, merged, report);
                }
            }
        }
        return merged;
    }

    /// <summary>Parses one file's documents into raw definitions without merging. Used by tools and tests.</summary>
    public static IEnumerable<JsonObject> ReadDocuments(string file, ValidationReport report)
    {
        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception ex)
        {
            report.Error("file", file, $"Could not read: {ex.Message}");
            yield break;
        }
        foreach (var doc in ParseText(text, file, report))
        {
            yield return doc;
        }
    }

    public static IEnumerable<JsonObject> ParseText(string text, string origin, ValidationReport report)
    {
        var results = new List<JsonObject>();
        var ext = Path.GetExtension(origin).ToLowerInvariant();
        try
        {
            if (ext == ".json")
            {
                var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (node is JsonObject o)
                {
                    results.Add(o);
                }
                else if (node is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is JsonObject io)
                        {
                            results.Add(io);
                        }
                    }
                }
            }
            else
            {
                foreach (var node in YamlToJson.ParseDocuments(text))
                {
                    if (node is JsonObject o)
                    {
                        results.Add(o);
                    }
                    else if (node is not null)
                    {
                        report.Warn("file", origin, "Top-level YAML document is not a mapping; skipped.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            report.Error("file", origin, $"Parse error: {ex.Message}");
        }
        return results;
    }

    private static void ApplyDocument(JsonObject doc, string origin, Dictionary<string, Dictionary<string, RawDefinition>> merged, ValidationReport report)
    {
        var kind = doc["kind"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(kind))
        {
            report.Error("file", origin, "Document has no 'kind'.");
            return;
        }
        if (!merged.TryGetValue(kind, out var byId))
        {
            byId = new Dictionary<string, RawDefinition>(StringComparer.Ordinal);
            merged[kind] = byId;
        }
        var entries = doc["entries"];
        if (entries is null)
        {
            report.Warn(kind, "", "Document has no 'entries'.", origin);
            return;
        }
        if (entries is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not JsonObject obj)
                {
                    report.Error(kind, "", "Entry is not an object.", origin);
                    continue;
                }
                var id = obj["id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.Error(kind, "", "Entry has no 'id'.", origin);
                    continue;
                }
                ApplyEntry(kind, id, obj, origin, byId, report);
            }
        }
        else if (entries is JsonObject map)
        {
            foreach (var kv in map)
            {
                if (kv.Value is not JsonObject obj)
                {
                    report.Error(kind, kv.Key, "Entry is not an object.", origin);
                    continue;
                }
                obj["id"] ??= kv.Key;
                ApplyEntry(kind, kv.Key, obj, origin, byId, report);
            }
        }
        else
        {
            report.Error(kind, "", "'entries' must be a list or a map.", origin);
        }
    }

    private static void ApplyEntry(string kind, string id, JsonObject obj, string origin, Dictionary<string, RawDefinition> byId, ValidationReport report)
    {
        var remove = obj["remove"]?.GetValue<bool>() ?? false;
        var patch = obj["patch"]?.GetValue<bool>() ?? false;
        obj.Remove("remove");
        obj.Remove("patch");

        if (remove)
        {
            if (!byId.Remove(id))
            {
                report.Warn(kind, id, "Remove requested but no earlier layer defines it.", origin);
            }
            return;
        }
        if (patch)
        {
            if (!byId.TryGetValue(id, out var existing))
            {
                report.Warn(kind, id, "Patch requested but no earlier layer defines it; treated as a new entry.", origin);
                byId[id] = new RawDefinition(kind, id, (JsonObject)obj.DeepClone(), origin);
                return;
            }
            var target = (JsonObject)existing.Object.DeepClone();
            MergePatch(target, obj);
            byId[id] = new RawDefinition(kind, id, target, $"{existing.Origin} + {origin}");
            return;
        }
        byId[id] = new RawDefinition(kind, id, (JsonObject)obj.DeepClone(), origin);
    }

    /// <summary>JSON Merge Patch semantics: objects merge recursively, null deletes, anything else replaces.</summary>
    public static void MergePatch(JsonObject target, JsonObject patch)
    {
        foreach (var kv in patch)
        {
            if (kv.Value is null)
            {
                target.Remove(kv.Key);
                continue;
            }
            if (kv.Value is JsonObject po && target[kv.Key] is JsonObject to)
            {
                MergePatch(to, po);
                continue;
            }
            target[kv.Key] = kv.Value.DeepClone();
        }
    }
}
