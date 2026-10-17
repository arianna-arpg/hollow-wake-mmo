using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Item fields of a glyph: axes whose rows are rolled per copy. A glyph reference names the row it rolled
// on each axis; the compiler folds the rows' modifiers and gates, and a reference that names none takes
// the first row of each axis (the canonical roll of granted copies).
public sealed partial record GlyphDef
{
    public List<GlyphAxis> Axes { get; init; } = new();
}

/// <summary>One rolled dimension of a glyph: its rows, each with a weight, modifiers and its own mechanism demands.</summary>
public sealed record GlyphAxis
{
    public required string Id { get; init; }
    public List<GlyphRow> Rows { get; init; } = new();
}

public sealed record GlyphRow
{
    public required string Id { get; init; }
    public double Weight { get; init; } = 1;
    public List<Modifier> Modifiers { get; init; } = new();
    /// <summary>Mechanisms the host weave must have for this row, joining the glyph's own.</summary>
    [JsonPropertyName("requires_mechanisms")]
    public TagSet RequiresMechanisms { get; init; } = TagSet.Empty;
    public string Text { get; init; } = "";
}
