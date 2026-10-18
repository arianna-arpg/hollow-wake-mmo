using System.Text.Json.Serialization;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

/// <summary>A compiled release policy. The first active glyph replaces the preset or form block whole.</summary>
public sealed record ReleaseCheckSpec
{
    public string Policy { get; init; } = "body";

    /// <summary>Maximum vertical separation for a stored body; null reads casting.release_height_tolerance.</summary>
    [JsonPropertyName("height_tolerance")]
    public Expr? HeightTolerance { get; init; }
}
