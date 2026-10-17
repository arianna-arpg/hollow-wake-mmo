using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>
/// A service a station performs for a price (row I12): raise a stone or a codex a level, merge two copies of a codex
/// into a rarer one, reset the passives, chisel a socket into gear. The <see cref="Type"/> names a registered service;
/// content says where it is offered, what it takes and what it costs.
/// </summary>
public sealed record StationServiceDef : Definition
{
    /// <summary>The registered service that does the work (level_item, merge_codices, reset_passives, add_socket).</summary>
    public string Type { get; init; } = "";

    /// <summary>Interactable kinds or station tags that offer it (font, forge).</summary>
    public List<string> At { get; init; } = new();

    /// <summary>Item tags of what it applies to (glyph, codex, weapon); empty applies to anything the service takes.</summary>
    public TagSet Accepts { get; init; } = TagSet.Empty;

    /// <summary>The price per currency: expressions of the hero and the target (item_level, rarity_rank, sockets, refunds).</summary>
    public Dictionary<string, Expr> Price { get; init; } = new(StringComparer.Ordinal);

    public int Order { get; init; }
}
