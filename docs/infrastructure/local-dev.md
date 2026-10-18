# Local development

## Requirements

- .NET 10 SDK. It builds the .NET 8 projects too by fetching the targeting pack.
- Godot 4.7 .NET edition for the desktop client. The client assembly compiles without Godot installed; running it needs the editor or an export. Godot export templates (the editor's Export dialog installs them) to export it locally.
- Node 22 for the web client in `web/`; Node 24 for the launcher in `launcher/`.
- Docker, optionally, for the container build.
- Windows, Linux or macOS for the pure .NET host. The BRUTAL host ships natives for linux-x64, win-x64 and osx-arm64.

## Everyday loop

```bash
dotnet build HollowWake.sln
dotnet test HollowWake.sln
dotnet run --project src/HollowWake.Tools -- validate
dotnet run --project src/HollowWake.Server.Host        # TCP on 7777, WebSocket and the web client on 7778, data under ./data
```

Then open `src/HollowWake.Client/project.godot` in Godot and press play. Connect to `127.0.0.1:7777` with any account name (development auth creates accounts on first use).

Without the client, `hollowwake probe` connects over TCP, logs in, creates a character, enters the world, walks and reports what it saw.

## The web client

```bash
npm --prefix web ci                 # pinned dependencies
npm --prefix web run build          # type-check and build into web/dist
npm --prefix web test               # unit tests
npm --prefix web run dev            # hot reload on 5173, proxying /ws to a server on 7778 (HOLLOWWAKE_SERVER points elsewhere)
npm --prefix web run smoke -- --shots shots   # scripted play-through in an installed Edge or Chrome
```

The server serves `web/dist` on `websocket_port` beside the game socket, so with a build in place `http://localhost:7778/` is the game in a browser; the page finds the server at its own address. Client settings are in `web/public/config.json` (and `dist/config.json` after a build). On Windows, `HttpListener` needs a URL ACL to listen on every interface, so without one the server listens on `localhost` and `127.0.0.1` only and says so in its log; running `netsh http add urlacl url=http://+:7778/ user=Everyone` once as administrator opens it to other machines. Linux, macOS and containers have no such limit. After changing a message, regenerate the client's types with `dotnet run --project src/HollowWake.Tools -- protocol-ts`; the server tests fail until the module matches.

### GitHub Pages and Codespaces

- `.github/workflows/pages.yml` publishes the client to GitHub Pages at <https://arianna-arpg.github.io/hollow-wake-mmo/> on every push to main that touches `web/`: Pages is enabled on the repository with GitHub Actions as its source, the build job tests and builds the client and the deploy job publishes it. A pull request that touches `web/` gets the build and the tests without the deploy. In a fork where Pages is off, or set to a branch, the workflow still builds and tests the client, posts a notice and attaches the build as an artifact; the fallback is the server serving `web/dist` itself.
- The Pages copy is only a client: the player types or links the address of a running server, which an HTTPS page can reach only over `wss://`. A public Codespace port works; a server on the player's own machine is best played from the page it serves at `http://localhost:7778/`, since a browser may refuse to let an HTTPS page open a plain `ws://` socket.
- In a Codespace, `scripts/codespace-play.sh` builds and runs everything. Port 7778 has to be made public for other people or for the Pages client to reach it, the desktop client's TCP port is not forwarded over HTTPS, and the codespace stops when idle. [codespaces.md](codespaces.md) has the steps.

## The launcher

```bash
cd launcher
npm ci
npm start          # the launcher from source; npm test for its unit tests
```

It installs the desktop client into the per-user data folder from GitHub Releases. To try an install or an update without publishing anything, build a feed folder with `scripts/package-client.sh`, serve it with `node launcher/scripts/serve-feed.mjs`, and start the launcher with `HOLLOWWAKE_FEED` pointing at the served `manifest.json` and `HOLLOWWAKE_DATA_DIR` at a scratch folder. [design/launcher.md](../design/launcher.md) lists every override and flag.

## Server configuration

`config/server.yaml` is read relative to the repository root (or `--root`). Fields and defaults are in `src/HollowWake.Server/Config/ServerConfig.cs`. `--port`, `--config`, `--root` and `--log` are command line overrides. Data (accounts, characters, world, modified chunks) is JSON under `data/`, ignored by git.

To require a shared secret: `auth: token` and `token: ...`; clients send it in the login message.

## The BRUTAL host

`dotnet run --project src/HollowWake.Server.Host.Brutal` runs the same server over RakNet UDP on port 7777. It needs the .NET 10 SDK; with an older SDK the project compiles to a stub that says so. The Godot client speaks TCP today; a RakNet client transport is on the roadmap, so use the pure host for client play. The BRUTAL host has no WebSocket transport, so the web client needs the pure host too.

## Docker

```bash
docker build -f deploy/Dockerfile -t hollowwake-server .
docker run -d --name hollowwake -p 7777:7777 -v hollowwake-data:/app/data hollowwake-server
docker build -f deploy/Dockerfile --build-arg HOST=brutal -t hollowwake-server:brutal .
```

`deploy/docker-compose.yml` runs the server with a persistent volume and mounts `config/server.yaml`; a Postgres service is defined behind the `db` profile for when the stores move to a database.

## Hosting

`hosting.md` in this folder is the researched plan: a one-box Docker Compose starter for a handful of players, the provider comparison with prices, the four-stage scaling ladder, the VPS bootstrap, backups and gotchas. `deploy/bootstrap-vps.sh` performs the bootstrap on a fresh Ubuntu LTS server.
