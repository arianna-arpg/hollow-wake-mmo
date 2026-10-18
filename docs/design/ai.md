# AI

Behaviors are state machines in content. A behavior names an initial state, a think interval, aggro and leash range expressions, and states with enter, tick and exit actions and prioritized transitions. Monsters, minions and constructs all think this way, in one behavior system; what differs is the content they run and the actions it names. Every action acts on the simulation through `AiIntents.Send`, with the same intents and the same handlers a client's intent runs through; the few world rules a body is under rather than does, and the device actions' own casting, are named below (see Acting through intents).

## Perception

Each think builds a scope over the entity's stats with: `has_target`, `target_distance`, `target_life_pct`, `nearest_enemy_distance`, `home_distance`, `state_time`, `aggro_range`, `leash_range`, `preferred_range`, `taunted`, the target mounted under `target.`, and memory values under `memory.`. A minion also perceives its owner: `has_owner`, `owner_distance`, `owner_has_target` and `owner_target_distance` (what the owner fights: its AI's target, else what it last hit or cast at within `minions.assist_memory` seconds), with the owner's stats mounted under `owner.`; and the order it stands under: `commanded` and `command_distance` (to the order's mark). Bodies without an owner or an order read those as absent. Perception is measured once, when the think starts; what actions change during the think is seen from the next think on. Transitions are expressions over that scope, evaluated highest priority first; the first that holds switches state.

## Actions

A registry: `idle`, `stop`, `acquire_target` (nearest hostile within range, preferring recent attackers, passing over bodies wearing any of its `ignore` tags), `drop_target`, `chase` (path toward the target, stopping at a range), `attack` (choose a ready weave by the forms' AI hints and cast it as a player presses its key), `wander`, `return_home`, `flee`, `face_target`, `use_weave`, `remember`. Minions add `follow_owner`, `assist_owner`, `return_to_owner` (with a leash recall that teleports a minion that strayed too far or stopped making headway) and `obey_command`; constructs add `construct_cast`, `spring` and `echo_owner`. Their parameters and rules are in `docs/design/minions.md`. New actions register from code and act through intents (below); validation refuses a behavior naming an action the registry lacks.

## Acting through intents

An action reads perception and writes the AI's own mind (its target, memory and order), and changes the simulation by sending the intents a player sends, validated by the same handlers, so every rule a handler checks applies to an AI body as to a player. Two fields of those intents are sent by the AI but by no client yet: `MoveToIntent.Stop` (how near a walk counts as arrived) and `KeepPlan` (a destination sent again every think, whose standing plan may be kept); neither reaches beyond the sender's own walk. `AiIntents.Send(context, intent)` dispatches an intent at once, during the think, through the simulation's `IntentHandlers` with a context built for the actor; it is never queued, since a queued intent would act a tick late, after the input phase. The outcome is accepted, or refused with the handler's reason, and each refusal is recorded on the think's context (`AiContext.Refusals`, valid during the think) for the decision record. No `IntentRejected` is published for an AI's refusal, so no client hears of it; what the runtime publishes itself, such as `WeaveFailed` for a cast it refuses, stays on the simulation's bus as before. A refused intent is never replaced by a direct write. Walking leaves as `MoveToIntent` (with the action's stop distance and `KeepPlan`), stopping as `StopIntent`, casting as `UseWeaveIntent`. A walk whose plan already leads to the same point and still stands is not sent again.

The world rules a body is under rather than does, which no intent asks for, are applied directly and named where they happen:

- **Facing.** `chase` within reach and `face_target` turn the body toward its target at once. Facing is a rule of the simulation, not something a client asks for. A turn rate, and the movement locks of a held cast holding the body's facing, are deferred: either would move duels with roots and guards in them.
- **The leash.** A minion's leash recall teleports it (`MinionService.Teleport`).
- **A spent trap.** A trap that sprang goes (`MinionService.Dismiss`).

The device actions (`construct_cast`, `spring`, `echo_owner`) still fire their weaves through `ConstructCasting.Fire`, which runs a weave's effects directly, past the runtime's costs, cast times and checks: the one direct effect run left in the AI.

## Orders

An order is a `MinionOrder` on the behavior: the command it came from, the kind that drives it, a mark, a pinned quarry, the issuer and an expiry. `obey_command` steps the order's kind each think until the kind reports it fulfilled or the order expires; an expired order is cleared when perception is built. Kinds are code registered in `MinionCommands` (`assault`, `hold`, `recall`); commands are content in rule `minions.commands`, given by the `command_minions` effect or by the minion command intent. A kind fights through the same chase and attack the actions use, and moves under the same planning policy.

## Moving

The movement actions (`chase`, `wander`, `return_home`, `flee`, and the minion actions that walk) send `MoveToIntent`, and its handler plans every mover's walk the same way, a player's click included (`Movement/MovePlanner.cs`), built to remove work rather than repeat it:

