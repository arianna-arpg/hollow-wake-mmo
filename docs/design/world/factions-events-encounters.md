# Factions, events and encounters

Above the zones runs a world simulation on a slower clock. In code and content it is called the Weft, after the moving part of the Loom: the days, the weather, the lives of creatures, everything that crosses the fixed Warp and is woven in. It moves armies, caravans and beasts between places, spawns and resolves the encounters where they meet, leaves consequences on the land through the ledger, and feeds the quest system. It is the Skyrim and World of Warcraft part of the owner's picture: a gnoll army marching from a generated gnoll town, an iron crusade that intersects it, a battlefield left behind, a dragon that has a cave because it moved into one.

## Factions

Factions are content: ids, relations, colors, whether they hold territory. The Weft gives each faction **standing** per region (influence, from the holdings, places and agents it has there), **goals** (content: hold the Lanterns, harvest the Knots, free the Marches, let the Loom go), and **agent kinds** it can field. Standing is read by generation through place states and overlays (a region a faction dominates sites its camps, flies its banners, names its posts), by the zone through spawn tables keyed by faction tags, and by quests through reputation. The lore's five powers and the wild things with no banner are the core content; a mod adds a faction by adding content.

## Agents

An agent is a thing that moves on the world's time scale: a warband, an army column, a caravan, a patrol, a pilgrim company, a beast herd, a migration, a named creature. Agents are abstract: a kind (content), a faction, a strength (a budget of patterns by tags, so the combat content supplies the bodies), supplies, morale, an intent, and a position on the **route graph**: at a place, or on an edge between two places with a fraction travelled. Agent kinds are behaviors in the AI contract's sense, state machines in content with perception over the world (nearest hostile agent, the richest unguarded place in reach, the home camp's state, the season) and actions from a registry (march to, raid, besiege, hold, trade, forage, hunt, flee, disband, found a camp). Agents are created by the Weft's providers: a faction's goals spawn warbands from its camps, a place's kind spawns a caravan on a schedule, a beast biome spawns herds, a unique place spawns its named resident, an event spawns what it says.

## Populations

Not everything that lives is an agent. Each explored region keeps **populations**: stocks per population kind (the wolves of a valley, the hobs of the hedgerows, the Tangled of a Snarl, a foe faction's warriors, the folk of a stead), each with a size, a home (a biome, a place or a region) and a model as content: how it breeds, what it eats, how much pressure drives it off, how far it roams, where it goes when it leaves (along the routes to the neighboring region with the least pressure and the most of what it needs), how it recovers, and when it is gone. Pressure comes from deeds the ledger counts (kills by tag in a region, camps burned, Snarls cleared), from other populations (a predator's size, a foe faction's raids), from the weather and the season, and from the Weft's events. Generation never reads a population: the spawn pass writes spawn sites with stable ids, pure as everything generated, and a loading zone activates sites from the stock, so a valley hunted empty stays quiet until the stock recovers, and the pack that fled it fills the next valley's sites and starts on the deer there. A body in a loaded zone is a loan from a stock or an agent: materialization checks strength out, a respawn spends stock, a kill counts against the stock once and is pressure only then, stacking for the players inside is paid from the stock's reserve by rule, and unloading or lost interest returns the survivors with their positions; the Weft freezes a stock while a zone holds its bodies. A population that grows past its home's capacity founds a new place (a camp, a den, a stead) through a pin, and one that falls to nothing leaves its place abandoned, so the land's places follow its life. The country a foe faction leaves when it falls is capacity like any other: beasts and the Tangled flow into its emptied caves and camps by the same rules, and a kind the content marks as able to rise, grown into a power there, may become a new foe faction ([the campaign](campaign.md)). Agents are drawn from populations (a warband musters from a foe faction's stock and returns to it).

Populations step on the day clock in every tier, double-buffered in canonical region order, so a stock never depends on when a player passed; migration reads a coarse region graph recorded when the region entered the frontier (its neighbors and route costs as a few numbers), never a full route plan; far regions step round-robin under a fixed budget per day, so the cost of the far world is the budget, not the frontier's size, and a region far from everyone drifts rather than churns. A region entering the frontier is born on the world thread with a birth record: its starting stocks from what generation says lives there, filtered by the world's roster (a foe the world did not draw or has already lost seeds nothing, and its places stand as ruins), plus the immigrants waiting at its border; a stock that fled toward latent land waits at the frontier as pending immigrants until that land is born. Agents of a foe whose home lies in latent land enter at the frontier on their home's bearing after a delay the rule sets, so a drawn foe marches before anyone has walked to it.

## The route graph

Places are the nodes; edges are the routes the region plan lays between them (roads, rivers, passes, coasts, sea lanes) with a cost by terrain and season from the `routes` rules. The graph is generated, so it exists for every region whether or not a zone has ever loaded there, and it is the same graph on every server with the seed. The Weft moves agents along it in world ticks; an agent on an edge is somewhere between two places, which a loaded zone turns into a column walking that road.

## Encounters

An encounter is what happens when agents, or an agent and a place, meet: two hostile warbands on one road, an army at a town's gate, a herd in a hunting party's path, a caravan at a camp's ambush. The Weft detects meetings per tick on the graph and hands each to an **encounter resolver** chosen by the encounter's kind from a registry.

- **Materialized**: if a zone is loaded where the meeting happens, the resolver checks the agents' strength out into bodies in it (a strength becomes packs through the spawners, staged by interest so a large army streams in as players approach and never exceeds the zone's body budget) and lets the zone simulation fight it out. The zone reports back through events: who died, who fled, what was taken; when it unloads or interest is lost, the survivors and their positions go back to the agent. The Weft reads the outcome and updates the agents and the place. Players who are there are in the battle and their deeds count. A meeting staged in a zone that never unloads (the start zone) still resolves: a staged encounter nobody approaches resolves abstractly after the rule's patience.
- **Abstract**: if no zone is loaded, the resolver settles the meeting from strengths, terrain, morale, supplies and a keyed roll, in one step or over several ticks, and writes the same outcome shape. An abstract outcome is sized so that it would be plausible had it been fought: the resolvers are tuned against materialized fights in the balance harness.

A resolver never cares whether a player watched; the shape of its outcome is the same, which is what makes the world consistent when a player arrives at a battlefield the Weft fought in the night. Every roll the Weft makes is keyed by the world tick, the region, the purpose and the id, never drawn from one sequential stream, so a meeting that materialized instead of resolving abstractly shifts no other roll.

## Consequences

Outcomes become ledger entries, and nothing else:

- **overlays** scoped to a place or a circle: scorched ground after a fire, a Void thickening around an Unmaking, a Bloom tide, a faction's banners raised as a tag overlay;
- **chunk edits** at a place: broken walls, a trench line, corpses and wreckage as props, a camp's palisade raised, a burned house's bones;
- **place states**: a town occupied, a camp founded, a Snarl cleared and re-tangling, a lair emptied;
- **pins**: a place promised beyond the frontier or sited inside it: a warband's new camp, the den a pack fled to, a Fount waking out past the explored valleys; a pin is resolved to an absolute site on the world thread when it is written, in ledger order, against the world plan (which is pure even for latent land) with a bounded search and a fallback kind, so rumor names a real place and no two pins want the same hill;
- **populations**: a stock grown, broken, fled or gone;
- **agents**: created, moved, weakened, disbanded, promoted (a warband that wins twice becomes an army);
- **chronicle entries**: text in the lore's voice, generated from templates by the kind of outcome, with a time and a place.

Generation reads the first three; the zones materialize the fourth; towns, notice boards, NPC dialogue and the client's map read the fifth. A battlefield is therefore not a feature: it is a chunk edit and an overlay left by an encounter resolver, which the land shows and a quest template can point at.

## Recurring and unique events

An event is content: a kind, a scope (a region, a zone, a place, a circle, the world), triggers, phases with durations, what each phase does (overlays, spawns, agents, modifiers, place states, quests, effects on start and end), and what ends it. The scheduler that today picks a weighted event per interval becomes one **event provider** among several in a registry: the calendar provider raises seasonal events (Tidespan floods, Stormspan gales, the Selvage's unbinding at the Quiet Well), the faction provider raises campaigns when standings cross thresholds, the territory provider raises contests and sieges around holdings, the campaign provider raises beats, and a random provider keeps the surprise. Recurring events repeat under weights and cooldowns; **unique** events (Skyfold's first Vigil, an Unmaking opening in the Hush, the dragon waking) carry a condition over the ledger and happen once per world, after which the ledger remembers them. Every event runs on the Weft's clock, is replayable from the seed and the ledger, and is observable in the chronicle.

## How far ahead the world simulates

The Weft runs in bounded tiers so it never costs more than the budget, and the tiers are drawn around the players and the frontier:

- **observed** regions (a zone loaded) run every world tick with materialized encounters;
- **near** regions (a ring around the observed) run every world tick abstractly, so what walks toward the players is already moving;
- **far** regions (explored, beyond the ring) step on the day clock, round-robin under a fixed budget of regions per day: populations, pins and agents of consequence, with meetings swept once per step and nothing else; the far world drifts rather than churns, and a region's state is a handful of numbers;
- **latent** regions (beyond the frontier) are not simulated. They are consistent, because generation is pure, and they are promised: an event or a beat may pin something there, resolved to a site when written, which generation honors when a player arrives and rumor speaks of before; the first player across the frontier moves it, and the region is born with its birth record;
- while the server is down, nothing moves; on restart the Weft may catch up a bounded number of ticks, abstractly, so a world left for a night has turned a little and never a decade.

Agents per region, bodies materialized per zone and pins per region are capped by rule; beyond the caps the Weft merges, delays or disbands rather than exceeding the zone tick budget. Cost follows the tiers and the far budget, never the map.

## Replay

The Weft's inputs are a journal, persisted with the ledger: every deed with its world tick and commit sequence, every materialized outcome, every zone load and unload (which move regions between tiers), every frontier entry with its birth record, and every catch-up count. Replay is replay of the journal: `world --simulate` run twice against a recorded journal writes the same chronicle and the same ledger, and the proof includes a recorded loopback session with zones loading and unloading and a restart in the middle. Nothing the Weft does reads a zone's own clock, which restarts when a zone loads; every time it records is world time.

## How it is observed

- `hollowwake world --simulate <days>` runs the Weft headless from a seed and prints the chronicle, the agents' journeys and the ledger it wrote; run twice it prints the same text, which is the determinism proof.
- The chronicle persists with the ledger and reaches players as notices, as rumors in dialogue, as notice boards in towns and on the map as marks with a time.
- A zone loading reads the Weft's view of itself: which agents are there, what state its places are in, which encounters are in progress, and materializes them before the first player sees it.
- The server's log lines the Weft's tick time against its budget, and the benchmark has a world group.

## Feeding the quest system

Encounters and events raise quest offers at nearby hubs through templates whose slots fill from the event's own agents and places: the survivors of the burned stead ask for the warband hunted, the Compact asks for the road cleared, the Company pays for the caravan escorted, the Menders ask for the Unmaking's edge held. The quest system's `event` objective and the new objective kinds (hunt, hold, escort, claim) bind a quest to the agent or the place, so a quest completes when the world's fact changes, whoever changed it. A quest that the world overtakes (the warband the player was hunting died to another army) completes or withdraws by rule rather than hanging forever. An Odyssey's objectives are the one exception: they never complete by the world's fact and are never concluded by a fallback, because a latecomer must still be able to walk them in the world of their own stage ([the campaign](campaign.md)). The Weft simulates the foes of the front stage live and seals each stage the front leaves, so that past stages replay for those who come later; everything that is not a foe it simulates once, for every stage.

## Rules used

The `weft` group: the world tick, the tiers' cadences and the near ring, the far tick, the catch-up bound, agent, population and pin caps per region, bodies per zone, encounter detection radius, morale and supply formulas, the chronicle's size; the `events` group: provider weights, cooldowns, the active cap.
