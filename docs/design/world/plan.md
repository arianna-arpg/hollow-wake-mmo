# The living world: plan

This document plans the work that builds what the [living world contract set](overview.md) describes, in the form the [parity analysis](../parity/hollow-wake-gap.md) used: a survey of where the world stands today, rows per area with a proposal, dependencies, a size and a priority, sketches of the content, and work packages on disjoint files with a foundations package that freezes the seams first. It changes no system by itself. It is reviewed by Codex and the coordinator before any code.

Conventions used throughout:

- Code paths are relative to `src/HollowWake.Core/` unless they start with `src/`, `content/`, `tests/`, `web/` or `docs/`.
- Status: **present** means the contract's behavior exists, **partial** means the mechanism exists but misses parts, **missing** means nothing comparable exists.
- Size: **S** is a day or less for one agent in a file or two, **M** is a few days inside one subsystem, **L** is a week or more across subsystems.
- Priority: **P0** is what the owner named explicitly: sensible, exciting terrain with real rivers, mountains and caves; dense places with generated quests and things to do; unique, discoverable places of danger; a world simulation that marches armies, meets them and leaves battlefields; destructible, persistent land; and the determinism and client parity that make all of it shippable. **P1** is the rest of the contract set: weather and seasons, settlements with streets, the campaign's beats, healing, the chronicle in the interface. **P2** is later.
- Every proposal is held to `docs/VISION.md` and to the tenets on the overview page: data over code, registries never switches, events between systems, determinism from the seed, server authority for every change to the land, the client generating what the server generates, budgets as gates.
- Tool commands named in acceptance checks (`validate`, `load`, `fight`, `zone --map`, `bench`, `probe`) are the tools CLI in `src/HollowWake.Tools`, run as listed in `CLAUDE.md`.

## 1. Summary

### 1.1 Where the world stands

Generation is a pure function of the seed, the content, the hand-placed overrides and the active overlays, cached by coordinate and pinned by golden hashes. Regions take an archetype by distance window and weight, blend their lift, scale, climate biases and Thread biases bilinearly so borders never seam, and carry a level, a faction and a point-of-interest budget by tag. Climate is continental, ridge and detail noise with temperature, moisture and per-Thread density fields; water is elevation below a level; biomes are windows over those dimensions with the most specific match winning and terraced highlands making ledges. The zone planner lays a jittered grid, picks points of interest by weight and budget under biome, region, faction, elevation, slope, water-distance and spacing constraints, pins overrides first, gives each a landmark by weight, a name from a list, a faction and a level, and instantiates quest templates whose slots fill with real nearby places, patterns and items. Chunk passes write terrain, flatten and stamp landmarks (ASCII prefabs with legends; layer cells make bridges and floors; spawn cells seed props and people), scatter flora with clumping noise, and place spawn points by biome density and inhabitants. Overlays scope parameters by region, zone or circle and are added by world events and holding decrees; zones drop their caches when overlays change; chunks a simulation modified persist whole and are sent whole. The server hosts one generator and world state, schedules a weighted random event over a loaded region on a timer, persists holdings, events and modified chunks as JSON, and the desktop client generates its own terrain from the seed and overlays while the browser client requests chunks. The lore under `docs/lore` describes a world far richer than the content yet expresses: five sub-regions with named Founts, Landmarks and Knots of several kinds, five powers and three minor groups, a calendar of nine Spans, named rivers and roads.

### 1.2 The shape of the proposal

Five moves carry the contract set, each in the vocabulary the engine already has:

1. **Plans at every scale, and a ledger generation reads.** A world plan (plates, continents, belts, bands), region plans (hydrology, settlement field, routes) and zone plans (places with layouts, layers and instances) sit above the chunk passes, each a cached pure function of the plans above it and a bounded neighborhood. The persisted facts of a world live in one ledger, of which generation reads exactly four kinds: overlays, chunk edits, place states and pins. That is what keeps the client's generation identical to the server's with nothing more on the wire than today plus place states, pins and edits. The world has no edge: it generates outward as it is explored, cost follows the frontier of explored regions, and pins let the world promise things beyond it.
2. **A grammar of places.** Points of interest become place kinds that compose a site, a layout grammar, parts, inhabitants, services, quest hooks, names by culture, states and appearance ids. Layout rule types and interior grammars are registries; a place plan is cached in the zone plan and consumed by chunk passes. Insides are stacked surfaces, interior layers of the same zone, or instances keyed by a turn counter.
3. **The Weft.** A world simulation in the core, stepped by the server on a world clock in bounded tiers, deterministic from the seed and the ledger, keeping populations per explored region as stocks that breed, migrate and die out under pressure, moving agents on a generated route graph, resolving encounters through registered resolvers either abstractly or materialized in loaded zones, placing pins beyond the frontier, and writing consequences only as ledger entries and events.
4. **Edits as deltas through one path.** Every change to cells is a chunk edit with an origin, a time and a kind, persisted as a delta, replayed over regeneration, healed on the Weft's clock, sent on the wire as an edit, and made by a terrain effect family that players, monsters, structures and events share. Precedence between player and world change is a rule over origins.
5. **Registries and proofs frozen first.** Generation passes, plan kinds, overlay kinds, grammar rules, event providers, encounter resolvers, edit kinds and the world clock register from one file per area and are frozen by the foundations package together with the golden, replay and parity tests and the benchmark rows, so the packages run in parallel on disjoint files against measured budgets.

### 1.3 Who builds it, and how Opus and Codex are used

The living world session coordinates and integrates: it writes the foundations package, briefs one engineer per package, reviews each pull request against the acceptance list below, and merges. The engineers are Opus sessions in their own worktrees, one per package, with briefs that name their files, their rows and their gates; a Codex chat may claim a package in a review record instead. Codex reviews every pull request as an independent engineer and co-designs the hardest part, the Weft's model of populations, agents and encounters, through review records before that package starts.

Procedural content is the other half of the work, and Opus is used for it at scale, at authoring time, through content forges. A forge is an Opus session given one kind of content to author in bulk from the contract pages, the lore and the naming conventions (place kinds and their grammars by rung and culture, parts, names, population models, agent kinds, encounter kinds, chronicle templates, foe factions and their questlines, biome features), with `validate`, the headless tools (`place`, `zone --map`, `world --simulate`) and the golden tests as its gates, and a Codex review of samples before the batch merges. A forge never changes code; what it authors must load, validate and generate within budget, so the registries and validators the packages build are what make bulk authoring safe. Runtime generation stays deterministic from the seed and never calls a model: a world must replay the same story twice and generate the same chunk on every client, and a model's output is neither cheap nor repeatable. The chronicle's voice, the names and the quests' text come from templates a forge wrote, chosen by seed.

Two other sessions touch neighboring files: the visuals session draws what this plan generates and needs appearance ids on places, parts and props, which the places package freezes in its first commit; the combat port supplies monsters and encounter content, which agent kinds, populations and spawn tables reference by tags, never by pattern id.

## 2. Matrix

Rows are grouped by area: L the landscape, P places, F factions, events and encounters (the Weft), C the campaign, D destruction and persistence, G the pipeline, W the wire and the clients. Row ids are referenced from dependencies and from the work packages. Sketches follow in 2.8.

