# Destruction and persistence

The land can be changed, by players and by the world. Most changes are passing: the land heals back to what generation made, so a meteor's crater is a crater for a season and a meadow again after, and no one walks into a wasteland another player left. Changes last where they are meant to: in a claim the player holds, under a Holding's structures, where a place's kind says the marks of war stay. This page is the one path a change to the land takes, what can and cannot be changed, what heals and what sticks, what persists for whom, and what happens when a player's change meets the world's.

## One path: chunk edits

Every change to cells goes through a **chunk edit**: a list of cell operations on a chunk (set a height, set or clear flags, set surface tags, add or remove a layer surface, add or remove a prop or a spawn) with an origin (a player, a holding, an event, the Weft, a structure), a time and a kind. Edits are the only way a simulation writes terrain; the generator never does after it returns a chunk. The territory system's structure stamping, which writes cells directly today, becomes an edit; so does everything below.

Edits persist as deltas, not as whole chunks. The store holds a chunk's edits in order; loading a chunk regenerates it from the seed and the current overlays, then replays its edits on top. That is what lets an overlay change the land under a player's structure without erasing the structure, lets edits expire, and lets the wire carry a chunk edit instead of a chunk. A chunk without edits is never stored. A whole-chunk snapshot remains the wire's fallback for a client joining with many edits outstanding.

## What is destructible

What can be changed is said by tags and rules, never by type:

- **Surfaces and props**: flora, props, fences, doors, furniture; anything a pattern or a stamp cell tags as breakable, with a hardness.
- **Structure cells**: walls, roofs, towers, bridges; stamp cells carry hardness by material tag, and a structure's parts fall when their hardness is spent, through edits that lower heights, clear flags and leave rubble.
- **Terrain**: heights move within bounds: a crater, a trench, a raised bank, a collapsed slope. The bounds are rules: how far below the generated height digging may go and how far above raising may reach, by surface tag (soil gives, rock resists, roadstone and lantern-steel never), and what the biome's Thread density does to it (Stone resists, the sag yields).
- **Water**: an edit may dam or breach within the same bounds; water levels follow the overlays and edits that set them.

What cannot: cells tagged protected (authored landmarks, the Firstworks, safe hubs' hearts, a holding's structures to anyone but its holder until sieges exist), cells below bedrock depth, and anything a rule marks immovable. What sticks is a separate question, answered below: outside a claim or a Holding, everything on this list heals.

## How changes happen

- **Effects**: a terrain effect family (dig, raise, break, burn, place, flood) registered like any other handler, with parameters an expression can read (amount, radius, shape, the surface tags it affects) and the hooks weaves and glyphs reach, so a Stone weave tagged for the earth digs, a Bloom weave regrows, an Ember weave burns a palisade, and a monster's charge breaks a fence through the same family. Players, monsters and events use the same handlers.
- **Structures**: a holding raises and razes through edits with the holding as origin.
- **Encounters and events**: battlefields, floods, fires and sieges write edits with the event as origin and an expiry.
- **Tools**: content may give items that edit (a shovel, a pick, a charge) as effects on use.

Every edit is validated by the simulation: range, line of sight, the target cell's hardness and protection, the actor's reach, the rules' bounds. The client sends an intent or a cast; it never writes a cell.

## Healing and regrowth

The Loom resolves what is not held. Every edit outside a claim carries an expiry and a regrowth rule by its kind: craters fill over a season, burned ground greens, rubble weathers, a felled tree's stump makes way for a sapling; the generated chunk is the before, the edit is the after, and healing is the return. Edits anchored by something that persists do not heal while the anchor stands: a claim the player holds (the land inside it is theirs to shape and keep), a structure in a standing Holding, a protected place, a road, a place whose kind says a battlefield stays. Regrowth runs on the Weft's clock as edits that reverse edits, through the same path, so it is persistent and replayable, and it is budgeted so that a region's healing is a few edits per world day. Anchoring is the healer's business, decided on the Weft's clock from the ledger's claims, Holdings and place kinds; the edit path itself applies what it is given and reads nothing but the chunk and the edit, so the server and the clients apply an edit identically. Flora and resource nodes respawn by their own rules as today; destruction of them is an edit only when the player meant to clear land.

## What persists

- **Per world**: the ledger: overlays, chunk edits, place states, holdings, faction standing, agents and their positions, active and historic events, the chronicle, campaign state, the world clock, the item uid counter. The ledger has one store interface with the same shape for JSON files and a database.
- **Per zone**: nothing. A zone is reconstituted from the generator plus the ledger every load; its spawners restart from their definitions, drops expire with their rules, and bodies that were only ever a zone's (monsters, projectiles) are not saved. A zone that unloads while an encounter is materialized reports the encounter's state to the Weft, which continues it abstractly.
- **Per player**: the character's sheet as today, plus discoveries of places and layers, deeds and story flags, instance turn counters, and the holdings owned. Instances keep nothing between visits except what the ledger records about their outcome.
- **On the wire**: entering a zone sends the overlays and place states with the world information and the edits of the chunks the player can see; new edits arrive as they happen within interest; a client regenerates a chunk when an overlay changes it and replays the edits it holds, exactly as the server does.

## When player change meets world change

Precedence is by layer, then by rule, never by who was first:

1. Generation and overlays paint the base. An overlay may change the base under any edit; the edit replays on top.
2. Edits apply in time order; a later edit to a cell wins over an earlier one.
3. Protection stops an edit before it is made: a protected cell refuses, and the actor is told why.
4. Origins have standing by rule: a claim's and a Holding's edits inside their footprint outrank an event's unless the event is a siege or a contest aimed at them; a player's edits outside any holding are ordinary and the world may overwrite them (a flood fills a trench, an army tramples a garden); a world edit that destroys a player's structure is itself a chunk edit and an event the player hears about.
5. Healing reverses what has no anchor, oldest first, and never touches protected or anchored cells.

Conflicts are therefore ordinary edits with a rule choosing the order, visible in the ledger and in the chronicle, and replayable.

## Determinism and budgets

Replaying a chunk's edits is deterministic and bounded; an edit that would make a chunk cost more than the chunk budget to load is refused or compacted by rule, and the store compacts long edit histories into a snapshot plus a tail. A world's ledger size per chunk and the edit replay time are benchmark rows with budgets in [performance.md](../performance.md).

## Rules used

The `destruction` group: hardness by surface tag and material, dig and raise bounds, bedrock depth, protection tags, regrowth rules by edit kind, origin standing, compaction thresholds.
