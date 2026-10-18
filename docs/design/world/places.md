# Places

A place is anything on the land that is more than land: a campfire, a cairn, a shrine, a camp, a farm, a hamlet, a town, a city, a ruin, a barrow, a Delve, a Snarl, a lair, a Lantern, a Firstwork. This page is the grammar that makes them, the rules that space them, the ways they have insides, the authored ones beside the generated ones, and the quests that hang on them. Places are the Hytale and Cube World part of the owner's picture: dense sections of things to do, spread across interesting terrain, found by exploring.

## The grammar

A place is an instance of a **place kind**, content that composes:

- a **site**: where it may stand (biomes, regions, factions, elevation, slope, water distance, exposure, a cliff or a shore for kinds that need one), its footprint and spacing, whether the ground is flattened, and its weight and budget tags;
- a **layout**: a grammar that lays the place out from its center: rules that expand a start symbol into parts at offsets and headings under constraints, with terminals that are landmark stamps (the ASCII prefabs with their legends), prop scatters, roads, walls, fields, water features, or sub-places (a hamlet's grammar emits a mill by the river, a farm on the flat, a shrine on the knoll, each a place of its own kind within the parent's reach); a stamp alone is the degenerate grammar, which is what every place has today;
- **parts**: the pieces the layout places, each with its own tags, props, spawners, services and appearance ids, so a wall is destructible by its tags and a tavern opens a vendor;
- **inhabitants**: who lives there, by faction and by the place's state, as spawn specifications with packs and respawns;
- **services**: the interactables a part offers (stations, vendors, quest givers, founts, portals to interiors);
- **quest hooks**: the templates the place can seed and the parts that carry objectives (a chest to open, a Snag to cut, a prisoner to free);
- **names**: lists by culture, from the lore's naming conventions, so a Marcher stead, an Ilvarene font and a Durn delve do not sound alike;
- **states**: the ways the place can be found (intact, abandoned, burned, occupied, re-tangling) with overrides per state for layout, inhabitants, services and hooks;
- **appearance**: ids under `meta` that the visuals read and the simulation never does.

Grammar rule types are a registry (offset, ring, along a road, fill a lot, scatter, by constraint), so a new way of laying out a place is a registration and a content change, never a switch in the planner. A layout is deterministic from the place's own random stream, bounded in depth and part count by rules, and cached in the zone plan as a **place plan**: the list of parts with their positions, headings and resolved stamps, which the chunk passes consume and the client computes identically.

## The ladder

Place kinds sit on a ladder of scales so that content can be dense without being samey:

