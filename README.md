<p align="center"><img src="src/HollowWake.Client/icon.svg" width="96" alt="Hollow Wake MMO"></p>

<h1 align="center">Hollow Wake MMO</h1>

<p align="center"><b>An MMOARPG foundation where every rule, number, creature, place and system is data, and the world is a seamless, generated land.</b></p>

Hollow Wake MMO is a server-authoritative online action RPG set in **Aureth**, a high-fantasy world woven from nine Threads. Players arrive in the Weftmarches, a frontier of generated regions, zones and chunks seeded with hand-placed landmarks, points of interest and the quests those places give rise to. They fight with weaves they compose themselves, loot and reweave gear, bind Founts to raise Holdings, and grow a character whose build is theirs to invent.

This repository is the foundation: the engine-agnostic simulation core, the authoritative server, the wire protocol, a Godot client, the first content pack, the tools and the documents that hold the vision steady. It is built so that nearly everything is **extensible, flexible, customizable, modifiable, attributable and configurable** rather than hard-coded.

## What is here

| Part | What it is |
|---|---|
| `src/HollowWake.Core` | The simulation. Attributes and modifiers, expressions, content layers, entities, weaves and effects, combat, statuses, items, loot, AI, quests, crafting, territory, world generation, movement with z-levels. No engine, no sockets. |
| `src/HollowWake.Protocol` | The messages between client and server. |
| `src/HollowWake.Server` | Sessions, zones, interest management, persistence, world events. Hosted by `HollowWake.Server.Host` (pure .NET, TCP) or `HollowWake.Server.Host.Brutal` (.NET 10 + BRUTAL RakNet UDP). |
| `src/HollowWake.Client` | Godot 4 client: generates the same terrain locally from the server's seed, renders placeholder bodies, offers an isometric and a third-person camera over one fully 3D world. |
| `src/HollowWake.Tools` | The `hollowwake` command line: validate content, roll items, simulate fights, describe generated regions and zones, run a zone headless, probe a live server. |
| `launcher/` | The desktop launcher players install: it installs and updates the game client from GitHub Releases and starts it. |
| `content/core` | The first content layer. Everything the engine runs comes from here. |
| `docs/` | The vision, the architecture, one design contract per system, the lore bible, hosting research, decision records. |
| `deploy/` | Dockerfile, Compose file, a VPS bootstrap script. |

## Play it

Players install the launcher, and the launcher installs the game.

- **Windows.** Run `HollowWakeMMO-Launcher-Setup-<version>.exe` from the [latest release](https://github.com/arianna-arpg/hollow-wake-mmo/releases/latest). It installs for the current user, no admin needed. Builds are not code-signed yet, so Windows SmartScreen may ask before the first run.
- **Linux.** Take `HollowWakeMMO-Launcher-<version>-x86_64.AppImage` from the same page, `chmod +x` it and run it.

On start the launcher checks GitHub Releases, installs or updates the game client (every download is checked against the release's manifest), and keeps itself up to date from stable releases. Enter a server in the Server field (`127.0.0.1:7777` is a server on the same computer) and press Play. The channel toggle switches between Stable releases and Nightly pre-releases for testers; Developer mode adds DevTools and verbose logs. While this repository is private, the launcher needs a GitHub token with read access, under Settings or in `HOLLOWWAKE_GITHUB_TOKEN`. How the launcher, the releases and the channels work: [docs/design/launcher.md](docs/design/launcher.md).

## Run it from source

Requires the .NET 10 SDK (the .NET 8 targeting pack is fetched automatically) and, for the client, Godot 4.7 .NET edition.

```bash
dotnet build HollowWake.sln
dotnet test HollowWake.sln
dotnet run --project src/HollowWake.Tools -- validate
dotnet run --project src/HollowWake.Server.Host
```

Open `src/HollowWake.Client/project.godot` in Godot, press play, connect to `127.0.0.1:7777`, make a character and walk out of Ninewells.

Useful tools:

```bash
dotnet run --project src/HollowWake.Tools -- zone --x 0 --y 0 --map
dotnet run --project src/HollowWake.Tools -- fight loosed_kindler boar --seconds 20
dotnet run --project src/HollowWake.Tools -- item random --class weapon --level 20 --rarity rare
dotnet run --project src/HollowWake.Tools -- probe --host 127.0.0.1 --port 7777
```

## Where the truth lives

| If you want | Go to |
|---|---|
| The vision and the pillars every change is measured against | [docs/VISION.md](docs/VISION.md) |
| How the pieces fit, the data flow, the scaling path | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| One system in depth | [docs/design/](docs/design/) |
| The launcher, releases and update channels | [docs/design/launcher.md](docs/design/launcher.md) |
| The world's lore | [docs/lore/](docs/lore/) |
| How to add or change content, or write a mod | [docs/modding.md](docs/modding.md) |
| Hosting, scaling, costs | [docs/infrastructure/](docs/infrastructure/) |
| Why a decision was made | [docs/adr/](docs/adr/) |
| What comes next | [docs/ROADMAP.md](docs/ROADMAP.md) |
| Working conventions, verification gates | [CLAUDE.md](CLAUDE.md) |

## Status

A foundation, not a game yet. The simulation runs end to end headless and over the network: characters, movement with jumping and stacked surfaces, weaves and projectiles, damage, statuses, loot, quests generated from the world, crafting minigames verified by the server, fount binding and holdings, world events that reshape generation. Presentation is placeholder geometry. See the roadmap for the order of what follows.

## License

Not yet chosen. All rights reserved until a license is added. BRUTAL (used by the optional server host) is Copyright (c) 2024 Ahwoo Limited under its own licence.

Made by Arianna.
