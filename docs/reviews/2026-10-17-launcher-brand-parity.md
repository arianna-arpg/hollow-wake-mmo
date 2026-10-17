# Quick-play launcher identity parity

The owner requested alignment with the in-flight `Unify Hollow Wake branding` chat. Its worktree is `codex/brand-and-wisp-loading`, with identity and loading ownership recorded in `2026-10-17-brand-and-loading.md` there. This follow-up on PR #10 imports its identity foundation into the quick-play branch so the launcher and installer can be tried with the same artwork now.

## Ownership and integration

Before editing the overlapping files: this change reuses that session's canonical `assets/branding` files, their provenance, asset-only synchronization, launcher artwork projections, and window/header/installer bindings. It also synchronizes the matching client artwork, login headers, native splash/export settings and browser favicon, without importing the still-in-progress loading implementation. No files in the branding worktree are modified. The provenance's legacy import tool arrives with that branch; this branch consumes the already-imported canonical assets.

The overlapping launcher changes are intentionally identical. When the branding branch merges, retain its additional loading-bootstrap section in `scripts/sync-branding.mjs`, its import tool, client startup settings, and loading contract. Keep PR #10's release picker, primary Play action, local startup, and quick-play hooks. The Crossing owns actual client preparation and terrain readiness; launcher download progress and game-process startup must not claim that terrain is ready. No extra splash delay or separate launcher mini-game is introduced.

The quick-play hooks use the ordinary login panel's `Enter`/`enter` methods. Keep the branding branch's selection handlers (`SelectCharacter` and `beginEntry`) behind those events so resumed characters acquire the same loading lease as manual selection. The native quick-play node belongs after initialization and the smoke/mockup exclusions; the browser prefill can connect normally while the existing boot lease finishes. These are code-inspection integration notes, not a claim of a combined loading runtime test.

## Verification

This serves data over code and shared presentation: the identity has one canonical source, byte-identical adapter copies, and the same checks across clients. Gameplay and the server protocol are unchanged.

| Check | Result |
|---|---|
| `node scripts/sync-branding.mjs --check` | Brand parity PASS; CI runs the same check |
| `dotnet build HollowWake.sln --nologo -v quiet` | 0 warnings, 0 errors |
| `dotnet test HollowWake.sln --no-build --nologo -v quiet` | Core 430, Server 36, Client 79 passed |
| Tools `validate` | 0 errors, 0 warnings |
| Launcher build, tests, offline smoke | Build clean; 33 passed; SMOKE launcher PASS |
| `npm --prefix launcher run dist:win` and packaged offline smoke | Installer built; packaged SMOKE launcher PASS |
| `node experiments/launcher-quick-play/verify-launcher.mjs --packaged` | Packaged insignia rendered; bundled SVG/PNG/ICO match canonical bytes; selected older build installed; Play during download, duplicate prevention and stalled-feed launch passed |
| Web tests/build and final full browser smoke | 138 tests passed; build clean; 25/25 smoke checks passed, no page errors |
| `node experiments/launcher-quick-play/verify-browser.mjs` | Branded Pages branch picker fits at 700x700; branch link, first-run creation and remembered-character resume passed |
| Native smoke against a fresh dedicated server on port 17787 | 22/22 passed, including villager conversation and vendor interaction |
| Windows `package-client.sh --check` | Branded export booted and loaded content with 0 errors/warnings; test captures excluded from the final archive |

The first native run on the reused shared playtest world passed 21/22, timing out on villager conversation. A second passed 20/22, failing quartermaster and vendor interactions. The final run on a fresh, dedicated server passed all 22 without changing gameplay or the smoke script. This isolates a successful run; it does not claim those shared-world test failures were fixed. Both failures and the clean run are retained under the ignored `experiments/launcher-quick-play/output` directory (`brand-native`, `brand-native-final`, `brand-isolated`).

The packaged launcher's `launcher-ready.png` and browser branch-picker captures were visually inspected. Artwork was imported from the active branding worktree, not regenerated or redrawn. Full loading integration is owned by that branch and remains an integration check until both implementations are combined. Claude review is requested for the shared-file merge and for keeping the Crossing's readiness and cancellation behavior behind the quick-play selection hooks. The final PR checks and release dry run will be linked on PR #10.

Acknowledged by: pending Claude review.
