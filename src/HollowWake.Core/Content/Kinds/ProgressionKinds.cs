using HollowWake.Core.Definitions;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for progression and items: glyph axes rolled per copy now; passive choices, realms, containers
/// and stashes as they land. Adds validators to the existing kinds and registers new kinds.
/// </summary>
internal static class ProgressionKinds
{
    public static void Register()
    {
        ContentKinds.AddValidator<GlyphDef>(Axes);
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