### 2.1 L. Landscape

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| L1 | World plan: plates with velocities, continental and oceanic, belts at converging boundaries, rifts, a land mask, climate bands | Missing: continental noise everywhere; archetypes by distance from the origin | `WorldGen/Landscape/WorldPlan.cs`: a jittered plate lattice seeded from the world seed, belt strength and land mask as point functions over the lattice, bands by the plan's latitude axis; read by `ShapeAt` and the climate formulas through the generator. The lore's edges are pins in the plan around the origin. | G1, G2 | L | P0 |
| L2 | Archetypes pick with plan context: coast, belt, interior, plus the distance window | Partial: distance windows and weights | `RegionArchetypeDef` gains `needs` (coast, belt, interior, sea) checked against the world plan at the region's center before the weighted pick. | L1 | S | P0 |
| L3 | Belts: ridge noise scaled by belt strength, foothills, a maximum height rule | Partial: ridge noise scaled by the archetype's elevation scale | `ShapeAt` multiplies the ridge term by the belt strength and adds the foothill term; `landscape.max_height` clamps; the archetype scale stays as a second dial. | L1 | M | P0 |
| L4 | Rivers traced from springs on a coarse grid with bounded length, tributaries by size, carved by a water pass with flow direction, depth, banks and fords | Missing (roadmap M2) | `Landscape/Hydrology.cs`: springs per region from seed, moisture and elevation; descending traces on a sample grid; a region's trace set cached in its plan; `Passes/WaterPass.cs` reads the traces of the regions within the longest river's reach, carves the profile from `landscape.rivers.profile`, writes water flags, `river`, `bank` and `ford` tags and a flow direction per cell. | L1, G1 | L | P0 |
| L5 | Lakes by flooding a hollow to its spill height within a bounded area; a local water level | Missing | Part of the tracer: a trace reaching a hollow floods it on the coarse grid up to `landscape.lakes.max_area`, records the lake polygon and level in the plan, and continues from the spill; the water pass sets the chunk's local water level inside a lake. | L4 | M | P0 |
| L6 | Erosion as functions: valley profiles along rivers, talus at cliff feet, damped detail on floodplains, sharpened ridges | Missing | `Landscape/Erosion.cs` as terms in the terrain pass that read the river traces and the belt strength; no iterative pass. | L4 | M | P1 |
| L7 | Coasts: beaches, cliffs, tidal flats, dunes, salt marsh, sea stacks, sea caves, harbors; a tide overlay | Partial: a shore biome at low relative elevation | `Landscape/Coasts.cs` writes coast features from slope, exposure and shelf depth as surface tags and small height terms; `tide` is an overlay kind the world clock drives; harbors are place sites with a `sheltered_water` constraint. | L1, L13 | M | P1 |
| L8 | Caves and overhangs: surfaces below the base with headroom, cave systems as bounded worms from mouths, movement that respects headroom | Missing: layers only above the ground | `Surface` gains `Headroom`; `Passes/CavePass.cs` grows worms with chambers from mouths the zone plan sites in cliffs, writing floor layers and `overburden` on the base; `Terrain.TryFindGround` and the movement system stop a jump at a ceiling and refuse a surface with less headroom than the body. | WP1 headroom, P1 | L | P0 |
| L9 | Ledges on cliff faces, passes and switchbacks with the road pass | Missing | Ledges as layers from the cave pass's sibling rule; passes as place sites on belt saddles; switchbacks from the road pass's slope limit. | L3, L12 | M | P1 |
| L10 | Biome transitions: ecotone width, a jittered match, edge tags | Missing: hard borders | `BiomeDef.Ecotone`; the terrain pass offsets the match values by a seeded noise inside the ecotone and writes `edge:<biome>` on cells whose grid neighbor differs; flora and clients read it. | none | M | P0 |
| L11 | Sub-biome features: clearings, bogs, groves, thickets, boulder fields | Missing | `BiomeDef.Features` with density, radius window and windows of their own; `Passes/FeaturePass.cs` stamps seeded blobs that override surface tags and flora inside the biome. | none | M | P1 |
| L12 | Roads and paths: a route plan per region (least-cost between places on the coarse grid), a road pass, the Loomroad pinned | Missing (roadmap M2) | `Landscape/Routes.cs` builds the region's route graph from its zone plans' places and the neighbors' hubs with costs by slope, water and biome; `Passes/RoadPass.cs` writes `road` cells, fords and bridges (a bridge is a layer stamp); authored routes are pinned waypoint lists. | P4, L4 | L | P0 |
| L13 | The world clock and the calendar: Spans, days, time of day, seasons as a global overlay | Partial: a day length and a world time on the world state | `World/WorldClock.cs` reads a `calendar` kind; the Weft applies the Span's overlay as it turns; `WorldInfo` carries the clock. | WP1 | M | P1 |
| L14 | Weather as regional states with transitions, parameters, surface tags, modifiers and presentation | Missing | `weather_kinds` content; `Landscape/Weather.cs` turns each region's state on the Weft's tick from seed and time; a weather state is an overlay of kind `weather` with the parameters and modifiers; clients draw `meta.presentation`. | L13, G3, F12 | M | P1 |
| L15 | The lore's edges as world-plan pins: the Hem sea south, the Wake west, the Girdle north, the Fray east, the Weftmarches around the origin | Partial: overrides at the origin's neighbors; girdle and fray archetypes by distance | The world plan's pins are content (`world_plan` kind): plate roles and belts around the origin; the existing region overrides stay for the five sub-regions. | L1 | S | P0 |
| L16 | Pinned traces: authored springs and waypoints for the laid rivers and the Wend | Missing | `river_pins` entries the tracer honors before seeded springs. | L4 | S | P1 |

### 2.2 P. Places

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| P1 | Place kinds compose a site, a layout, parts, inhabitants, services, quest hooks, names by culture, states and appearance | Partial: `PoiDef` with landmarks by weight, inhabitants, quest templates, one name list | `PlaceDef` as the successor of `PoiDef` (the `pois` kind stays loadable as the degenerate form): `site`, `layout`, `parts`, `inhabitants`, `services`, `quests`, `names` by culture, `states`, `meta.appearance`; validators per field. | WP1 | M | P0 |
| P2 | Layout grammars: rule types from a registry, bounded expansion, a place plan cached in the zone plan | Missing: one stamp at the center | `WorldGen/Places/PlaceGrammar.cs` expands a start symbol through rules whose types (`at`, `ring`, `along`, `fill`, `scatter`, `constraint`, `place`) register in `WorldGen/Grammars.cs`; depth and part limits from rules; output a `PlacePlan` of resolved parts that `Passes/PartPass.cs` stamps. | P1, G4 | L | P0 |
| P3 | Parts with tags, props, spawners, services, hardness and appearance ids | Missing: legend cells carry tags and one spawn | `PartDef`: a landmark or a scatter, tags, hardness, services, inhabitants, `meta.appearance`; stamped cells carry the part's tags and a `part:<id>` tag so edits, quests and the visuals find them. | P2 | M | P0 |
| P4 | Sub-places and clusters; a settlement field; density by rung | Missing | A `place` rule emits a sub-place of another kind within reach; the region plan's settlement field (`Landscape/Settlement.cs`) scales budgets; spacing counts a parent's reach for its children. | P2, L4 | M | P0 |
| P5 | The ladder as content: features, sites, settlements, complexes, landmarks with rung budgets | Partial: a handful of kinds | `content/core/world/places/*.yaml` per rung with budgets per archetype by rung tag; the lore's kinds (steads, Candles, Hospice-gates, Delves, Hollows, Wracks, Kilns, Echoes) authored. | P1 | M | P0 |
| P6 | Interior layers: a chunk grid per layer, entities with a layer, spatial grid, terrain, line of sight and interest per layer, portals | Missing | `World/TerrainLayers.cs` holds the open-air terrain and interior terrains by id; `Transform.Layer`; the spatial grid partitions by layer; `Passes` run per layer with the layer folded into every salt; `portal` interactables move a body between layers at matching coordinates. | WP1 layers seam | L | P0 |
| P7 | Instances: a zone keyed by the place and a turn; a turn opened by the first arrival and seeded by them, joined by their party and by anyone who enters while it is open unless the kind is private, ended when the heart is cut or it stands empty past a grace; Knots turn | Missing | `ZoneKey` gains an instance part; `ZoneHost` opens a turn on the first portal use and routes later entrants into it; the ledger counts turns per place (and per character for private kinds); the instance seed folds the turn and the opener. | WP1 zone keys, D9 | M | P0 |
| P8 | Interior grammars: rooms and corridors as a graph grammar, difficulty by depth, a heart, encounter and loot rooms | Missing | `WorldGen/Places/RoomGrammar.cs` lays a room graph onto the layer's grid and carves floors, walls with headroom and doors; rooms pull spawn tables by tags and depth; the heart is a part. | P6, P2 | L | P0 |
| P9 | Settlements with services by rung; towns with streets, lots, walls and gates | Missing: a hamlet stamp | Grammar rules `street`, `lot` and `wall` plus building parts by culture; services granted by rung in the kind. | P2, P3 | L | P1 |
| P10 | Unique places placed world-wide under constraints; named residents; a ledger state | Missing | `WorldGen/Places/UniquePlaces.cs`: a world-level pass sites each `unique_places` entry deterministically by constraint; the zone plan pins the result; the place has a world-wide id and a state in the ledger; a resident is an agent kind. | L1, F3 | M | P0 |
| P11 | Authored sites by constraint; protected cells | Partial: pins by coordinate | `authored_sites` entries resolved by the unique-place pass; stamp cells may carry `protected`. | P10 | S | P1 |
| P12 | Place states with overrides per state; a ruin from another kind's burned state | Missing | `states` on the kind; the zone plan reads the state from the ledger's place states and applies the override before layout; the ruin kind may name a source kind. | P1, WP1 ledger | M | P0 |
| P13 | Quest hooks on parts; templates per kind and per state | Partial: templates per kind | `PartDef.Hooks` list objectives a template may bind (`$part`); state overrides swap the template list. | P3 | S | P0 |
| P14 | Objective kinds: clear, hold, escort, deliver, claim, recover, scout, hunt | Partial: kill, collect, reach, discover, interact, craft, bind, event, level, cast | Registrations in `Quests/Objectives.cs` fed by place, Weft and edit events; `hunt` binds to an agent id, `clear` and `hold` to a place state. | WP1 events | M | P0 |
| P15 | Danger and reward budgets by rung and depth; loot by place tags | Missing | `reward` on the kind as expressions over `rung` and `depth`; loot tables read the place's tags through the drop context. | P5 | S | P1 |
| P16 | Discovery by rumor and map marks | Partial: by walking and by the marker | A `rumor` dialogue effect names a place from the zone plan within `places.rumor_range` and raises `PoiDiscovered` as rumored; the map message carries marks. | W6 | S | P1 |
| P17 | Names by culture from the naming conventions | Partial: one list per kind | `names` keyed by culture with the region's culture chosen from its faction and archetype; validation warns on words to avoid. | P1 | S | P1 |
| P18 | Claims: claimable place kinds a player takes and ties to a Holding; a footprint inside which edits stick and a township can grow | Missing | `claimable` on the kind with a `claim` footprint and the rite or deed that takes it; `ClaimIntent` through the territory service; the claim's state and footprint in the ledger; the destruction package anchors edits inside it (D11). | P1, P12 | M | P0 |
| P19 | Stacking: an instance's inhabitants, encounter rooms and reward budgets scale with the players inside by rule | Missing | `places.stacking` expressions over the occupant count read when rooms populate and when a turn's spawners refresh; the encounter resolvers read the same count when they materialize. | P7, P8 | S | P0 |
| P20 | Bounty boards: a settlement part that generates quests per character from the board's seed, the character and the board's refresh, aimed at shared places, Knots and agents within reach | Missing | A `board` part kind whose interactable asks the quest service for the character's bounties; templates with place, agent and Knot slots; the refresh on the world clock; the quests are the character's, the targets the world's. | P13, P14 | M | P1 |

