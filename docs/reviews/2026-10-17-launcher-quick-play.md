# Launcher quick play

Codex claims local playtest startup and launcher launch affordances on `codex/launcher-quick-play`.

The existing release launcher requires a separately started server for localhost. Its `--play` path waits for the feed and automatic updates. This change will provide a repeatable local startup path and keep installed builds readily playable.

Scope confirmed by the owner: desktop release selection, branch-specific browser play links, and one-step local startup with a server. This owns launcher sources, startup scripts and the Pages workflow. Client startup additions may touch `web/src/app.ts` and `src/HollowWake.Client/Main.cs`, also used by PR #6; those additions will stay confined to login/startup, with no cast-presentation edits. The integration checkout stays clean. Claude's ongoing repository-parity work is a dependency to check again before handoff.

## Integration coordination

PR #8 (`claude/github-setup-63b667`) appeared during implementation. It overlaps README, `docs/design/launcher.md`, `docs/infrastructure/codespaces.md`, `.github/workflows/ci.yml`, and the launcher's main/renderer files. Preserve its rewritten README, public-repository wording and aggregate `ci` check when integrating this feature. This branch does not alter visibility, branch protection, licensing or branch cleanup. PR #9 currently has only its presentation review record; the startup hooks in the client roots are additive and must be retained when its presentation code lands. No simulation or protocol changes are part of this feature.

Claude review requested for process ownership, repeat startup, error recovery, the release selection contract and the overlap with #8. Validation evidence follows below.

## What changed

- Release picker: choose a recent published build or follow the latest channel; explicit pins allow downgrades and are not automatically replaced. Bad/missing selected releases leave the installed game available.
- Installed Play stays primary during checks and downloads. `--play` starts immediately; a second click cannot spawn a duplicate. Downloads that finish while a game runs retain its build folder.
- Local entry points: `Play.cmd`, `Play Desktop.cmd`, `Stop Playtest.cmd`, backed by a cross-platform source helper. It caches successful builds, starts/reuses a checkout-owned server, handles conflicts without killing other processes and stops through the normal server save/shutdown path.
- Quick play on both clients connects and resumes a known or sole character. First-time and ambiguous accounts retain normal character creation/selection. All requests still pass through server authentication and character selection.
- Pages adds a branch picker and desktop-download link. Branch links create/resume the matching Codespace. Its dev-container creation/resume hooks start the matching client and server. Pages does not host a server. Older branches need these hooks merged before startup is automatic.

## Evidence

Commands ran from the isolated worktree on Windows; timing is local evidence, not a cross-machine performance guarantee.

| Check | Result |
|---|---|
| `dotnet build HollowWake.sln --nologo -v quiet` | 0 warnings, 0 errors |
| `dotnet test HollowWake.sln --no-build --nologo -v quiet` | Core 430, Server 36, Client 79 passed; 0 failed |
| `dotnet run --project src/HollowWake.Tools --no-build -- validate` | 0 errors, 0 warnings |
| Tools `load --x 0 --y 0 --ticks 100`, `fight loosed_kindler boar --seconds 20`, `probe --host 127.0.0.1 --port 7777` | Completed; PROBE OK |
| Launcher `npm run build`, `npm test`, `npm run smoke -- --offline` | Build clean; 33 tests passed; SMOKE launcher PASS |
| Web `npm test`, `npm run build` | 138 tests passed; build clean |
| Web smoke against local server | 25/25 passed; no page errors |
| Godot smoke against local server | 22/22 passed |
| Godot `--headless --quit-after 240 -- --quick-play --account quick-play-desktop-smoke` | QUICKPLAY READY: entered the world |
| `node --test scripts/play-local.test.mjs` | 2 passed: cache edits/deletions/output exclusion, configuration validation |
| `scripts/package-client.sh --version 0.1.1-quickplay.1 --platforms windows-x64 --check` with pinned Godot editor | Windows archive produced; exported build loaded content with 0 errors |
| `node experiments/launcher-quick-play/verify-launcher.mjs` | Real archive installed from chosen older release; Play during download; duplicate launch prevented; active build retained; --play started before an intentionally stalled feed, about 0.5 seconds to process launch |
| `node experiments/launcher-quick-play/verify-browser.mjs` | Encoded branch/resume link correct; narrow layout fits; first-run creation; existing-character resume, 241 ms from reload to enter_world in this run |
| `node experiments/launcher-quick-play/verify-local.mjs` | Port conflict preserved unrelated listener; repeat preparation 170 ms with same server PID; graceful save/shutdown cleared owned server record |

Reproducible integration scripts and ignored screenshots/results are under `experiments/launcher-quick-play/`. Larger package archives, engine tools and smoke captures stay ignored under `artifacts/`. The offline Electron screenshot flag hit a local GPU capture error; the required offline boot smoke passed, and the actual launcher was then captured and tested successfully through Playwright with GPU disabled.

## Remaining review/deployment checks

The release dry run and PR CI results will be recorded after the request opens. No release was published and Pages was not redeployed from this worktree. No billable Codespace was created; the same startup helper was exercised locally, while the live Codespace provisioning flow remains a cloud integration check. The current Pages settings already use Actions at the documented repository URL.

Acknowledged by: pending Claude review.
