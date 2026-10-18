# Architecture

## Shape

```
content/core (YAML)  --->  HollowWake.Core (simulation, .NET 8, no engine)  <---  HollowWake.Protocol (messages)
                                 ^                     ^
                                 |                     |
                 HollowWake.Server (.NET 10) ----------- HollowWake.Client (Godot 4.7, .NET 8)
                 zones, sessions, persistence       terrain from seed, views, cameras, UI
                        ^         ^
                        |         +------- WebSocket ------- web/ (TypeScript, three.js, in a browser)
                        |                                    terrain from the server, types generated from Protocol
          HollowWake.Server.Host (TCP + WebSocket)   HollowWake.Server.Host.Brutal (RakNet UDP via BRUTAL)
```

The core is the product. It compiles for .NET 8 so both the Godot client and the .NET 10 server can reference it. It has no sockets, no rendering, no database. Everything that runs in a zone on the server can run in a tool, a test or the client.

## The core, layer by layer

**Expressions** (`Expressions/`). A small formula language with identifiers, arithmetic, comparisons, logic, a ternary, functions and `has("flag")`. Parsed once and cached. Every formula in content is an `Expr` evaluated against an `IExprScope`: an entity, a weave, a holding, a test dictionary. Dotted names mount sub-scopes (`target.life`, `weave.area.radius`).

**Attributes** (`Attributes/`). `AttributeSet` holds modifiers per attribute and computes values on demand: base (a constant or a formula from the catalog, or a per-owner override), plus flat and links, times (1 + sum of increased), times product of (1 + more), then override, then clamp and rounding. Modifiers carry a tag scope (apply only when the query context has all scope tags), a condition expression, an optional gauge (scale by or gate on a live quantity), a source (for bulk removal) and a priority. Query-local extra modifiers join the same fold, which is how a weave's glyphs and a character's gear add together as one "increased" layer.

**Content** (`Content/`, `Definitions/`). YAML or JSON files declare `kind` and `entries`. Layers load in order; later layers replace, `patch` (JSON merge patch) or `remove` entries. Everything binds to typed records with snake_case keys, tag sets, expressions and polymorphic `TypedSpec`s (`{type: ..., ...}`) for effects, actions, generators and procedures. `ContentKinds` is a registry: built-in kinds register with a validator, each area adds validators for its own fields (`Content/Kinds/`), and mods can register new kinds. Attribute families expand into attributes at load. Definitions that several areas extend are partial records, one file per area. `Rules` flattens rule groups into dotted paths that code reads with defaults.

