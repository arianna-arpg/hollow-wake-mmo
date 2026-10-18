# Presentation integration review

Date: 2026-10-17 (local). Reviewer: Codex. Request: #9. Original head: `f5bd628`; integrated main through `5843798` in an isolated worktree. The author and the active forecasting/launcher checkouts were not changed.

## Findings and changes

- Integrated the confirmed cast-cue renderer alongside the appearance service in the browser composition root. Both services survive the merge; presentation never changes authoritative damage or geometry.
- Fixed empty self equipment on both clients. Previously an empty authoritative map restored the Calling's kit. Now an available empty map draws the unadorned body. The production browser plan regression failed before this fix and passes after, also retaining other-player fallback.
- Added content looks for the new proving hexer/plaguer and the incoming pitfighter/mower. The coverage gate caught the first pair after main's status roster merge. Families, palettes and gear remain content; no pattern-specific rendering branch was added.
- Fixed global controller confirmation cancelling a background loading screen. Native confirmation requires window focus, browser confirmation requires a visible focused document, and both continue tracking edges while unfocused. The production loading smoke covers a background press, focus return with the button held, and a subsequent fresh focused press. The isolated commit is `b57ee40`, shared with the launcher and forecasting owners.
- Found the same empty-equipment error in the new public appearance descriptor seam in #22 and recorded the required absent-versus-empty correction there. That follow-up remains the active author's work.

Read: appearance contracts, content registration/validation, the paired look/gear/body/animator paths, equipment refresh and card lifetime paths, the composition merge, and loading confirmation. This is not a claim to have compared every body-family line or every original visual asset. Looked at native/browser captures including geometry, cards, impact and grayscale. Canopy occlusion and crowded labels remain visible and are not certified as a completed human readability study.

## Verification on the follow-up tree

- `dotnet build HollowWake.sln -c Release`: 0 warnings, 0 errors. `dotnet test HollowWake.sln -c Release --no-build`: 548 core, 110 client, 38 server passed after integrating the reviewed #18/#19 stack (the earlier main integration had 531 core tests).
- `npm --prefix web test`: 180 passed. `npm --prefix web run build`: passed.
- `hollowwake validate`: 0 errors, 0 warnings. `protocol-ts --check`: current. `load --x 0 --y 0`: 100 chunks, 200 ticks. The standard kindler/boar 10-second trace was unchanged in this review; no simulation behavior was edited.
- Isolated local server, TCP probe with delta/interface negotiation: PROBE OK.
- Production `web/scripts/loading-smoke.mjs` against Vite: LOADING SMOKE PASS, including the new focus-return checks, the existing keyboard/mouse/touch/controller directions, quarantine, stale leases, retry and reduced-motion checks.
- Final fresh-account browser playthrough with `--calling kindler --cast-cues`: 30/30 in 76.2 seconds, no page errors. Geometry 165 fps / p95 7.70 ms; cards 207 fps / p95 6.30 ms, 17 atlases / 9.86 MiB. These are this loaded workstation's measurements, not a low-end hardware claim.
- The first fresh native attempt after the loading fix entered, moved, jumped and exercised both representations, then failed the character-sheet/Escape checks and accessed a disposed `DialoguePane` in `Dev/Smoke.TradeWithAVendor` after an awaited layout. Result 17/20, not accepted as a passing gate. This was a stale reference in the test driver, not a demonstrated production pane-lifecycle defect.

## Final native and browser closure

Integrated the shared background runner commits `65984a6` and `d1d58db` from #23 and the smoke synchronization/reacquisition changes from #20's `48a402f`, keeping this branch's appearance stages. The driver waits for actual world-input readiness and Escape transitions and reacquires the dialogue view after layout. Native headless mode explicitly skips appearance rendering measurements/readability and theme pixels, while retaining day state and all behavioral assertions. The earlier graphical run measured geometry at 65 fps / p95 19 ms and cards at 70 fps / p95 18 ms (18 atlases, 10.27 MiB); its screenshots remain the graphical evidence, not a claim that headless mode renders.

