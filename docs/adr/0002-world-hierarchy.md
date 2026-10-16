# ADR 0002: A seamless generated world of regions, zones and chunks

**Status:** accepted.

## Context

The world should be large and seamless like a voxel sandbox, with terrain that reads as a heightmap, infinite exploration outward, and generation intelligent enough to place towns, ruins, shrines, Knots and the quests they imply where they make sense. Events should be able to change generation for parts of the world. Zones must be the unit of simulation so servers can scale by zones.

## Decision

- World coordinates are continuous and signed; the world extends in every direction from the origin.
- **Chunks** (32 cells) are the unit of generation, streaming and persistence. **Zones** (8 by 8 chunks) are the unit of simulation and scaling. **Regions** (8 by 8 zones) are the unit of macro generation, politics and world events. Sizes come from rules.
- Generation is a pure function of seed, content, hand-placed overrides and active overlays. Region archetypes shape elevation and climate and set budgets of points of interest; zone planning places points of interest under constraints and instantiates quest templates from them; chunk passes write terrain, stamp landmarks, scatter flora and place spawns. Anything a chunk needs from its neighbors is recomputed from the same function, so edges agree.
- Terrain cells hold stacked surfaces, so bridges, floors and caves are z-levels of the same cell. Terraced biomes quantise heights to make stepped cliffs that must be jumped or climbed by stairs.
- Hand-placed content pins over generation: region and zone overrides name places, set archetypes and factions, and fix points of interest at coordinates.
- Overlays (world events, decrees) change generator parameters over regions, zones or circles; chunks regenerate on next load, and modified chunks persist.

## Consequences

- The client generates unmodified chunks itself from the seed and overlays, so terrain costs no bandwidth.
- Rivers and roads are later passes; the pipeline is built for adding them.
- Cross-zone gameplay (fighting across a border) needs border mirroring, scheduled for the scaling milestone.
