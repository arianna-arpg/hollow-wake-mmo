using System.Runtime.CompilerServices;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Crafting;

/// <summary>One input of a recipe as the hero sees it: what it is, how many the hero has, how many it needs.</summary>
public readonly record struct RecipeInputView(string Text, int Have, int Need);

/// <summary>A recipe at a station as the hero sees it: its inputs, a card of what it makes, and whether it can be made now and why not.</summary>
public sealed record RecipeView(RecipeDef Recipe, IReadOnlyList<RecipeInputView> Inputs, ItemInstance Output, bool Craftable, string? Reason);

/// <summary>
/// A crafting station's pane: opening it remembers the station for the hero, and the recipes it offers are read from
/// content with what the hero has, so the client draws inputs, outputs and readiness without folding a number.
/// </summary>
public sealed class StationService
{
    // A generator of its own mints the output cards, so previews never take uids from real items.
    private static readonly ConditionalWeakTable<ContentRegistry, ItemGenerator> Previews = new();

    public void Open(Simulation sim, Entity hero, Entity station, TagSet tags)
    {
        var panes = sim.World.GetOrAdd<Panes>(hero);
        var reach = sim.World.Get<Interactable>(station)?.Range ?? 2.5f;
        panes.Station = new StationPane { Source = station, Tags = tags, Reach = reach + sim.Content.Rules.Number("dialogue.reach_slack", 1.5) };
        sim.Events.Publish(new StationOpened(hero, station, tags) { Tick = sim.Clock.Tick });
    }

    public void Close(Simulation sim, Entity hero, string why)
    {
        if (sim.World.Get<Panes>(hero) is not { Station: { } pane } panes)
        {
            return;
        }
        panes.Station = null;
        sim.Events.Publish(new StationClosed(hero, pane.Source, why) { Tick = sim.Clock.Tick });
    }

    /// <summary>The recipes a station offers (its tags meet the recipe's, or the recipe needs no station), in id order.</summary>
    public List<RecipeView> Recipes(Simulation sim, Entity hero, Entity station, TagSet tags)
    {
        var views = new List<RecipeView>();
        var inv = sim.World.Get<Inventory>(hero);
        var resolver = sim.Service<ItemResolver>();
        var crafting = sim.Service<CraftingService>();
        var preview = Previews.GetValue(sim.Content, static c => new ItemGenerator(c));
        foreach (var recipe in sim.Content.Each<RecipeDef>().OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (!recipe.Station.IsEmpty && !tags.HasAny(recipe.Station))
            {
                continue;
            }
            var inputs = new List<RecipeInputView>(recipe.Inputs.Count);
            foreach (var input in recipe.Inputs)
            {
                var have = inv?.CountOf(i => CraftingService.MatchesInput(resolver, i, input)) ?? 0;
                var text = input.Item is not null ? sim.Content.Get<ItemBaseDef>(input.Item)?.DisplayName ?? Definition.Humanize(input.Item) : string.Join(", ", input.Tags.Select(Definition.Humanize));
                inputs.Add(new RecipeInputView(text, have, input.Count));
            }
            var output = preview.Roll(recipe.Output.Item, recipe.Output.Level ?? recipe.Level, recipe.Output.Rarity ?? "common", new Rng(Rng.Hash64("recipe:" + recipe.Id)));
            output.Count = Math.Clamp(recipe.Output.Count, 1, Math.Max(1, output.Stack));
            var craftable = crafting.CanCraft(sim, hero, recipe, station, out var reason);
            views.Add(new RecipeView(recipe, inputs, output, craftable, craftable ? null : ReasonText(sim, recipe, reason)));
        }
        return views;
    }

    /// <summary>Why a recipe cannot be made, as a player reads it.</summary>
    private static string ReasonText(Simulation sim, RecipeDef recipe, string code)
    {
        var colon = code.IndexOf(':');
        var head = colon < 0 ? code : code[..colon];
        var rest = colon < 0 ? "" : code[(colon + 1)..];
        return head switch
        {
            "level" => $"Needs level {recipe.Level}.",
            "missing" => $"Needs {sim.Content.Get<ItemBaseDef>(rest)?.DisplayName ?? Definition.Humanize(rest)}.",
            "currency" => $"Not enough {sim.Content.Get<CurrencyDef>(rest)?.DisplayName ?? Definition.Humanize(rest)}.",
            "too_far" => "Too far from the station.",
            "wrong_station" => "Not made here.",
            _ => Definition.Humanize(head) + ".",
        };
    }
}
