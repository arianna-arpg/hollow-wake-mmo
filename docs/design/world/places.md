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

Siting never reads an overlay. The zone plan samples the founding climate of the land, without overlays, so a season, a decree or an event changes what a place's chunks look like, never where a place stands, what it is or what it is called; its id, its Fount, its state and every quest that names it stay valid for the life of the world. Pins written after a zone was planned take the lots the plan reserved for them or free ground outside every footprint, drawn from their own stream, so the places already placed never move. Every candidate of the zone's grid draws from its own stream, and a place's id is its zone and its slot, never its coordinates, so a budget that grows adds slots at the end and nothing already placed changes; the only overlay kinds a plan may read are those registered as structural, such as the budget bonus a decree grants.

Two mechanisms make the land dense where it should be and empty where it should be. Budgets per region archetype by tag (as today) set how much of each kind a zone wants, scaled by overlays (`poi_bonus`) and by the region plan's **settlement field**, a map from water distance, slope, Thread densities and faction influence that says where people would live, so steads cluster along rivers and roads and the high moor stays bare. Spacing is per kind, with a parent's reach counting for its sub-places, so a town's farms stand close while two towns do not. A place belongs to the zone its center lies in: a sub-place a parent emits across a zone border is owned by that zone's plan, which recomputes the parent's expansion from the same pure inputs, so the sub-place is stamped, populated and marked by the zone it stands in. The candidate grid, the shuffle and the weighted picks stay as they are; what changes is that a pick may expand into a cluster, that budgets read the settlement field, and that features and sites fill the gaps between larger places at their own densities. Density is content: a mod that wants a crowded frontier raises budgets and shrinks spacing.

## Interiors

A place has an inside in one of three ways, chosen per kind:

