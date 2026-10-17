# Roadmap

Milestones, in order. Each is done when its verification holds, not when its code exists.

## M0: Foundation (this repository's first state)

Core simulation with every system present end to end, the server and protocol, a Godot client that connects and plays, a content pack that exercises everything, tools, tests, docs, deployment files. Verification: build, tests, validate, headless zone, headless duel, loopback session, live TCP probe.

## M1: Playable loop for four

- Client prediction for the local player's movement with server reconciliation.
- Client-side cast bars, projectile and field visuals, hit feedback, death and respawn flow.
- Inventory and equipment UI, passive constellation UI, weave composition UI (form, Threads, glyphs with live validation from the compiler).
- Crafting minigame UI for the three procedures.
- Quest journal, map overlay of discovered points of interest, zone names.
- NPC dialogue as content (lines keyed by quest state).
- Vendors and currency sinks.
- Party grouping and shared quest credit.
- Verification: four players on one hosted server for an evening without a crash; the probe tool extended to scripted bots.

## M2: The world breathes

The living world's contract set and its work packages are in [design/world](design/world/overview.md) and [its plan](design/world/plan.md); the items below are the milestone's shape.

- Rivers and roads as generator passes (flow accumulation, least-cost paths between hubs).
- Region-level narrative: faction control per region, warfronts between adjacent regions, event chains.
- Knots as instanced sub-areas with their own generators (caves, towers) entered from the overworld.
- Day and night, weather as overlays.
- World event spawns and quest hooks wired into zones.
- Persistence to PostgreSQL behind the existing store interfaces.
- Verification: a scripted week of world events replays deterministically from a seed.

## M3: Build depth

- Charges and combo resources, use-charges on weaves, channels with ramping, triggered weaves.
- Status interactions (shatter, detonate), stat trades, keystone-scale passives, specialization realms.
- Item sockets with glyph gems, crafting benches that add and remove affixes, bench costs as currency sinks.
- Balance harness: seeded batch duels and drop statistics in the tools with baselines checked in.
- Verification: the balance harness runs in CI against baselines with tolerances.

## M4: Scale

- Zones on worker threads behind the job scheduler seam.
- Gateway process and zone server processes; cross-zone chat and parties.
- Interest management by chunk, delta snapshots, binary serialization.
- Load test with bot swarms; the hosting ladder from the infrastructure docs exercised at 50 and 500.

## M5: Presentation

- Real bodies, animation, VFX driven by weave tags and Thread colors.
- Terrain materials and props from biome definitions, landmark meshes from prefab tags.
- Sound hooks on events.

Order matters less than the rule that every milestone keeps the pillars intact.

## Parity follow-ups

Rows of the parity analysis ([hollow-wake-gap.md](design/parity/hollow-wake-gap.md), section 1.5) that remain open after the first seven packages, for a later round: the P2 cast and effect rows (companions, grabs, possession, clouds, lightwells, fog, orbs, ledgers), skill grants from items, movement states on statuses, weak spots, luck and taunt-scoped damage, passive conduits, memories, salvage, per-weave tree pages on the clients, an account settings message for account-scoped options, and audio. Each belongs to the package that owns its files in section 3 of that document.
