# The generation pipeline and its budgets

How the pieces of the living world register, layer, prove themselves and stay within cost. This page is the engineering contract behind the other pages; [the plan](plan.md) freezes these seams first and holds every package to them.

## Registries

Everything open is a registry with a name, registered from one file per area and ordered by rules, never a switch inside a system:

- **Generation passes**: each pass has a scale (world plan, region plan, zone plan, chunk) and a name; `world.gen.passes` lists the order per scale. The terrain, landmark, flora and spawn passes of today register under their names; the water, road, cave, feature, place and edit-replay passes join them. A pass reads the plans above it and the chunk it writes, and nothing else.
- **Plans**: a plan kind per scale registers how it is built and cached; the generator's coordinate caches hold them, drop them when overlays change, and rebuild them lazily.
- **Overlay kinds**: how a parameter combines across covering overlays (sum, override by priority, maximum, a tag set) and which passes read it; the generator and the climate grid resolve parameters through the kind, so a new parameter is a registration.
- **Place grammars**: the rule types a layout may use, and the interior grammar types.
- **Event providers**: who may propose events to the Weft (the calendar, factions, territory, the campaign, randomness, mods).
- **Encounter resolvers**: by encounter kind.
- **Edit kinds**: what an edit operation may do to a cell and how it heals.
- **Agent actions and perception names**: the AI registries, reused by the Weft's agent kinds.
- **Objective types**: the quest registry, extended.
- **The world clock**: the calendar is content; the clock is one service every system reads for the Span, the day, the time of day and the season.

Content kinds for all of the above register through the content registry with validators that check references, expressions and bounds, so `hollowwake validate` catches a grammar naming an unknown rule type or a beat whose gate reads an undeclared name.

## Layering of content

Content composes in layers as everywhere else: the core pack defines the world; a later layer may replace, patch or remove any kind. Within the world, kinds compose rather than special-case: a place kind names grammars, parts, inhabitants and templates by id; an event kind names overlays, spawns and agent kinds; a beat names kinds and constraints. Presentation (appearance ids, colors, chronicle voice) sits under `meta` and the simulation never reads it. Lore names come from lists by culture and the naming conventions in [docs/lore/naming.md](../../lore/naming.md).

## Golden tests and replay

Determinism is proved, never assumed:

- **Golden hashes** pin regions, climate samples, zone plans, place plans, chunks of every layer, and overlaid worlds. Each package adds golden facts for what it builds in its own test file. An existing constant changes only in a pull request that names the change, says why generation moved, and shows the before and after (the zone map from the tools) so the reviewer can see it was meant; a constant that changes without that is a review block. The start zone stays walkable and its pins stay where the lore put them, tested by loading it and walking the smoke's path.
- **Replay**: the Weft run headless from a seed for a span of world days writes a chronicle and a ledger; run twice, byte-identical. The same holds with a scripted sequence of player deeds injected, so player change is replayable too.
- **Client parity**: a generator constructed from the world information a client receives (seed, layout, content hash, overlays, place states) hashes the same chunks and plans as the server's generator, with and without edits replayed; the test runs against the core, which both the server and the desktop client link.
- **Edit replay**: a chunk regenerated under a changed overlay with its edits replayed equals the chunk edited live.
- **Traces**: `load` and `fight` traces stay byte-identical across a package that claims not to change behavior.

## Budgets

Budgets are gates: a change that crosses one is wrong until the budget is restored or argued down in [performance.md](../performance.md). The living world adds rows there; the numbers are entered with the first measurement of each seam by the foundations package and tightened by the packages that own them.

| Measure | What it bounds |
|---|---|
| `worldgen.chunk`, `worldgen.chunk.alloc` | a chunk of any layer from cached plans, as today |
| `worldgen.zone.plan` | placing and laying out a zone's places, with their quests |
| `worldgen.region.plan` | a region's hydrology, belts, settlement field and routes, amortized over its zones |
| `worldgen.world.plan` | the world plan, once per process |
| `worldgen.place.plan` | one place's layout at the largest rung |
| `worldgen.edits.replay` | replaying a chunk's edits on load, and the ledger bytes per edited chunk |
| `weft.tick` | one world tick with the agent and region caps at their defaults |
| `weft.catchup` | catching up the bounded number of ticks after a restart |
| `zone.materialize` | materializing an encounter's bodies into a loaded zone, per tick |
| `zone.tick.p95`, `zone.tick.alloc` | unchanged; the living world's systems in a zone fit inside them |
| `snapshot.*` | unchanged; place states, weather and chronicle messages fit inside the delta budget |

The benchmark gains `--only worldgen` groups for plans and edits and a `weft` group; `--json` feeds CI comparisons.

## Threads and seams

- The generator is safe to read from many threads: plans are built under their cache's add lock and published whole, scratch is per thread, and a pass allocates only what the chunk keeps. The desktop client already generates on workers; the server warms plans ahead of players on a worker.
- Zones own their simulations; the world thread owns the Weft and the ledger. Zones never call the Weft and the Weft never touches an entity: the seam is an inbox and outbox of events per zone, drained on each side's tick, and a read-only snapshot of the Weft's view that a loading zone takes once. This is the same seam that moves zones to worker threads and later to other processes.
- The ledger is written only on the world thread; zones send edits as events the world thread commits and persists, and reads of the ledger from a zone are of a snapshot taken when the zone loaded plus the entries it has been sent since.
- Persistence is behind store interfaces with JSON files today and a database later, unchanged in shape.

## Tools

`hollowwake region`, `zone --map`, `world`, `load` and `bench` stay; the living world adds `place <kind> --seed` (lay out one place and print it), `world --simulate <days>` (run the Weft and print the chronicle), `chronicle`, `campaign`, `ledger --zone` (what the ledger holds there), and `layer --zone --id` (the map of an interior layer). Every tool runs the real core headless, so a designer sees the world without a client and a reviewer gets evidence in text.