### 2.3 F. Factions, events and encounters

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| F1 | The Weft: a world simulation service in the core, stepped by the host on a world tick, deterministic from the seed and the ledger | Missing: a timer picks a weighted random event over a loaded region | `Weft/WeftSimulation.cs` with its own clock, seeded stream and ledger view; `Server/World/WorldHost.cs` steps it; `hollowwake world --simulate` runs it headless. | WP1 | L | P0 |
| F2 | Faction standing per region from holdings, places and agents; goals as content | Partial: factions with relations; a region's faction picked at generation | `Weft/Standing.cs` folds the ledger into standing per faction per region each tick; `FactionDef` gains `goals`; generation reads standing through place states and tag overlays, never directly. | F1 | M | P0 |
| F3 | Agents: kinds as behaviors with world perception and a world action registry | Missing | `agent_kinds` content reusing the behavior schema; `Weft/Agents.cs`, `Weft/AgentActions.cs` (march_to, raid, besiege, hold, trade, forage, hunt, flee, disband, found_camp); strength as a pattern budget by tags. | F1 | L | P0 |
| F4 | A route graph from the region plans | Missing | `Weft/RouteGraph.cs` reads the route plans of the regions in the Weft's tiers lazily. | L12 | M | P0 |
| F5 | Encounter detection and a resolver registry; abstract resolution tuned against materialized fights | Missing | `Weft/Encounters.cs` detects meetings per tick; `encounter_kinds` content picks a resolver by tags; `Weft/Resolvers.cs` registers `pitched_battle`, `raid`, `siege`, `hunt`, `ambush`; the balance harness compares abstract and materialized outcomes. | F3, F4 | L | P0 |
| F6 | Materialization in loaded zones staged by interest within a body budget, outcomes reported by events | Missing | `Systems/MaterializationSystem.cs` in the zone reads the Weft's inbox, spawns strength as packs through spawners near the agents' positions as players approach, and reports deaths, routs and captures back through the outbox. | F5, G12 | L | P0 |
| F7 | Consequences as ledger entries only: overlays, edits, place states, agents, chronicle | Missing: an event adds one overlay | `encounter_kinds.consequences` and `world_events.phases` list entries; `Weft/Consequences.cs` writes them through the ledger's edit and overlay paths. | WP1 ledger, D1 | M | P0 |
| F8 | Event providers registry: calendar, faction, territory, campaign, random | Partial: one random scheduler | `Weft/Providers.cs`; each provider proposes events with a weight; the Weft picks under caps and cooldowns from `events` rules. | F1, G6 | M | P1 |
| F9 | Event kinds with triggers, scopes, phases, conditions; unique events once per world | Partial: duration, a region scope, generation, spawns, modifiers, quests | `WorldEventDef` gains `provider`, `when`, `unique`, `phases`; the ledger remembers unique events. | F8 | M | P1 |
| F10 | The chronicle: templated text in the lore's voice, persisted, surfaced | Missing | `chronicle_templates` content; `Weft/Chronicle.cs` appends entries with a time and a place; the server sends them as notices and map marks and dialogue reads them as rumors. | F7 | M | P1 |
| F11 | Tiers: observed, near, far and latent, drawn around the players and the frontier; a catch-up bound; agent, population and pin caps | Missing | `weft` rules; the Weft steps regions by tier (every tick, every tick abstractly, a coarse step per world day, not at all); `WorldHost` catches up on restart within the bound. | F1, F18 | M | P0 |
| F12 | Scope modifiers: entities inside an overlay's scope get its modifiers through one system | Partial: holdings apply theirs; events' modifiers are declared but unapplied | `Systems/ScopeModifierSystem.cs` applies and removes modifiers under source `scope:<overlay>` for events, weather and decrees; the territory system hands its application over. | G3 | S | P1 |
| F13 | Quest offers from encounters and events; quests the world overtakes resolve by rule | Missing | Templates with agent and place slots raised at the nearest hub by the Weft's events; `quests.overtaken` rule: complete, withdraw or retarget. | P14 | M | P1 |
| F14 | Named residents of unique places as agents; lairs | Missing | A unique place's `resident` names an agent kind; the agent is created with the place and lives at it between sorties. | F3, P10 | M | P0 |
| F15 | Caravans and trade; prices that move | Missing | Later: a `trade` action and vendor stock from the ledger. | F3 | M | P2 |
| F16 | Sieges and contests of holdings as events | Missing (the territory contract defers them) | Later: a `siege` encounter kind over a holding's structures through the edit path. | F9, D4 | L | P2 |
| F17 | Populations: stocks per explored region and kind that breed, eat, roam, migrate along routes under pressure, recover and die out; read by spawn tables through density multipliers; founding and abandoning places through pins | Missing: spawn densities are fixed per biome | `population_models` content; `Weft/Populations.cs` steps stocks per tier from deeds the ledger counts (kills by tag per region, camps burned, Snarls cleared), from other stocks, from the season and from events; `SpawnPass` and inhabitants read multipliers by tag; agents muster from stocks and return to them. | F1, F4 | L | P0 |
| F18 | The frontier and the latent tier: the set of regions any player has entered, in the ledger; latent regions never simulated, built or stored; the frontier moves with the first player across it | Missing | `World/Ledger.cs` frontier entries written by `ZoneHost` on first entry; the Weft's tiers read it; plan caches and the ledger are bounded by it (F20). | WP1 | S | P0 |
| F19 | Pins placed at run time: an event, a population or a beat promises a place beyond the frontier by direction and distance, or sites one inside it; rumor and chronicle speak of it; generation honors it on first reach | Missing | The `pins` kind of the generation ledger with origin, constraint or coordinate, kind and expiry; `WorldGen/Places/Pins.cs` resolves authored and runtime pins alike; `WorldInfo` carries pins and `pin` messages carry changes; capped per region. | WP1, P10 | M | P0 |
| F20 | Bounded cost: plan caches keep the observed and near tiers and drop the farthest first; the far tier's step is a few numbers per region; nothing is built for latent land | Partial: caches grow without bound | `Plans` caches with a bound from `weft` rules and tier-aware eviction; the `worldgen.frontier` and `weft.ecology` benchmark rows. | G2, F11 | M | P0 |

### 2.4 C. The campaign

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| C1 | Beats as content: chapter, personal or worldly, gates, requests, offers, conclusions | Missing: story flags and prerequisites on quests | `campaign_beats` kind, `Campaign/CampaignService.cs` evaluating personal beats per character and worldly beats on the Weft's tick. | WP1 | M | P1 |
| C2 | Gates over flags, discoveries, reputation, level, ledger facts, the calendar and other beats | Partial: quest prerequisites over flags | Expression functions `discovered()`, `deed()`, `beat()` and a `ledger.*` scope in `Campaign/Gates.cs`. | C1 | S | P1 |
| C3 | Requests through providers, resolved deterministically and recorded in the ledger | Missing | `Campaign/Requests.cs` asks the unique-place pass, the event providers and the agent factory with the beat's constraints; the choice is a ledger entry keyed by beat. | P10, F8 | M | P1 |
| C4 | Fail-forward policies; validation of hanging beats | Missing | `fallback` on every request; the validator walks the beat graph for requests without a fallback and gates that nothing can set. | C3 | S | P1 |
| C5 | The first chapter authored from the lore | Missing: two starter quests | `content/core/world/campaign/ninewells.yaml` and `the-loosening.yaml`, with dialogue lines keyed to beats. | C1 | M | P1 |
| C6 | Worldly beats advancing by collective deeds | Missing | The Weft counts deeds into the ledger; worldly gates read them. | C1, F1 | M | P1 |
| C7 | A `campaign` tool | Missing | Lists open beats, gate values and resolved requests. | C1 | S | P1 |
| C8 | Foe factions: a content pool of hostile powers, each with agent kinds, populations, place kinds, lieutenants and a questline; a world draws a rule's number of them by its seed, pins their homes beyond the start region's horizons and seeds their populations | Missing: hostile factions exist without a campaign | `foe_factions` content; `Campaign/Foes.cs` draws at world creation and writes the draw to the ledger; the pins and populations go through F19 and F17. | C1, F17, F19 | M | P0 |
| C9 | Each foe's questline authored in its beats and generated in everything they touch: stronghold, captain, camps, agents, quest slots | Missing | Beats under the faction with requests resolved through providers (C3) against the faction's own kinds; the stronghold is a unique place pinned at the draw. | C8, C3 | L | P0 |
| C10 | Order by player choice; escalation by rule when a foe falls; the chapter's end waits for the number the rule names | Missing | Every drawn line opens on the discovery of its first place; `campaign.on_foe_defeated` rules move survivors, hand country to a rival and shift the others' goals; the finale beat's gate counts defeats. | C9, F2 | M | P1 |
| C11 | The line: a world's ordered list of lost foes, extended by the character at the front; each character's progress a prefix of it; the front chooses freely, the rest follow the precedent | Missing | `Campaign/Line.cs` over a ledger entry; a foe's final beat is gated on the foe being the world's next loss, or the world having no next; the `campaign` tool prints the line and each character's position. | C8, C9 | M | P0 |
| C12 | Phased completion for latecomers: a lost foe's questline walked through its instances and remnant pins, never the open world; validation that every foe questline completes from instances and pins alone | Missing | Beat fallbacks of kind `remnant` that raise a private instance from the faction's own kinds; the validator walks each questline with the open world removed and reports one that cannot finish. | C11, P7, F19 | L | P0 |
| C13 | Replacement foes and memorials: when a foe falls the pool may raise a new one into the emptied country by rule and weight, so the line never runs out; a memorial pin and the chronicle name the exterminator | Missing | `campaign.on_foe_defeated.raise` rule drawing as at world creation; a `memorial` pin kind written with the chronicle entry. | C8, C10 | M | P1 |