The first background attempt, before `d1d58db` isolated physical controllers in the browser, disconnected immediately after entry and finished 5/10. That failed log/result is retained. After the shared isolation correction, `node scripts/test-background.mjs --suite all --calling kindler --godot <Godot 4.7.2 .NET executable> --out artifacts/pr9-background-isolated --timeout 300` passed: browser 29/29 in 78.2 seconds, native 25/25 in 37.0 seconds, cleanup errors empty. The browser's explicit cast-cue course remains the separate 30/30 pass above; the default shared runner does not request that optional course. These input-isolated runs do not certify physical controller support. Runner lifecycle regressions pass 9/9 with normal process-management permissions; an attempted restricted run could not clean up its test children, which were then removed by their verified parent PID before the successful run.

Final content validation is 0/0, protocol is current, and the kindler/boar trace remains SHA256 `8CE22EA7521959FEF67D57E344BB33CAC910085017D4DF15D70D626010E07B68`. Final runner result and both smoke logs are in `experiments/appearance/evidence/finalization-background-*`.

Raw final probe, loading/browser results and the failed native result are in `experiments/appearance/evidence/finalization-*`. Full local captures and earlier attempts remain under `artifacts/pr9-*` in the review worktree. Earlier attempts included an integrated native 27/27 before the last main merge, a resumed-account movement/menu failure, a fresh browser dialogue timeout, and a fresh native `Loading cancelled` failure. Those failures are retained alongside the final closure rather than treated as passing results. Failed native shutdown also reported leaked rendering/text resources; the crash log is retained.

## Review handoff

Claude: review the empty-equipment semantics, the added looks, the cast-cue/appearance merge, loading focus guard and imported test-driver changes. The final native behavior gate is now closed with the graphical limits above; #23 owns the shared harness review. No review or gate is waived. Public equipment/day synchronization remains #22; forecast and positional-hit integration remains #20. Art assets, audio, low-end card certification and the human readability check remain the original record's explicit limitations.

Acknowledged by: Claude, in the review comment on pull request 9 and the follow-up paragraph of [the presentation record](2026-10-17-presentation-and-feel.md). The empty-equipment rule, the looks, the composition merge, the focus guard and the imported driver changes (the forecast integration's synchronization, the background harness's controller isolation) are accepted as written; the gates were rerun graphically on the same tree (desktop 27 of 27 with the look stages, browser 29 of 29) beside your harness closure.

## Current-main closure after launcher merge

Integrated main through the merged launcher #10 and projectile roster #21, retaining both quick-play UI and appearance styling. Added content looks for proving_fletcher and proving_evoker. The launcher's `--no-physical-gamepads` smoke option is retained as an alias of the shared `--isolated-input` helper, including controller connection-event isolation.

On source b58a83f: Release build 0 warnings/errors; all 563 core, 111 client and 38 server tests pass; web 182 tests pass; the shared runner rebuilds its Debug outputs, typechecks/builds web, and passes browser 29/29 and native headless 25/25 with no cleanup errors. Evidence is `finalization-launcher-background-*`. The first attempted `--no-build` run after #21 used stale Debug outputs and failed validation on consume_corpse; its result/log is retained locally under `artifacts/pr9-current-main-background/run-xXek5y`. Rebuilding those outputs passed before and after the launcher merge.

Preserved the author's e256b55 addition of an optional desktop `--isolated-input` flag. Read the input-map and loading-cover changes: ordinary client behavior is unchanged unless the smoke selects the flag, and controller bindings remain excluded after settings reapplication. The merged client compiles with 0 warnings/errors. The author records its explicit-flag graphical 27/27 and loading-smoke pass in [the Claude review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/9#issuecomment-6244203586); the default headless integration course above does not select that optional flag. Native rendering limits remain as recorded above.

Claude final integration ask: the two added content looks and #10 merge resolution are the remaining small follow-up; the prior corrections are accepted in the linked review. The #23 harness review remains separately required.
