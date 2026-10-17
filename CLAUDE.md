# CLAUDE.md — Skein

Guidance for Claude Code (and people) working in this repository. Committed and shared.

## What this is

Skein is an MMOARPG foundation: an engine-agnostic C# simulation core, an authoritative server, a Godot client, a YAML content pack and tools. The core vision is in [docs/VISION.md](docs/VISION.md) and is the test for every change: **data over code, one modifier engine, tags as the universal join, composition over bespoke, server authority, a seamless generated world, and observability.** If a change hard-codes something content could express, it is probably wrong.

## Repository map

- `src/Skein.Core` — simulation. Namespaces mirror systems: `Attributes`, `Expressions`, `Content`, `Definitions`, `Ecs`, `Sim`, `Components`, `Effects`, `Weaves`, `Combat`, `StatusEffects`, `Items`, `Loot`, `Patterns`, `Ai`, `Movement`, `World`, `WorldGen`, `Quests`, `Crafting`, `Territory`, `Progression`, `Characters`, `Interaction`, `Intents`, `Systems`.
- `src/Skein.Protocol` — `Messages.cs` (every wire message) and `MessageSerializer.cs` (JSON + length framing).
- `src/Skein.Server` — `GameServer` (composition root and loop), `Sessions/`, `World/` (`ZoneHost`, `ZoneInstance`, `Snapshots`), `Net/` (`ITransport`, TCP, loopback), `Persistence/` (JSON stores), `Config/`.
- `src/Skein.Server.Host` — pure .NET entry point. `src/Skein.Server.Host.Brutal` — .NET 10 + BRUTAL RakNet entry point (compiles to a stub without the .NET 10 SDK).
- `src/Skein.Client` — Godot 4.7 C#. `Main.cs` orchestrates; `World/` generates terrain locally and renders entities; `Camera/CameraRig.cs` has both camera modes; `UI/` is placeholder UI.
- `src/Skein.Tools` — the `skein` CLI (`Program.cs`, `Probe.cs`).
- `content/core` — the base content layer, one YAML file per area. `rules.yaml` holds every formula and tunable.
- `tests/` — xunit. `Skein.Core.Tests` (foundations and gameplay against the real content), `Skein.Server.Tests` (loopback end to end).
- `docs/` — `VISION.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `modding.md`, `design/` (one contract per system), `lore/`, `infrastructure/`, `adr/`.
- `deploy/` — Dockerfile, Compose, VPS bootstrap. `config/server.yaml` — server settings.

## Commands

```bash
dotnet build Skein.sln                                   # everything, including the Godot client assembly
dotnet test Skein.sln                                    # all tests
dotnet run --project src/Skein.Tools -- validate          # content must report 0 errors (warnings are reviewed)
dotnet run --project src/Skein.Tools -- load --x 0 --y 0  # headless zone smoke
dotnet run --project src/Skein.Tools -- fight a b         # headless duel with event trace
dotnet run --project src/Skein.Server.Host                # run the server (TCP 7777)
dotnet run --project src/Skein.Tools -- probe             # connect to a running server over TCP and exercise it
godot --path src/Skein.Client -- --smoke --shots DIR      # scripted client play-through against a running server (Godot 4.7 .NET)
```

## Verification gates

Before calling work done:
1. `dotnet build Skein.sln` is clean.
2. `dotnet test Skein.sln` passes.
3. `skein validate` reports 0 errors and no new warnings.
4. If the change touches generation, movement, combat or networking: `skein load`, `skein fight`, and the loopback server tests still pass; for networking also run the server and `skein probe`.
5. If the change touches the client: `dotnet build src/Skein.Client` compiles, and when Godot is available, the smoke play-through above passes against a local server (it saves screenshots to look at).

## Conventions

- **Content first.** New gameplay = a definition kind, a handler or a system that reads content. Numbers go in `rules.yaml` or on definitions, never in code. Formulas are expressions.
- **One modifier engine.** Every numeric change to anything is a `Modifier` on an `AttributeSet` (flat, increased, more, override, link; scoped by tags; conditions; gauges). Do not invent parallel stat systems.
- **Tags, not type checks.** Scope, gate and filter by tags. A new tag costs nothing.
- **Registries for open sets.** Effect types, AI actions, objective types, interactable kinds, component binders, content kinds, procedures, generator passes: register, never switch on strings inside systems.
- **Events, not calls.** Systems publish `GameEvent`s; quests, loot, networking and tests subscribe.
- **Determinism.** Every roll uses an `Rng` derived from a seed. Never `System.Random` or wall-clock time in gameplay code.
- **Server authority.** The client sends intents and renders state. Validation happens in the simulation.
- **Coordinates.** Core is X east, Y north, Z up. Godot conversion only in `Coords.cs`.
- **Numbers from JSON nodes** go through `JsonNum` (nodes hold ints, longs or doubles depending on origin).
- **Docs are durable.** No counts, rosters or version strings that go stale. Each system has one contract in `docs/design/`; update it with the system.
- Avoid em-dashes in prose. Files are UTF-8 with LF.

## Decision records

Stack and shape decisions live in `docs/adr/`. Read them before proposing a different engine, transport, format or topology.
