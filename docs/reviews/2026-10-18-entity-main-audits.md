# Entity stack integration with main audits

Codex owns the #31 integration of main `45ce87dae2102245a91c09e1d12086d4481880f0` onto foundation `3007003db865eceafac955ecf2a859431f010ae2`, at the coordinator's request. This record declares overlap with #47's `AxisBindings`, grammar tests and frozen-literal fixture before the reporting repair. #36 owns its separate contact/aim bindings and literal census; its complete refreshed branch will be consumed by #34, not copied backward into #31.

Hosted run [39158058935](https://github.com/arianna-arpg/hollow-wake-mmo/actions/runs/39158058935) tested the PR/main merge and failed three new main grammar tests: the `summon.raise_as` reference had no axis binding, the signature parameter census found the same omission, and the frozen-literal census found 21 pre-existing undead preset keys. The branch-only local suite did not contain these main-only tests. The Windows replay job checks duel hashes, not the complete grammar suite; its pass is not evidence that Windows passes these three tests.

Main merged automatically without conflicts. Its existing movement/AI, entry, icon, reporting and other changes remain intact. In particular, summon/minion plan invalidation now uses main's `Motion.Planned`; the entity branch's corpse gating and faction/birth behavior remain intact. This full main merge is not runtime-equivalent to the previous foundation or the retained #37 binary, even if the following audit repair is reporting-only.

The integration is prepared in a separate managed worktree. The final #37 source/content `ce3bcf569fb27426991ec69931c950e6951c74e1`, evidence head `c6e117a362b552f9865155789fdf020d979f97d2`, and its built outputs remain untouched for the serialized benchmark window. No builds, tests or benchmarks have run for this main integration yet. Full combined gates will run after the coordinator releases heavy work; no pass or merge acceptance is claimed.

Acknowledged by: pending Claude review.

## Audit-only repair

The `summon.raise_as` binding derives its `patterns` reference kind from the already-declared handler signature and uses the existing summons report reader. It adds the actual raised body to `agency.summon` beside the corpse requirement. It changes no handler, summon recipe, compiler, numeric content, gameplay rule, seed or runtime scheduling. A targeted report regression covers corpse conversion and an ordinary summon; the existing exhaustive signature/reference and frozen-literal assertions remain unchanged.

The following 21 keys are reviewed additions to the census of numbers already authored on #31 before main's audit existed. Each was checked against the exact JSON value in `3007003` and the main-merge tree `44a8015`; the content Git blob is identical. Evidence: `experiments/entity-review/evidence/31-main-audit-literals.json`. This admits existing debt explicitly, not a claim that frozen axes are desirable or an exemption from future census checks. Converting lifetime, capacity, reservation, respawn and decay to composable attribute expressions is a separate behavior change that must preserve sentinel semantics and be tested with modifiers; it is intentionally not hidden in this integration repair.

| Exact census key | Existing value | Reason for retaining behavior |
| --- | ---: | --- |
| `preset bone_arrow effects[0].radius` | `0.16` | Existing arrow collision radius in world units; no geometry change. |
| `preset raise_dead effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset raise_zombie effects[0].count` | `1` | Existing one-corpse, one-body request; corpse gating is unchanged. |
| `preset raise_zombie effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_abomination effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_barrow_hound effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_blade_wraith effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_bone_golem effects[0].contract.reserve.mana` | `30` | Existing contract mana reservation, preserving its economy. |
| `preset summon_bone_golem effects[0].contract.respawn` | `8` | Existing contract respawn seconds, preserving return timing. |
| `preset summon_bone_golem effects[0].count` | `1` | Existing single-body contract request, preserving capacity behavior. |
| `preset summon_bone_golem effects[0].max_active` | `1` | Existing one-active-golem cap, preserving exclusivity/capacity. |
| `preset summon_ossuary_lich effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_skeletal_duelist effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_skeletal_mages effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_skeletal_sentinel effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_skeleton effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_skeleton_archer effects[0].duration` | `0` | Existing no-timed-expiry sentinel; capacity, death and dismissal still apply. |
| `preset summon_wraith effects[0].decay.delay` | `4` | Existing delay before decay begins, in seconds. |
| `preset summon_wraith effects[0].decay.fraction` | `0.04` | Existing initial fraction lost per decay step. |
| `preset summon_wraith effects[0].decay.growth` | `1.35` | Existing decay growth factor, preserving escalation. |
| `preset summon_wraith effects[0].duration` | `0` | Existing no-timed-expiry sentinel; decay still removes life. |

`AxisBindings` is consumed by the axis readers, composition lint and CLI report, not by the simulation execution path. The audit repair itself is reporting/test/fixture/documentation-only relative to `44a8015`. The full merge with main still includes runtime changes; therefore older #31/#37 timing cannot certify the merged tree. The retained #37 measurement remains evidence only for its exact pre-integration source/content and binary hashes. Full gates and a regenerated census equality check remain pending until heavy work is released.

## #31 main integration gates

Source `a8a52b9770e01ef9156b2bfbf0aa02bb4d41bb8d`: solution build zero warnings/errors; all core 801, server 53 and client 128 tests pass. Validation is zero errors/warnings; load and the skeleton/zombie duel pass. The generated frozen-literal keys exactly equal the reviewed fixture; full census and keys are retained with these gates under `experiments/entity-review/evidence/31-main-*`. This verifies functional integration, not performance after main. Earlier benchmark misses remain open; no timing run or budget change accompanied these gates.

## #32 existing content census admission

Main's new audit finds the following existing constants in #32's pre-merge source `1fa611f3ef52703febab46d62b16e08126c8e90a`. Each raw content value and full owning content-file Git blob was verified unchanged; no literal disappeared from the inherited fixture. This record explicitly admits these old constants to the new census while retaining the exhaustive equality test. It does not add or change combat numbers. The axis and shadowed attribute below show the future composability work: replacing a literal with an attribute expression changes which modifiers reach it and must be a separate, tested combat-content change. Exact source blobs and values are retained in `32-main-audit-literals.json`.

| Exact key | Existing value | Reported axis/unit | Attribute shadowed |
| --- | ---: | --- | --- |
| `preset goblin_cinder_keg effects[0].on_end[0].radius` | `2.2` | `geometry.radius` (units) | `area.radius` |
| `preset goblin_cinder_keg effects[0].radius` | `0.3` | `geometry.radius` (units) | `none declared` |

## #32 main integration gates

Source `f1e2927105c95b4326c38503f7054bd3f8da6181`: solution build zero warnings/errors; all core 849, server 53 and client 129 tests pass. Validation is zero errors/warnings; load and the skeleton/zombie duel pass. The generated frozen-literal keys exactly equal the reviewed fixture; full census and keys are retained with these gates under `experiments/entity-review/evidence/32-main-*`. This verifies functional integration, not performance after main. Earlier benchmark misses remain open; no timing run or budget change accompanied these gates.

## #33 main integration gates

Source `921ba9e93beacf608985cf21dcdca47fed6c63e8`: solution build zero warnings/errors; all core 871, server 53 and client 130 tests pass. Validation is zero errors/warnings; load and the skeleton/zombie duel pass. The generated frozen-literal keys exactly equal the reviewed fixture; full census and keys are retained with these gates under `experiments/entity-review/evidence/33-main-*`. This verifies functional integration, not performance after main. Earlier benchmark misses remain open; no timing run or budget change accompanied these gates.

## #34 complete hit-body and main reconciliation

Fauna consumes complete #36 `c07b6df017ac9c7f9a854db836d5689e70debe16`, which includes the validated #33 `80dcac71e6de2d561cd79d58755b34433ffd0e81` and the complete prior hit-body lineage. This follows the agreed forward order #33 -> #36 -> #34, with no backward fauna merge or partial runtime picks. The sole conflict is the entity contract's anatomy sentence; the incoming explicit navigation-radius independence is retained alongside the fauna sections. AI documentation merges both main's intent contract and provisional wildlife assignments. No fauna dimensions, visuals, kits, population content, anatomy implementation or generation-golden constant is changed by the reconciliation itself. Main's existing shared runtime changes are included. Functional gates begin only after the coordinator releases the short diagnostic window.

## #34 intentional wildlife replay baseline

The first combined suite at `2bf403c` failed only the newly introduced main replay gate: three standard pairings involving the wolf/boar bodies intentionally reauthored in the existing fauna pass. All other 2,288 core tests, 59 server and 133 client tests pass; the original failure is retained as `34-main-tests-before-replay-update.txt`. No numeric content or runtime was changed to resolve it.

Causal check, same Debug runtime/source and Windows platform, ten seeds and a sixty-second cap throughout: exact archived #36 `c07b6df` content reproduces all seven inherited hashes. Current fauna content changes only the three rows below. A temporary final-content overlay replacing only wolf and boar with their exact old definitions restores all seven inherited hashes. This isolates the changes to those two already-authored patterns, rather than an unexamined main/runtime regression. The updated patterns include explicit height/radius/materials, fauna resource bases and poise (wolf 18, boar 40); both now emit poise-breaking/rearming statuses. The counterfactual isolates the whole patterns, not each property's individual contribution. Other four pairing hashes, seed count, cap and platform remain unchanged, including the prior #36 projectile baselines where unaffected.

| Pairing | Before hash | Fauna hash | Median time before -> after |
| --- | --- | --- | --- |
| `loosed_kindler:boar` | `ae3a86f086814949` | `e59288fd8b0152b9` | 6.7 -> 6.65 s |
| `loosed_skirmisher:boar` | `7dd259e21c8ac0e8` | `1f6c4b594340b306` | 7.6 -> 8.7 s |
| `wolf:hob` | `043fb9358749be95` | `ef8f11679dddfa70` | 14.8 -> 14.8 s |

Winner/draw rates for these three pairings are unchanged. All seven standard runs and the counterfactual contain zero simulation warnings/errors. Full reports and text output are retained in `34-main-replay-*`. The evaluation contract permits a reviewed baseline change for intentional content changes; only these three proven hashes are updated.

The required proving-pair comparison is also retained, in `34-main-proving-*`. Its entire before/after JSON is identical, including mechanisms, trace hashes, outcomes and breaches. Both runs fail the pre-existing 0.8 win ceiling for the bulwark, plaguer and pitfighter, each winning ten of ten. These inherited balance breaches are not claimed passed or attributed to fauna; no bands or runtime were relaxed. Release replay and the full suite will now verify the narrowly updated baseline. The shared snapshot/planning timing misses remain separate and open.