### 2.5 D. Destruction and persistence

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| D1 | Chunk edits as deltas with origin, time and kind; regenerate and replay; compaction | Partial: whole modified chunks are saved and sent | `World/ChunkEdits.cs`: `ChunkEdit` records and an `Apply` that both the simulation and the client use; the provider regenerates and replays; the store compacts past `destruction.compact_after_edits`. | WP1 edits shell | L | P0 |
| D2 | A terrain effect family: dig, raise, break, burn, place, flood | Missing | `Effects/Handlers/TerrainHandlers.cs` with signatures and hooks; each validates against hardness, bounds and protection and emits an edit. | D1 | M | P0 |
| D3 | Hardness, bounds and protection by tags and rules | Missing | `destruction` rules; stamp cells and patterns carry hardness and `protected`. | D2 | S | P0 |
| D4 | Structure destruction: parts fall to rubble; holdings raze | Partial: stamping only | A part's cells share a hardness pool; spent, the part's `fallen` form stamps as an edit; `RazeStructureIntent`. | D2, P3 | M | P1 |
| D5 | Healing: expiry and regrowth as reversing edits on the Weft's clock | Missing | Edit kinds declare heal rules; the Weft emits reversing edits for unanchored ones. | D1, F1 | M | P1 |
| D6 | Precedence rules between origins | Missing | `destruction.standing` and the anchor rules evaluated when an edit is made. | D1 | S | P1 |
| D7 | `chunk_edit` on the wire with a whole-chunk fallback; the client replays | Partial: whole `chunk_data` | `ChunkEditMsg` in `Protocol/Messages.World.cs`; `Server/World/ChunkStreaming.cs` sends edits within interest and falls back to `chunk_data` past a threshold; both clients apply edits with the shared `Apply`. | D1 | M | P0 |
| D8 | The ledger store: one interface, JSON files today, a database later | Partial: `world.json` plus a file per modified chunk | `Server/Persistence/LedgerStore.cs` behind `ILedgerStore` with entries by kind and key; the JSON implementation keeps one file per kind and per chunk. | WP1 | M | P0 |
| D9 | Per-player discoveries of layers and instance turns | Partial: discoveries of places | The sheet's discoveries carry layer ids; a per-character turn map rides on the sheet. | P6, P7 | S | P0 |
| D10 | Encounter state reported on unload and continued abstractly | Missing | The materialization system writes the encounter's remaining strength to the outbox when the zone unloads. | F6 | S | P1 |
| D11 | Passing by default, lasting on purpose: every edit outside a claim or a Holding carries an expiry by kind and heals to the generated land; edits inside a claim's footprint are anchored and stick | Missing | Edit kinds declare their expiry; `ChunkEdits.Apply` reads the claims of the ledger to anchor; the healing of D5 skips anchored edits; a `battlefield` kind may be declared lasting by the place's kind. | D1, D5, P18 | M | P0 |

### 2.6 G. The pipeline

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| G1 | A generation pass registry per scale, ordered by rule | Missing: fixed calls in `GenerateChunk` | `WorldGen/Passes.cs`: `GenerationPasses.Register(scale, name, pass)`; `world.gen.passes` orders each scale; today's four passes register under their names and the order reproduces today's output exactly. | WP1 | M | P0 |
| G2 | Plan kinds and caches with warming | Partial: region and zone caches | `WorldGen/Plans.cs`: a plan kind registers a builder; `CoordCache` holds them; the server warms the plans of the zones around players on a worker. | G1 | M | P0 |
| G3 | An overlay kind registry | Partial: sum and override chosen by parameter name in code | `WorldGen/OverlayKinds.cs`: `sum`, `override`, `max`, `tags`; `GenerationOverlays.Param` and `Sum` dispatch through it; the climate grid's `OverlayParam` too. | WP1 | S | P0 |
| G4 | A grammar rule registry | Missing | `WorldGen/Grammars.cs`. | WP1 | S | P0 |
| G5 | An encounter resolver registry | Missing | `Weft/Resolvers.cs` registration surface frozen by WP1. | WP1 | S | P0 |
| G6 | An event provider registry | Missing | `Weft/Providers.cs` registration surface frozen by WP1. | WP1 | S | P0 |
| G7 | An edit kind registry | Missing | `World/ChunkEdits.cs` registration surface frozen by WP1. | WP1 | S | P0 |
| G8 | Golden tests per package and the change rule | Partial: one golden file | A golden file per package; `WorldGenGoldenTests.cs` frozen; a changed constant is named in the pull request with the reason and the before and after maps. | WP1 | S | P0 |
| G9 | A Weft replay test, with deeds injected | Missing | `WeftReplayTests.cs`: the chronicle and ledger of a seeded run are byte-identical twice, with and without a scripted deed sequence. | F1 | M | P0 |
| G10 | A client parity test from the world information | Missing | `WorldParityTests.cs`: a generator built from a `WorldInfo` (seed, layout, content hash, overlays, place states, pins) hashes the same plans and chunks as the server's, with edits replayed. | WP1 | S | P0 |
| G11 | Benchmark rows and budgets | Partial: zone plan, chunk, sample | The rows of the pipeline page added to `Bench.cs` with first measurements entered in `performance.md`. | WP1 | S | P0 |
| G12 | The world-thread and zone seam: inbox, outbox, a ledger snapshot | Missing | `Weft/ZoneLink.cs`: per-zone queues drained on each side's tick; a zone takes a read-only Weft view when it loads; the ledger is written only on the world thread. | F1 | M | P0 |
| G13 | Tools: place, world --simulate, chronicle, campaign, ledger, layer | Partial: region, zone, world, load | `Tools/WorldReport.cs`. | each area | M | P1 |

### 2.7 W. The wire and the clients

| ID | Contract | Today | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| W1 | World information carries the clock, the calendar, weather, place states and pins | Partial: seed, layout, overlays, content hash | `WorldInfo` gains `clock`, `calendar`, `weather`, `place_states` and `pins`; `world_clock`, `place_state` and `pin` messages carry changes. | L13, P12, F19 | S | P0 |
| W2 | Layers and instances on the wire: a layer on entity states and chunk messages; `request_chunk` takes a layer | Missing | Fields added to the entity state, the delta, `chunk_data`, `chunk_edit` and `request_chunk`; absent means the open-air layer, so an old client is unaffected. | P6 | M | P0 |
| W3 | Appearance ids on places, parts and props for the visuals | Missing | `meta.appearance` keys frozen by the places package and recorded for the visuals session; the marker and prop states carry them as `look` already does. | P3 | S | P0 |
| W4 | The desktop client generates layers and places, draws caves with ceilings, ledges, water flow, weather and the season | Partial: terrain from the seed, stamps, water plane | `src/HollowWake.Client/World/*` after the visuals pull request lands: per-layer chunk stores, ceiling quads from headroom, flow and sky from the overlays. | L8, P6, L14 | L | P0 |
| W5 | The browser client requests layered chunks and draws the same | Partial | `web/src/world/*` likewise. | W2 | M | P1 |
| W6 | A map of discovered places, the chronicle and rumors in the interface | Missing | A `map_state` message and a map page under the interface contract; chronicle notices. | F10 | M | P1 |

### 2.8 Sketches

A place kind on the settlement rung with a layout, parts, states and names by culture. A `place` rule emits a sub-place; a part's cells carry its tags and hardness.

```yaml
kind: places
entries:
  - id: stead
    name: Stead
    rung: settlement
    tags: [settlement, stead, hub]
    site: { biomes: [meadow, heath, forest], slope: { max: 0.4 }, water_distance: { min: 3, max: 60 }, radius: 12, min_spacing: 120, flatten: true }
    layout:
      start: stead
      rules:
        - symbol: stead
          expand:
            - { part: farmhouse, at: center }
            - { part: barn, ring: { radius: 6, count: 1 } }
            - { part: field, fill: { radius: 11, exclude: [structure] } }
            - { place: well, within: 8, chance: 0.6 }
            - { place: shrine, within: 24, chance: 0.3 }
    parts:
      farmhouse: { landmark: marcher_house, tags: [house, wood, breakable], hardness: 3, services: [vendor.steadwife], meta: { appearance: { kind: house, culture: marcher, age: new } } }
      barn: { landmark: barn, tags: [barn, wood, breakable], hardness: 2 }
      field: { scatter: { pattern: wheat, density: 30 }, tags: [field, crop] }
    inhabitants:
      - { pattern: villager, density: 3, faction: weftmarch_compact }
    quests: [gather_floss, cull_beasts, clear_knot]
    names: { marcher: [Ashdown, Tallowcombe, Burrstead, Coppice Bottom], ilvarene: [Corbenne, Maurenne] }
    states:
      burned: { layout: { start: ruin_of_stead }, inhabitants: [], quests: [rebuild_stead], meta: { appearance: { age: burned } } }
      occupied: { inhabitants: [ { pattern: loosed_brigand, density: 4, faction: the_loosed } ], quests: [free_stead] }
```

