# CLAUDE.md — Hollow Wake MMO

Guidance for Claude Code (and people) working in this repository. Committed and shared.

## What this is

Hollow Wake MMO is an MMOARPG foundation: an engine-agnostic C# simulation core, an authoritative server, a Godot client, a YAML content pack and tools. The core vision is in [docs/VISION.md](docs/VISION.md) and is the test for every change: **data over code, one modifier engine, tags as the universal join, composition over bespoke, server authority, a seamless generated world, and observability.** If a change hard-codes something content could express, it is probably wrong.

## Repository map

- `src/HollowWake.Core` — simulation. Namespaces mirror systems: `Attributes`, `Expressions`, `Content`, `Definitions`, `Ecs`, `Sim`, `Components`, `Effects`, `Weaves`, `Combat`, `StatusEffects`, `Items`, `Loot`, `Patterns`, `Ai`, `Movement`, `World`, `WorldGen`, `Quests`, `Crafting`, `Territory`, `Progression`, `Characters`, `Interaction`, `Intents`, `Systems`.
- `src/HollowWake.Protocol` — `Messages.cs` (every wire message) and `MessageSerializer.cs` (JSON + length framing).
- `src/HollowWake.Server` — `GameServer` (composition root and loop), `Sessions/`, `World/` (`ZoneHost`, `ZoneInstance`, `Snapshots`), `Net/` (`ITransport`, TCP, loopback), `Persistence/` (JSON stores), `Config/`.
- `src/HollowWake.Server.Host` — pure .NET entry point. `src/HollowWake.Server.Host.Brutal` — .NET 10 + BRUTAL RakNet entry point (compiles to a stub without the .NET 10 SDK).
- `src/HollowWake.Client` — Godot 4.7 C#. `Main.cs` orchestrates; `World/` generates terrain locally and renders entities; `Camera/CameraRig.cs` has both camera modes; `UI/` is placeholder UI.
- `src/HollowWake.Tools` — the `hollowwake` CLI (`Program.cs`, `Probe.cs`).
- `launcher/`: the desktop launcher (Electron + TypeScript, pinned versions, electron-builder). `src/main/` holds the release feed, the verified download, safe extraction, installs, self-update and game start; `src/renderer/` is the page; `src/test/` the node:test suite; `news.json` is the news panel's source. It holds no game logic. Contract: `docs/design/launcher.md`.
- `scripts/package-client.sh`: exports the client per platform with `src/HollowWake.Client/export_presets.cfg`, bundles `content/core`, zips, and writes the release `manifest.json`.
- `.github/workflows/`: `ci.yml` (every push and pull request), `release.yml` (v* tags; dry runs on dispatch and on pipeline pull requests), `nightly.yml` (nightly pre-releases), `package.yml` (the jobs both release workflows share).
- `content/core` — the base content layer, one YAML file per area. `rules.yaml` holds every formula and tunable.
- `tests/` — xunit. `HollowWake.Core.Tests` (foundations and gameplay against the real content), `HollowWake.Server.Tests` (loopback end to end).
- `docs/` — `VISION.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `modding.md`, `design/` (one contract per system), `lore/`, `infrastructure/`, `adr/`.
- `deploy/` — Dockerfile, Compose, VPS bootstrap. `config/server.yaml` — server settings.

## Commands

```bash
dotnet build HollowWake.sln                                   # everything, including the Godot client assembly
dotnet test HollowWake.sln                                    # all tests
dotnet run --project src/HollowWake.Tools -- validate          # content must report 0 errors (warnings are reviewed)
dotnet run --project src/HollowWake.Tools -- load --x 0 --y 0  # headless zone smoke
dotnet run --project src/HollowWake.Tools -- fight a b         # headless duel with event trace
dotnet run --project src/HollowWake.Server.Host                # run the server (TCP 7777)
dotnet run --project src/HollowWake.Tools -- probe             # connect to a running server over TCP and exercise it
godot --path src/HollowWake.Client -- --smoke --shots DIR      # scripted client play-through against a running server (Godot 4.7 .NET)
```

Launcher and releases (from `launcher/` unless noted):

```bash
npm ci                                                        # install the pinned dependencies
npm run build                                                 # compile main, preload, page and tests into dist/
npm test                                                      # unit tests: feed, verified download, extraction, install, manifest
npm start                                                     # run the launcher from source
npm run smoke                                                 # headless boot check; add -- --offline to keep the network out
npm run dist:win                                              # NSIS installer in release/ (dist:linux for the AppImage)
npx electron . --screenshot shot.png                          # save the window once the feed and news settle
node scripts/serve-feed.mjs DIR                               # serve a feed folder; then HOLLOWWAKE_FEED=http://127.0.0.1:8787/manifest.json npm start
bash scripts/package-client.sh --godot GODOT --version 0.1.0 --out DIR --check   # from the repository root: client archives + manifest.json
git tag -a v0.2.0 -m "notes" && git push origin v0.2.0       # cut a stable release (release.yml); nightly.yml cuts pre-releases
```

## Verification gates

Before calling work done:
1. `dotnet build HollowWake.sln` is clean.
2. `dotnet test HollowWake.sln` passes.
3. `hollowwake validate` reports 0 errors and no new warnings.
4. If the change touches generation, movement, combat or networking: `hollowwake load`, `hollowwake fight`, and the loopback server tests still pass; for networking also run the server and `hollowwake probe`.
5. If the change touches the client: `dotnet build src/HollowWake.Client` compiles, and when Godot is available, the smoke play-through above passes against a local server (it saves screenshots to look at).
6. If the change touches the launcher, the release workflows, `scripts/package-client.sh` or the export presets: in `launcher/`, `npm run build` and `npm test` pass and `npm run smoke -- --offline` prints `SMOKE launcher PASS`; the release dry run on the pull request is green. For the update flow itself, build a feed folder with `scripts/package-client.sh`, serve it, and install from it with `HOLLOWWAKE_FEED` and a throwaway `HOLLOWWAKE_DATA_DIR`.

## Conventions

- **Content first.** New gameplay = a definition kind, a handler or a system that reads content. Numbers go in `rules.yaml` or on definitions, never in code. Formulas are expressions.
- **One modifier engine.** Every numeric change to anything is a `Modifier` on an `AttributeSet` (flat, increased, more, override, link; scoped by tags; conditions; gauges). Do not invent parallel stat systems.
- **Tags, not type checks.** Scope, gate and filter by tags. A new tag costs nothing.
- **Registries for open sets.** Effect types, AI actions, objective types, interactable kinds, component binders, content kinds, procedures, generator passes: register, never switch on strings inside systems.
- **Events, not calls.** Systems publish `GameEvent`s; quests, loot, networking and tests subscribe.
- **Determinism.** Every roll uses an `Rng` derived from a seed. Never `System.Random` or wall-clock time in gameplay code.
- **Server authority.** The client sends intents and renders state. Validation happens in the simulation.
- **The launcher only delivers.** It installs verified builds and starts the client with the server address; it holds no game logic and never speaks the protocol. What it installs is described by each release's `manifest.json`, never hard-coded.
- **Coordinates.** Core is X east, Y north, Z up. Godot conversion only in `Coords.cs`.
- **Numbers from JSON nodes** go through `JsonNum` (nodes hold ints, longs or doubles depending on origin).
- **Docs are durable.** No counts, rosters or version strings that go stale. Each system has one contract in `docs/design/`; update it with the system.
- Avoid em-dashes in prose. Files are UTF-8 with LF.

## Decision records

Stack and shape decisions live in `docs/adr/`. Read them before proposing a different engine, transport, format or topology.
