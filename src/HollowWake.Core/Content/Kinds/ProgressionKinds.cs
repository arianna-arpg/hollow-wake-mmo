using HollowWake.Core.Definitions;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for progression and items: the item fields of glyphs and forms (rolled axes, drop weights, level
/// floors, requirements), loot entries that name what a stone or codex holds; passive choices, realms,
/// containers, stashes, conversations and vendors as they land. Adds validators to the existing kinds and
/// registers new kinds.
/// </summary>
internal static class ProgressionKinds
{
    public static void Register()
    {
        ContentKinds.AddValidator<GlyphDef>(Axes);
        ContentKinds.AddValidator<GlyphDef>((d, c) => ItemFields(d.DropWeight, d.MinLevel, d.Requirements, c));
        ContentKinds.AddValidator<FormDef>((d, c) => ItemFields(d.DropWeight, d.MinLevel, d.Requirements, c));
        ContentKinds.AddValidator<LootTableDef>(LootPayloads);
    }

    private static void ItemFields(double dropWeight, int minLevel, Dictionary<string, double> requirements, ValidationContext c)
    {
        if (dropWeight < 0)
        {
            c.Error("drop_weight cannot be negative.");
        }
        if (minLevel < 1)
        {
            c.Error("min_level must be at least 1.");
        }
        foreach (var attr in requirements.Keys)
        {
            if (attr != "level" && !c.Registry.Attributes.Contains(attr))
            {
                c.Warn($"requirements name undeclared attribute '{attr}'.");
            }
        }
    }

    /// <summary>A loot entry that names a glyph, form or preset must name real ones, on an item whose class holds them.</summary>
    private static void LootPayloads(LootTableDef d, ValidationContext c)
    {
        foreach (var roll in d.Rolls)
        {
            foreach (var e in roll.Entries)
            {
                if (e.Glyph is null && e.Form is null && e.Weave is null)
                {
                    continue;
                }
                if (e.Glyph is not null)
                {
                    c.Ref<GlyphDef>(e.Glyph, "entries.glyph");
                }
                if (e.Form is not null)
                {
                    c.Ref<FormDef>(e.Form, "entries.form");
                }
                if (e.Weave is not null)
                {
                    c.Ref<WeaveDef>(e.Weave, "entries.weave");
                }
                if (e.Kind != "item" || e.Item is null)
                {
                    c.Error("entries naming a glyph, form or weave must be item entries that name the stone or codex base.");
                }
            }
        }
    }

    private static void Axes(GlyphDef d, ValidationContext c)
    {
        var axes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var axis in d.Axes)
        {
            if (!axes.Add(axis.Id))
            {
                c.Error($"axis '{axis.Id}' is declared twice.");
            }
            if (axis.Rows.Count == 0)
            {
                c.Error($"axis '{axis.Id}' has no rows.");
            }
            var rows = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in axis.Rows)
            {
                if (!rows.Add(row.Id))
                {
                    c.Error($"axis '{axis.Id}' declares row '{row.Id}' twice.");
                }
                if (row.Weight < 0)
                {
                    c.Error($"axis '{axis.Id}' row '{row.Id}' has a negative weight.");
                }
                c.Modifiers(row.Modifiers, $"axes.{axis.Id}.{row.Id}.modifiers");
                foreach (var m in row.RequiresMechanisms)
                {
                    if (MechanismRegistry.Validate(m, c.Registry) is { } problem)
                    {
                        c.Error($"axes.{axis.Id}.{row.Id}.requires_mechanisms {problem}.");
                    }
                }
            }
        }
    }
}
