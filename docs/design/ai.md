# AI

Behaviors are state machines in content. A behavior names an initial state, a think interval, aggro and leash range expressions, and states with enter, tick and exit actions and prioritized transitions.

## Perception

Each think builds a scope over the entity's stats with: `has_target`, `target_distance`, `target_life_pct`, `nearest_enemy_distance`, `home_distance`, `state_time`, `aggro_range`, `leash_range`, `preferred_range`, `taunted`, the target mounted under `target.`, and memory values under `memory.`. Transitions are expressions over that scope, evaluated highest priority first; the first that holds switches state.

## Actions

A registry: `idle`, `stop`, `acquire_target` (nearest hostile within range, preferring recent attackers), `drop_target`, `chase` (path toward the target, stopping at a range), `attack` (choose a ready weave by the forms' AI hints and cast it through the shared runtime), `wander`, `return_home`, `flee`, `face_target`, `use_weave`, `remember`, `heal_self`. New actions register from code.

## Weave choice

`WeaveRuntime.ChooseForAi` filters the entity's weaves by readiness, cost, range and the hint's `when` expression, then weights by the hint. `PreferredRange` is the longest hinted range, used by `chase` and perception.

Taunts set the target and a memory timestamp the behaviors read as `taunted`.
