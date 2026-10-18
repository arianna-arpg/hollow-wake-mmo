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
