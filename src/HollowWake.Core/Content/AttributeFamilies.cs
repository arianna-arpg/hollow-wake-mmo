using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content;

/// <summary>
/// Expands attribute families into attribute definitions at load. A family's pattern names placeholders in
/// braces; each ranges over the ids of a content kind or over a list of values; every combination becomes
/// one attribute with the family's category, base and bounds, and its name and description templates
/// filled with the members' display names. Attributes declared one by one win over family members.
/// </summary>
public static partial class AttributeFamilies
{
    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder();

    /// <summary>The placeholders of a pattern, in the order they appear, each once.</summary>
    public static List<string> PlaceholdersOf(string pattern)
    {
        var list = new List<string>();
        foreach (Match m in Placeholder().Matches(pattern))
        {
            var name = m.Groups[1].Value;
            if (!list.Contains(name))
            {
                list.Add(name);
            }
        }
        return list;
    }

    /// <summary>
    /// The members of one family, or the problems that prevent expanding it. Values from a kind come in id
    /// order, so the expansion is the same on every load.
    /// </summary>
    public static List<AttributeDef> Members(ContentRegistry registry, AttributeFamilyDef family, List<string> problems)
    {
        var members = new List<AttributeDef>();
        var placeholders = PlaceholdersOf(family.Pattern);
        var ranges = new List<(string Placeholder, List<(string Id, string Display)> Values)>();
        foreach (var p in placeholders)
        {
            JsonNode? source = family.Over switch
            {
                JsonObject map => map[p],
                _ when placeholders.Count == 1 => family.Over,
                _ => null,
            };
            if (source is null)
            {
                problems.Add($"placeholder '{{{p}}}' has nothing to range over; give 'over' a kind name or a list, or a map with an entry for '{p}'.");
                continue;
            }
            var values = ValuesOf(registry, source, p, problems);
            if (values is not null)
            {
                ranges.Add((p, values));
            }
        }
        if (problems.Count > 0)
        {
            return members;
        }
        if (placeholders.Count == 0)
        {
            members.Add(Member(family, family.Pattern, new Dictionary<string, string>()));
            return members;
        }
        var chosen = new Dictionary<string, (string Id, string Display)>(StringComparer.Ordinal);
        Expand(family, ranges, 0, chosen, members);
        return members;
    }

    private static void Expand(AttributeFamilyDef family, List<(string Placeholder, List<(string Id, string Display)> Values)> ranges, int depth, Dictionary<string, (string Id, string Display)> chosen, List<AttributeDef> into)
    {
        if (depth == ranges.Count)
        {
            var id = family.Pattern;
            var display = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (p, value) in chosen)
            {
                id = id.Replace("{" + p + "}", value.Id, StringComparison.Ordinal);
                display[p] = value.Display;
            }
            into.Add(Member(family, id, display));
            return;
        }
        var (placeholder, values) = ranges[depth];
        foreach (var v in values)
        {
            chosen[placeholder] = v;
            Expand(family, ranges, depth + 1, chosen, into);
        }
        chosen.Remove(placeholder);
    }

    private static AttributeDef Member(AttributeFamilyDef family, string id, Dictionary<string, string> display)
    {
        return new AttributeDef
        {
            Id = id,
            Name = Fill(family.Name, display),
            Description = Fill(family.Description, display),
            Tags = family.Tags,
            Category = family.Category,
            Base = family.Base,
            Min = family.Bounds?.Min,
            Max = family.Bounds?.Max,
            Decimals = family.Bounds?.Decimals,
            Percent = family.Percent,
            Hidden = family.Hidden,
            Family = family.Id,
        };
    }

    private static string Fill(string template, Dictionary<string, string> display)
    {
        if (template.Length == 0)
        {
            return template;
        }
        foreach (var (p, name) in display)
        {
            template = template.Replace("{" + p + "}", name, StringComparison.Ordinal);
        }
        return template;
    }

    private static List<(string Id, string Display)>? ValuesOf(ContentRegistry registry, JsonNode source, string placeholder, List<string> problems)
    {
        switch (source)
        {
            case JsonValue v when v.TryGetValue<string>(out var kind):
            {
                if (!ContentKinds.TryGet(kind, out _))
                {
                    problems.Add($"'{{{placeholder}}}' ranges over unknown content kind '{kind}'.");
                    return null;
                }
                return registry.IdsOfKind(kind)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => (id, registry.DisplayNameOf(kind, id)))
                    .ToList();
            }
            case JsonArray list:
            {
                var values = new List<(string, string)>();
                foreach (var item in list)
                {
                    var text = item is JsonValue iv && iv.TryGetValue<string>(out var s) ? s : item?.ToJsonString();
                    if (string.IsNullOrEmpty(text))
                    {
                        problems.Add($"'{{{placeholder}}}' has an empty value.");
                        continue;
                    }
                    values.Add((text, Definition.Humanize(text)));
                }
                return values;
            }
            default:
                problems.Add($"'{{{placeholder}}}' must range over a kind name or a list.");
                return null;
        }
    }
}
