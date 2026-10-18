# The living world: contract set and plan, review requested

Date: 2026-10-17. Author: the Claude living-world session, started by the coordinator to own everything about how the land, its places, its events and its story come to be, change and persist. Base: main `b910e94`. Pull request: [arianna-arpg/hollow-wake-mmo#12](https://github.com/arianna-arpg/hollow-wake-mmo/pull/12). This record claims the work, asks Codex and the coordinator for a review of the documents before any code, and lists the questions that wait on the owner.

## Claim

This session claims, under [COLLABORATION.md](../COLLABORATION.md): `docs/design/world/` (the contract set and the plan), the retirement of `docs/design/world-generation.md`, and, once the plan is reviewed, the foundations package the plan names (WP1), which freezes the seams the other packages build on. The packages after it are claimed by whoever takes them in a record of their own; two of them (destruction and persistence, the wire and the tools) are self-contained behind frozen shapes and suit a Codex chat.

## What was done

Seven contract pages under `docs/design/world/`, one page each in durable prose with no counts that go stale, replacing and extending the old world generation page: the [overview](../design/world/overview.md) (what is generated, simulated, authored and persisted, and how players change it), the [landscape](../design/world/landscape.md), [places](../design/world/places.md), [factions, events and encounters](../design/world/factions-events-encounters.md), [the campaign](../design/world/campaign.md), [destruction and persistence](../design/world/destruction-and-persistence.md) and [the pipeline and its budgets](../design/world/pipeline-and-budgets.md). Then [the plan](../design/world/plan.md), in the shape of the parity analysis: where the world stands, seventy-odd rows by area with a proposal, dependencies, a size and a priority, content sketches, seven work packages on disjoint files with a foundations package that freezes the seams first, acceptance per package with determinism proofs and budgets, and eight open questions for the owner.

The five moves the plan rests on: plans at every scale above the chunk passes, and a ledger of which generation reads exactly three kinds of entry (overlays, chunk edits, place states) so the client stays in step; a grammar of places with interiors as stacked surfaces, interior layers or turning instances; a world simulation in the core, called the Weft, stepped on a world clock in bounded tiers and writing only ledger entries and events; chunk edits as deltas through one path shared by players, structures and events; and registries plus golden, replay and parity tests frozen before the parallel packages start.

## What was verified and how

- The pages were written against the code as it stands: the generator, the planner, the chunk passes, the overlays, the terrain model, the territory and quest services, the zone host and the stores, the golden tests, the benchmark's world group and the tool commands, each read in full before writing. Every "today" column in the plan's matrix names what exists.
- The lore under `docs/lore` was read in full; the vocabulary of the pages (Knots that turn, Founts, Holdings, the calendar's Spans, the five powers, the laid rivers, the world's four edges) follows it, and the plan's first chapter is the lore's.
- The documents hold no code change. The build, content validation and tests were run on the branch to show the tree is as main left it; the numbers are in the pull request.
- Not verified, by design: nothing in the plan is measured yet. The budgets the pipeline page proposes become numbers in `performance.md` when the foundations package measures them, which is the plan's first acceptance item.

## The owner's answers, the same day

The owner answered the first round of questions directly in the living-world session, and the pull request was revised before review. Decided, and now in the pages and in the plan's section 4: the world has no edge and generates outward with exploration, with cost bounded by the frontier of explored regions (Q1); players per world are variable and the plan assumes no count (Q2); the far world is simulated as populations that breed, migrate and die out under the players' pressure, the latent world beyond the frontier is never simulated but may be promised through pins that generation honors on first reach (Q3); destruction is passing by default and heals to the generated land, lasting only in claims a player holds, under Holdings and where a place's kind says so (Q4); the campaign draws foe factions per world from a content pool, each with an authored questline over generated content, taken in the order the players choose (Q5); a day is half an hour (Q7). The second round decided the rest: Knots are shared by arrival and by kind (the first player to enter a Snarl opens its turn, their party and anyone entering while it is open join it, difficulty stacks with the players inside; Q6), the world simulation is the Weft (Q8), and a shared world's campaign is a line every character walks as a prefix, the front writing it by finishing a living foe's Odyssey, lost foes phased into instances and remnant pins for latecomers (Q9). The owner's word for a foe's questline, Odyssey, is adopted.

Two consequences for the review: the generation ledger now has four kinds (overlays, chunk edits, place states, pins), which answers the second ask below in advance, and the Weft gained populations as the far tier's model, which is the part of the design most worth a second pair of eyes.

The owner also asked that Opus sessions be used extensively for procedural content and that Codex be a full collaborator; the plan's section 1.3 says how: one Opus engineer per package, Opus content forges at authoring time gated by validation and the headless tools, no model calls at run time, and Codex co-designing the Weft's model through records and reviewing every pull request.

## Asks of Codex

1. Review the contract set and the plan as an independent engineer, against the vision's tenets in this order: does anything hard-code what content could express; is every change to the land server-authoritative and deterministic; are the open sets registries; do the proofs (golden, replay, parity) pin what matters; do the file splits of the plan's section 3.2 leave the packages disjoint; are the budgets the right rows.
2. Say whether the three-kind generation ledger (overlays, chunk edits, place states) is the right slice to keep the clients in step, or whether a case needs a fourth kind.
3. Say whether a Codex chat will claim the destruction and persistence package or the wire and tools package once the plan is accepted.

## Asks of the coordinator

1. Review for fit with the parity plan's conventions and with the visuals and combat-port sessions: the appearance keys the places package will freeze, and the tag-based references to monsters the Weft's agent kinds use.
2. Relay the open questions in the plan's section 4 to the owner; the foundations package waits on the answers to the first, third and fourth (world size, simulation without observers, how destructive destruction is), because they set the world plan's pins, the Weft's tiers and the edit bounds.

Acknowledged by: pending Codex and coordinator review.
