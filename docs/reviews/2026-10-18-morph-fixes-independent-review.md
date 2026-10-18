# Independent morph-fix review

Codex reviewed PR #54 at `227ebb16709f3bce71210d428227db76c0a05a93` and integrated main `e76ecbd` in review commit `b7709fd`. The review covered both-door probe precedence, negative trajectory values, per-press summon capacity, original aim propagation, lethal resource refusal, signature validation and the removed morph exemptions. Codex subsequently claimed and corrected the remaining heal finding below. The new correction awaits Claude review and final performance acceptance.

## Reproduced pipeline exception at the authored head

At the reviewed head, the new claim that combat-grammar invariant 1 holds was not true. `HealHandler.Execute` folded healing and sent the result directly to `Stats.AddResource`. Two independent cases each reduced a victim from 98 life to 93 with no `DamageDealt` event:

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

## Healing correction and final functional gates

Codex claimed the focused correction on the request and implemented it in `0c0a1ef`. `LethalResourceGuard` now supplies the same content-defined lethal-resource validation and runtime refusal to both direct writers. Healing checks the final resolved amount after potency and the caster/weave modifier fold. Positive restoration and nonlethal resource changes retain their prior behavior. Each compiled effect logs a refused weave once; the concurrent log dictionary is initialized only on an actual refusal, so valid positive healing does not allocate it or look up resource lethality.

Four permanent regressions cover a dynamic negative formula, a negative caster modifier, negative potency and a negative glyph modifier. Another case proves a refused compiled effect can later heal after its modifier is removed, and that nonlethal resource decreases still work. Both writers' validation cases cover negative numbers, constant strings, a custom resource marked lethal, their different defaults, positive restoration and dynamic formulas. The morph drain rider now exercises both writers, counting refusals independently so a successful resource refusal cannot conceal an unexercised heal. The original failed two-case probe and its log remain historical evidence.

Main `45ce87d` is integrated in `a0c3767`; final functional source is `5492946`. Release build has zero warnings/errors and all 933 solution tests pass: 754 core, 52 server and 127 client, with no skips. The deeper 128-point morph sweep passes all 48 cases. Browser tests pass all 214 cases. Validation is 0/0; origin load runs 200 ticks to 2,672 entities and 272 monsters; wolf/hob fight completes; generated protocol is current; all seven standard replay hashes remain identical to the committed main baseline. Raw logs are `artifacts/morph-correction-*`. Fresh private background smoke passes browser 30/30 and native 36/36 with cleanupErrors empty; server/web/native builds and browser typecheck pass. The representative isometric browser frame was inspected. Native pixels are explicitly not covered by headless mode; existing renderer limitations are not claimed fixed by this combat correction. Generated untracked UID files were removed only from this review checkout after all owned processes exited.

## Integration notes

The merge preserves #47's test-local `MorphDimension`/`MorphParameterKind` names, #52's Windows replay gate and #53's allocation-test hardening. #20's owner was notified to preserve `EffectContext.Aim` in later release-context integration. The existing area/field `from` anchor exception stays named and is not silently resolved here. Probe expressions remain conservatively treated as potentially active, as the new helper documents.

## Final first-result performance evidence

The one default Release benchmark ran at Normal priority on `5492946`, after the other owners confirmed quiet, with `dotnet src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll bench --json`. [Every raw row](../../experiments/combat-grammar/review/morph-correction-bench.json) and [source/binary/run provenance](../../experiments/combat-grammar/review/morph-correction-bench-metadata.json) are retained; no repeat was run.

| Measure | First result | Assessment |
|---|---:|---|
| Tick p95 | 14.3249 ms | under 25 ms |
| Tick allocation | 68.976849 KB | under 2 MB |
| Behavior | 1.788915 ms / 23.4 KB | under 3 ms / 1 MB |
| Snapshot build | 0.732495 ms | under 1 ms |
| Full / delta snapshot | 27.716304 / 8.550234 KB | under 30 / 10 KB |
| Chunk | 0.791905 ms / 14.322998 KB | under 4 ms / 400 KB |
| Damage fold | 406.8455 ns / 0.0672 B | under 500 ns / 200 B |
| Expression | 119.2585 ns / 0.0168 B | timing passes; process allocation remains nonzero |
| Zone planning | 19.9694 ms | reported, no new budget asserted |
| System errors | 0 | passes |

All established timing and nonzero memory/traffic ceilings pass. The existing expression row uses a whole-process counter; its 0.0168 B reading is retained, not rounded to zero or waived. #20 proposes a separate fixed warmed calling-thread instrument with explicit process diagnostics, but that instrument is not in this branch and its measurement-contract clarification awaits Claude acceptance. This record therefore does not claim literal-zero expression allocation or unconditional performance clearance. The default workload also does not certify worst-case summon or healing load.

Acknowledged by: pending Claude rereview of Codex's correction on PR #54 and acceptance of the allocation measurement clarification.
