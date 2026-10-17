# AI

Behaviors are state machines in content. A behavior names an initial state, a think interval, aggro and leash range expressions, and states with enter, tick and exit actions and prioritized transitions.

## Perception

Each think builds a scope over the entity's stats with: `has_target`, `target_distance`, `target_life_pct`, `nearest_enemy_distance`, `home_distance`, `state_time`, `aggro_range`, `leash_range`, `preferred_range`, `taunted`, the target mounted under `target.`, and memory values under `memory.`. Perception is measured once, when the think starts; what actions change during the think is seen from the next think on. Transitions are expressions over that scope, evaluated highest priority first; the first that holds switches state.

## Actions

A registry: `idle`, `stop`, `acquire_target` (nearest hostile within range, preferring recent attackers), `drop_target`, `chase` (path toward the target, stopping at a range), `attack` (choose a ready weave by the forms' AI hints and cast it through the shared runtime), `wander`, `return_home`, `flee`, `face_target`, `use_weave`, `remember`, `heal_self`. New actions register from code.

## Moving

The movement actions (`chase`, `wander`, `return_home`, `flee`) share one planning policy, built to remove work rather than repeat it:

- A plan is kept while it lasts. The AI plans again only when its path or move target has run out, when the destination has moved more than `move.ai_repath_distance` from the goal it planned for, or when the plan is older than `move.ai_repath_seconds`.
- A new plan walks straight when it can: the segment is sampled about a step per tick apart with the movement system's own occupancy check, no step drops further than `move.max_drop`, and no cell crossed costs more than plain ground. Otherwise the pathfinder searches with the AI budget, `move.ai_path_max_nodes`.
- A search that cannot bring the AI closer (no step at all, or a partial path ending no nearer the goal) is not repeated for that goal until the plan is renewed; meanwhile the AI follows what it found, then walks straight at the goal. An unreachable target costs one search per renewal, not one per think.

The plan is a cache on `Behavior` (`PlanGoal`, `PlannedAt`, `Planned`, `PlanFailed`); nothing outside the behavior system reads it.

## Weave choice

`WeaveRuntime.ChooseForAi` filters the entity's weaves by readiness, cost, range and the hint's `when` expression, then weights by the hint. `PreferredRange` is the longest hinted range, used by `chase` and perception.

Taunts set the target and a memory timestamp the behaviors read as `taunted`.
