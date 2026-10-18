# The living world

This is the entry page of the living world's contract set: how the land, its places, its events and its story come to be, change and persist. The other pages are [landscape](landscape.md), [places](places.md), [factions, events and encounters](factions-events-encounters.md), [the campaign](campaign.md), [destruction and persistence](destruction-and-persistence.md) and [the generation pipeline and its budgets](pipeline-and-budgets.md). The work to build what these pages describe is planned in [the plan](plan.md). The world's fiction is under [docs/lore](../../lore/00-bible.md); this set says how the engine makes that fiction stand up.

## The idea

The world is a canvas three things paint on, in a fixed order:

1. **Generation** paints the land from the seed and the content. It is a pure function: give it the seed, the content, the hand-placed pins and the generation ledger, and it returns the same region, zone, place or chunk every time, on the server, in a tool, in a test and on the player's own machine.
2. **The world simulation** paints over the land on a slow clock. Factions, armies, caravans, beasts, weather and the seasons move between places and meet each other whether or not anyone is watching. What they do becomes entries in the ledger, which generation then reads.
3. **Players** paint last and sharpest. A zone simulation resolves their deeds at the tick rate; the deeds become ledger entries too: a bound Fount, a raised wall, a dug trench, a burned camp, a cleared Snarl, a discovered Firstwork.

The ledger is the hinge. It is the persisted record of everything about a world that cannot be recomputed from the seed and the content. Generation reads a narrow slice of it, the generation ledger; the simulations read the rest. Because generation reads only that slice, a client that holds the seed, the content and the slice generates exactly what the server generates, and the wire carries only what the simulations changed.

The owner's picture, in the terms of this set: the landscape is a sandbox world's terrain, generated but sensible, the field of adventure. Places are dense and varied, from a campfire to a city, each with things to do. Encounters and quests are discovered by exploring, some recurring, some unique to a world, and they leave marks. The world does not revolve around the players, but the players can change what and how it revolves.

## What is generated

- **The landscape**: continents and seas, mountain belts, rivers and lakes, coasts, caves and overhangs, biomes and their transitions, roads and paths, the day's weather under the season. Every part is a pure function of the seed, the content, the pins and the generation ledger, computed from plans at the world, region and zone scale down to chunk passes ([landscape](landscape.md)).
- **Places**: points of interest from a grammar of place kinds, laid out by rules, populated, named in the culture of their region, with interiors as layers or instances, and with the quests, objectives and rewards their kind attaches ([places](places.md)).
- **Encounters**: what happens where moving things meet, resolved in a zone when one is loaded and abstractly when none is ([factions, events and encounters](factions-events-encounters.md)).
- **The content the campaign asks for**: a beat names a kind of place, event or agent and the constraints it must satisfy; the world supplies a generated one ([campaign](campaign.md)).

## What is simulated

Two clocks run.

- **Zones** run at the tick rate: entities, movement, combat, casting, loot, quests, interaction, territory, as `docs/ARCHITECTURE.md` describes. A zone is a window onto the world: it loads when a player is near and unloads when nobody is, and it reconstitutes itself from generation plus the ledger every time.
- **The world simulation**, named the Weft after the moving part of the Loom, runs above zones on the world clock, in bounded steps, deterministic from the world seed and the ledger. It moves agents along routes between places, resolves encounters, schedules recurring events, raises unique ones when their conditions hold, turns the weather and the seasons, and advances the campaign's world beats. It never touches an entity directly: it writes the ledger and publishes events; loaded zones materialize what it says is there.

## What is authored

Hand-placed content pins over generation; it never replaces it. Authored things: the lore's fixed places (the start town, the Firstworks, the Lanterns, the Last Stitch) as pins by coordinate or by constraint; region archetypes and overrides; the biome table; place kinds and their grammars; landmark stamps; faction definitions and their agent kinds; event kinds; weather and calendar; campaign beats; names by culture. Everything authored is content with a validator; nothing authored is a special case in code.

## What persists

The ledger, per world, holds: generation overlays, chunk edits, place states, holdings, faction state and influence, agents and their journeys, the event history and the chronicle, campaign state, the world clock. Each kind of entry has an origin (generation, the Weft, a holding, a player) and a time, so later rules can decide precedence and healing. Per zone there is nothing of its own: a zone is a view of the ledger and the generator, and its spawners, respawn timers and dropped loot restart when it loads. Per player: discoveries, deeds and story flags, quests, reputation, holdings, and the turn counters of the instances they have entered. [Destruction and persistence](destruction-and-persistence.md) has the rules.

## How players change it

A deed is a simulation event that the ledger records. Binding a Fount makes a Holding; a decree adds an overlay and the land shifts under it; a structure stamps the terrain through a chunk edit; a weave tagged for the earth digs or raises it within the rules of hardness; clearing a Snarl turns its place state and starts its re-tangling; killing a marching warband removes an agent and leaves a battlefield; discovering a Firstwork advances a campaign beat for everyone. Every change goes through the server: the client sends intents and draws state, and every change to the land arrives as an overlay, a chunk edit or a place state, which is also how the client keeps its own generation in step.

## The tenets this set holds to

Data over code: biome tables, place grammars, event kinds, agent kinds, calendars and campaign beats are content with validators, and new kinds register through the content registry. Registries, never switches: generation passes, place grammars, overlay kinds, event providers, encounter resolvers, objective types and edit kinds are open sets. Events between systems: the Weft, the quest system, territory and networking listen to each other through events and the ledger, never by calls. Determinism: every roll derives from the seed and a salt, never from wall-clock time; a seeded world tells the same story twice. Server authority for every change to the land. The client generates what the server generates and receives only what the simulations changed. Performance budgets in [performance.md](../performance.md) are gates, not goals.
