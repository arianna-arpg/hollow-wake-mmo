# Movement and z-levels

## Terrain model

A cell has a base surface (height, flags, tags) and may have layers: extra surfaces at other heights (bridges, tower floors, decks). `Terrain.TryFindGround(x, y, z, stepUp)` returns the highest walkable surface not more than `stepUp` above the feet, with the base surface sampled bilinearly for smooth slopes and layers kept flat. That one query resolves standing on a bridge over a river and walking under it.

Cells are blocked by cliffs (slope above `world.gen.max_walkable_slope` in non-terraced biomes), deep water, structure walls, and props flagged blocking (tracked per simulation in `BlockedCells`). Terraced biomes quantise heights to whole steps, which creates ledges you cannot walk up but can jump up.

## Movement

The movement system integrates per tick: path following or a target or a held direction produce a desired direction; speed is `move.speed`; casting a non-mobile weave, stuns and roots lock movement; jumping sets vertical velocity from `move.jump_velocity` scaled by `jump.height` when grounded; gravity is `move.gravity`. Horizontal motion is validated against the destination cell and slides along X or Y when blocked. Vertical motion lands on the ground under the new position and can fall any distance; fall damage is a rule.

Dashes and teleports resolve their destination by marching the segment and stopping at the first cell that cannot be occupied.

Movement keeps the spatial grid current, and everything that asks who is near (perception, targeting, areas, interest) reads it through radius queries. A radius query walks the grid in place without copying: a caller that adds or removes entities, or moves them between grid cells, while enumerating materializes the results first, or the enumeration throws.

## Pathfinding

A* over cells with eight neighbors, no corner cutting past blocked cells, step-up and max-drop limits, cell costs by tags (roads cheap, bog dear) and a node budget: `move.path_max_nodes` for a player's click-to-move, `move.ai_path_max_nodes` for AI movers, which also try a straight walk first and keep their plans until they go stale (see the AI contract). A failed search returns the best partial path so click-to-move still makes progress, and reports whether it reached the goal, got partway or found no step.

The pathfinder keeps its search state between searches and can write into the caller's path list, and ground queries resolve stacked surfaces without building anything, so planning and walking allocate nothing in steady state.

## Rules used

`move.gravity`, `move.jump_velocity`, `move.step_up`, `move.max_drop`, `move.default_speed`, `move.path_max_nodes`, `move.ai_path_max_nodes`, `move.ai_repath_seconds`, `move.ai_repath_distance`, `move.fall_damage_per_unit`, `move.fall_damage_threshold`.
