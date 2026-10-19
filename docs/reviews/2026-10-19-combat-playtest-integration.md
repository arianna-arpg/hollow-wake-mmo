# Combat playtest integration

The owner explicitly authorized the temporary Codex coordinator to finish PR #20, use the arianna-arpg GitHub account, merge #57 after CI passes, and make the combined combat available through the root `Play.cmd`. Claude is temporarily unavailable. New enemy populations and living-world implementation remain deferred for joint review.

## Scope and ownership

Continue the existing `codex/shared-cast-forecasts-integration` branch and PR #20. No new feature branch. Its previous source/evidence head is `d73a64dbf9083e70ccbb13c9a5354de752e0b402`; the previous worktree was detached at that identical commit with its evidence retained. Current work lives under the ignored `artifacts/combat-integration` worktree. The rendering repair in #57 intentionally overlaps the repair already in #20.

The candidate integrates committed casts, authoritative delivery forecasts, precise damage anatomy, layered aiming/projectile support, paid device casts and queued echoes. It does not complete the proposed AI overhaul, every combat grammar parity item, progression or balancing. Earlier evidence and retained failures are in `2026-10-18-final-forecast-foundation.md`.

## Population boundary

Move Orc point-of-interest definitions and wildlife spawn tables outside core into `experiments/parked-populations/content`. Preserve their proposed biome assignments in that explicit optional layer. Restore core biome populations to the pre-integration main version. Keep reusable creature appearances, anatomy and kits in the catalog; existence does not imply population placement.

Habitat and proposed-site authoring tests explicitly load the optional layer. New playtest isolation tests verify that core excludes its sites/tables, and that generated spawns and quest targets do not select the parked bodies. Generation goldens are compared against the pre-integration main baseline, rather than accepting the expanded population baseline.

Living-world work remains archived planning material. There is no activation or new simulation in this change. Claude retrospective review is requested when available, especially for later population placement; the owner's temporary coordinator authorization permits the combat integration to proceed through ordinary CI protection in the meantime.

## Acceptance

Local acceptance is complete. Required GitHub CI must pass on the final revision before merge; local results never bypass branch protection. The retained [measurement and gate evidence](../../experiments/combat-playtest/README.md) accompanies this record.

#57 passed the required `ci` check and merged normally as `08ee81fef4a0c819039c83e2ac96cc922bd19931` on 2026-10-19. No protection bypass was used.

The first isolated-population Release benchmark retained `worldgen.zone.plan=40.827 ms` and `snapshot.build=1.0169695 ms/player`, both over budget; the combined shipped-client path was `0.85473 ms/player`. A fixed-source snapshot diagnostic attributed about `0.4962 ms/player` to full entity encoding and `0.2823 ms/player` to entity capture. Its results are diagnostic, not replacement budget evidence. An initial repeated-work reduction still measured `44.40 ms` and `1.03 ms/player`; that unsuccessful result is retained too. Further changes avoid unneeded water-distance ring sampling and generate the repeated entity JSON writer. Exact wire-byte and generation tests protect the existing output. No budget was changed.

A bounded process-scoped trace on saved source `3f231d2b` identified content lookup locking in the measured paths and first-use coordinate-cache work in zone planning. It is sampled thread-time evidence, not a claim of operating-system contention. Content kind indexes now publish immutable snapshots; lookup remains extensible without read-side locks. Validator arrays also use explicit publication. Quest population facts reuse the existing coordinate cache. Registration/replacement/concurrent publication tests and unchanged world goldens pass.

The final default Release measurement is within the unchanged budgets: zone planning **36.75 ms / 40**, tick p95 **11.97 ms / 25**, behavior **2.64 ms / 3**, full snapshot **0.83 ms / 1**, combined snapshot **0.82 ms / 1**, full traffic **29.47 KB / 30**, delta traffic **8.98 KB / 10**, combined traffic **7.96 KB / 10**. Expression evaluation is **110.26 ns / 150**, warmed calling-thread allocation **0**, and modifier extras **367.62 ns / 500**. Preserve the process allocation rows and all earlier measurements; no retry selected a zero result. Measurements ran sequentially with no other owned heavy gate; they do not certify arbitrary hardware or machine-wide quiet.

The complete suite passed **3064 core, 98 server and 188 native logic tests**. The final publication-order refinement additionally passed its focused tests and the exact-source benchmark. Browser tests passed **472/472**, and production builds/type checks passed. Release and native builds reported **0 warnings, 0 errors**. Content validation reported **0 errors, 0 warnings**; the origin zone ran **200 ticks**; `fight wolf hob` passed; all **seven Windows standard replay hashes** were identical to the committed baseline. The generated TypeScript contract remains byte-identical. The private TCP probe negotiated all seven requested features, entered, moved and received entity, interface, cue, forecast and hit-body messages. Launcher/background lifecycle tests passed **12/12**.

Browser Holdfast and Kindler playthroughs each passed **34/34**, including rendered-world pixels, card viewport restoration, real landed hits, lifecycle phases and primary forecasts. Screenshots of cards and forecasts were inspected. Native Kindler passed **38/38**. Native Holdfast initially passed **37/38**: the old cue script stopped three units away, outside Strike admission, and received no lifecycle; its later two-unit landed-hit check succeeded. Aligning the cue/forecast approach with that existing combat check, waiting for idle and aiming near the feet produced **38/38** on rerun. Gameplay reach was not changed. All test processes cleaned up without errors.

Native checks use the headless display, so rendered pixels and physical controllers remain unverified. Native shutdown retains the previously documented diagnostics: six dummy textures, two shaped texts, one font, one CanvasItem and 29 ObjectDB instances. This integration does not claim those cleanup diagnostics fixed. Existing Bulwark, Plaguer and Pitfighter balance failures remain visible in the earlier evaluation record; no balance thresholds or replay expectations changed during this completion.

The temporary coordinator review checked the staged/live population boundary, unchanged main generation goldens, shared combat/protocol gates, byte-equivalent serialization, registration replacement and concurrent publication, and retained performance failures alongside the final pass. No remaining finding blocks this combat playtest. Claude retrospective review is requested when available; new populations and living-world implementation remain parked.
