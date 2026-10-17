using System.Runtime.CompilerServices;
using HollowWake.Core.Attributes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Progression;

/// <summary>
/// The options of choice groups: those declared, plus those generated over an attribute category (one per
/// attribute, a single modifier each), built once per content registry.
/// </summary>
public static class PassiveChoices
{
    private sealed class Table
    {
        public readonly Dictionary<string, List<PassiveOptionDef>> ByGroup = new(StringComparer.Ordinal);
    }

    private static readonly ConditionalWeakTable<ContentRegistry, Table> Tables = new();

    public static IReadOnlyList<PassiveOptionDef> OptionsOf(ContentRegistry content, string group)
    {
        var table = Tables.GetValue(content, static _ => new Table());
        lock (table)
        {
            if (table.ByGroup.TryGetValue(group, out var known))
            {
                return known;
            }
            var options = new List<PassiveOptionDef>();
            if (content.Get<PassiveChoiceDef>(group) is { } def)
            {
                options.AddRange(def.Options);
                if (def.Generate is { } generate)
                {
                    foreach (var attribute in content.Attributes.All.Where(a => a.Category == generate.Category && !a.Hidden).OrderBy(a => a.Id, StringComparer.Ordinal))
                    {
                        if (options.Exists(o => o.Id == attribute.Id))
                        {
                            continue;
                        }
                        options.Add(new PassiveOptionDef
                        {
                            Id = attribute.Id,
                            Name = attribute.DisplayName,
                            Modifiers = { new Modifier { Attribute = attribute.Id, Form = generate.Form, Value = generate.Value } },
                        });
                    }
                }
            }
            table.ByGroup[group] = options;
            return options;
        }
    }

    public static PassiveOptionDef? Option(ContentRegistry content, string group, string option)
    {
        foreach (var o in OptionsOf(content, group))
        {
            if (o.Id == option)
            {
                return o;
            }
        }
        return null;
    }

    /// <summary>How many options a node lets its owner pick: the node's own count, else its group's.</summary>
    public static int PicksAt(ContentRegistry content, PassiveNodeDef node) =>
        node.Choice is null ? 0 : Math.Max(0, node.Choice.Pick ?? content.Get<PassiveChoiceDef>(node.Choice.Group)?.Pick ?? 1);

    /// <summary>The modifier source of a pick, so a refund removes it in one call.</summary>
    public static string SourceOf(string node, string option) => "passive:" + node + ":" + option;
}
