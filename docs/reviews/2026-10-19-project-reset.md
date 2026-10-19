# Combat-first project reset

Date: 2026-10-19. Coordinator: Codex, at the owner's explicit request while Claude is unavailable. Initial main: `f7ef072cc322b93b628229916c5220024e74a89c`.

## Finding and scope

The remote had 13 branches: main, nine draft requests, two review branches and the living-world planning branch. Local Git had 59 branches before this cleanup branch was created. The repository's branch-retirement rule had not been followed. Several feature requests carried the complete histories of their dependencies, multiplying review surfaces without giving the owner one clear playable build.

The pending #20 head `d73a64dbf9083e70ccbb13c9a5354de752e0b402` already contains #50 constructs, #36 precision hitboxes, #33 Orcs and #34 fauna. It is the sole remaining integration candidate, not an approved main build. Its functional evidence does not settle its performance or balance limits. Additional creature branches include live population proposals; these are not merely art assets. The living-world branch itself changes only documents, not runtime generation.

The owner explicitly accepts assets waiting for integration and wants combat and playtesting first. Deferred creature rollout and living-world implementation are parked. Existing main populations are retained; this cleanup does not silently rebalance or remove them. Before #20 lands, review its population additions as a separate rollout decision.

## Ownership and isolated rendering repair

This record declares overlap with #20's `web/src/world/appearance/cards.ts`, its regression tests, the browser smoke and the developer rendering observation in `web/src/app.ts`. The cleanup extracts only the card-baking renderer-state correction from #20, preserving its authorship/provenance here. No cast, hitbox, aiming, population, protocol or combat changes are imported.

The first main smoke reported browser 30/30 and native state 36/36, but manual inspection of `05_combat.png` showed the world confined to a small rectangle after card baking. `01_isometric.png` had shown the normal full scene before baking. An offscreen atlas was mutating the shared renderer's canvas viewport/scissor. The existing smoke checked entities and UI but did not establish that terrain still rendered. The fix keeps atlas rectangles on the render target, restores clear state and the previous target even on failure, and disposes failed work. The extracted tests cover restoration, offscreen callers, high-DPI cells and interrupted baking. A browser viewport assertion and sampled-world screenshot check prevent the observed false pass.

## Archive disposition

The machine-readable [archive index](2026-10-19-branch-archive.json) records exact original tips. Remote tags under `archive/2026-10-19/` preserve every retired remote branch; `archive/local/2026-10-19/` preserves local-only tips not reachable from the original remote branches. Archived work is deferred or superseded, **not merged**.

| Former request / branch | Disposition |
| --- | --- |
| #33 Orcs, #36 hitboxes, #34 fauna, #50 constructs | Their full histories are already in #20; retire the separate review surfaces and retain archive tags. |
| #37 Gnolls/Beastkin, #38 Kobolds, #39 golems, #40 elementals | Preserve complete work and evidence in archive tags; defer rollout until combat and living-world placement are ready. |
| `claude/living-world-4b4aca` | Preserve the newer planning documents as an archive; no runtime implementation is implied. |
| `codex/pr-50-review`, `codex/elemental-shared-review-checks` | Preserve review work as archives; close redundant branch surfaces. |

Before deleting any remote ref, verify its remote annotated tag dereferences to the exact original tip; delete with leases so a concurrently moved branch aborts the operation. Before retiring local branch names, preserve each tip remotely, check tracked changes and detach worktrees at their identical commits. All worktree directories, untracked files and ignored evidence remain. No recursive filesystem cleanup is part of this reset.

## Verification

Baseline on Windows: solution build 0 warnings/errors; .NET 897 core + 53 server + 129 native client = 1,079 passing tests; browser 246 tests; content validation 0 errors/warnings; origin zone 200 ticks; all seven standard duel hashes identical. Launch/background helper tests pass 12/12 with process-tree access. Initial sandbox attempts failed package restore/process cleanup and were rerun with the required local permissions; these are not gameplay failures.

Initial private smoke evidence: `artifacts/playtest-consolidation/artifacts/reset-smoke/run-FIoTAQ/`. Its automated pass is explicitly superseded for browser rendering by the visual defect above. Native 36/36 checks state, not native pixels or physical controller input.

Final repair verification: all six extracted renderer regressions fail against the original implementation and pass after the repair; the full browser suite passes 252 tests across 34 files. Type checking and the production build pass, retaining the existing bundle-size advisory. Fresh private browser smoke passes 32/32 with no cleanup errors. The viewport is full-canvas after baking; the sampled world has 168 color bins and its largest bin occupies 18.55%, below the 92% blank-scene rejection threshold. Card and combat screenshots were inspected. The native source and simulation are unchanged, so the completed native and .NET gates were not repeated.

Durable local evidence is under `artifacts/project-reset-2026-10-19/baseline/` and `fixed/` in the root checkout, including stage logs, results and original screenshots. Baseline `05_combat.png` preserves the visible defect; fixed `16_cards.png` and `05_combat.png` show the full world. These files are ignored local artifacts, not bloated committed binary evidence. The generated TypeScript protocol is unchanged; the server contract tests passed in the baseline suite. No launcher or release pipeline changed. No simulation hot path changed, and browser card rendering was exercised in the smoke; no server budget clearance is claimed.

Cleanup completed: 19 annotated preservation tags were published and their remote peeled commits verified before any retirement. Eleven remote branches were removed with exact-tip leases. The remote now has main and #20 only, before the temporary cleanup PR. Eight old draft requests are closed, with archive disposition prefixed to their retained descriptions. Fifty-seven local branch names were retired; main, #20 and this temporary cleanup branch remain until this PR merges. All pre-existing worktree folders, untracked files and ignored evidence remain at the same commits. The local archive index retains every original branch tip. Existing combat performance and balance blockers remain attached to #20; this small browser fix does not clear them.

Review: the extracted `cards.ts` matches #20's implementation exactly. Inspection confirms target-local cell rectangles, prior-target face/mip restoration, clear-state restoration and failure disposal. The six regressions exercise both normal and interrupted bakes and on/offscreen callers. The smoke additions observe rendering only. No unreviewed combat or population code is included. The owner-facing guide distinguishes engine capabilities from equipped starter kits and unmerged work. Required hosted CI must still pass before merging this cleanup.

Acknowledged by: owner-directed temporary Codex coordination; normal Claude review is unavailable for this reset. Claude retrospective review is requested when service resumes, without blocking the gated playtest repair.
