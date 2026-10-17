namespace HollowWake.Core.Definitions;

// Minion fields of a glyph: forwarding to the minions' own weaves and riders on summoned bodies.
public sealed partial record GlyphDef
{
    /// <summary>
    /// Whether the glyph boards the weaves of the minions a summoning weave raises, once that weave has the
    /// mechanism rule <c>minions.forward_requires</c> names (constructs board without it). Null boards unless
    /// the glyph carries a trigger, whose machinery needs its owner's hands; the glyph that grants the
    /// mechanism never boards.
    /// </summary>
    public bool? Forwards { get; init; }
}
