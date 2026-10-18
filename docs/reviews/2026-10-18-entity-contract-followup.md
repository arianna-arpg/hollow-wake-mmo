# Entity contract and census follow-up

Addresses [Claude rereview 6255391549](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6255391549). The review accepts the owner-selected Knot implementation. This follow-up changes contracts, review records and one regression only; simulation and content are unchanged.

Entities and places now agree: pattern levels are the default, inhabitant specifications or landmark legend cells explicitly opt into site levels, and the shipped Knot opts its three inhabitants in. The quest contract distinguishes optional schema support from the shipped templates, which all constrain local-population target levels. Earlier authored-Knot-level wording is explicitly superseded. Missing acknowledgements link the actual review without implying final timing approval. Saved-world migration and unsupported opt-ins on unrelated spawn paths are not broadened here.

The anchored reach-40 regression uses a real resident outside the giver's radius. It must still offer from the selected site's first chunk; changing only the radius check back to the giver would fail it. Existing one-chunk, zero-budget, wrong-level, cached-budget and unresolved-anchor cases remain.

## Named undead census additions

These 21 keys are new to main's frozen-literal census (476 to 497) and belong to this PR's authored undead presets. They were already present in the undead content before the report-only `raise_as` binding repair; that repair did not change their values. They are explicitly named admissions, not an unnamed fixture expansion. The original value/blob evidence remains in `experiments/entity-review/evidence/31-main-audit-literals.json`.

| Exact census key | Authored value | Meaning retained |
|---|---:|---|
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

## Verification and planning follow-up

All 34 focused local-population, quest-anchor and population tests pass, including the new radius case. The build and test output is retained in `experiments/entity-review/evidence/31-contract-focused.txt`. [Claude independently verified a918 and accepted this follow-up](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6255604087).

The requested exact mainfa7/PR31a918 planning pair is now recorded in [the planning review](2026-10-18-entity-planning-pair.md): 19.1739 ms versus35.2371 ms, both below the unchanged40 ms limit, with the83.8% increase explicit. Every first row and source/binary/process identity is retained; there was no retry. This does not certify later combined-runtime performance or balance.

Acknowledged by: Claude accepted the contracts, named census and radius case in the linked rereview; the paired measurement's final interpretation remains pending.
