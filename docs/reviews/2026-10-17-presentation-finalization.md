# Presentation integration review

Date: 2026-10-17 (local). Reviewer: Codex. Request: #9. Original head: `f5bd628`; integrated main through `91f234f` in an isolated worktree. The author and the active forecasting/launcher checkouts were not changed.

## Findings and changes

- Integrated the confirmed cast-cue renderer alongside the appearance service in the browser composition root. Both services survive the merge; presentation never changes authoritative damage or geometry.
- Fixed empty self equipment on both clients. Previously an empty authoritative map restored the Calling's kit. Now an available empty map draws the unadorned body. The production browser plan regression failed before this fix and passes after, also retaining other-player fallback.
- Added content looks for the new proving hexer/plaguer and the incoming pitfighter/mower. The coverage gate caught the first pair after main's status roster merge. Families, palettes and gear remain content; no pattern-specific rendering branch was added.
- Fixed global controller confirmation cancelling a background loading screen. Native confirmation requires window focus, browser confirmation requires a visible focused document, and both continue tracking edges while unfocused. The production loading smoke covers a background press, focus return with the button held, and a subsequent fresh focused press. The isolated commit is `b57ee40`, shared with the launcher and forecasting owners.
- Found the same empty-equipment error in the new public appearance descriptor seam in #22 and recorded the required absent-versus-empty correction there. That follow-up remains the active author's work.

Read: appearance contracts, content registration/validation, the paired look/gear/body/animator paths, equipment refresh and card lifetime paths, the composition merge, and loading confirmation. This is not a claim to have compared every body-family line or every original visual asset. Looked at native/browser captures including geometry, cards, impact and grayscale. Canopy occlusion and crowded labels remain visible and are not certified as a completed human readability study.

## Verification on the follow-up tree

- `dotnet build HollowWake.sln -c Release`: 0 warnings, 0 errors. `dotnet test HollowWake.sln -c Release --no-build`: 531 core, 110 client, 38 server passed.
- `npm --prefix web test`: 180 passed. `npm --prefix web run build`: passed.
- `hollowwake validate`: 0 errors, 0 warnings. `protocol-ts --check`: current. `load --x 0 --y 0`: 100 chunks, 200 ticks. The standard kindler/boar 10-second trace was unchanged in this review; no simulation behavior was edited.
- Isolated local server, TCP probe with delta/interface negotiation: PROBE OK.
- Production `web/scripts/loading-smoke.mjs` against Vite: LOADING SMOKE PASS, including the new focus-return checks, the existing keyboard/mouse/touch/controller directions, quarantine, stale leases, retry and reduced-motion checks.
- Final fresh-account browser playthrough with `--calling kindler --cast-cues`: 30/30 in 76.2 seconds, no page errors. Geometry 165 fps / p95 7.70 ms; cards 207 fps / p95 6.30 ms, 17 atlases / 9.86 MiB. These are this loaded workstation's measurements, not a low-end hardware claim.
- Final fresh native attempt after the loading fix entered, moved, jumped and exercised both representations, then failed the character-sheet/Escape checks and accessed a disposed `DialoguePane` in `Dev/Smoke.TradeWithAVendor` after an awaited layout. Result 17/20, not accepted as a passing gate. The forecasting owner is correcting the shared smoke driver's observed-state waits and stale view reference; this request must absorb that focused fix and rerun before merge.

Raw final probe, loading/browser results and the failed native result are in `experiments/appearance/evidence/finalization-*`. Full local captures and earlier attempts remain under `artifacts/pr9-*` in the review worktree. Earlier attempts included an integrated native 27/27 before the last main merge, a resumed-account movement/menu failure, a fresh browser dialogue timeout, and a fresh native `Loading cancelled` failure. None substitutes for the pending final native pass. Failed native shutdown also reported leaked rendering/text resources; the crash log is retained.

## Review handoff

Claude: review the empty-equipment semantics, the added looks, the cast-cue/appearance merge and the loading focus guard. A clean final native playthrough and its driver correction are still required. No review or gate is waived. Public equipment/day synchronization remains #22; forecast and positional-hit integration remains #20. Art assets, audio, low-end card certification and the human readability check remain the original record's explicit limitations.

Acknowledged by: pending Claude focused review and the final native gate.
