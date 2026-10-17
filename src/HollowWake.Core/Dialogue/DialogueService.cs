using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Interaction;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Dialogue;

/// <summary>A conversation's current line as the hero sees it: who speaks, what they say, the choices offered.</summary>
public sealed record DialogueView(Entity Source, string Conversation, string Node, string Speaker, string Line, IReadOnlyList<DialogueChoiceView> Choices, double Reach);

/// <summary>A choice as offered: whether it can be taken now, and why not.</summary>
public sealed record DialogueChoiceView(string Id, string Text, bool Enabled, string? Reason);

/// <summary>
/// Runs conversations on the server: opens the one bound to the entity a hero talks to, shows each line (running its
/// effects), checks every choice against its gates before taking it, runs its effects, moves on, and ends the
/// conversation when a choice says so, when the hero walks out of reach or closes it.
/// </summary>
public sealed class DialogueService
{
    /// <summary>The id of the choice every line offers when none of its own is open.</summary>
    public const string Leave = "leave";

    public DialogueActions Actions { get; } = DialogueActions.CreateDefault();

    /// <summary>The conversation an entity holds for a hero: the highest priority one it is for whose gate holds.</summary>
    public DialogueDef? For(Simulation sim, Entity hero, Entity speaker)
    {
        var pattern = sim.World.Get<Identity>(speaker)?.Pattern;
        var reference = sim.World.Get<Interactable>(speaker)?.Ref;
        DialogueDef? best = null;
        HeroScope? scope = null;
        foreach (var d in sim.Content.Each<DialogueDef>())
        {
            if (!(pattern is not null && d.For.Contains(pattern)) && !(reference is not null && d.For.Contains(reference)))
            {
                continue;
            }
            if (best is not null && (d.Priority < best.Priority || (d.Priority == best.Priority && string.CompareOrdinal(d.Id, best.Id) > 0)))
            {
                continue;
            }
            if (d.When is not null && !d.When.IsTrue(scope ??= new HeroScope(sim, hero)))
            {
                continue;
            }
            best = d;
        }
        return best;
    }

    /// <summary>Opens a conversation with a speaker at its first line, ending any the hero was in.</summary>
    public bool Open(Simulation sim, Entity hero, Entity speaker, DialogueDef dialogue, out string reason)
    {
        if (!dialogue.Nodes.ContainsKey(dialogue.Start))
        {
            reason = "broken_dialogue";
            return false;
        }
        var panes = sim.World.GetOrAdd<Panes>(hero);
        if (panes.Conversation is not null)
        {
            Close(sim, hero, "replaced");
        }
        panes.Conversation = new ConversationPane { Dialogue = dialogue.Id, Source = speaker, Node = dialogue.Start, Reach = ReachOf(sim, dialogue) };
        Show(sim, hero, dialogue, dialogue.Start);
        reason = "";
        return true;
    }

    public static double ReachOf(Simulation sim, DialogueDef dialogue) => dialogue.Reach ?? sim.Content.Rules.Number("dialogue.reach", 6);