- **features**: a campfire, a corpse, a cairn, a fallen tree across a path, a floss drift; tiny, frequent, mostly scenery with something to pick up or read;
- **sites**: a camp, a shrine, a well, a fishing spot, a watchpost, a barrow mouth; small, with one thing to do;
- **settlements**: a stead, a hamlet, a village, a town, a city; each a grammar that emits the next rung down around it (a town has a market, a Candle, a Hospice-gate, walls, gates, farms outside), with services growing with the rung;
- **complexes**: a Snarl, a Delve, a Wrack, a fortress, a mine, a lair; places whose inside is the point, with interiors and a heart;
- **landmarks**: the authored places of the lore and the generated great works (a Lantern, a Loomroad bridge, a laid river's glass stair); unique or rare, pinned or sited by constraint.

## Density and spacing

Two mechanisms make the land dense where it should be and empty where it should be. Budgets per region archetype by tag (as today) set how much of each kind a zone wants, scaled by overlays (`poi_bonus`) and by the region plan's **settlement field**, a map from water distance, slope, Thread densities and faction influence that says where people would live, so steads cluster along rivers and roads and the high moor stays bare. Spacing is per kind, with a parent's reach counting for its sub-places, so a town's farms stand close while two towns do not. The candidate grid, the shuffle and the weighted picks stay as they are; what changes is that a pick may expand into a cluster, that budgets read the settlement field, and that features and sites fill the gaps between larger places at their own densities. Density is content: a mod that wants a crowded frontier raises budgets and shrinks spacing.

## Interiors

A place has an inside in one of three ways, chosen per kind:

- **Stacked surfaces** inside the open-air layer: towers with floors, bridges, decks, walls to stand on, ledges, shallow caves and overhangs. Enough surfaces per cell for a building with two or three floors, and the cheapest, because nothing changes about the zone.
- **Interior layers**: a separate chunk grid at the same coordinates, keyed by a layer id, owned by the zone, generated by the place's interior grammar (rooms and corridors as a graph grammar laid onto the grid and carved as surfaces with walls and headroom), simulated by the same zone simulation. An entity carries its layer; the spatial grid, the terrain queries, line of sight and interest are per layer. Portals (a cave mouth, a door, a stair, a hatch) move a body between layers at matching coordinates. Deep caves, dungeons, mines, cellars and the dry rooms under the Sunken Lantern are interior layers.
- **Instances**: a zone of their own, keyed by the place, the party or the character, and a turn counter, so that a Knot turns: each entry re-tangles it from a new seed folded from the world seed, the place id and the turn, which the ledger counts per place (for a shared Knot) or per character (for a private one). An instance generates and simulates like any zone, with its own interior layers if its kind says so, unloads when empty, and keeps nothing but what the ledger records about its outcome (the Snag cut, the Burl taken).

Which of the three a kind uses, and whether an instance is shared or private, is on the kind; the lore's Knots are instances, Landmarks are layers or surfaces, and most settlements are surfaces.

## Dungeons, caves, camps, towns, ruins and lairs

Each is a family of place kinds with a characteristic grammar, population and payoff; the engine knows none of them by name.

- **Dungeons** (Snarls, Wracks, Delves, Drownings, Unmakings in the lore) are complexes with an interior grammar of rooms, a difficulty curve by depth, a heart (the Snag) that ends them, encounter rooms that pull from the region's monster tables by tags, and loot rooms with reward budgets by depth. Most are instances so they can turn.
- **Caves** are open-air cave systems or interior layers, sited in cliffs and hillsides, with their own biome-like windows, flora, spawns and floss. A lair is a cave or ruin with a named resident: a unique place.
- **Camps** are sites of a faction: a palisade, tents, a fire, a captain, prisoners or loot, patrols that the world simulation moves. Camps are where armies rest, so a camp's population follows the agents at it.
- **Towns** are settlements with services; a town's grammar lays streets, lots and walls, fills lots from the kind's building parts, and sites the services the rung grants. A town has a state: besieged, occupied, burned, rebuilt, which the ledger records and the grammar reads.
- **Ruins** are the Concord's works gone wild and the recent dead: a ruin kind may be generated fresh, or made from another kind's layout in its burned or abandoned state, so that a burned stead is a ruin with the stead's bones.
- **Lairs and unique places** are placed by a world-level pass under constraints rather than by the zone grid: one dragon's cave in the mountains of a world, one gnoll town on a plain within a day's march of a frontier town, one Greyheron Light on a sea stack. A unique place has a world-wide id, a state in the ledger, and usually an agent (the dragon) and a campaign beat attached. Recurring places are the rest.

## Authored landmarks beside generated ones

Pins come in two forms. By coordinate: a region or zone override fixes a place at a cell, as the start town is fixed today, with a name and a landmark. By constraint: an authored site names a kind, a region and a rule (the highest hill within two zones of the start town, the first sea stack south of the river's mouth) and the world-level placement pass sites it deterministically. Authored places use the same grammars and states as generated ones; what is authored is that they exist, where, and what they are called. Authored places may be marked indestructible (the Firstworks) and may carry campaign hooks. Generated places near an authored one are sited around it, never through it.

## Quests, objectives and rewards per kind

Quest templates attach to kinds and to parts, and slots fill from real places, patterns and items within reach, so a town asks about the actual ruin on the actual hill. The template set grows with objective kinds the registry gains: clear (reduce a place's population below a threshold), hold (survive the waves an event sends), escort and deliver (move a person or a thing between places along the routes), claim (bind, plant a banner, re-light), recover (a part's loot), scout (discover a set of places), hunt (a named agent). Rewards scale by the place's danger: each kind declares a danger and a reward budget by rung and depth, so a deep Delve pays more than a hedgerow Hob, and loot tables read the place's tags. Discovery is by walking into a place, by its marker, by a rumor (NPC dialogue that names a nearby place from the zone plan and marks it on the map), or by a campaign beat. A place's quests follow its state: a burned stead offers rebuilding, an occupied town offers liberation, a cleared Snarl offers nothing until it turns.

## Current population integration

Point-of-interest `inhabitants` are ordinary pattern spawn specifications. Their `level` window is checked against the site's effective level, which includes its authored offset from the region, before any population rolls. Naturally generated sites with inhabitants are ineligible when no inhabitant window admits that level. Pinned sites deliberately bypass placement constraints and can be used for empty/below-window probes. Spawn specifications and landmark legend cells retain the pattern's level by default. Either may explicitly set `use_site_level: true` to carry the site level through initial birth and respawn. Eligibility and scaling are separate: a level window alone never changes a resident's life, accuracy, damage or rewards. Roaming biome packs retain their pattern level; the opt-in applies only where an owning site exists. Older stored chunks with a null spawn level preserve pattern levels; already stored explicit levels remain authoritative. Density in this path is a rounded placement count with a minimum of one, not a fractional chance; use an explicit level window for eligibility. Biome tables continue to use their existing density sampling.

The place definition's `factions` list restricts which region factions may host the site. It does not set the combat faction of spawned inhabitants. An unfiltered hostile ruin may therefore sit within a settled political region while each resident uses its own hostile pattern faction. Region budget values multiply each matching candidate's weight; they are not a fixed allocation shared between matching definitions. Adding ruin kinds increases their combined selection weight relative to other kinds, subject to placement and spacing constraints. See [entity content](../entities.md).

## What the visuals read

Every place, part and prop carries appearance ids under `meta` (a kind, a culture, an age, a state), and every stamp cell carries tags; the clients choose meshes, sprites and materials from them. The simulation reads none of it.

## Rules used

The `places` group: grammar depth and part limits, cluster reach, settlement field weights, interior layer limits, instance keys and turn policy, reward budgets by rung and depth, rumor range.

## Clan encampments

Clan camp content uses existing ruin budgets and registered landmarks with sparse weighted candidates and spacing. Each inhabitant carries its minimum level; a champion only appears when the effective site level reaches it. A camp can occur within another faction’s region: its residents retain their own pattern faction. Gnoll and Beastkin camp references are explicit on each resident’s habitat tags, and their placement/population replay is pinned by generation tests.
