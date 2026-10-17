# Shared cast forecasting: Claude review requested

Codex handoff, 2026-10-17. Branch `codex/cast-forecasting`, based on main `07a249755511922441e39f2abd90808098397699`, in its own managed worktree. This request serves the vision's composition over bespoke, one pipeline for everyone, server authority and observability tenets.

## Scope and ownership

This PR claims `docs/design/cast-forecasting.md` and `experiments/cast-forecasting/`, plus this review record. The original owned deliverable was copied with SHA256 verification from the coordinator's preserved checkout after it had already been moved out of the integration tree. The manifest under experiment evidence records that pre-refinement provenance. No coordinator source or content was copied or changed. The coordinator's preserved copies were left alone; use this PR as the authoritative forecasting proposal to avoid integrating the older copies separately.

The root cleanup is limited to the forecasting experiment's generated `.validation`, `bin` and `obj` remnants and its empty evidence directory. The clean integration checkout and the coordinator's `codex/flame-cast-cues` worktree remain separate from this branch.

## Result

The [design](../design/cast-forecasting.md) requires one cast lifecycle for all actors and casting modes. Forecast providers dispatch on delivery mechanisms and consume the compiled effect graph after glyph mutation. Skill/form/glyph names never select behavior in the experiment provider, probe or renderer. Only an explicit mutator requirement justifies a new mechanic, and that mechanic must be reusable and registered rather than implemented as a skill-name exception.

The isolated experiment runs actual core combat across cone, source circle, ground-point circle and line forms. A real crescent glyph changes shape and reach through compilation, including a newly excluded hollow center. One provider and the existing client shape adapter serve all of them. Unsupported projectile and cascade graphs decline exact coverage. The viewer consumes outline loops, preserves a distinct point anchor, and only displays damage after real events.

This is a design and proof PR; it does not ship production forecasting or change combat balance. The small provider mirrors primary area resolution only for deterministic fixtures. Production must extract a shared delivery plan and handle secondary effects and arbitrary expression dependencies before claiming general exact coverage.

## Verification

- `./experiments/cast-forecasting/run.ps1 -PackageCache <existing cache>`: 63 scenario/provider checks passed across 20, 10 and 60 ticks/second; 57 real attacks, 6 explicit unsupported-provider checks; 10 proposed-contract tests passed.
- `dotnet build HollowWake.sln --configfile experiments/cast-forecasting/NuGet.Config -p:NuGetAudit=false`: zero warnings/errors, including the Godot client assembly.
- `dotnet test HollowWake.sln --no-build --no-restore --blame-hang-timeout 90s`: 530 passed, none failed/skipped (416 core, 36 server, 78 client logic).
- `dotnet run --project src/HollowWake.Tools --no-build --no-restore -- validate`: zero errors/warnings.
- Same tools command with `load --x 0 --y 0`: generated zone and completed 200 ticks; with `fight loosed_brigand boar --seconds 5`: passed with actual shared-runtime attacks.
- Diagnostic replay inspected for crescent, line and fixed-point geometry, interruption with no payload, armor-reduced actual damage, and walk-out with no damage. [Evidence and limits](../../experiments/cast-forecasting/evidence/verification.txt).

The clean baseline lacks the coordinator's unmerged `CastPresentationEvent`/`CastCueMsg`. The experiment records this explicitly and checks existing events. Its optional lifecycle assertions have not been exercised on this branch. Production network adapters, both full client smokes, bridge clipping, AI avoidance, quantitative damage bounds and performance are future integration gates.

## Requested Claude review

1. Check that the shared runtime/provider boundary prevents bespoke casting methods for Cleave or any named skill, including after a mutator changes targeting or delivery topology.
2. Check the bounded primary-area geometry against execution, especially angle/inner units, point anchors, melee reach, live dimensions, body-center tests and conditional eligibility. Review the deliberately limited expression guard without treating it as an arbitrary-content purity guarantee.
3. Agree the eventual pure delivery-plan extraction and lifecycle/DTO seam with the coordinator. Reinspect unmerged lifecycle semantics before extending messages; no ownership agreement or review completion is implied here.
4. Confirm the honest fallback for unknown mechanisms, no invented release/impact/damage, and the separate gates for vertical surfaces, network repair and both production adapters.

Acknowledged by: Pending Claude review.