A complex whose inside is a private instance that turns, with a room grammar and a reward budget by depth.

```yaml
  - id: snarl
    name: Snarl
    rung: complex
    tags: [knot, snarl, dungeon]
    site: { regions: [hallows, sallows, emberfells], slope: { max: 0.8 }, radius: 9, min_spacing: 110, flatten: false }
    layout:
      start: mouth
      rules:
        - { symbol: mouth, expand: [ { landmark: knot_stones, at: center }, { portal: { to: inside, kind: tangle_gate } } ] }
    inside:
      mode: instance
      shared: false
      turn: per_character
      grammar: rooms
      rooms:
        start: entry
        max_depth: 7
        rules:
          - { symbol: entry, expand: [ { room: antechamber }, { corridor: { to: gallery, length: [4, 9] } } ] }
          - { symbol: gallery, expand: [ { room: encounter, tables: [tangled], depth: 1 }, { corridor: { to: heart, chance: 0.4 } }, { corridor: { to: gallery, chance: 0.6 } } ] }
          - { symbol: heart, expand: [ { room: snag, part: snag } ] }
    reward: { danger: "2 + depth", budget: "40 * (1 + depth * 0.5)" }
```

A biome patched with an ecotone and features; a landscape rule group; the pass order per scale.

```yaml
kind: biomes
entries:
  - id: forest
    patch: true
    ecotone: 6
    features:
      - { id: clearing, density: 0.4, radius: [4, 9], surface: [grass, meadow], flora: [ { pattern: shrub, density: 0.5 } ] }
      - { id: bog, density: 0.15, radius: [3, 6], surface: [bog], moisture: { min: 0.6 } }
---
kind: rules
entries:
  - id: landscape
    values:
      plates: { cell: 6144, jitter: 0.45, continental_chance: 0.4 }
      belts: { width: 900, height: 120, foothills: 2200 }
      max_height: 160
      rivers: { spring_spacing: 256, min_moisture: 0.45, max_length: 6144, grid: 16, profile: "clamp(size * 0.6, 1, 8)" }
      lakes: { max_area: 4096 }
      caves: { mouth_slope: { min: 0.8 }, worm_length: [24, 160], headroom: [2.2, 5] }
      ecotone_default: 4
      calendar: { span_days: 40, selvage_days: 5, day_seconds: 1800, spans: [lumen, tide, storm, bloom, ember, stone, umbra, mind, void] }
  - id: world
    patch: true
    values:
      gen:
        passes:
          world: [plates, continents, belts, bands, unique_places]
          region: [hydrology, settlement, routes]
          zone: [places, layouts, quests, layers]
          chunk: [terrain, water, roads, caves, features, landmarks, parts, flora, spawns, edits]
```

An agent kind as a behavior over world perception, an event raised by the faction provider, and an encounter kind with its resolver and consequences.

```yaml
kind: agent_kinds
entries:
  - id: loosed_warband
    faction: the_loosed
    strength: { patterns: { tags: [loosed], budget: "12 + 2 * region_level" }, leader: { tags: [loosed, caster] } }
    supplies: 6
    targets: { places: { tags: [settlement], factions: [weftmarch_compact], within: 4 } }
    behavior:
      initial: muster
      states:
        muster: { tick: [ { action: wait, days: 1 } ], transitions: [ { to: raid, when: "supplies > 2 && target_distance < 3" }, { to: forage, when: "supplies <= 2" } ] }
        raid: { enter: [ { action: march_to, target: target } ], transitions: [ { to: muster, when: "arrived && outcome == 'won'" }, { to: flee, when: "morale < 0.3" } ] }
        forage: { enter: [ { action: forage } ], transitions: [ { to: muster, when: "supplies > 4" } ] }
        flee: { enter: [ { action: march_to, target: home } ], transitions: [ { to: disband, when: "strength < 0.2" } ] }
---
kind: world_events
entries:
  - id: iron_crusade
    name: The Iron Crusade
    provider: faction
    unique: true
    scope: region
    when: "standing.lantern_chancery >= 0.6 && deeds.founts_chartered >= 3"
    phases:
      - { id: muster, days: 3, agents: [ { kind: chancery_column, at: { place_tags: [candle] }, count: 2 } ], chronicle: crusade_musters }
      - { id: march, until: "agents_resolved", quests: [escort_column, hold_the_road] }
      - { id: aftermath, days: 20, generation: { tag: scorched }, place_states: { tags: [camp, loosed], state: burned } }
---
kind: encounter_kinds
entries:
  - id: field_battle
    when: { hostile: true, both: [army, warband] }
    resolver: pitched_battle
    materialize: { bodies_per_tick: 12, max_bodies: 160, stage_by_interest: true }
    consequences:
      - { edit: battlefield, radius: "6 + strength * 0.2", expires_days: 30 }
      - { overlay: { tag: trampled }, radius: 12, expires_days: 10 }
      - { chronicle: battle_fought }
```

A personal beat and a worldly beat. Requests name kinds and constraints with a fallback; conclusions write flags and the ledger.

```yaml
kind: campaign_beats
entries:
  - id: the_bottom_field
    chapter: ninewells
    personal: true
    gates: 'has("quest.completed.first_threads") && discovered("poi.0_0.hamlet")'
    requests:
      hob: { place: { tags: [knot, tangle], within: 2, of: start }, fallback: { substitute: [knot], within: 4 } }
    offers: { quests: [the_hob_in_the_bottom_field], dialogue: { speaker: quartermaster, line: maud_about_the_hob } }
    concludes: { flags: [chapter.ninewells.hob], opens: [sev_is_spoken_of] }
  - id: skyfold_wakes
    chapter: the_loosening
    worldly: true
    gates: "deeds.snarls_cleared.emberfells >= 20 && calendar.span == 'storm'"
    requests:
      vigil: { event: { kind: contested_vigil, at: { unique: skyfold } } }
    concludes: { place_states: { skyfold: contested }, chronicle: skyfold_wakes }
```

Destruction rules and a digging effect in a weave's graph.

```yaml
kind: rules
entries:
  - id: destruction
    values:
      hardness: { soil: 1, sand: 0.5, bog: 0.5, rock: 4, stone: 6, wood: 2, roadstone: 0, lantern_steel: 0 }
      dig_depth: 6
      raise_height: 4
      bedrock: -12
      protected: [protected, firstwork, hub_heart]
      heal:
        crater: { after_days: 30, over_days: 20 }
        burned: { after_days: 5, over_days: 40 }
        rubble: { after_days: 90, over_days: 90 }
      standing: { holding: 3, event: 2, weft: 2, player: 1 }
      compact_after_edits: 200
---
effects:
  - { type: dig, amount: "weave.base_damage * 0.05", radius: "weave.area.radius", shape: circle, on: [soil, sand, bog] }
```

## 3. Implementation plan

### 3.1 Order and rules

One foundations package (WP1) lands first and alone. It freezes the shared seams: the pass, plan, overlay-kind, grammar, resolver, provider and edit-kind registries; the ledger's shape and its generation slice; headroom on surfaces and a layer on bodies; the world clock; the file splits below; the golden, replay and parity tests; and the benchmark rows with their first numbers in `performance.md`. It changes no generated output and no trace. Then five packages run in parallel on disjoint files: WP2 the landscape, WP3 places, WP4 the Weft, WP5 the campaign and quests, WP6 destruction and persistence, which lands its world message shapes first so that WP4 and WP7 build against them. WP7, the wire, the clients and the tools, runs against those shapes and after the visuals pull request lands on main. Each package does its P0 rows first and its P1 rows after its P0 acceptance passes.

Rules for every package:

- Never edit a file another package owns. Cross-package additions go through the registries WP1 freezes.
- New content goes in package-owned files; rule groups use new ids (`landscape`, `weather`, `places`, `weft`, `events`, `campaign`, `destruction`) so no two packages write the same rules entry; `content/core/rules.yaml` is frozen.
- New events go in a package-owned file under `Sim/Events/`; `Sim/Events.cs` is frozen.
- New definition fields go in the package's own partial definition file; `Definitions/World.cs` is frozen.
- New tests go in package-owned test files. `WorldGenGoldenTests.cs` is frozen; a package whose change legitimately moves an existing golden constant names it in its pull request with the reason and the before and after `zone --map` output, and the reviewer checks that the start zone still loads and walks.
- Every package proves determinism: its golden file, the replay test where it touches the Weft, the parity test where it touches generation, and byte-identical `load` and `fight` traces where it claims no behavior change.
- Every package stays within the budgets: `bench --only worldgen` and `--only weft` in Release, and the zone tick, per `performance.md`.
- Every package keeps both clients' local generation identical to the server's: the parity test is part of its gates, and a package that adds something the client must know adds it to the generation ledger or to the world information, never to a side channel.
- When a package needs a change in a file it does not own, the coordinator sequences it with the owner. Files shared with other sessions (the movement system, the pathfinder, the spatial grid, line of sight, the client world folders) are changed only through the coordinator.
- The verification gates in `CLAUDE.md` apply to every package.

### 3.2 File ownership after WP1

