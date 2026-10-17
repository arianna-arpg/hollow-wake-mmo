# Shared delivery-plan foundation: ownership and Claude handoff

Base: main `b910e94`. The owner requested checking cast cues and positional hits before implementation. Cast cues (PR #6) is still open and positional hits are absent from this baseline. The owner explicitly selected building the independent core foundation now, with cue integration deferred.

This branch claims a new shared area-plan/forecast capability, its core expression-safety support, the area execution adapter in `Effects/Handlers/DeliveryHandlers.cs`, focused tests, the forecasting/delivery contracts and the experiment's adapter. Work stays in `codex/delivery-plan-forecasting` in its own worktree. No coordinator worktree, runtime cast state, wire messages, client renderers, targeting rules or positional-hit content will be modified. The area execution refactor is a potential future integration seam with positional hits; those changes must use this shared plan when they land, rather than duplicate its geometry.

ARPG references are read-only: `src/engine/skillPreview.ts` requires previews to use the same resolvers as gameplay; `docs/design/combat-readability.md` requires committed placements, timing and hit regions to agree; `docs/design/casting-cues.md` separates readiness from actual release. This foundation follows those contracts. It does not claim all of the older game's mechanics or its client presentation are already implemented here.

The later owner decision keeps Ember. The earlier forecasting proposal's Flame alias will be corrected within its owned files.

## Implementation and boundaries

The area handler now resolves a `ResolvedAreaPlan` for execution and implements the optional `IDeliveryPlanSource` capability through that same resolver. Shapes, anchors, facing, placement scale, melee reach, live dimensions, center exclusion, relation filtering, height and LOS retain their existing semantics. The shared candidate predicate runs before the unchanged requirement, target selection, payload and echo logic. Capabilities come from compiled registered effects after glyph patches, with no dispatch by skill, form, glyph or species identity.

`DeliveryForecasts.Describe` returns a primary candidate region or an explicit unavailable reason. It cannot certify the whole graph, secondary scatter/pulses, damage, timing or commitment. Multiple roots, unsupported wrappers and deliveries stay unknown. Dependency preflight rejects random or opaque geometry inputs before either context or stats random streams are read; it follows formulas, modifiers, conditions, gauges, links and resource-derived flags, then uses the existing numeric fold. Custom expression functions and curves explicitly opt into purity. Observation uses a detached context so it cannot freeze the execution scope's time/distance.

The diagnostic experiment now consumes that production capability. Its duplicate geometry calculations and expression parser are removed. Its current results differ from the merged proposal only in `sourceDigest` and `coreModuleId`; every scenario result, position and event trace is unchanged. The proposal's Ember display mapping now follows the later owner naming decision.

PR #6 initially carried older copies of the forecasting files already merged through PR #3. While this foundation was being prepared, its head advanced to `edb0084` and removed that history overlap. The remaining shared files are `experiments/cast-forecasting/Program.cs` and its generated `evidence/core-results.json`: PR #6 enables `sim.EmitCastPresentation` inside `#if CAST_CUES`, while this branch replaces the prototype adapter calls. Both source edits should be retained on integration and the evidence regenerated afterward. This branch updates the current main versions as their original forecasting owner; it does not copy or modify PR #6's worktree. `DeliveryHandlers.cs`, the new core API, expression files and the contract files were not in another open PR's file list when rechecked. The coordinator root remains clean.

## Verification

All commands ran in `C:/Users/John/.codex/worktrees/delivery-plan-forecasting/MMOARPG` on `codex/delivery-plan-forecasting`, based on `b910e94`. The installed package cache was selected with `NUGET_PACKAGES=C:/Users/John/.nuget/packages`; the initial build used `--configfile experiments/cast-forecasting/NuGet.Config -p:NuGetAudit=false` for offline restore.

Stored text outputs use LF with trailing whitespace trimmed. The raw fight outputs were compared before that normalization and were identical.

- `dotnet build HollowWake.sln -c Release --no-restore --verbosity quiet`: zero warnings/errors, including the Godot assembly.
- `dotnet test HollowWake.sln -c Release --no-build --no-restore --verbosity minimal`: 491 core, 36 server and 78 client tests pass, 605 total. The new focused suite contributes 61 cases: registered shapes at different facings, compiled forms/glyphs, live modifiers, source/point/target anchors, height and filtering, purity through direct and indirect dependencies, custom registrations, scope isolation and deterministic repeated observations. Existing server loopback and WebSocket tests pass.
- `dotnet run --project src/HollowWake.Tools -c Release --no-build --no-restore -- validate`: zero errors and warnings. [Output](../../experiments/delivery-plan/evidence/validate.txt).
- Same tools command with `load --x 0 --y 0`: 200 ticks complete. [Output](../../experiments/delivery-plan/evidence/load.txt).
- Same tools command with `fight loosed_brigand boar --seconds 20`: [feature trace](../../experiments/delivery-plan/evidence/feature-fight.txt) is byte-for-byte identical to the [pre-edit baseline](../../experiments/delivery-plan/evidence/baseline-fight.txt).
- Same tools command with `bench`: Release budgets pass. [Baseline](../../experiments/delivery-plan/evidence/baseline-bench.txt), [feature](../../experiments/delivery-plan/evidence/feature-bench.txt). Feature measurements: attribute extra fold 334.902 ns; expression evaluation 103.825 ns; zone p95 8.933 ms and 67.464 KB/tick; behavior 1.579 ms and 22.8 KB/tick; snapshot build 0.770 ms, full states 26.971 KB and deltas 8.059 KB/player; chunk generation 0.725 ms and 14.319 KB/chunk; zero zone errors. Timing differences from baseline are measurements, not a claimed speedup. Expression allocation is unchanged at the harness's 0.017 bytes/evaluation reading.
- `./experiments/cast-forecasting/run.ps1 -PackageCache C:/Users/John/.nuget/packages`: 63 real-core scenario/provider checks and 10 proposed contract-model tests pass. [Output](../../experiments/delivery-plan/evidence/probe.txt). The probe confirms lifecycle extension availability is false and checks core/content fingerprints remain stable during execution.
- `git diff --check` passes. Root status is clean. No coordinator checkout files were cleaned, reset, staged or edited.

No runtime cast-state, positional-hit, protocol, server or production-client change is included. Production client smokes and network forecast delivery are therefore not recertified here. The old diagnostic screenshots are historical; the viewer geometry data is unchanged. Forecast observation cost is not yet a per-player/per-tick budget: this API adds no observer loop, and its future wire consumer must gate sampling and measure its actual audience/update rate.

## Claude review asks

1. Review the shared resolver/candidate extraction for exact execution behavior, especially origin, placement scale, shape width defaults, height, relation filtering and echo radius. Review the scoped attribute dependency preflight against `Stats`, `AttributeSet` and `EffectScope` for purity gaps or false certainty.
2. Confirm that primary-only coverage and unavailable reasons are a sound integration seam for the next lifecycle slice. Check that unsupported wrappers, target selection, random scatter and future timing cannot be presented as guaranteed safe space or damage.
3. Compare the foundation with the older ARPG's shared geometry/actual-clock principles. Future mechanics should extend registered delivery capabilities and content composition; they should not introduce Cleave-specific state or duplicated calculations.
4. Integrate only after review. Recheck the actual cast-cue and positional-hit commits before consuming this API; adapt their separate changes in their owning worktrees. Preserve PR #6's opt-in probe lifecycle observation alongside this branch's production provider calls, then regenerate the shared probe evidence on the integrated source.

Acknowledged by: Pending Claude review.