    /// <summary>Moves the conversation to a line, runs the line's effects, and tells the hero what is said.</summary>
    private void Show(Simulation sim, Entity hero, DialogueDef dialogue, string nodeId)
    {
        var panes = sim.World.Get<Panes>(hero);
        if (panes?.Conversation is not { } pane || !dialogue.Nodes.TryGetValue(nodeId, out var node))
        {
            return;
        }
        pane.Node = nodeId;
        Run(sim, hero, pane.Source, dialogue, node.Effects, nodeId);
        // The line's effects may have ended the conversation or opened another.
        if (ReferenceEquals(panes.Conversation, pane) && pane.Node == nodeId)
        {
            sim.Events.Publish(new DialogueShown(hero, pane.Source, dialogue.Id, nodeId) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Takes a choice of the current line, after checking it is offered and open, and moves the conversation on.</summary>
    public bool Choose(Simulation sim, Entity hero, string conversation, string choiceId, string? node, out string reason)
    {
        if (sim.World.Get<Panes>(hero)?.Conversation is not { } pane || pane.Dialogue != conversation)
        {
            reason = "not_talking";
            return false;
        }
        if (node is not null && node != pane.Node)
        {
            reason = "stale";
            return false;
        }
        if (!InReach(sim, hero, pane.Source, pane.Reach))
        {
            Close(sim, hero, "departed");
            reason = "too_far";
            return false;
        }
        if (sim.Content.Get<DialogueDef>(pane.Dialogue) is not { } dialogue || !dialogue.Nodes.TryGetValue(pane.Node, out var line))
        {
            Close(sim, hero, "broken");
            reason = "broken_dialogue";
            return false;
        }
        var scope = new HeroScope(sim, hero);
        DialogueChoiceDef? choice = null;
        var anyOffered = false;
        foreach (var c in line.Choices)
        {
            if (c.When is not null && !c.When.IsTrue(scope))
            {
                continue;
            }
            anyOffered = true;
            if (c.Id == choiceId)
            {
                choice = c;
            }
        }
        if (choice is null)
        {
            if (choiceId == Leave && !anyOffered)
            {
                Close(sim, hero, "ended");
                reason = "";
                return true;
            }
            reason = "unknown_choice";
            return false;
        }
        if (choice.Enabled is not null && !choice.Enabled.IsTrue(scope))
        {
            reason = "not_enabled";
            return false;
        }
        sim.Events.Publish(new DialogueChosen(hero, pane.Source, dialogue.Id, pane.Node, choice.Id) { Tick = sim.Clock.Tick });
        Run(sim, hero, pane.Source, dialogue, choice.Effects, pane.Node + "." + choice.Id);
        if (!ReferenceEquals(sim.World.Get<Panes>(hero)?.Conversation, pane))
        {
            reason = "";
            return true;
        }
        if (choice.End)
        {
            Close(sim, hero, "ended");
        }
        else
        {
            Show(sim, hero, dialogue, choice.Goto is { } next && dialogue.Nodes.ContainsKey(next) ? next : pane.Node);
        }
        reason = "";
        return true;
    }

    /// <summary>Ends the hero's conversation, if any.</summary>
    public void Close(Simulation sim, Entity hero, string why)
    {
        if (sim.World.Get<Panes>(hero) is not { Conversation: { } pane } panes)
        {
            return;
        }
        panes.Conversation = null;
        sim.Events.Publish(new DialogueEnded(hero, pane.Source, pane.Dialogue, why) { Tick = sim.Clock.Tick });
    }

    /// <summary>The hero's current line with its offered choices evaluated, or null when the hero is not talking.</summary>
    public DialogueView? View(Simulation sim, Entity hero)
    {
        if (sim.World.Get<Panes>(hero)?.Conversation is not { } pane || sim.Content.Get<DialogueDef>(pane.Dialogue) is not { } dialogue
            || !dialogue.Nodes.TryGetValue(pane.Node, out var line))
        {
            return null;
        }
        var scope = new HeroScope(sim, hero);
        var speaker = line.Speaker ?? dialogue.Speaker ?? sim.World.Get<Identity>(pane.Source)?.Name ?? dialogue.DisplayName;
        var heroName = sim.World.Get<Identity>(hero)?.Name ?? "";
        var choices = new List<DialogueChoiceView>();
        foreach (var c in line.Choices)
        {
            if (c.When is not null && !c.When.IsTrue(scope))
            {
                continue;
            }
            var enabled = c.Enabled is null || c.Enabled.IsTrue(scope);
            choices.Add(new DialogueChoiceView(c.Id, Fill(c.Text, heroName, speaker), enabled, enabled ? null : (c.Reason.Length > 0 ? Fill(c.Reason, heroName, speaker) : null)));
        }
        if (choices.Count == 0)
        {
            choices.Add(new DialogueChoiceView(Leave, sim.Content.Rules.Text("dialogue.leave_text", "Leave."), true, null));
        }
        return new DialogueView(pane.Source, dialogue.Id, pane.Node, speaker, Fill(line.Line, heroName, speaker), choices, pane.Reach);
    }

    private static string Fill(string text, string hero, string speaker) =>
        text.Replace("{hero}", hero, StringComparison.Ordinal).Replace("{speaker}", speaker, StringComparison.Ordinal);

    /// <summary>
    /// Runs a list of a conversation's effects: actions by name from <see cref="Actions"/>, anything else through the
    /// effect registry with the speaker as source and the hero as target.
    /// </summary>
    private void Run(Simulation sim, Entity hero, Entity speaker, DialogueDef dialogue, List<TypedSpec> effects, string where)
    {
        if (effects.Count == 0)
        {
            return;
        }
        var ctx = new DialogueContext { Sim = sim, Hero = hero, Speaker = speaker, Dialogue = dialogue };
        foreach (var spec in effects)
        {
            try
            {
                if (Actions.TryGet(spec.Type, out var action, out _))
                {
                    action(ctx, spec.Params);
                    continue;
                }
                var registry = sim.Service<EffectRegistry>();
                if (!registry.Has(spec.Type))
                {
                    sim.Log.Warn($"dialogue {dialogue.Id} at {where}: unknown effect '{spec.Type}'");
                    continue;
                }
                var compiled = registry.Compile(spec, new EffectCompileContext { Content = sim.Content, Registry = registry, Owner = "dialogue:" + dialogue.Id });
                var at = sim.World.Get<Transform>(hero)?.Position ?? Vector3.Zero;
                compiled.Execute(new EffectContext { Sim = sim, Source = speaker, Target = hero, Point = at, Rng = sim.RngFor(hero, "dialogue:" + dialogue.Id + ":" + where) });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ExprEvalException or System.Text.Json.JsonException)
            {
                sim.Log.Error($"dialogue {dialogue.Id} at {where}: effect '{spec.Type}' failed", ex);
            }
        }
    }

    public static bool InReach(Simulation sim, Entity hero, Entity source, double reach) =>
        sim.World.IsAlive(source) && !sim.World.Has<Dead>(source) && sim.World.TryGet<Transform>(hero, out var h) && sim.World.TryGet<Transform>(source, out var s)
        && h.GroundDistanceTo(s.Position) <= reach + h.Radius + s.Radius;
}
