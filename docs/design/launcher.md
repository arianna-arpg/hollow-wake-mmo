# Launcher and releases

The launcher is how players get, update and start the game. It is an Electron app in `launcher/` that installs the Godot client from this repository's GitHub Releases and hands it a server address. It holds no game logic and never talks to the game server: it passes the address to the client and steps aside.

## What a player sees

A plain game launcher: the title, a news panel, a server address field, a channel toggle, the big button, and a footer with Developer mode, Settings and Quit.

- **News** comes from `launcher/news.json` on main (a message of the day plus dated items with optional https links), so editing that file and merging it is how news reaches players. Everything in it is reduced to bounded plain text before the page sees it. When the file cannot be fetched the last copy that loaded is shown, and before any copy has loaded, the news that shipped inside the launcher, with a note saying which.
- **Server** is `host`, `host:port`, `[ipv6]:port` or a bare IPv6 address; the port defaults to 7777 and the field defaults to `127.0.0.1:7777`. It is checked for shape only and remembered.
- **Channel** picks which releases install: Stable (hand-cut releases) or Nightly (pre-releases for testers, plus any stable that outranks them).
- **The big button** reads Install, Update, Switch (after a channel change that leads to an older build), Play, Cancel while working, or Retry when nothing is installed and the feed is unreachable. When an update is waiting, a link still plays the installed build.
- **Developer mode** opens DevTools (F12 toggles them), shows the log with debug lines, and copies the client's output into it while the game runs.
- **Settings** holds Install updates automatically (on by default), the GitHub token field, and a link to the data folder.

## Starting the game

Play runs the installed executable with `--host H --port P` from the address field, with its working directory set to the build folder and its output written to `launcher/logs/client.log`. The client reads those arguments (engine or user arguments, `--host=H` too) and fills its login panel with them; it finds `content/core` beside its executable, where the release archive puts it. The game is started detached, so closing the launcher leaves it running, and the GitHub token and Electron's variables are removed from its environment. Play works offline whenever a build is installed.

`--play` on the launcher's command line starts the game once the start-up check, and any update it began, are done.

## The update flow

1. **Ask the feed.** Stable reads `GET /repos/{owner}/{repo}/releases/latest`, which by GitHub's definition is never a draft or a pre-release. Nightly reads the release list and takes the newest published release by version, pre-releases included. A release counts only when it carries a fully uploaded `manifest.json`; drafts never count. The repository comes from `repository` in `launcher/package.json`.
2. **Read the manifest** and validate it: schema, semantic version, channel, and per platform a plain archive file name, a positive size, a sha256 and a safe relative executable path. A manifest schema newer than the launcher understands is refused with a request to update the launcher.
3. **Compare** the manifest's version with `client/installed.json`, provided the release has a build for this platform (`windows-x64`, `linux-x64`). Within a channel an install only moves up; switching channel may move down, because the player asked for that channel's build.
4. **Download** the archive to `downloads/`, hashing as it streams, with progress and speed. A stalled connection fails after a minute without data; Cancel stops it. The file is kept only if its size and sha256 match the manifest; otherwise it is deleted and the update reported as failed, and the installed build stays playable.
5. **Extract** into a staging folder beside the target. Entries that climb out of the folder, absolute paths and symbolic links are refused; Unix permission bits recorded in the archive are restored (minus group and world write) and the executable is marked executable.
6. **Switch.** The staging folder becomes `client/<version>/`, `installed.json` is rewritten atomically, the archive is deleted, and older build folders are removed. Builds live side by side, so an update never touches a running game; a folder still in use is left and removed on a later start.

With Install updates automatically on, steps 4 to 6 start by themselves after the check. A failed or cancelled automatic update is not retried until the next start or Check for updates.

## Release layout

Every release, stable or nightly, carries:

| Asset | What it is |
|---|---|
| `manifest.json` | The launcher's entry point: version, channel, commit, and per platform the archive name, size, sha256 and executable. |
| `HollowWakeMMO-client-<version>-windows-x64.zip` | The exported Windows client: `HollowWakeMMO.exe`, its `.pck`, the .NET data folder, `content/core` and `hollowwake-build.json`. |
| `HollowWakeMMO-client-<version>-linux-x64.zip` | The same for Linux, with `HollowWakeMMO.x86_64`. |
| `HollowWakeMMO-Launcher-Setup-<version>.exe` and its `.blockmap` | The Windows launcher installer (NSIS, per user, no admin). |
| `HollowWakeMMO-Launcher-<version>-x86_64.AppImage` | The Linux launcher. |
| `latest.yml`, `latest-linux.yml` | electron-updater's feed for the launcher's own update, with sha512 checksums. Nightlies carry them too, but the launcher only reads them from the latest stable release. |
| `SHA256SUMS.txt` | Checksums of everything above, for people. |

