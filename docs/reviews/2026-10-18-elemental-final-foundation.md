# Elemental integration of the final reviewed foundation

Historical checkpoint at `064ff049`. The [complete foundation union](2026-10-18-elemental-complete-union.md) records the subsequent occupied-start, flight-floor, return and layered-ground integration; the evidence below remains tied to its original source.

PR #40 consumes the exact complete #34 `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`, including runtime `26431c48`, reviewed #36 `01b2884`, #33 `a3eafab`, main `17ca2f1` and the complete main `fa7f5f0` / PR #56 test-only lifecycle correction. No duplicate cherry-pick or competing runtime implementation is added. The existing shared renderer and moving-villager harness corrections are retained.

Merge conflicts are limited to the population contract and branch-specific world goldens. The reviewed population contract is accepted, preserving pattern-level defaults and explicit site-level opt-ins, including the shipped Knot. The elemental golden file initially retains this branch's prior values so the first comparison can expose changes for audit; foundation golden constants are not copied. Elemental body dimensions, content, site-level opt-ins and AI/anatomy boundaries remain unchanged.

The combined runtime is `3f414d7da5f65212d6272a6bab042141f4127adb`. Evidence uses the `final-foundation-` prefix under `experiments/elementals/evidence/review-fixes/`. Old evidence at `ce4c992` remains tied to that source.

## Own world audit

The eight-zone audit compares `main-levels-after.json` from `ce4c992` with `final-foundation-levels-after.json`. All site identities, site levels and per-pattern spawner/body counts are unchanged. All four terrain maps and POI lines are identical. Only Knot resident levels change: cinder imp, remnant shade and tangle now explicitly use their own site's level instead of pattern levels 4, 2 and 6. Other residents retain their prior levels, including level-5 starting guards/quartermasters and the elemental opt-ins. The full per-site changes are in `final-foundation-world-review.txt` and `final-foundation-level-diff.json`.

The starter offers are now all level 1: Clear Knot reaches `poi.0_0.knot.200.60` and kills Cinder Imp; Gather Floss and Cull Hob retain their targets; Scout Ruin moves from `poi.0_1.ruin.213.450` to `poi.1_1.ruin.305.334`. The Knot's three resident types are level 3, within the shipped two-level quest window. The other seven zones still have no generated offers. The original missing offer is therefore resolved on this branch without changing ordinary residents to suit the quest.

The initial unchanged-golden run records four failures and three passes. After auditing the intended level and offer changes, only the origin info hash, three affected chunk hashes and overlaid-world hash are refreshed from this branch's generated output. The other three info hashes, `(1,0)` chunks and climate hash remain unchanged. The origin and overlay values differ from the foundation's constants; no foundation golden file was copied. The audited source is `11b26cf0d49f94a67d8577e7b948a8b2ec5185ad`; runtime and content are unchanged from `3f414d7`.

## Functional evidence

`dotnet build HollowWake.sln -c Release` passes with zero warnings/errors. After the audited golden update, `dotnet test HollowWake.sln -c Release` passes Core 2580, native client 134 and server 59 (2773 total), including all 168 elemental cases and the incoming projectile-launch, nested-flight, aim-wrapper/placement and population-anchor coverage. There are no new elemental dimensions, anatomy, AI behaviors or collision implementations.

`npm --prefix web test` passes 314 tests across 39 files, including the three moving-villager interaction cases. The first combined Node invocation incorrectly included that Vitest file: the 10 lifecycle and 10 porter cases passed, then Vitest failed to initialize under Node's runner. `final-foundation-harness-tests.txt` preserves this invocation error. The corrected `node --test scripts/test-background.test.mjs scripts/port/port-prototype.test.mjs` passes all 20; no product or test-source workaround was made.

Release Tools `validate` reports 0 errors/warnings. The 200-tick origin `load`, `fight ember_elemental frost_elemental`, `protocol-ts --check` and private TCP probe pass. All six requested features are granted: `hit_bodies_v2`, `projectile_slabs_v1`, `entry_flow`, `cast_cues`, `entity_delta` and `interface`.

`node scripts/test-background.mjs --suite all` completes private run `run-OmYdVD`: browser 31/31, native state 36/36, `cleanupErrors: []`. Typecheck, web production build and native build/import pass. Fresh card/grayscale frames were inspected: terrain and bodies render in both modes, grayscale is applied, and starting guards/quartermasters remain level 5. The large-chunk advisory and native shutdown RID/ObjectDB diagnostics are retained. Native rendered pixels and controller/desktop integration remain outside headless coverage. Harness/probe workers stopped; only native import UIDs newly generated by this run were removed.

The generated `weave --axes --all` census matches all 499 reviewed keys exactly, in ordinal order; the existing fixture is unchanged. Windows Release `tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json` matches all seven inherited hashes. No replay baseline, seeds or cap changed.

The same current Release runtime runs `tournament --bands` with exact complete `c3099a8` foundation content and with elemental content. Both exit 1, and their entire JSON is byte-identical. After the standard evidence line-ending normalization, it is also byte-identical to `ce4c992`'s prior `main-proving-after.json`, SHA256 `ad9fca611d36b510c684173752559f37f3a34b376c10dea50f57ffd1a4b67282`. Bulwark, plaguer and pitfighter each still win 10/10 against 80% ceilings. All mechanisms, traces, outcomes and breaches are preserved. No band widening or balance clearance is claimed.

No benchmark is authorized or run. The previous snapshot miss (1.222392 ms/player against 1 ms), original process-wide expression allocation counter (0.0168 bytes/eval), and inherited proving-pair balance breaches remain open. Shared #20/#50 combined acceptance belongs to the coordinator. This PR remains draft; no merge into main is authorized.

Claude rereview is requested for the complete reviewed dependency ancestry, own world/quest audit and refreshed constants, preserved elemental dimensions/content, incoming aim/placement corrections and explicit functional-versus-performance boundaries. This integration is functionally READY/QUIET; hosted checks will be reported against the published evidence head.

Acknowledged by: pending Claude rereview.
