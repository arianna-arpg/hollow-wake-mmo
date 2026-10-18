# AI

Behaviors are state machines in content. A behavior names an initial state, a think interval, aggro and leash range expressions, and states with enter, tick and exit actions and prioritized transitions. Monsters, minions and constructs all think this way, in one behavior system; what differs is the content they run and the actions it names.

## Perception

Each think builds a scope over the entity's stats with: `has_target`, `target_distance`, `target_life_pct`, `nearest_enemy_distance`, `home_distance`, `state_time`, `aggro_range`, `leash_range`, `preferred_range`, `taunted`, the target mounted under `target.`, and memory values under `memory.`. A minion also perceives its owner: `has_owner`, `owner_distance`, `owner_has_target` and `owner_target_distance` (what the owner fights: its AI's target, else what it last hit or cast at within `minions.assist_memory` seconds), with the owner's stats mounted under `owner.`; and the order it stands under: `commanded` and `command_distance` (to the order's mark). Bodies without an owner or an order read those as absent. Perception is measured once, when the think starts; what actions change during the think is seen from the next think on. Transitions are expressions over that scope, evaluated highest priority first; the first that holds switches state.

## Actions

A registry: `idle`, `stop`, `acquire_target` (nearest hostile within range, preferring recent attackers, passing over bodies wearing any of its `ignore` tags), `drop_target`, `chase` (path toward the target, stopping at a range), `attack` (choose a ready weave by the forms' AI hints and cast it through the shared runtime), `wander`, `return_home`, `flee`, `face_target`, `use_weave`, `remember`, `heal_self`. Minions add `follow_owner`, `assist_owner`, `return_to_owner` (with a leash recall that teleports a minion that strayed too far or stopped making headway) and `obey_command`; constructs add `construct_cast`, `spring` and `echo_owner`. Their parameters and rules are in `docs/design/minions.md`. New actions register from code; validation refuses a behavior naming an action the registry lacks.

## Orders

An order is a `MinionOrder` on the behavior: the command it came from, the kind that drives it, a mark, a pinned quarry, the issuer and an expiry. `obey_command` steps the order's kind each think until the kind reports it fulfilled or the order expires; an expired order is cleared when perception is built. Kinds are code registered in `MinionCommands` (`assault`, `hold`, `recall`); commands are content in rule `minions.commands`, given by the `command_minions` effect or by the minion command intent. A kind fights through the same chase and attack the actions use, and moves under the same planning policy.

## Moving

The movement actions (`chase`, `wander`, `return_home`, `flee`, and the minion actions that walk) share one planning policy, built to remove work rather than repeat it:

- A plan is kept while it lasts. The AI plans again only when its path or move target has run out, when the destination has moved more than `move.ai_repath_distance` from the goal it planned for, or when the plan is older than `move.ai_repath_seconds`.
- A new plan walks straight when it can: the segment is sampled about a step per tick apart with the movement system's own occupancy check, no step drops further than `move.max_drop`, and no cell crossed costs more than plain ground. Otherwise the pathfinder searches with the AI budget, `move.ai_path_max_nodes`.
- A search that cannot bring the AI closer (no step at all, or a partial path ending no nearer the goal) is not repeated for that goal until the plan is renewed; meanwhile the AI follows what it found, then walks straight at the goal. An unreachable target costs one search per renewal, not one per think.

The plan is a cache on `Behavior` (`PlanGoal`, `PlannedAt`, `Planned`, `PlanFailed`); nothing outside the behavior system reads it, except that a teleport drops it.

## Weave choice

`WeaveRuntime.ChooseForAi` filters the entity's weaves by readiness, cost, range and the hint's `when` expression, skipping triggered weaves, toggles that are on, empty banks and failed gates, then weights by the hint. `PreferredRange` is the longest hinted range, used by `chase` and perception.

Taunts set the target and a memory timestamp the behaviors read as `taunted`.

## Temporary close-combat content

`close_melee` is a reusable behavior for kits that must close despite a longer-ranged secondary skill. It explicitly supplies the chase stop distance and keeps ordinary acquire/attack/return actions. Goblinkin leaders opt into it; downstream faction kits can reuse the same definition. Other behaviors retain their own policy and PreferredRange remains the longest hinted range. The stop distance currently follows the existing center-distance movement convention; future contact-aware AI must revisit that contract together with collision clearance. This is replaceable content scaffolding, not the planned tactical AI engine.