- The work is bounded by a budget the server picks by who moves: a player's is the pathfinder's own `move.path_max_nodes`, any other mover's the smaller `move.ai_path_max_nodes`, since the AI's movers are many and renew their plans as they go.
- A new plan walks straight when it can: the segment takes no more samples than the budget, sampled about a step per tick apart with the movement system's own occupancy check, no step drops further than `move.max_drop`, and no cell crossed costs more than plain ground. Otherwise the pathfinder searches on the budget, so a point at any distance costs bounded work and loads bounded ground. A click also stops any steering by direction; a destination sent again leaves it, since the movement system keeps steering by its last direction on the tick a waypoint is reached.
- A search with no step leaves the mover walking straight at the goal. A partial path is followed to its end; a click stops there, while a subsequent `KeepPlan` destination can resume walking straight after a partial path that made no progress, without repeating the failed search until renewal.
- The AI sends its destination again every think, marked `KeepPlan`, so a plan is kept while it lasts: the handler plans again only when the path or move target has run out, when the destination has moved more than `move.ai_repath_distance` from the goal it planned for, or when the plan is older than `move.ai_repath_seconds`, and a search that failed is not repeated for that goal until the plan is renewed, so an unreachable target costs one search per renewal, not one per think. A click is planned afresh every time. The flag only lets a sender keep its own plan; it never chooses the budget, so it could go on the wire without letting a client pick the AI's.

The plan is a cache on `Motion` (`PlanGoal`, `PlannedAt`, `Planned`, `PlanFailed`), kept by the move-to handler; a teleport or an order drops it.

The straight-walk test's sample spacing is 0.25 units, so its maximum reach is that spacing times the mover's budget (125 units with the shipped AI budget). An order beyond that reach uses bounded searches even across clear ground. This also bounds the work of a distant minion order, which previously tested the whole straight segment.

Seeded replay is exact within the tested platform/runtime. The simulation does not promise identical floating-point state across platforms where native math functions contribute values. `AiTrajectoryTests` compares full-precision positions and path counts from non-wandering chases; facing angles are excluded because platform `Atan2` results can differ in their last bits. `AiIntentTests` retains the original mixed scenarios, including two idle-wander rows, under a 0.0001 coordinate and facing tolerance with exact path counts. Its `wall_detour` also overlaps the exact detour fixture. Neither suite claims portable combat or world-generation state.

## Weave choice

`WeaveRuntime.ChooseForAi` filters the entity's weaves by readiness, cost, range and the hint's `when` expression, skipping triggered weaves, toggles that are on, empty banks and failed gates, then weights by the hint. A zero-weight `min_range` rejection happens before geometry resolution or LOS work. Ordinary timed and instant positional attacks with a supported primary region also test the current target against that same region before committing; unsupported graphs retain range-based choice. This prevents stationary edge stalls; the optional anticipation policy below adds pre-commit movement estimation without retargeting a committed swing. `PreferredRange` is the longest hinted range, used by `chase` and perception.

Taunts set the target and a memory timestamp the behaviors read as `taunted`.

### Commitment-aware approach

The weave attribute `ai.anticipation` controls pre-commit movement estimation, from zero (current position only) to one (the actual windup, including speed modifiers). Shipped single-target melee selectors author one; other compositions can opt in through the same attribute and modifier fold. For a safe primary-area provider with anticipation enabled, the chooser first requires the authored preferred range (or folded range attribute). This also covers a moving target that briefly stops to cast. It then tests the observed target velocity projected over that interval against the shared region, translating the actual damage anatomy for body-contact plans while leaving the live body untouched. Unknown providers keep their prior range-based fallback. This is a heuristic, not future knowledge: changes in direction, terrain or speed can defeat the estimate.

`PreferredRange` accepts the current target and reduces the approach stop distance by its outward travel during that interval. It never increases collision reach or widens a sector, and a started swing keeps its original direction. Body collision still limits how close an actor can get. A target too fast to catch may continue avoiding melee; the policy does not promise a hit. Players can make the same positioning and timing choices manually.

The chooser rejects ordinary busy casts before expensive geometry work, preserving the runtime's instant-reflex exception. Converted and combo faces retain their existing path. The AI intent seam owns dispatch and move planning; this policy only supplies choice eligibility and preferred approach distance.
## Provisional wildlife assignments

Wildlife content may use existing combat behaviors or a flee-only content state machine. A spawn pack is not coordinated AI, a predator tag is not predation, and a hostile faction does not imply every body attacks. Critters have empty weave lists and no drops; they flee perceived faction enemies without an attack state. Future sensory, tactical, social and need-based decisions require a separate reviewed contract and deterministic simulation evidence. Authoring intent under `meta` is never a simulation input. The [wildlife AI handoff](../reviews/2026-10-18-fauna-ai-handoff.md) records the proposed design questions and scenarios for Claude review.

## Temporary close-combat content

`close_melee` is a reusable behavior for kits that must close despite a longer-ranged secondary skill. It explicitly supplies the chase stop distance and keeps ordinary acquire/attack/return actions. Goblinkin leaders opt into it; downstream faction kits can reuse the same definition. Other behaviors retain their own policy and PreferredRange remains the longest hinted range. The stop distance currently follows the existing center-distance movement convention; future contact-aware AI must revisit that contract together with collision clearance. This is replaceable content scaffolding, not the planned tactical AI engine.

## The AI as a player

What any AI in this engine must and must not be, whatever runs it, is stated in [combat-grammar.md](combat-grammar.md): its inputs are what a client could show and what the compiled weave states, its outputs are the intents a client sends, its decisions are registered affordances, perception names and considerations bound by doctrines authored as content, and every decision publishes an event that explains it. The behavior machine above stays the outer mode machine; each piece of that section replaces what it supersedes here as it lands.
