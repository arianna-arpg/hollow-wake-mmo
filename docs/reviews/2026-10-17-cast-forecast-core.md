# Live cast observation and positional release: core ownership

Base: combined integration commit `1ee5388f78fcfc2e7abc1259060f10484a1e9c48`, which includes cast presentation `edb0084` and delivery-plan foundation `a9402ac`. Work is isolated in `codex/cast-forecast-core`. The owner's coordination request in the integration chat explicitly authorizes working together on shared forecasts and hitboxes in both clients. This core slice implements the [positional-hit handoff](2026-10-17-positional-hits.md) and a read-only active-cast observation API.

Ownership: core runtime/definitions/validation, combat content, core tests, combat/delivery contracts and this record. The integration chat owns protocol, server routing, both clients, their configuration/tests and `cast-forecasting.md`. No changes to those files or another checkout belong here. This branch will supply commits for cherry-pick into the integration branch, not merge into main.

Agreed API: `WeaveRuntime.DescribeCast(Simulation, Entity) -> CastForecast?`. It exposes cast identity and content identity, live source position, sample tick/time, independent emission/readiness/maximum-hold deadlines, emission ordinal, direction policy/commitment, a detached effect context and the shared `DeliveryForecast`. The context is for server-side current-candidate eligibility checks, never serialization. Unsupported graphs remain explicitly unavailable. Primary coverage never implies complete coverage, damage or future eligibility.

Observation must not compile a new weave, initialize runtime scopes, spend resources, evaluate opaque inputs, roll RNG, emit events or create scheduled work. Cast-local geometry modifiers must come from the same helpers used by execution, with honest unavailable coverage where prospective state cannot be safely represented. Held readiness and a scheduled beat do not prove emission. Existing ordinary timed facing commits at start; held aim retains its existing tracking/release policy.

Implementation checkpoint `73ce718` was supplied for the integration chat to cherry-pick before the tests/contracts handoff. It added the core API, common release gate, data-authored melee sectors and shared modifier helpers. A follow-up guards invalid pure cast curves as explicit unavailable coverage. The exact public API is unchanged.

## Verification

- `dotnet build HollowWake.sln -c Release --no-restore`: zero warnings/errors, including the Godot assembly.
- `dotnet test HollowWake.sln -c Release --no-build`: 560 core, 90 client and 38 server tests pass. The core additions cover move-out, behind-the-swing, nearest bystander/aim preference, full/range/none policies, walls, dead targets, ally heals, channel recovery after a missed beat, charge/overcharge release, triggers, delayed presets, repeats and a moving target versus ordinary/seeking bolts.
- Observation tests cover distinct deadlines, held size versus actual execution, beat timing/ordinal, committed facing, absent source transforms, pending/opaque inputs, invalid custom curves, every shipped preset and every shipped glyph composed on an area. Full serialized event traces and gameplay state match with repeated observations interleaved through timed, channel, charge, overcharge, guard and random geometry execution.
- `hollowwake validate`: zero errors/warnings. `hollowwake load --x 0 --y 0`: 200 ticks, no system errors.
- Seeded `fight` runs below use the default seed, 60 seconds, and the same spawn positions as the combined baseline. [Evidence](../../experiments/cast-forecast-core/evidence/) retains baseline/after outputs with LF and trailing-whitespace normalization. CLI excerpts are limited to its default first 40 events except the complete after-proving trace; final life totals are from the full run.

| Duel | Combined baseline ending | Core ending | Named change |
|---|---|---|---|
| kindler / boar | kindler 112/112; boar dead | same | No forced-target melee damage was needed. |
| wolf / hob | wolf 96/96; hob dead | wolf 92/96; hob dead | Actual sector reach delays AI commitment and changes hit times/seeded rolls. |
| proving reaver / bulwark | reaver 71/148; bulwark dead | bulwark 83/220; reaver dead | Area payloads use normal delivery depth, so echo procs start at depth 2 instead of 1 and reach the recursion cap sooner; initial approach timing also changes. |
| tide channeler / spark duelist | channeler 80/116; duelist dead | channeler 116/116; duelist dead | The channeler kites outside committed swings. A separate stationary-channeler regression proves pursuit reaches it and landed hits still alternate both triggered sparks. |
| skirmisher / hob | skirmisher 116/116; hob dead | same | Projectile delivery is unchanged. |
| skirmisher / boar | not captured for this baseline | skirmisher 116/116; boar dead | Additional shipped delivery acceptance duel; no before/after equality claim. |

The combined-base Release bench is retained. The integration chat owns the final default Release and minion-heavy benchmark runs, including its new forecast builder benchmark, to avoid competing heavy runs. Those integrated measurements and both production client smokes remain integration gates, not claims made by this core record.

## Prototype comparison and review asks

Read-only reference: `arianna-arpg/arpg-game-world` checkout `118b510f8b5bfb781163c5bc13ea4908f698e75b`, `src/engine/skillPreview.ts` shared-resolver contract; `world.ts` melee, cast-lock and `updateCasting` paths; `docs/design/combat-readability.md` and `casting-cues.md`. Ordinary bars preserve a stamped aim; channels/guards explicitly steer, and readiness is separate from release. The MMO uses its existing area body-center convention and world units, rather than copying the prototype's body-edge tests or screen-unit constants. This is architectural parity for shared resolution and counterplay, not a claim that every prototype delivery or mutator already has a forecast provider.

Claude: review the shared provider/purity boundary, full/range/none policy at all stored-target applications, conditional held timing, and the explicit area-depth/proc-budget consequence in the proving duel. No skill IDs dispatch behavior. Unknown graph coverage remains unavailable; requirements, selection, payload damage and secondary emissions are not promised by a primary region. The integration chat owns the final PR, wire/client claims, performance evidence and smoke results.

## Allocation follow-up

The integration chat requested inspection of repeated AI area-plan work after its integrated benchmark showed a behavior cost increase. Fresh internal observation contexts now avoid a redundant clone. Dependency preflight rents cleared thread-local scratch storage, reuses the existing bounded name-suffix cache and walks indexed collections without boxing. Public providers still receive the detached context required by the observation contract. Every query still checks live dependency safety, resolves current geometry and tests current target eligibility; no safety result, geometry, target choice or random result is cached. Returning scratch clears its context, actor references, dependency set, depth and failure.

The small [AI query probe](../../experiments/cast-forecast-core/AiPlanProbe.cs) measured 1,312 to 568 allocated bytes per query against `f51599d`, a 57% reduction in this isolated scenario. [Evidence and command](../../experiments/cast-forecast-core/evidence/ai-plan-allocation.txt) record the conditions. Its elapsed times are illustrative, not a production performance or budget claim. Final default and minion-heavy benchmarks remain owned by integration; no competing heavy benchmark ran here.

Verification: Release solution build has zero warnings/errors; Release solution tests pass with 562 core, 90 client and 38 server cases. New regressions exercise reused storage after a declined observation and across different arenas, and repeated AI queries with changing time, target mana, position, targetability, walls and phasing. Content validation has zero errors/warnings; the load smoke runs 200 ticks without system errors. Normalized duel output matches the retained core baseline exactly: complete proving trace (513 lines), tide channeler / spark duelist and wolf / hob (41 lines each, including full-run final totals). No content, selection order or RNG behavior changed in this follow-up.

Acknowledged by: integration chat accepted this API seam and requested the allocation follow-up; Claude review pending the integrated PR.
