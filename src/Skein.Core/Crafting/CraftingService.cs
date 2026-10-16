using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Expressions;
using Skein.Core.Items;
using Skein.Core.Loot;
using Skein.Core.Progression;
using Skein.Core.Sim;

namespace Skein.Core.Crafting;

/// <summary>Raised when a challenge is issued so the server can send it to the client.</summary>
public sealed record CraftChallengeIssued(Entity Crafter, string Recipe, ProcedureChallenge Challenge) : GameEvent;

/// <summary>Raised when a submission is scored, success or not.</summary>
public sealed record CraftResolved(Entity Crafter, string Recipe, double Score, double Quality, bool Success, string Reason) : GameEvent;

/// <summary>
/// Reweaving. Start issues a seeded challenge from the recipe's procedure; Submit scores the input,
/// consumes inputs, mints the output with a quality that biases rarity and local values.
/// </summary>
public sealed class CraftingService
{
    private readonly ProcedureRegistry _procedures;
    private readonly Dictionary<Entity, (string Recipe, ProcedureChallenge Challenge, Entity Station)> _pending = new();

    public CraftingService(ProcedureRegistry procedures)
    {
        _procedures = procedures;
    }

    public bool HasPending(Entity crafter) => _pending.ContainsKey(crafter);

    public bool CanCraft(Simulation sim, Entity crafter, RecipeDef recipe, Entity station, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Stats>(crafter, out var stats) || !sim.World.TryGet<Inventory>(crafter, out var inv))
        {
            reason = "no_inventory";
            return false;
        }
        if (stats.Level < recipe.Level)
        {
            reason = "level";
            return false;
        }
        if (!recipe.Station.IsEmpty)
        {
            if (!sim.World.TryGet<Interactable>(station, out var st) || !st.Tags.HasAny(recipe.Station))
            {
                reason = "wrong_station";
                return false;
            }
            if (sim.World.TryGet<Transform>(station, out var stt) && sim.World.TryGet<Transform>(crafter, out var ct) && ct.GroundDistanceTo(stt.Position) > st.Range + 1)
            {
                reason = "too_far";
                return false;
            }
        }
        if (!sim.TryService<ItemGenerator>(out var gen))
        {
            reason = "no_generator";
            return false;
        }
        foreach (var input in recipe.Inputs)
        {
            var have = inv.CountOf(i => MatchesInput(gen.Resolver, i, input));
            if (have < input.Count)
            {
                reason = "missing:" + (input.Item ?? input.Tags.ToString());
                return false;
            }
        }
        foreach (var (currency, amount) in recipe.Currency)
        {
            if (inv.CurrencyOf(currency) < amount)
            {
                reason = "currency:" + currency;
                return false;
            }
        }
        return true;
    }

    private static bool MatchesInput(ItemResolver resolver, ItemInstance item, ItemRequirement req)
    {
        if (req.Item is not null)
        {
            return item.Base == req.Item;
        }
        return !req.Tags.IsEmpty && resolver.TagsOf(item).HasAll(req.Tags);
    }

    public ProcedureChallenge? Start(Simulation sim, Entity crafter, string recipeId, Entity station, out string reason)
    {
        reason = "";
        if (!sim.Content.TryGet<RecipeDef>(recipeId, out var recipe) || recipe is null)
        {
            reason = "unknown_recipe";
            return null;
        }
        if (!CanCraft(sim, crafter, recipe, station, out reason))
        {
            return null;
        }
        var spec = ResolveProcedure(sim, recipe);
        if (!_procedures.TryGet(spec.Type, out var procedure) || procedure is null)
        {
            reason = "unknown_procedure";
            return null;
        }
        var stats = sim.World.Get<Stats>(crafter)!;
        var challenge = procedure.Create(spec, sim.RngFor(crafter, "craft:" + recipeId + ":" + sim.Clock.Tick), stats, sim.Clock.Time);
        _pending[crafter] = (recipeId, challenge, station);
        sim.Events.Publish(new CraftChallengeIssued(crafter, recipeId, challenge) { Tick = sim.Clock.Tick });
        return challenge;
    }

    private static TypedSpec ResolveProcedure(Simulation sim, RecipeDef recipe)
    {
        var spec = recipe.Procedure ?? new TypedSpec { Type = "none" };
        if (spec.Type == "ref" && spec.Params["id"]?.GetValue<string>() is { } id && sim.Content.TryGet<ProcedureDef>(id, out var def) && def is not null)
        {
            return def.Spec;
        }
        return spec;
    }

    public ItemInstance? Submit(Simulation sim, Entity crafter, string recipeId, ProcedureInput input, out string reason)
    {
        reason = "";
        if (!_pending.TryGetValue(crafter, out var pending) || pending.Recipe != recipeId)
        {
            reason = "no_challenge";
            return null;
        }
        _pending.Remove(crafter);
        var recipe = sim.Content.Require<RecipeDef>(recipeId);
        if (!CanCraft(sim, crafter, recipe, pending.Station, out reason))
        {
            sim.Events.Publish(new CraftResolved(crafter, recipeId, 0, 0, false, reason) { Tick = sim.Clock.Tick });
            return null;
        }
        var elapsed = sim.Clock.Time - pending.Challenge.IssuedAt;
        if (elapsed > pending.Challenge.TimeLimit + sim.Content.Rules.Number("crafting.grace_seconds", 5))
        {
            reason = "timeout";
            sim.Events.Publish(new CraftResolved(crafter, recipeId, 0, 0, false, reason) { Tick = sim.Clock.Tick });
            return null;
        }
        if (!_procedures.TryGet(pending.Challenge.Type, out var procedure) || procedure is null)
        {
            reason = "unknown_procedure";
            return null;
        }
        var score = Math.Clamp(procedure.Score(pending.Challenge, input), 0, 1);
        var stats = sim.World.Get<Stats>(crafter)!;
        var scope = new ScopeOverlay(stats).Set("score", score).Set("level", stats.Level);
        var quality = Math.Clamp(recipe.Quality is null ? score : recipe.Quality.Evaluate(scope), 0, 1);
        quality = Math.Clamp(quality + stats.Get("crafting.quality"), 0, 1);

        var inv = sim.World.Get<Inventory>(crafter)!;
        var gen = sim.Service<ItemGenerator>();
        foreach (var req in recipe.Inputs.Where(r => r.Consume))
        {
            var remaining = req.Count;
            foreach (var item in inv.Items.ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }
                if (!MatchesInput(gen.Resolver, item, req))
                {
                    continue;
                }
                var take = Math.Min(remaining, item.Count);
                item.Count -= take;
                remaining -= take;
                if (item.Count <= 0)
                {
                    inv.Remove(item);
                }
            }
        }
        foreach (var (currency, amount) in recipe.Currency)
        {
            inv.AddCurrency(currency, -amount);
        }

        var rng = sim.RngFor(crafter, "craft-output:" + recipeId + ":" + sim.Clock.Tick);
        var rarity = recipe.Output.Rarity ?? RarityForQuality(sim, quality);
        var level = recipe.Output.Level ?? Math.Max(recipe.Level, stats.Level);
        var output = recipe.Output.Unique is not null ? gen.RollUnique(recipe.Output.Unique, level, rng) : gen.Roll(recipe.Output.Item, level, rarity, rng);
        output.Quality = quality;
        output.Crafter = sim.World.Get<Identity>(crafter)?.Name;
        output.Count = Math.Clamp(recipe.Output.Count, 1, Math.Max(1, output.Stack));
        sim.Service<LootService>().Give(sim, crafter, output);
        if (recipe.Xp is not null && sim.TryService<ProgressionService>(out var progression))
        {
            progression.GrantXp(sim, crafter, recipe.Xp.Evaluate(scope));
        }
        sim.Events.Publish(new ItemCrafted(crafter, recipeId, output.Uid, quality) { Tick = sim.Clock.Tick });
        sim.Events.Publish(new CraftResolved(crafter, recipeId, score, quality, true, "") { Tick = sim.Clock.Tick });
        return output;
    }

    /// <summary>Quality thresholds to rarity from crafting rules, best first.</summary>
    private static string RarityForQuality(Simulation sim, double quality)
    {
        var thresholds = sim.Content.Rules.Node("crafting.rarity_by_quality") as System.Text.Json.Nodes.JsonObject;
        if (thresholds is null)
        {
            return quality >= 0.9 ? "rare" : quality >= 0.6 ? "magic" : "common";
        }
        string best = "common";
        double bestT = -1;
        foreach (var (rarity, node) in thresholds)
        {
            var t = Content.JsonNum.Of(node, 0);
            if (quality >= t && t > bestT)
            {
                best = rarity;
                bestT = t;
            }
        }
        return best;
    }
}
