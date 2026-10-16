# World generation

## Hierarchy

Cells (1 unit) in chunks (32) in zones (8 by 8 chunks) in regions (8 by 8 zones); sizes from `world.*` rules. Coordinates are signed; the start zone is the first zone override flagged `player_start`, else the origin.

## Determinism

`WorldGenerator` is a function of seed, content, overrides and overlays. Caches are the only state. Two servers with the same inputs generate the same world; the client generates the same terrain as the server.

## Regions

Each region gets an archetype by weighted pick among archetypes whose distance window (in regions from the origin) contains it, or a pinned archetype from a region override. Archetypes carry elevation lift and scale, temperature and moisture bias, per-Thread biases, a level offset, a point-of-interest budget by tag, candidate factions and names. Region level is `1 + distance * level_per_region + offset`. Parameters blend bilinearly between the four nearest region centers so borders never seam.

## Climate and biomes

Elevation is continental noise plus ridge noise (scaled by the region's elevation scale) plus detail, lifted by the region, offset and multiplied by overlays. Temperature and moisture are noise plus region bias, lapse by elevation, plus overlays. Each Thread has its own density field plus region bias plus overlays. Water is where elevation is below the water level (sea level or an overlay).

Biomes declare windows on elevation (relative to water), temperature, moisture, slope and Thread densities, optional region tags, surface tags, terrace size, flora, spawns and yields. The most specific match wins (deepest parent chain, then priority); a child must also satisfy its parents.

## Zones

The zone planner lays a jittered candidate grid inside the zone with a margin, samples each candidate, and picks a point of interest by weight times the region's budget for its tags, honoring biome, region, faction, elevation, slope, water distance and spacing constraints, until the budget is spent. Pinned points of interest from zone overrides come first. Each placed point gets a deterministic id, a name from its template, a landmark by weight, a base height and a faction.

Quest templates named by a hub's template are instantiated against points of interest within reach (this zone and its neighbors) and patterns or items by tags; `$slot` placeholders in targets, points and text resolve to real ids and names. Unfillable templates are skipped.

## Chunks

Passes: **terrain** (height, biome, flags, surface tags, water, cliffs, terracing), **landmarks** (flatten footprints with a falloff, stamp rows; `layer` cells add surfaces above the ground; `spawn` cells add spawn points), **flora** (density per hundred cells with clumping noise), **spawns** (biome and spawn table densities per thousand walkable cells, kept away from safe hubs, plus point-of-interest inhabitants).

## Overlays

`GenerationOverlay` scopes (global, regions, zones, a circle) carry parameters: `elevation_offset`, `elevation_multiplier`, `water_level`, `temperature_offset`, `moisture_offset`, `thread.{id}`, `poi_bonus`. World events and holding decrees add and remove overlays; the generator drops zone caches when overlays change, and chunks regenerate on next load unless modified.

## Tools

`skein region`, `skein zone --map`, `skein world` and `skein load` show what the generator does without a client.