| File | Owner |
|---|---|
| `WorldGen/WorldGenerator.cs`, `BiomeTable.cs`, `ClimateGrid.cs` | WP2 |
| `WorldGen/Passes/TerrainPass.cs`, `FloraPass.cs` (split from `ChunkPasses.cs` by WP1), new `WaterPass.cs`, `RoadPass.cs`, `CavePass.cs`, `FeaturePass.cs` | WP2 |
| `WorldGen/Landscape/*` (new) | WP2 |
| `World/Terrain.cs` (headroom in the ground query) | WP2 |
| `WorldGen/ZonePlanner.cs`, `WorldGen/Grammars.cs`, `WorldGen/Passes/LandmarkPass.cs`, `SpawnPass.cs`, new `PartPass.cs`, `WorldGen/Places/*` (new) | WP3 |
| `World/TerrainLayers.cs` (new), `Sim/ZoneLoader.cs` (the `LoadZone` half of `ZoneBuilder`, moved by WP1), `Interaction/InteractionService.cs`, `Server/World/ZoneHost.cs` | WP3 |
| `Weft/*` (new), `Territory/Territory.cs`, `Server/World/WorldHost.cs` (world events and the Weft step, extracted by WP1), `Server/Config/ServerConfig.cs`, new `Systems/MaterializationSystem.cs`, `Systems/ScopeModifierSystem.cs` | WP4 |
| `Campaign/*` (new), `Quests/*` | WP5 |
| `World/ChunkEdits.cs` (the `TerrainEdits` of `Territory.cs`, moved by WP1), `Effects/Handlers/TerrainHandlers.cs` (shell by WP1), `Server/World/ChunkStreaming.cs` (the chunk send path, extracted by WP1), `Server/Persistence/*`, `Protocol/Messages.World.cs` (the world messages, moved by WP1), `web/src/net/protocol.ts` (regenerated) | WP6 |
| `Server/World/ZoneInstance.cs`, `Snapshots.cs`, `SnapshotBuilder.cs`, `Tools/Program.cs`, `Tools/Bench.cs`, new `Tools/WorldReport.cs`, `src/HollowWake.Client/World/*`, `web/src/world/*`, `web/src/app.ts` | WP7 |
| `WorldGen/Passes.cs`, `Plans.cs`, `OverlayKinds.cs`, `GenerationOverlays.cs`, `CoordCache.cs`, `World/ChunkData.cs`, `WorldLayout.cs`, `WorldClock.cs`, `Ledger.cs`, `Components/Body.cs`, `Sim/ZoneBuilder.cs`, `Sim/Events.cs`, `Definitions/World.cs`, `Content/BuiltinKinds.cs`, `Effects/BuiltinEffects.cs`, `Protocol/Messages.cs`, `TypeScriptContract.cs`, `Characters/CharacterSheet.cs`, `content/core/rules.yaml`, `tests/HollowWake.Core.Tests/WorldGenGoldenTests.cs` | frozen after WP1; changes through the coordinator |
| `Definitions/World.Landscape.cs`, `Content/Kinds/LandscapeKinds.cs`, `Sim/Events/LandscapeEvents.cs`, `World/Ledger.Landscape.cs` | WP2 |
| `Definitions/World.Places.cs`, `Content/Kinds/PlaceKinds.cs`, `Sim/Events/PlaceEvents.cs`, `World/Ledger.Places.cs` | WP3 |
| `Definitions/World.Weft.cs`, `Content/Kinds/WeftKinds.cs`, `Sim/Events/WeftEvents.cs`, `World/Ledger.Weft.cs` | WP4 |
| `Definitions/World.Campaign.cs`, `Content/Kinds/CampaignKinds.cs`, `Sim/Events/CampaignEvents.cs`, `World/Ledger.Campaign.cs` | WP5 |
| `Definitions/World.Destruction.cs`, `Content/Kinds/DestructionKinds.cs`, `Sim/Events/DestructionEvents.cs`, `World/Ledger.Edits.cs` | WP6 |
| `content/core/world/biomes.yaml`, `regions.yaml`, new `landscape/*.yaml`, `rules-landscape.yaml` | WP2 |
| `content/core/world/pois.yaml`, `landmarks.yaml`, new `pins.yaml` (the zone overrides, moved by WP1), `places/*.yaml`, `rules-places.yaml` | WP3 |
| `content/core/world/events.yaml`, `content/core/factions.yaml`, `content/core/play/territory.yaml`, new `weft/*.yaml`, `rules-weft.yaml` | WP4 |
| `content/core/play/quests.yaml`, `content/core/dialogue/*`, new `campaign/*.yaml`, `rules-campaign.yaml` | WP5 |
| new `content/core/world/destruction.yaml` | WP6 |
| `tests/HollowWake.Core.Tests/LandscapeGoldenTests.cs` | WP2 |
| `PlaceGoldenTests.cs`, `LayerTests.cs` | WP3 |
| `WeftReplayTests.cs`, `EncounterTests.cs` | WP4 |
| `CampaignTests.cs`, `ObjectiveTests.cs` | WP5 |
| `DestructionTests.cs`, `tests/HollowWake.Server.Tests/ChunkEditTests.cs`, `LedgerStoreTests.cs` | WP6 |
| `tests/HollowWake.Server.Tests/WorldMessageTests.cs`, the client and web unit tests, the smokes | WP7 |
| `docs/design/world/landscape.md` | WP2 |
| `docs/design/world/places.md` | WP3 |
| `docs/design/world/factions-events-encounters.md` | WP4 |
| `docs/design/world/campaign.md`, `docs/design/quests.md` | WP5 |
| `docs/design/world/destruction-and-persistence.md`, `docs/design/networking-and-persistence.md`, `docs/design/territory.md` | WP6 |
| `docs/design/client.md`, `docs/design/web-client.md` | WP7 |
| `docs/design/world/overview.md`, `pipeline-and-budgets.md`, `plan.md`, `docs/design/performance.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md` | the coordinator |

Rows A13 and the like in the parity analysis are untouched by this plan; `MovementSystem.cs` receives one change (the ceiling check, L8) sequenced by the coordinator with the sessions that own movement work.

### 3.3 Work packages

#### WP1 Foundations (lands first, one agent)

Goal: every shared seam exists, generated output and traces are unchanged, and the other packages can work without touching each other's files.

Rows: G1, G2, G3, G4, G5, G6, G7, G8, G10, G11, G12 (the queues as shells), F18 (the frontier entries), the pins kind of F19 as a ledger shell, the schema shells for L13, P1, P6, P7, D1, D8, W1 and W2; the file splits of 3.2.

Owns, then hands over as noted:

