# Local development

## Requirements

- .NET 10 SDK. It builds the .NET 8 projects too by fetching the targeting pack.
- Godot 4.7 .NET edition for the client. The client assembly compiles without Godot installed; running it needs the editor or an export.
- Docker, optionally, for the container build.
- Windows, Linux or macOS for the pure .NET host. The BRUTAL host ships natives for linux-x64, win-x64 and osx-arm64.

## Everyday loop

```bash
dotnet build HollowWake.sln
dotnet test HollowWake.sln
dotnet run --project src/HollowWake.Tools -- validate
dotnet run --project src/HollowWake.Server.Host        # TCP on 7777, data under ./data
```

Then open `src/HollowWake.Client/project.godot` in Godot and press play. Connect to `127.0.0.1:7777` with any account name (development auth creates accounts on first use).

Without the client, `hollowwake probe` connects over TCP, logs in, creates a character, enters the world, walks and reports what it saw.

## Server configuration

`config/server.yaml` is read relative to the repository root (or `--root`). Fields and defaults are in `src/HollowWake.Server/Config/ServerConfig.cs`. `--port`, `--config`, `--root` and `--log` are command line overrides. Data (accounts, characters, world, modified chunks) is JSON under `data/`, ignored by git.

To require a shared secret: `auth: token` and `token: ...`; clients send it in the login message.

## The BRUTAL host

`dotnet run --project src/HollowWake.Server.Host.Brutal` runs the same server over RakNet UDP on port 7777. It needs the .NET 10 SDK; with an older SDK the project compiles to a stub that says so. The Godot client speaks TCP today; a RakNet client transport is on the roadmap, so use the pure host for client play.

## Docker

```bash
docker build -f deploy/Dockerfile -t hollowwake-server .
docker run -d --name hollowwake -p 7777:7777 -v hollowwake-data:/app/data hollowwake-server
docker build -f deploy/Dockerfile --build-arg HOST=brutal -t hollowwake-server:brutal .
```

`deploy/docker-compose.yml` runs the server with a persistent volume and mounts `config/server.yaml`; a Postgres service is defined behind the `db` profile for when the stores move to a database.

## Hosting

`hosting.md` in this folder is the researched plan: a one-box Docker Compose starter for a handful of players, the provider comparison with prices, the four-stage scaling ladder, the VPS bootstrap, backups and gotchas. `deploy/bootstrap-vps.sh` performs the bootstrap on a fresh Ubuntu LTS server.
