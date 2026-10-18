# Hollow Wake identity and loading

Codex owns this implementation on `codex/brand-and-wisp-loading`, in the dedicated managed worktree. The integration checkout was not edited. An unrelated `.claude/` directory appeared there during the work and was left untouched.

## Source and scope

The owner clarified that the Mu wisp-and-vessels scene is future character selection. This change ports the loading mini-game from `arianna-arpg/arpg-game-world@64819bd4351001a34c92eed13b63e1f55f0c52ef`. Character selection retains the current flow. Identity assets and menu glyphs come from the earlier checkout at `118b510f`, recorded in `assets/branding/provenance.json`.

The original PNG/ICO/favicon are copied byte for byte; the website SVG mark and menu glyphs retain their geometry. A canonical asset directory and checked synchronizer supply both clients and the launcher. The existing icon registry gains filled accent paths while retaining its MMO-specific mappings.

Both clients use the Crossing for startup, character entry and authoritative world re-entry. The content definition owns tunings, seed sequence, controls, labels, colors, composition and the original wisp recipe. Token-owned preparation leases isolate work from the toy. The model is rewardless and separate from simulation state and RNG. World entry finishes on actual hero/nearby-terrain readiness. Retry, cancellation and held-input release are explicit. Native queued messages and deferred disconnect callbacks carry a connection generation; browser callbacks require the current socket.

Vision: data over code, composition and registered presentation adapters; no protocol shape change, gameplay reward or server-authority change. Contract: [loading.md](../design/loading.md). Captures: [experiment evidence](../../experiments/brand-and-loading/README.md).

## Ownership and integration

At the initial claim, open PR #6 owned cast presentation and its study; those files were not edited. At final verification, PR #10 had opened on launcher quick play and PR #9 had a presentation claim record. Shared areas with #10 are launcher main/header, client orchestration, browser startup and the associated contracts/CI. This record calls out that overlap before integration; no other branch's files were checked out or overwritten.

Merge coordination with #10 must route automatic selection through the loading-aware selection callback (`SelectCharacter` / `beginEntry`), and launch it after content initialization. Keep the quick-play ribbon when resolving browser startup, and retain the canonical icon/header when resolving launcher changes. PR #9's future appearance implementation should compose with this loading recipe rather than introducing another identity source.

## Verification

Local gates on Windows:

- `dotnet build HollowWake.sln --no-restore`: 0 warnings, 0 errors.
- `dotnet test HollowWake.sln --no-build`: Core 430, Client 86, Server 36 passed (552 total). Includes malformed loading-content rejection, stale/retry leases, transforms and source-trace parity.
- `hollowwake validate`: 0 errors, 0 warnings.
- `hollowwake load --x 0 --y 0`: 100 chunks; ran 200 ticks successfully. `fight hob hob`: completed with life 0/78 and 26/78. The documentation's literal `fight a b` was also tried; those placeholder patterns are absent and that invocation is not counted as evidence.
- Live `hollowwake probe --host 127.0.0.1 --port 17877 --account loading-probe --seconds 3 --features entity_delta,interface`: `PROBE OK (walked south)`, both features granted and interface states received.
- Native real-client smoke against the isolated server: 22/22 passed, including entry, movement, combat and all interface pages. Native loading smoke: all checks passed, including repeated Tab focus containment, with captures for down/left/right. Native `--boot-check`: `BOOT PASS`.
- `npm --prefix web test`: 142/142 passed. `npm --prefix web run build`: passed. Protocol regenerated without a diff. Browser real-client smoke: 25/25 passed, no page errors.
- Browser loading course: every direction with keyboard, pointer, touch and controller; stale leases; actual/unknown progress; retry and failure without retry; held keys/controller quarantine; focus restoration; portrait/reduced-motion and touch cancellation. `LOADING SMOKE PASS`.
- Browser delayed-entry course against the real server: playable slow entry, cancellation, ignored late frames, reconnect, timeout, retry without duplicate selection, and completion on readiness. `ENTRY SMOKE PASS`.
- Launcher build, 32/32 tests and offline `SMOKE launcher PASS`. Compatibility icon script runs successfully. `node scripts/sync-branding.mjs --check`: `Brand parity PASS`.
- `git diff --check`: clean. PR release dry-run status is reported by its checks; it must pass before merge.

`scripts/loading-reference.mjs` runs the original model at the pinned commit and records independent traces. Both ports match the same seeded courses in all directions, including gate geometry and encounter/impact/pace values. Screenshots were inspected for the inherited mark, alphabet, wisp, soul flames, gate openings and responsive layout.

Native full-smoke shutdown still reports an orphaned standard Button and dependent rendering resources. An untouched archive of base `63436df`, built and run under the ignored experiment output, also reports that orphan (16 ObjectDB instances on base, 19 on this branch). The diagnostic comparison's baseline course had one vendor-state timing failure (21/22), so it is not claimed as a passing gate; the branch's full course passed 22/22. Dedicated loading and boot courses exit without that diagnostic. Loading-owned fonts and gradient resources are explicitly disposed. This unrelated standard-UI lifetime issue is documented rather than treated as a clean shutdown.

## Limits and review request

The pre-scene engine splash is static Hollow Wake artwork; the interactive Crossing begins as soon as the scene tree is running. The native and browser rendering backends differ in rasterization, font edges and glow gradients; this is shared mechanical and compositional parity, not a pixel-identity claim. The old account cosmetic inventory and Mu character-selection scene are not migrated here. Native controller hardware and touch hardware were not physically exercised; browser device inputs were exercised through browser automation, and native pointer transforms and model traces are pinned.

Claude review requested: check content ownership/validation, original-reference fidelity, connection cancellation and input handoff, export bootstrap/assets, and the merge points with quick play before merging. No peer review or merge is claimed.

Acknowledged by: pending Claude review.