`scripts/package-client.sh` builds the archives and the manifest: it exports each platform with its preset in `src/HollowWake.Client/export_presets.cfg` (the executable name comes from the preset's export path), copies `content/core` beside the executable, writes `hollowwake-build.json`, zips the folder and runs `launcher/scripts/make-manifest.mjs`. The launcher's parser and that writer are tested against each other.

## Channels and versions

Versions are semver. A stable release is `vX.Y.Z`. A nightly is the patch after the newest stable with a nightly suffix, `vX.Y.(Z+1)-nightly.YYYYMMDD.RUN` (before the first stable, the launcher's `package.json` version takes the place of X.Y.Z). Semver orders a nightly above the stable before it and below the stable it leads to, so the stable supersedes it the moment it ships. Any tag with a pre-release suffix publishes a pre-release that only the nightly channel sees.

## The pipeline

`.github/workflows/package.yml` holds the jobs every release runs:

- **client**: installs the .NET SDKs, downloads the official Godot .NET editor and export templates for the Godot version the client's csproj names (checked against Godot's `SHA512-SUMS.txt` and cached), runs `scripts/package-client.sh --check`, which also boots the Linux build headless and fails unless it loads its content.
- **launcher**, on Windows and Linux runners: stamps the release version into `launcher/package.json`, builds, runs the unit tests, packages with electron-builder (`--publish never`) and boots the packaged app through `--smoke-test --offline`.
- **publish**: collects everything, writes `SHA256SUMS.txt`, uploads into a draft release and then publishes it in one step. Launchers and electron-updater never see drafts, so a release appears complete or not at all, and a failed job publishes nothing.

`release.yml` runs those jobs for a pushed `v*` tag, with the annotated tag's message as the release notes (GitHub generates notes for a lightweight tag). It also runs as a dry run, building everything and publishing nothing, by hand and on pull requests that touch the pipeline. `nightly.yml` runs on a schedule when main has moved since the last nightly (or by hand, with force to build anyway), publishes a pre-release with the commits since the previous nightly or stable, and prunes older nightlies.

To cut a stable release:

```bash
git tag -a v0.2.0 -m "What changed, for players"
git push origin v0.2.0
```

## The launcher's own update

electron-updater checks the repository's latest stable release a few seconds after start, whatever the game channel: pre-releases are never offered and the launcher never downgrades. The update downloads in the background, is verified against the sha512 in `latest.yml` or `latest-linux.yml`, and installs on Restart to update (offered in the corner) or when the launcher quits. It works for the NSIS install on Windows and the AppImage on Linux; a development run (`npm start`) says it does not self-update.

## Private repositories

While the repository is private, GitHub answers 404 to a request without a token, for the feed, the news and electron-updater alike. The launcher tells that case apart from an empty feed (it asks for the repository itself) and says to add a token. A token comes from `HOLLOWWAKE_GITHUB_TOKEN`, which wins, or from the Settings field, where it is stored encrypted with the system key store (stored plainly, and said so, where none exists). It is sent only to the GitHub API host: releases are listed there, assets and the news are fetched through the API endpoints (whose redirects to storage carry no token), and electron-updater switches to its private GitHub provider. A fine-grained token with read access to the repository's contents is enough. With the repository public, no token is needed.

## Where files live

One per-user folder holds everything the launcher writes:

| Platform | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\HollowWakeMMO` |
| Linux | `$XDG_DATA_HOME/hollowwakemmo`, normally `~/.local/share/hollowwakemmo` |

| Inside | What |
|---|---|
| `client/<version>/` | The installed build. |
| `client/installed.json` | Version, channel, platform, executable, sha256 and tag of the installed build. |
| `downloads/` | Archives in flight (`.part` until verified); cleared at start. |
| `launcher/settings.json` | Address, channel, Developer mode, automatic updates, the token. |
| `launcher/logs/launcher.log`, `client.log` | The launcher's log (rotated at a megabyte) and the client's output from the last Play. |
| `launcher/news-cache.json` | The last news that loaded. |
| `launcher/electron/` | Chromium's profile for the launcher window. |

The launcher itself installs to `%LOCALAPPDATA%\Programs\Hollow Wake MMO` on Windows; the AppImage runs from wherever it is kept. electron-updater keeps a pending launcher update in `%LOCALAPPDATA%\hollow-wake-mmo-launcher-updater` (on Linux, `~/.cache/hollow-wake-mmo-launcher-updater`).

## Testing it without a release

Environment overrides, shown in a banner while in effect:

| Variable | Effect |
|---|---|
| `HOLLOWWAKE_FEED` | A `manifest.json` (path, `file:` or `http(s):` URL) to install from instead of GitHub; archives resolve beside it, or from a platform's `url`. |
| `HOLLOWWAKE_GITHUB_API` | The REST root instead of `https://api.github.com` (an Enterprise host or a mock). |
| `HOLLOWWAKE_REPO` | `owner/name` instead of `package.json`'s repository. |
| `HOLLOWWAKE_NEWS` | A `news.json` path or URL. |
| `HOLLOWWAKE_DATA_DIR` | The data folder, so a trial never touches a real install. |
| `HOLLOWWAKE_LAUNCHER_UPDATE_URL` | A folder URL holding `latest.yml` and an installer, for trying the launcher's own update. |
| `HOLLOWWAKE_LOG_STDOUT=1` | Copy the log to stdout. |

`scripts/package-client.sh --godot <editor> --version 0.1.0 --out <folder>` builds a feed folder from a checkout, `node launcher/scripts/serve-feed.mjs <folder> [--rate KB/s]` serves it on loopback, and `HOLLOWWAKE_FEED=http://127.0.0.1:8787/manifest.json` points a launcher at it. Corrupting a sha256 in that manifest shows the failure path.

Flags for checks: `--smoke-test` boots the page and checks its bridge, toggles and news, prints one `SMOKE launcher PASS` or `FAIL` line and exits with the result; `--screenshot FILE` saves the window once the feed and news have settled (`--screenshot-delay MS` waits longer, to catch a download); `--offline` keeps the network out of it. Smoke and screenshot runs never install a launcher update. `npm test` runs the unit tests: release selection, the private-repository and token cases against a mock API (including that the token never reaches another host), verified and refused downloads, hostile archives, installs, updates and failed updates, news sanitising, settings, and the manifest writer against the parser.
