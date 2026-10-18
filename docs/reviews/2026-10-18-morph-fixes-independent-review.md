# Independent morph-fix review

Codex reviewed PR #54 at `227ebb16709f3bce71210d428227db76c0a05a93` and integrated main `e76ecbd` in review commit `b7709fd`. The review covered both-door probe precedence, negative trajectory values, per-press summon capacity, original aim propagation, lethal resource refusal, signature validation and the removed morph exemptions. The four corrections fit the shared contracts; approval remains pending the finding below and final performance evidence.

## Remaining pipeline exception

The new claim that combat-grammar invariant 1 holds is not yet true. `HealHandler.Execute` folds healing and sends the result directly to `Stats.AddResource`. Two independent cases each reduce a victim from 98 life to 93 with no `DamageDealt` event:

- A heal amount of `-min(5,target.life)`.
- A positive heal amount of 5 with a flat -10 modifier on the caster's `healing` attribute.

This runtime behavior predates the request. The request's new invariant claim and removal of the last named exception make it material to this review. Guarding negative input literals alone is insufficient: the complete amount, potency and modifier fold must be checked. Either correct healing with regression and morph coverage, or retain the invariant as a target with this explicit exception until corrected. The finding is [published on the request](https://github.com/arianna-arpg/hollow-wake-mmo/pull/54#issuecomment-6253968530).

`experiments/combat-grammar/review/MorphNegativeHealProbe.cs` is the two-case reproduction. Copy it into the core test project in a disposable checkout and run `dotnet test tests/HollowWake.Core.Tests -c Release --filter FullyQualifiedName~MorphReviewProbeTests`. Both assertions failed, as retained in the neighboring `morph-negative-heal-probe.txt`. It is kept outside the normal passing suite and is not an exemption or a passing test.

## Independent gates

On integrated source `b7709fd`, with the intentionally failing probe outside the test project:

- Release solution build: zero warnings, zero errors.
- Full solution tests: 748 core, 52 server, 119 client, all 919 passing with no skips.
- Deeper morph check with `HOLLOWWAKE_MORPH_POINTS=128`: all 48 morph cases pass.
- Content validation: zero errors and warnings.
- Origin zone load: 200 ticks, 2,672 entities and 272 monsters.
- `fight wolf hob`: completed successfully.
- `tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json`: all seven pair hashes identical to the merged baseline.

Raw functional logs are in the review checkout's ignored `artifacts/morph-review-*` files. A Release hot-path benchmark is still required after the final runtime correction; no timing acceptance is claimed from this review. No client, network or graphical behavior changed in these four corrections, so this review did not repeat graphical checks.

## Integration notes

The merge preserves #47's test-local `MorphDimension`/`MorphParameterKind` names, #52's Windows replay gate and #53's allocation-test hardening. #20's owner was notified to preserve `EffectContext.Aim` in later release-context integration. The existing area/field `from` anchor exception stays named and is not silently resolved here. Probe expressions remain conservatively treated as potentially active, as the new helper documents.

Acknowledged by: Claude's [correction record](2026-10-18-combat-morph-fixes.md), with the heal corrected at `8f3451a`; the rereview on PR #54 is pending.
