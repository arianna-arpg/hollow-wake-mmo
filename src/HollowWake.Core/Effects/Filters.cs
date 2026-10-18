using HollowWake.Core.Combat;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Effects;

/// <summary>
/// The words a <c>filter</c> parameter may hold, an open registry: each names which bodies a delivery or a rider
/// reaches, judged from the effect's origin and source. Validation refuses a word nobody registered, and at run
/// time such a word reaches nobody, so a misspelling can never strike everyone.
/// </summary>
public static class Filters
{
    private static readonly Dictionary<string, Func<EffectContext, Entity, bool>> Words = new(StringComparer.Ordinal);

    static Filters()
    {
        Register("enemy", static (ctx, e) => Relations.IsHostile(ctx.Sim, ctx.Origin, e));
        Register("ally", static (ctx, e) => !Relations.IsHostile(ctx.Sim, ctx.Origin, e) && e != ctx.Origin);
        Register("ally_or_self", static (ctx, e) => !Relations.IsHostile(ctx.Sim, ctx.Origin, e));
        Register("self", static (ctx, e) => e == ctx.Source);
        Register("not_self", static (ctx, e) => e != ctx.Source);
        Register("all", static (_, _) => true);
    }

    /// <summary>Registers a word; a later registration of the same word replaces the earlier one.</summary>
    public static void Register(string word, Func<EffectContext, Entity, bool> passes) => Words[word] = passes;

    public static bool Has(string word) => Words.ContainsKey(word);

    public static IEnumerable<string> Names => Words.Keys;

    /// <summary>Whether a living, targetable body passes the word; an unregistered word passes nobody.</summary>
    public static bool Passes(EffectContext ctx, Entity e, string word) => Words.TryGetValue(word, out var passes) && passes(ctx, e);
}
