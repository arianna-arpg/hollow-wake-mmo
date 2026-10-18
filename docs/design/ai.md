# AI

Behaviors are state machines in content. A behavior names an initial state, a think interval, aggro and leash range expressions, and states with enter, tick and exit actions and prioritized transitions. Monsters, minions and constructs all think this way, in one behavior system; what differs is the content they run and the actions it names. Every action acts on the simulation through `AiIntents.Send`, with the same intents and the same handlers a client's intent runs through; the few world rules a body is under rather than does are named below (see Acting through intents).

## Perception

Each think builds a scope over the entity's stats with: `has_target`, `target_distance`, `target_life_pct`, `nearest_enemy_distance`, `home_distance`, `state_time`, `aggro_range`, `leash_range`, `preferred_range`, `taunted`, the target mounted under `target.`, and memory values under `memory.`. A minion also perceives its owner: `has_owner`, `owner_distance`, `owner_has_target` and `owner_target_distance` (what the owner fights: its AI's target, else what it last hit or cast at within `minions.assist_memory` seconds), with the owner's stats mounted under `owner.`; and the order it stands under: `commanded` and `command_distance` (to the order's mark). Bodies without an owner or an order read those as absent. Perception is measured once, when the think starts; what actions change during the think is seen from the next think on. Transitions are expressions over that scope, evaluated highest priority first; the first that holds switches state.

## Actions

A registry: `idle`, `stop`, `acquire_target` (nearest hostile within range, preferring recent attackers, passing over bodies wearing any of its `ignore` tags), `drop_target`, `chase` (path toward the target, stopping at a range), `attack` (choose a ready weave by the forms' AI hints and cast it as a player presses its key), `wander`, `return_home`, `flee`, `face_target`, `use_weave`, `remember`. Minions add `follow_owner`, `assist_owner`, `return_to_owner` (with a leash recall that teleports a minion that strayed too far or stopped making headway) and `obey_command`; constructs add `construct_cast`, `spring` and `echo_owner`. Their parameters and rules are in `docs/design/minions.md`. New actions register from code and act through intents (below); validation refuses a behavior naming an action the registry lacks.

## Acting through intents

An action reads perception and writes the AI's own mind (its target, memory and order), and changes the simulation by sending the intents a player sends, validated by the same handlers, so every rule a handler checks applies to an AI body as to a player. Two fields of those intents are sent by the AI but by no client yet: `MoveToIntent.Stop` (how near a walk counts as arrived) and `KeepPlan` (a destination sent again every think, whose standing plan may be kept); neither reaches beyond the sender's own walk. `AiIntents.Send(context, intent)` dispatches an intent at once, during the think, through the simulation's `IntentHandlers` with a context built for the actor; it is never queued, since a queued intent would act a tick late, after the input phase. The outcome is accepted, or refused with the handler's reason, and each refusal is recorded on the think's context (`AiContext.Refusals`, valid during the think) for the decision record. No `IntentRejected` is published for an AI's refusal, so no client hears of it; what the runtime publishes itself, such as `WeaveFailed` for a cast it refuses, stays on the simulation's bus as before. A refused intent is never replaced by a direct write. Walking leaves as `MoveToIntent` (with the action's stop distance and `KeepPlan`), stopping as `StopIntent`, casting as `UseWeaveIntent`. A walk whose plan already leads to the same point and still stands is not sent again.

The world rules a body is under rather than does, which no intent asks for, are applied directly and named where they happen:

- **Facing.** `chase` within reach and `face_target` turn the body toward its target at once. Facing is a rule of the simulation, not something a client asks for. A turn rate, and the movement locks of a held cast holding the body's facing, are deferred: either would move duels with roots and guards in them.
- **The leash.** A minion's leash recall teleports it (`MinionService.Teleport`).
- **A spent trap.** A trap that sprang goes (`MinionService.Dismiss`), once its cast is over.
- **An echo's replay.** An echo resolves each use its owner paid for, from where it stands (`WeaveRuntime.Replay`): not a new use, so not an intent.

The device actions `construct_cast` and `spring` cast like anyone, by `UseWeaveIntent` (see [minions.md](minions.md)).

## Orders

An order is a `MinionOrder` on the behavior: the command it came from, the kind that drives it, a mark, a pinned quarry, the issuer and an expiry. `obey_command` steps the order's kind each think until the kind reports it fulfilled or the order expires; an expired order is cleared when perception is built. Kinds are code registered in `MinionCommands` (`assault`, `hold`, `recall`); commands are content in rule `minions.commands`, given by the `command_minions` effect or by the minion command intent. A kind fights through the same chase and attack the actions use, and moves under the same planning policy.

## Moving

The movement actions (`chase`, `wander`, `return_home`, `flee`, and the minion actions that walk) send `MoveToIntent`, and its handler plans every mover's walk the same way, a player's click included (`Movement/MovePlanner.cs`), built to remove work rather than repeat it:

- The work is bounded by a budget the server picks by who moves: a player's is the pathfinder's own `move.path_max_nodes`, any other mover's the smaller `move.ai_path_max_nodes`, since the AI's movers are many and renew their plans as they go.
- A new plan walks straight when it can: the segment takes no more samples than the budget, sampled about a step per tick apart with the movement system's own occupancy check, no step drops further than `move.max_drop`, and no cell crossed costs more than plain ground. Otherwise the pathfinder searches on the budget, so a point at any distance costs bounded work and loads bounded ground. A click also stops any steering by direction; a destination sent again leaves it, since the movement system keeps steering by its last direction on the tick a waypoint is reached.
- A search that cannot bring the mover closer (no step at all, or a partial path ending no nearer the goal) leaves it following what it found, then walking straight at the goal.
- The AI sends its destination again every think, marked `KeepPlan`, so a plan is kept while it lasts: the handler plans again only when the path or move target has run out, when the destination has moved more than `move.ai_repath_distance` from the goal it planned for, or when the plan is older than `move.ai_repath_seconds`, and a search that failed is not repeated for that goal until the plan is renewed, so an unreachable target costs one search per renewal, not one per think. A click is planned afresh every time. The flag only lets a sender keep its own plan; it never chooses the budget, so it could go on the wire without letting a client pick the AI's.

The plan is a cache on `Motion` (`PlanGoal`, `PlannedAt`, `Planned`, `PlanFailed`), kept by the move-to handler; a teleport or an order drops it.

## Weave choice

`WeaveRuntime.ChooseForAi` filters the entity's weaves by readiness, cost, range and the hint's `when` expression, skipping triggered weaves, toggles that are on, empty banks and failed gates, then weights by the hint. `PreferredRange` is the longest hinted range, used by `chase` and perception.

Taunts set the target and a memory timestamp the behaviors read as `taunted`.
