# Shared cast forecasting and positional hitboxes

The owner asked the integration chat, forecasting chat and Claude to implement shared forecasting in both clients, with content composition instead of per-skill branches. Work is isolated in `codex/shared-cast-forecasts-integration`, based on main `dcecf647` after #6 merged. The earlier integration branch is preserved without rewriting history. The integration checkout is untouched.

## Change and ownership

The implementation composes #6's confirmed cast/outcome stream, #11's shared delivery plan, and the forecasting chat's [positional-hit/runtime work](2026-10-17-cast-forecast-core.md). Every active cast has authoritative timing. Supported area roots expose the same resolved candidate shape used by execution, after forms, Threads, glyphs and live modifiers. The shipped melee attacks now use authored sectors and common release checks, so moving outside a committed swing can avoid damage. Armor, blocking and other build-based defenses still apply to landed hits.

The server sends opt-in `cast_forecast_v1` membership frames after interest refresh, with epochs, exact revisions, expiry, bounded membership and periodic full-record repair. Shared observation is cached per tick across viewers. Unchanged geometry is referenced rather than serialized repeatedly; a client missing the exact referenced revision removes that geometry until repaired. No future random outcome, hidden target position, or local timer is treated as a release.

Godot and browser consume `ui_cast_forecasts` for shape precision, colors, broken boundaries, direction markers, height-limit markers, screen-space timing panels and labels. Neither renderer switches on a spell, support, calling or monster identity. All registered shapes have combat-derived conformance fixtures, including narrow hollow variants. The fixture gate also checks current combat predicates and complete registered vocabularies.

The forecasting chat owns core runtime/provider changes, melee content, core regressions and combat/delivery contracts. This chat owns transport, server routing, both clients, cross-client fixtures and integration evidence. Ownership notices were posted on #9, #11, #13 and [Claude's preset compiler #15](https://github.com/arianna-arpg/hollow-wake-mmo/pull/15#issuecomment-6243398804). #9 still owns appearance/materials/body animation/camera; only forecast initialization and legacy cast-label suppression cross that seam. #6 cleanup remains with its owner. No merge into main is included.

## Verification

Evidence lives under [experiments/forecast-rendering](../../experiments/forecast-rendering/). The integrated allocation and channel-window follow-ups are included in these gates:

- `dotnet build HollowWake.sln -c Release --no-restore`: zero warnings/errors.
- `dotnet test HollowWake.sln -c Release --no-build --no-restore`: 563 core, 104 client and 40 server tests pass (707 total). The server path covers opt-in/out, exact live geometry, hidden targets, revision reuse/reset and bounded nearest-viewer selection. Both clients reject missing geometry revisions and retain readiness separately from emission.
- `hollowwake protocol-ts`, `npm --prefix web test`, `npm --prefix web run build`: generated contract matches, 212 browser tests pass, production build passes.
- `hollowwake validate`: zero errors/warnings. `hollowwake load --x 0 --y 0`: 200 ticks without system errors. `experiments/cast-forecasting/run.ps1`: 63 real-core/provider checks plus 10 historical contract-model checks pass. The latter are study checks, not a substitute for production transport regressions.
- `node experiments/visual-convergence/audit.mjs`: no unclassified captured vocabulary; partial/pending families remain explicitly partial/pending.

Final Release default benchmark: tick p95 10.919 ms, allocation 89.713 KB/tick, behavior 2.110 ms/tick, full snapshot 0.810 ms and 27.741 KB/player, delta snapshot 8.948 KB/player; all default budget rows pass. Incremental forecasts: 0.308 ms, 2.075 KB and 44.992 KB allocated per player/update over 3,459 active observations. See [default measurements](../../experiments/forecast-rendering/evidence/bench-default.txt).

The `--only tick --minions 12 --cast-cues true` stress run measured tick p95 13.507 ms and zero errors, with forecast cost 0.232 ms, 0.970 KB and 42.800 KB allocated per player/update. Entity full snapshots were 1.376 ms / 55.258 KB and delta snapshots 15.747 KB, above the default-workload reference budgets. This is retained as a scaling limit, not a budget waiver or crowd certification. Snapshot groups sample sequential windows; their averages are not an end-to-end simultaneous measurement. See [stress measurements](../../experiments/forecast-rendering/evidence/bench-minions.txt).

Live clients exercised normal input against an owned local server with fresh accounts and data. Before the current-main refresh, spell checks passed browser 27/27 and Godot 24/24; Cleave passed browser 26/26 and Godot 23/23. A later native repeat exposed smoke-driver failures: it could select an incidental NPC instead of the vendor, and could cycle into indefinite Guard while trying to prove damage. That failed run is retained. The driver now verifies the requested NPC, uses the attack slots, approaches again between attempts, and still requires actual damage rather than a miss log. Final combined-source reruns are recorded below when complete. [Native Cleave](../../experiments/forecast-rendering/evidence/godot-cleave.png) and [browser Cleave](../../experiments/forecast-rendering/evidence/browser-cleave.png) are unedited captures. They show placeholder world art; this work does not claim final appearance or dense-crowd polish.

An initial performance run exposed repeated full-region traffic and a noisy snapshot build measurement. Exact revision reuse reduced forecast traffic from 10.555 to 2.001 KB/player/update over the same 3,459 active observations. The forecasting chat also removed redundant detached-context and dependency-preflight allocation, with unchanged live checks and recorded combat traces. These are intermediate optimization measurements; the final gate results below supersede them.

## Gameplay changes requiring review

The proving reaver/bulwark duel changes winner relative to the named combined baseline: the bulwark survives with 83/220 life. Converting root direct damage into the standard area delivery puts payload effects one level deeper. Consequently `echoing_blow` starts at depth 2 instead of 1 and reaches the existing recursion limit sooner. AI commitment also occurs at actual sector reach, changing initial timing and seeded rolls. No special depth exception or skill-specific balance patch hides this consequence. See the [complete core comparison](2026-10-17-cast-forecast-core.md) and [integrated duel excerpt](../../experiments/forecast-rendering/evidence/proving-duel.txt).

The channeler now avoids committed melee through kiting; a stationary-channeler regression proves pursuit still reaches it. Moving out, moving behind, line of sight, invalid targets, held casts, channels recovering after a missed beat, triggers, delayed applications and ordinary/seeking projectile paths are covered by core tests. These are intentional positional counterplay changes, not a claim of unchanged combat balance or an AI dodge planner.

## Coverage and review asks

All active casts inherit the shared timing/lifecycle and configurable presentation. Instant effects do not acquire a fictional warning window. Supported area mechanisms inherit exact sampled geometry across all content composing them. Existing released projectiles and fields retain their authoritative shape stream. Unsupported delivery families, unsafe/random dependencies, dependent wrappers and multi-root graphs remain explicitly spatially unknown. Primary area coverage does not promise secondary emissions, targets, damage, survival, or terrain/bridge clipping. The original Hollow Wake catalog is still a migration obligation; its convergence audit remains partial by mechanism.

Claude: review the provider/purity boundary, release policy application points, held timing, hidden-target redaction, revision repair and exact shape adapters. Review the area-depth/proc-budget consequence before integration. When #15's preset overlays and later ports land, execution and observation must consume the same resolved casting options and the catalog/glyph gates must run over imported content. Preserve the separate #6 cleanup and #9 appearance work during integration, then rerun combined gates. Dense-crowd readability and summon-heavy entity bandwidth remain explicit follow-on acceptance work.

Acknowledged by: forecasting chat supplied and verified the core implementation; Claude review pending the integrated pull request.