- `WorldGen/Passes.cs` (the pass registry with scales; `GenerateChunk` and the zone and region builders dispatch through it; today's passes registered under their names with `world.gen.passes` reproducing today's order), `WorldGen/Plans.cs` (plan kinds over `CoordCache`), `WorldGen/OverlayKinds.cs` (`GenerationOverlays.Param`, `Sum` and the climate grid's `OverlayParam` dispatch through it, with today's names mapped to today's behavior), `WorldGen/Grammars.cs` (the registry and `PlacePlan` record, afterwards WP3); `ChunkPasses.cs` split into `WorldGen/Passes/TerrainPass.cs`, `FloraPass.cs` (WP2), `LandmarkPass.cs`, `SpawnPass.cs` (WP3).
- `World/ChunkData.cs` (`Surface.Headroom` defaulting to unbounded; a `Layer` id; an `Edits` list), `World/WorldClock.cs` (the calendar from a `calendar` kind, with today's day length as its default), `World/Ledger.cs` (entry base with origin, time and kind; the generation slice: overlays, edits, place states, pins; the frontier; partial files for each area with empty shells), `World/TerrainLayers.cs` (the open-air terrain plus a dictionary of interior terrains; afterwards WP3), `World/ChunkEdits.cs` (`TerrainEdits.Stamp` moved here unchanged, with an edit-kind registry shell; afterwards WP6); `Components/Body.cs` (`Transform.Layer`, default open air).
- `Definitions/World.cs` made partial with the area files of 3.2 as empty partials; `Content/BuiltinKinds.cs` calling the area kind files, each with an empty `Register`; `Effects/BuiltinEffects.cs` calling `TerrainHandlers.Register` on an empty family; `Sim/ZoneBuilder.cs` registering `TerrainLayers`, the ledger view, the world clock and the shells of `MaterializationSystem` and `ScopeModifierSystem`; `Sim/ZoneLoader.cs` holding `LoadZone` (afterwards WP3); `Sim/Events.cs` frozen with the area event files created empty.
- `Weft/WeftSimulation.cs`, `Weft/Resolvers.cs`, `Weft/Providers.cs`, `Weft/ZoneLink.cs` as shells with registration surfaces (afterwards WP4); `Campaign/CampaignService.cs` shell (afterwards WP5).
- `Server/World/WorldHost.cs` (the world-event scheduler and expiry moved out of `ZoneHost.cs` unchanged, plus the Weft step call; afterwards WP4), `Server/World/ChunkStreaming.cs` (the chunk send path moved out of `ZoneHost.cs` and `ZoneInstance.cs` unchanged; afterwards WP6), `Server/Persistence/JsonStores.cs` (`ILedgerStore` beside `IWorldStore`, JSON files; afterwards WP6).
- `Protocol/Messages.World.cs` (the world messages moved from `Messages.cs` unchanged; afterwards WP6); the generated TypeScript module is unchanged by a move and the contract test proves it.
- `content/core/world/pins.yaml` (the zone overrides moved from `regions.yaml`; afterwards WP3); `content/core/world/calendar.yaml` (the lore's calendar; afterwards WP2).
- `Tools/Bench.cs` (the rows of the pipeline page, measured; afterwards WP7), `docs/design/performance.md` (the rows entered with their first numbers).
- `tests/HollowWake.Core.Tests/WorldParityTests.cs` (G10) and `SeamTests.cs` additions (every registry has its built-ins; the pass order reproduces the goldens).
- Docs: `CLAUDE.md` (the registries in the repository map), `docs/ARCHITECTURE.md` (the world paragraph points at the contract set).

Acceptance: build clean; all tests pass with every golden constant unchanged; `validate` reports no errors and no new warnings; `load --x 0 --y 0` and the standard `fight` traces byte-identical to recordings taken before the package; `bench` within every existing budget, the new rows measured and entered; the parity test passes; every shell of this section exists and is registered; the protocol module is current.

#### WP2 The landscape

Goal: terrain that reads as a place: continents and belts, rivers and lakes, coasts, caves and overhangs, ragged biome borders with features, roads, and the calendar's seasons and weather.

Rows: P0 first: L1, L2, L3, L4, L5, L8, L10, L12 (with WP3's places, through the route plan seam), L15. Then L6, L7, L9, L11, L13, L14, L16.

Owns: the files of 3.2 marked WP2.

Content: `content/core/world/biomes.yaml`, `regions.yaml` (the existing files), new `landscape/world-plan.yaml`, `weather.yaml`, `river-pins.yaml`, `rules-landscape.yaml` (rule ids `landscape`, `weather`).

Docs: `docs/design/world/landscape.md`, the generation sections of `docs/design/movement-and-z-levels.md` for headroom.

Tests: `LandscapeGoldenTests.cs` (the world plan's plates and belts at sampled points; river traces of a region; a chunk with a river, a lake shore, a coast and a cave; ecotone edges; a season's overlay) and the start-zone walk (the start zone loads, the smoke's path from the start point to the quartermaster and the hob field is walkable, the pinned places stand where the pins put them).

Acceptance: the common gates; the existing golden constants that move are named in the pull request with before and after maps; `zone --map` of a named test region shows a river reaching a lake or the sea; a test body walks into a cave mouth and stands on the floor under the hill while another stands on the hill above; `worldgen.chunk` within its budget with every pass on, `worldgen.region.plan` within its budget; the parity test passes; the weather of a region replays identically from a seed and a time.

#### WP3 Places

Goal: a grammar of places from a campfire to a town, interiors as layers and instances, dungeons with rooms and hearts, unique places and residents, states that the world changes, and the quests that hang on all of them.

Rows: P0 first: P1, P2, P3, P4, P5, P6, P7, P8, P10, P12, P13, P14, P18, P19, the pin resolver of F19. Then P9, P11, P15, P16, P17; P20's board part (the quest half is WP5's).

Owns: the files of 3.2 marked WP3.

Content: `content/core/world/pois.yaml`, `landmarks.yaml`, `pins.yaml` (the existing files), new `places/features.yaml`, `sites.yaml`, `settlements.yaml`, `complexes.yaml`, `unique.yaml`, `parts-marcher.yaml`, `parts-concord.yaml`, `rules-places.yaml` (rule id `places`); the quest templates for the new objective kinds in `content/core/play/quest-templates-places.yaml`.

Docs: `docs/design/world/places.md`; a review record freezing the `meta.appearance` keys for the visuals session.

Tests: `PlaceGoldenTests.cs` (place plans of each rung from a seed; a town's streets and lots inside its footprint; a unique place sited by constraint; a runtime pin beyond the frontier honored when its land first generates; a state override changing a layout; a claimable place taken by a rite becomes the player's) and `LayerTests.cs` (a portal moves a body between layers at matching coordinates; walls in an interior block line of sight and paths; the first arrival opens a turn and a stranger entering while it is open joins it, a partied player joins the leader's wherever they are, a private kind gives each character their own; a turn ends when the heart is cut or the grace passes empty, and the next arrival's turn has a different layout while re-entering the same turn gives the same; inhabitants and rewards scale with the players inside; the zone plan's cost with clusters within budget).

Acceptance: the common gates; `worldgen.zone.plan` and `worldgen.place.plan` within budget; the hamlet's chunks cost no more than before; the parity test passes with layers; `load` of the start zone reports its places, layers and quests; the new objective kinds complete from the events that should complete them; the appearance keys are recorded.

#### WP4 The Weft

Goal: a world that moves without observers: populations that breed, migrate and die out under the players' pressure, factions with standing and goals, agents marching on generated routes, encounters resolved abstractly or in a loaded zone with the same outcome shape, consequences on the land, pins promised beyond the frontier, events from providers, the chronicle, the tiers.

Rows: P0 first: F1, F2, F3, F4, F5, F6, F7, F11, F14, F17, F19 (the placing half), F20. Then F8, F9, F10, F12, F13, D10 (with WP6's edit path). F15 and F16 wait.

Owns: the files of 3.2 marked WP4.

Content: `content/core/world/events.yaml`, `content/core/factions.yaml`, `content/core/play/territory.yaml` (the existing files), new `weft/agents.yaml`, `populations.yaml`, `encounters.yaml`, `chronicle.yaml`, `goals.yaml`, `rules-weft.yaml` (rule ids `weft`, `events`).

Docs: `docs/design/world/factions-events-encounters.md`, `docs/design/ai.md` (the world perception names and actions), `docs/design/territory.md` (standing).

Tests: `WeftReplayTests.cs` (a seeded run of a span of world days writes a byte-identical chronicle and ledger twice, with and without a scripted deed sequence; the catch-up bound holds; a valley's stock hunted to nothing by scripted kills migrates to the neighboring region and its spawn multipliers follow; a stock past capacity founds a camp through a pin; a latent region is never stepped and costs nothing until a scripted player enters it; a pin promised beyond the frontier appears in rumor before and in the zone plan after) and `EncounterTests.cs` (two warbands meet on a road and resolve abstractly; the same meeting materialized in a zone resolves with an outcome of the same shape; a battlefield's consequences appear in a zone loaded afterwards; the body budget holds under a large army; an unloaded zone's encounter continues abstractly).

Acceptance: the common gates; `bench --only weft` within `weft.tick`, `weft.catchup` and `weft.ecology`, and the plan caches within `worldgen.frontier`; `zone.materialize` within budget and `zone.tick.p95` unchanged in a zone with a materialized battle; `world --simulate` prints the chronicle; the standard duels unchanged.

#### WP5 The campaign and quests

Goal: authored beats that spawn generated content, gate by discovery and deeds, fail forward, and advance for a character and for a world; the foe factions a world draws and their questlines; the first chapter from the lore.

Rows: P0 first: C1, C2, C3, C8, C9, C11, C12. Then C4, C5, C6, C7, C10, C13, F13 (the quest half), P16, P20 (the quest half).

Owns: the files of 3.2 marked WP5.

Content: `content/core/play/quests.yaml`, `content/core/dialogue/*` (the existing files), new `campaign/ninewells.yaml`, `the-loosening.yaml`, `foes/*.yaml` (one file per foe faction with its kinds and its questline), `rules-campaign.yaml` (rule id `campaign`).

Docs: `docs/design/world/campaign.md`, `docs/design/quests.md`, `docs/design/dialogue.md` (rumors and beat-keyed lines).

Tests: `CampaignTests.cs` (a scripted character walks the first chapter headless with beats opening and concluding in order; two worlds with one seed draw the same foes and pin their homes in the same places; a foe's line opens on the discovery of its first place in any order; the world's line extends when the front character exterminates a foe, and a second character cannot finish a different foe ahead of that precedent; a latecomer walks a lost foe's questline through its instances and remnant pins while the open world shows the aftermath; validation reports a foe questline that needs the open world to finish; a defeated foe's survivors move and the others' goals shift by rule; a worldly beat opens when the ledger crosses its threshold; a request's place destroyed before the beat opens falls forward; validation reports a hanging beat; two characters at one bounty board draw different bounties toward the same Knot) and `ObjectiveTests.cs` (each new objective kind completes from its event and withdraws when the world overtakes it).

Acceptance: the common gates; `campaign` prints the chapter's state; no change to the existing starter quests' behavior; the loopback tests pass.

#### WP6 Destruction and persistence

Goal: one edit path for every change to the land, persisted as deltas and replayed over regeneration, healed on the Weft's clock, sent as edits, made by a terrain effect family under rules of hardness and protection; the ledger store; and the world message shapes every other package builds against, frozen in its first commits.

Rows: P0 first: the message shapes of W1 and W2 (served with today's data where the simulation is not ready), D1, D2, D3, D7, D8, D9, D11. Then D4, D5, D6.

Owns: the files of 3.2 marked WP6.

Content: new `content/core/world/destruction.yaml` (rule id `destruction`, hardness on the core's landmark legends through a patch file it owns).

Docs: `docs/design/world/destruction-and-persistence.md`, `docs/design/networking-and-persistence.md` (edits on the wire, the ledger), `docs/design/territory.md` (structures through edits), `docs/design/weaves-and-effects.md` (the terrain family).

Tests: `DestructionTests.cs` (an edit replayed over a regenerated chunk equals the chunk edited live; a chunk regenerated under a changed overlay keeps its edits; dig and raise stop at the bounds and at hardness; protected cells refuse with a reason; a crater outside any claim heals back to the generated heights by its kind's rule and a crater inside a held claim stays; healing leaves a Holding's structure alone; compaction keeps replay within budget), `ChunkEditTests.cs` (a `chunk_edit` reaches a session within interest and not one outside; the fallback sends a whole chunk past the threshold; a reconnecting client receives what it missed), `LedgerStoreTests.cs` (round trips per kind).

Acceptance: the common gates; `worldgen.edits.replay` within budget; the protocol module regenerated and the contract test green; `probe` against a running server receives an edit; the browser client applies an edit through the shared `Apply`.

#### WP7 The wire, the clients and the tools

Goal: both clients draw the living world: layers and instances, caves with ceilings, ledges, flowing water, weather and the season, places by their appearance ids, the map, the chronicle; the tools show every part of it headless.

Rows: W3 (the client half), W4, W5, W6, G13.

Owns: the files of 3.2 marked WP7, after the visuals pull request lands.

Dependency rule: WP7 reads messages and never core types beyond the generator it already links; when it needs a field a message lacks it asks the coordinator, who sequences the change with WP6.

Tests: the desktop smoke extended (walk into a cave and stand under the hill; enter a Snarl's instance and come back; see a season change by setting the clock in a developer option; open the map); the web smoke likewise with layered chunks requested; `WorldMessageTests.cs` for every new message; unit tests for the per-layer chunk store in each client.

Acceptance: the client assembly and the web client build; both smokes pass against a local server with screenshots the coordinator reviews; `bench --json` compared in CI; no client resolves gameplay or writes a cell.

#### Deferred rows

F15 caravans and trade, F16 sieges and contests, the database implementation of the ledger store, a second continent and cross-world travel wait for a later phase; each joins the package that owns the files it touches.

### 3.4 Contracts WP1 freezes

| Seam | Shape |
|---|---|
| Generation passes | `GenerationPasses.Register(scale, name, pass)`; order from `world.gen.passes` per scale; a pass reads plans and writes its scale's output |
| Plans | `Plans.Register<T>(kind, builder)`; cached by coordinate per generator, dropped when overlays change, warmed by the host |
| Overlay kinds | `OverlayKinds.Register(name, combine)`; `GenerationOverlays.Param` and `Sum` dispatch through it |
| The generation ledger | overlays, chunk edits, place states and pins, each with origin, time and kind; nothing else is read by generation |
| Pins | authored by coordinate or constraint in content, placed at run time by the Weft and the campaign as ledger entries; one resolver sites both; capped per region |
| The frontier | the set of regions any player has entered, in the ledger; the Weft's tiers and the plan caches' bounds are drawn from it |
| Populations | stocks per explored region and kind in the ledger, stepped by tier, read by spawn tables through density multipliers by tag |
| Chunk edits | `ChunkEdit` records applied by one `Apply` on the server and the clients; an edit-kind registry; an expiry by kind unless anchored by a claim, a Holding or a lasting place |
| Surfaces and layers | `Surface.Headroom`; `ChunkData.Layer`; `TerrainLayers` per simulation; `Transform.Layer` |
| Zone keys | a zone coordinate plus an optional instance part |
| The world clock | `WorldClock` from a `calendar` kind: Span, day, time of day, season |
| The Weft | `WeftSimulation.Step(ticks)`; `EncounterResolvers.Register`, `EventProviders.Register`; `ZoneLink` inbox and outbox |
| Messages | `Messages.World.cs` holds every world message; new fields are optional and absent means today's meaning |
| Content kinds | `Content/Kinds/*Kinds.cs` per package; several validators per kind |
| Events | package-owned files under `Sim/Events/` |
| Composition | `ZoneBuilder` registers every service and system shell named in this plan |

### 3.5 Coordination with the other sessions

- **Visuals**: the places package records the `meta.appearance` keys (kind, culture, age, state) and the stamp tags in a review record in its first commit; the visuals session reads them and chooses meshes, sprites and materials from them. The landscape package's edge tags, flow directions, headroom and weather overlays are the client's inputs for blending, water, ceilings and sky. WP7's client work waits for the visuals pull request.
- **The combat port**: agent strengths, encounter rooms, inhabitants and spawn tables reference patterns by tags (`tangled`, `beast`, `loosed`, a faction's tag), so the monsters the port adds flow into the world without a content change here; the port supplies the encounter content (what an army fields) and the balance harness compares abstract and materialized outcomes.
- **Codex**: reviews every pull request against the acceptance lists above and the vision's tenets; may claim WP6 or WP7 in a review record, both self-contained behind frozen shapes.

## 4. Decisions and open questions

The owner answered the first round on the day the plan was opened, and the second the same day. Decided questions stay here so the packages can cite them.

**Q1. How big is a world? Decided: it has no edge.** The world generates outward as it is explored, the way a sandbox world does, and the lore's edges are the heartland's horizons, not the world's. What exists is bounded by the frontier (the regions some player has entered) and by what the world has promised beyond it; cost follows the frontier and the tiers, never the map. The world plan is tiled so that an endless world costs the same at any distance from the origin (L1), the plan caches are bounded by the tiers (F20), and the latent world beyond the frontier is never built, simulated or stored except for its pins (F18, F19).

**Q2. How many players share one world? Decided: variable, and built to scale.** One shared world per server with instances for Knots and private beats; caps and budgets are rules sized to the population a server carries, and the ledger is the portable thing a database later holds. Nothing in the plan assumes a count.

**Q3. How far does the world simulate without observers? Decided: everywhere it has been, at a depth that falls off with distance, so that it has visibly moved when a player returns.** The owner's references are settlement and colony simulations that run the far world as aggregates; that is the far tier's populations (F17): stocks per region that breed, migrate along the routes and die out under pressure, stepped once per world day, read by spawn tables, founding and abandoning places through pins. A player who hunts a valley empty and burns its camps comes back to find the pack in the next valley and the camp's survivors settled past the ridge, without a body having been simulated. Beyond the frontier nothing moves, and that is where the world promises: an event may pin a waking Fount or a foe's stronghold out past the explored valleys by direction and distance, rumor speaks of it, generation honors it when a player arrives, and the frontier moves with the first player across it (F18, F19). Nothing moves while the server is down; a bounded catch-up runs on restart.

**Q4. How destructive is destruction? Decided: passing by default, lasting on purpose.** Every edit outside a claim or a Holding heals back to the generated land by its kind's rule, so a meteor's crater is a season's crater and no player inherits another's wasteland; the generated chunk is the before, the edit the after, healing the return (D5, D11). Destruction lasts in claims: place kinds marked claimable (an abandoned stead, a cleared camp, a plot by a Fount) that a player takes and ties to a Holding, inside which digging, raising and building stick and a township can grow (P18, D11); under a Holding's structures; and where a place's kind says the marks of war stay. Authored landmarks and the Firstworks are never destructible.

**Q5. What is authored versus generated in the campaign? Decided: both, through foe factions.** The content holds a pool of foe factions, each with an authored questline; a world draws a number of them by its seed, places their homes through pins and seeds their populations; each questline's places, agents, events and quest slots are generated; players choose the order they take the factions in, and defeating one changes the rest by rule (C8, C9, C10). The personal first chapter from the lore stays authored. Worldly beats run per world. The lore names the foes in Aureth's idiom; the owner's examples (gnoll heartlands, marauder bands, goblin kin, the Unmade) are kinds the pool can hold under whatever names the lore gives them.

**Q7. The pace of the calendar. Decided:** a day of half an hour, so a Span is twenty hours of real time and a year passes in about a week.

**Q9. The race. Decided: the line.** Every world is its own generation. The world keeps the ordered list of the foes it has lost; the character at the front extends it by finishing the Odyssey of a living foe of their choice; every other character's progress is a prefix of that list, so a foe the world has already lost is walked by latecomers in the same order through the faction's instances and remnant pins while the open world shows the aftermath (C11, C12). The line gates only an Odyssey's finishing beat; the open-world quests of living foes stay open to everyone, and a character partway through an Odyssey the front finishes keeps every beat done and phases the rest. The world never desynchronizes, because what a latecomer fights privately is already gone publicly, and nobody is locked out of a chapter. The alternative, strict shared progression, where an exterminated foe's chapter closes for everyone and latecomers find only its ruins, was not taken: it consumes content for the first to arrive, leaves a long-lived world thin for new players unless replacement foes rise, and makes the race absolute at the cost of everyone behind it. Still to decide, with the campaign package: whether a party at the front shares the finishing blow (recommended: yes, the party is named together) and whether replacement foes rise from the pool when one falls (C13; recommended: yes, by rule, so an old world keeps a living campaign).

**Q6. Knots: shared or private? Decided: by kind, shared by arrival.** In the lore, Knots are the places where Threads tangled in the Unraveling and never settled: Tangles (wild pockets), Snarls (the dungeons, with a heart called the Snag), Thinnings (anomalies) and Burls (Knots that have set, the prizes), and a Knot turns: each time it is entered it has re-tangled. In the engine: Snarls are shared instances; the first player to enter opens a turn seeded by them, their party joins the leader's turn wherever they are, anyone who enters while the turn is open joins it unless the kind is private, the turn ends when the Snag is cut or the Knot stands empty past its grace, and the next arrival opens the next (P7). Difficulty stacks with the players inside (P19). Tangles and Thinnings are shared open-world places the Weft turns on its clock; Burls are shared, never turn, and are contested; a beat that needs a Knot of its own gets a private one. What the owner described as client-side is here keyed by the player on the server: the opener is part of the instance's key, the server generates and simulates the turn, and the opener's client may generate the same tangle for drawing because it is told the key, but it never decides what is in it. Generated quests follow the same split through bounty boards: the quests are the character's, the targets are the world's (P20).

**Q8. The name of the world simulation. Decided: the Weft.** In the lore the Loom has a Warp and a Weft: the Warp is the fixed part, the long threads laid first, which people see as the rows of stars and the laid rivers; the Weft is the moving part, the days, the weather, the lives of creatures, everything that crosses the Warp and is woven in. Generation makes the fixed part of a world and the Weft makes the moving part.
