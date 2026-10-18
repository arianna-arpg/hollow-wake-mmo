# Forecast dependency observation candidate, 2026-10-18

Final selection: the lazy cycle-tracking experiment was removed in fb23c4f after the matched default workload did not establish an AI benefit (2.2718955 ms control versus 2.7940962 ms candidate). The original dependency preflight is restored. The eleven live-safety regressions and the single PreferredRange slot-cache reuse remain. The integration record retains both first samples and subsequent gates; this historical candidate report does not claim the removed logic ships.

This is a bounded core-side slice for coordinator-owned PR #20. Branch codex/forecast-ai-observation starts at published evidence head 2cc5d66297969c024fc3098cb6bde5e783f58fc8 (source 6edc124; approved main 8365775). It is a local integration candidate, not a performance certification or a separate pull request.

## Scope and finding

Owned scope: Effects/ForecastInputs.cs, one redundant slot-cache lookup in WeaveRuntime.PreferredRange, focused regression tests and this handoff's evidence. No tuning, precision, budgets, body-contact integration, server snapshots or icon work is included. The integration checkout and coordinator worktree were not edited.

Forecast preflight previously entered and left its cycle-tracking hash set even for a numeric leaf attribute. This candidate tracks an attribute on the recursion path only when its checks descend through expression identifiers/flags, a gauge or a single-hop link. Cycle rejection still precedes the unchanged depth limit. Expression purity, pending restrictions and every live dependency are still checked on each observation. No safety result, resolved geometry or eligibility survives across calls. Attribute numerical memoization is unchanged.

Dependent paths can incur an extra hash lookup; numeric leaves avoid hash-set insertion and removal. A coordinated before/after benchmark must determine whether the tradeoff helps the real workload. No timing or allocation improvement is claimed yet. PreferredRange also reuses the slot cache already obtained within the same iteration. ChooseForAi's eligibility checks, order and random draws are unchanged.

This serves composition over bespoke and one pipeline for everyone: the change applies to dependency analysis for every supported delivery and composed modifier, with no skill or mutator identities.

## Functional evidence

Saved output is under [experiments/forecast-dependency-optimization/evidence](../../experiments/forecast-dependency-optimization/evidence/).

- dotnet build HollowWake.sln: zero warnings/errors (build.txt).
- dotnet test tests/HollowWake.Core.Tests --filter 'FullyQualifiedName~ForecastDependencyObservationTests|FullyQualifiedName~DeliveryForecastTests|FullyQualifiedName~ForecastExpressionTests|FullyQualifiedName~AiApproachTests|FullyQualifiedName~CastForecastTests' --no-restore: 103 passed (focused-tests.txt).
- dotnet test HollowWake.sln --no-build: 744 core, 54 server and 132 client tests passed, 930 total, zero skipped (tests.txt). This includes the loopback suite.
- git diff --check: clean before handoff.

Eleven new cases cover same-tick safe-to-unsafe changes through values, conditions, gauges, links and cycles; untouched RNG streams; safe observations after a decline; curve purity re-registration; the exact depth boundary including leaves; target scope filtering; resource-derived flags; and single-hop mutual-link behavior. The curve case probes safety eligibility, not numerical cache invalidation after changing a curve's function.

## Deferred gates and handoff

The running functional command completed successfully at about 13:38 America/Chicago on 2026-10-18. All further heavy local work was then stopped for the coordinator's serialized #38/#37/#39/#40 performance windows. No benchmark has been started for this candidate.

Content validation, zone load, fight and the standard tournament replay remain pending under that hold. The baseline file is unmodified, SHA-256 006AC4DD5937DEB47EE91BBA6646666A0BB8075EE7FF494BF15C2F9677F50878. An unchanged file does not prove unchanged outcomes: run dotnet run --project src/HollowWake.Tools -c Release -- tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json after the hold. Do not regenerate the baseline for this optimization.

Coordinator: integrate or compare this isolated candidate only within PR #20, then complete aggregate functional gates and the scheduled normal-priority pre-optimization/candidate benchmark. Retain the existing behavior and combined-snapshot performance blockers until measured evidence resolves them. Claude should review lazy recursion tracking, cleanup on declines, depth/cycle precedence and the eventual tournament/performance evidence before acceptance.

Acknowledged by: integrated into PR #20 as 31c2546, then narrowed in fb23c4f to the slot-cache reuse and live-safety regressions. The integration record carries final verification; Claude review remains requested.