**Entities** (`Ecs/`, `Components/`). A small entity-component world with class components and snapshot-safe queries. Components are state only: `Transform`, `Motion`, `Identity`, `Stats` (the attribute set, resources, flags, gauges, recency ledger, and the entity's expression scope), `Statuses`, `WeaveBook`, `Inventory`, `Equipment`, `Behavior`, `QuestLog`, `Player`, `Minion`, `Projectile`, `Field`, `Fount`, `Spawner`, `Drops`, `Interactable`, `Prop`.

**Simulation** (`Sim/`). `Simulation` owns the world, the terrain window, a fixed-step clock, a scheduler, an event bus, seeded randomness, the registered systems and a service bag. `Tick` runs systems in phase order: Input, Think, Act, Move, Resolve, Status, React, Cleanup. `ZoneBuilder.Wire` is the one place that says what a running simulation consists of.

**Patterns** (`Patterns/`). Entity templates as bags of components, with `extends` merging. Binders registered by component name turn JSON into components. Monster rarities and affixes are rolled at spawn from content.

**Weaves and effects** (`Weaves/`, `Effects/`). A `WeaveSpec` (preset or form + threads + glyph references with levels and rolled rows + the spent nodes of the form's own tree) compiles into a `CompiledWeave`: tags (with a cast-scope tag), mechanisms (declared, and probed from the weave by registered probes), local attribute bases, glyph modifiers, and an effect graph that glyph patches and hooks edit before it compiles through the `EffectRegistry`. Glyphs whose gates fail stay socketed but dormant. Effect types are registered handlers grouped in families under `Effects/Handlers/`, each declaring its parameters and hooks; `cast` runs another weave from inside a graph and `delay` runs a list later. `WeaveRuntime` validates intent, pays costs, starts casts, channels, holds, charge-ups, guards and toggles, fires triggers and repeats, and keeps each seat's banks, stacks and faces, and executes effects with an `EffectContext`. Monsters use the same runtime through AI.

**Combat** (`Combat/`, `StatusEffects/`). `DamagePipeline` is the one path a hit takes: base from the weave, the attacker's `damage` fold scoped by the hit's tags (with victim tags prefixed `vs.`, some of them from rule expressions), conversion and extra-as and crit; then the defender's stages, registered by name and ordered by rules (immunity, guard, evade, block, mitigate, taken, pools, cull by default), then ailments. A pooled hit scope lets folds inside a hit read the struck body. `StatusService` applies, stacks, refreshes, ticks and expires statuses, keeping modifiers, flags and gauges on the bearer in sync. `DeathService` credits kills to owners, drops loot, grants experience and revives players. The `TriggerHub` turns the simulation's events into trigger kinds per owner for glyph triggers, procs and charge taps.

**Items** (`Items/`, `Loot/`). Base families with local value windows and a tier ladder from item level; affix families with groups, tiers and weights; uniques; rarities with slot counts; roll fractions stored on the item so a rebalance retunes existing items. `EquipmentService` applies wearer modifiers and weapon bases. `LootService` rolls nested tables, drops and picks up.

**World** (`World/`, `WorldGen/`). The world is a hierarchy: cells, chunks (generation, streaming, persistence), zones (simulation and scaling), regions (macro shape, politics, events). `WorldGenerator` is a pure function of seed, content, overrides and overlays: continental and ridge noise with region-blended lift and scale, climate, per-Thread density fields, biome selection by most specific match, zone-level point-of-interest placement under constraints and budgets, landmark stamps (ASCII prefabs with stacked surfaces for bridges and floors), flora scatter, spawn points, and quest templates instantiated from the placed points of interest. `Terrain` resolves ground among stacked surfaces; `Pathfinder` is A* honoring step height and drops; `MovementSystem` integrates walking, gravity, jumping, sliding along obstacles and landing. Where the world is going (plans at every scale, a grammar of places, a world simulation above zones, destruction through chunk edits) is the contract set under [design/world](design/world/overview.md).

**Play systems**. `QuestService` (hand-authored and generated quests, objective types as a registry, rewards), `CraftingService` (server-issued seeded challenges, deterministic scoring, quality into rarity and local values), `TerritoryService` (binding rites, holdings, decrees as modifiers and generation overlays, structures stamped into terrain, yields), `ProgressionService` (experience, levels, the passive constellation), `InteractionService` (interactable kinds as a registry), `CharacterFactory` (sheets to entities and back).

## The server

`GameServer` is the composition root: content, `ZoneHost`, `SessionManager`, a transport. One world thread steps everything at the tick rate: transport events are drained from a thread-safe inbox, each `ZoneInstance` ticks its simulation, snapshots go out to sessions by interest radius, autosave runs on a timer. `ZoneHost` creates zones on demand, moves players across zone borders by capturing the character sheet and materializing it in the neighbor, schedules world events as generation overlays, and persists holdings, events, characters, accounts and modified chunks through store interfaces (JSON files today). Transports implement `ITransport`: TCP with length framing, WebSocket with one message per binary frame (on `HttpListener`, which also serves the built web client from `web_root` on the same port), an in-process loopback for tests, and RakNet UDP through BRUTAL in the .NET 10 host. A `CompositeTransport` fans several into one, which is how the pure host runs TCP and WebSocket into the same world; it re-numbers connections so session ids stay unique across them.

## The client

Godot renders what the server says. On entering the world it receives the seed, layout and overlays and generates terrain locally with the same `WorldGenerator`; modified chunks arrive on the wire. Entities are placeholder bodies interpolated between snapshots. The camera rig is the only place that knows whether the view is isometric or third person. Input becomes intents; nothing is resolved on the client.

The web client in `web/` does the same in a browser, over WebSocket. It cannot run the C# generator, so it asks the server for every chunk it shows (`request_chunk`, answered by `chunk_data`); its message types are generated from `HollowWake.Protocol`. It is served by the game server itself or from GitHub Pages ([web-client.md](design/web-client.md), [ADR 0006](adr/0006-browser-client.md)).

Players get the desktop client through the launcher (`launcher/`, Electron): it installs and updates exported builds from GitHub Releases, verifying each against the release manifest, and starts the client with the server address. Tags and a nightly schedule produce those releases (`.github/workflows/release.yml`, `nightly.yml`). The launcher has no game logic and never speaks the protocol. See [design/launcher.md](design/launcher.md).

## Scaling path

1. Today: one process, many zones, one world thread, a handful of players.
2. Zones on worker threads behind a job scheduler seam (BRUTAL's job system is the intended implementation), still one process.
3. Zone servers as separate processes behind a gateway that owns sessions and routes by zone; world state in a database; cross-zone messaging through a bus.
4. Orchestrated zone servers (Kubernetes with Agones is the researched path) with regions as the unit of placement.

Nothing in the core changes across these steps; they are server topology.