- **Stacked surfaces** inside the open-air layer: towers with floors, bridges, decks, walls to stand on, ledges, shallow caves and overhangs. Enough surfaces per cell for a building with two or three floors, and the cheapest, because nothing changes about the zone.
- **Interior layers**: a separate chunk grid at the same coordinates, keyed by a layer id, owned by the zone, generated by the place's interior grammar (rooms and corridors as a graph grammar laid onto the grid and carved as surfaces with walls and headroom), simulated by the same zone simulation. An entity carries its layer; the spatial grid, the terrain queries, line of sight and interest are per layer. Portals (a cave mouth, a door, a stair, a hatch) move a body between layers at matching coordinates. Deep caves, dungeons, mines, cellars and the dry rooms under the Sunken Lantern are interior layers.
- **Instances**: a zone of their own, keyed by the place and a turn (and, where the campaign's stage changes what is inside, the stage), so that a Knot turns. A turn is opened by the first player to enter the place: it is seeded from the world seed, the place id, the turn count and the opener, so the same key gives the same tangle on any server and on the opener's own client, which may generate it for drawing but never decides what is in it. The opener's party enters the leader's turn wherever they are, and any player who enters the place while a turn is open joins it unless the kind is private, so strangers who meet at a Snarl's mouth fight the same Snarl. A turn ends when its heart is cut or it stands empty past the kind's grace, the ledger counts it, and the next arrival opens the next. Difficulty stacks with the players inside by rule, applied when the zone materializes the instance's spawners and rooms and never in generation: more and tougher inhabitants, richer reward budgets, so a party and a crowd of strangers both find the Knot sized to them. An instance generates and simulates like any zone, with its own interior layers if its kind says so, unloads when empty, and keeps nothing but what the ledger records about its outcome (the Snag cut, the Burl taken).

Which of the three a kind uses, and whether an instance is shared by arrival or private to a character or party, is on the kind. The lore's Snarls are shared instances that turn; its Tangles and Thinnings are shared open-world layers or surfaces the Weft turns on its clock; its Burls are shared, never turn, and are contested; a beat that needs a Knot of its own gets a private one. Landmarks are layers or surfaces, and most settlements are surfaces.

## Dungeons, caves, camps, towns, ruins and lairs

Each is a family of place kinds with a characteristic grammar, population and payoff; the engine knows none of them by name.

- **Dungeons** (Snarls, Wracks, Delves, Drownings, Unmakings in the lore) are complexes with an interior grammar of rooms, a difficulty curve by depth, a heart (the Snag) that ends them, encounter rooms that pull from the region's monster tables by tags, and loot rooms with reward budgets by depth. Most are instances so they can turn.
- **Caves** are open-air cave systems or interior layers, sited in cliffs and hillsides, with their own biome-like windows, flora, spawns and floss. A lair is a cave or ruin with a named resident: a unique place.
- **Camps** are sites of a faction: a palisade, tents, a fire, a captain, prisoners or loot, patrols that the world simulation moves. Camps are where armies rest, so a camp's population follows the agents and the stock at it, which the zone materializes; generation only places the camp.
- **Towns** are settlements with services; a town's grammar lays streets, lots and walls, fills lots from the kind's building parts, and sites the services the rung grants. A town has a state: besieged, occupied, burned, rebuilt, which the ledger records and the grammar reads.
- **Ruins** are the Concord's works gone wild and the recent dead: a ruin kind may be generated fresh, or made from another kind's layout in its burned or abandoned state, so that a burned stead is a ruin with the stead's bones.
- **Lairs and unique places** are placed under constraints rather than by the zone grid, and every constraint names an anchor and a bounded radius, so an endless world never searches without end: one dragon's cave in the mountains within so many regions of the start town, one lighthouse on a sea stack along the heartland's coast. A site that must stand relative to the frontier (a gnoll town a day's march past the farthest explored valley) is a runtime pin, resolved once when written and stored. A unique place has a world-wide id, a state in the ledger, and usually an agent (the dragon) and a campaign beat attached. Recurring places are the rest.

## Claims

A claim is a place a player can take and own: a kind marked claimable (an abandoned stead, a ruined tower, a cleared camp, a plot beside a Fount) that a rite or a deed turns into the player's, tied to their Holding. Inside a claim's footprint the rules of destruction change: edits stick, the player may dig, raise and build within the kind's bounds, the land does not heal, and the place's layout becomes whatever the player makes of it, with the claim's state and its edits in the ledger. A claim can grow into a township: its grammar offers lots and services the owner raises through structures, and its population comes from the folk who move in. Outside claims the land is the world's and heals; claims are the deliberate exception, placed by kinds so that a frontier offers them where settling makes sense.

## Authored landmarks beside generated ones

Pins come in three forms. By coordinate: a region or zone override fixes a place at a cell, as the start town is fixed today, with a name and a landmark. By constraint: an authored site names a kind, an anchor, a radius and a rule (the highest hill within two zones of the start town, the first sea stack south of the river's mouth) and the placement pass sites it deterministically inside that radius. At run time: the Weft and the campaign place pins the same two ways, usually beyond the frontier (a Fount waking somewhere east of the farthest explored valley, the home camp of a warband that came out of the north, the stronghold of a foe faction a world drew); a runtime pin is resolved to an absolute site on the world thread the moment it is written, against the world plan, in ledger order, with a bounded search and a fallback kind, then becomes a ledger entry the clients receive; the zone plan honors it when generation first reaches its land, and rumor and chronicle speak of it before anyone has seen it. A pin a foe faction makes carries a stage range, and a character sees it in the world of their stage ([the campaign](campaign.md)); nothing private to one character is ever a pin. Authored and promised places use the same grammars and states as generated ones; what is authored or promised is that they exist, roughly where, and what they are called. Authored places may be marked indestructible (the Firstworks) and may carry campaign hooks. Generated places near a pinned one are sited around it, never through it.

## Quests, objectives and rewards per kind

Quest templates attach to kinds and to parts, and slots fill from real places, patterns and items within reach, so a town asks about the actual ruin on the actual hill. The template set grows with objective kinds the registry gains: clear (reduce a place's population below a threshold), hold (survive the waves an event sends), escort and deliver (move a person or a thing between places along the routes), claim (bind, plant a banner, re-light), recover (a part's loot), scout (discover a set of places), hunt (a named agent). Rewards scale by the place's danger: each kind declares a danger and a reward budget by rung and depth, so a deep Delve pays more than a hedgerow Hob, and loot tables read the place's tags. Discovery is by walking into a place, by its marker, by a rumor (NPC dialogue that names a nearby place from the zone plan and marks it on the map), or by a campaign beat. A place's quests follow its state: a burned stead offers rebuilding, an occupied town offers liberation, a cleared Snarl offers nothing until it turns. A bounty board, a part a settlement may carry, generates quests per character from the board's seed, the character and the board's refresh, aimed at the shared places, Knots and agents within its reach, so two players at one board carry different bounties toward the same Snarl and meet inside it; the quests are the character's, the targets are the world's.

## What the visuals read

Every place, part and prop carries appearance ids under `meta` (a kind, a culture, an age, a state), and every stamp cell carries tags; the clients choose meshes, sprites and materials from them. The simulation reads none of it.

## Rules used

The `places` group: grammar depth and part limits, cluster reach, settlement field weights, interior layer limits, instance keys, turn grace and sharing policy, stacking by players inside, reward budgets by rung and depth, bounty board refresh and reach, rumor range.
