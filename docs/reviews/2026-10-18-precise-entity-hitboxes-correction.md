# Hit-body review correction

## Ownership and base

This supersedes the initial behavior claims in the [October 17 record](2026-10-17-precise-entity-hitboxes.md). It responds to [Claude's review of 2828586](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6245821648). Shared-file ownership was recorded [before the correction](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6252792265).

The branch remains based on Orc #33 at `8296c3ca34add6042691731a4e02bb1aa6aef0b1`. PR #20 was inspected at `da8e549547bea62538e9aba875e74bd7938f139e`; grammar #45 at `bfde71601e4b2b043f1468a5364d4d5bda706e39`. Neither is merged here. The owner authorized coordination with the owning chats. #20 owns committed release/miss policy and melee approach behavior. #36 owns shared damage geometry, projectile elevation and diagnostic presentation. Entity owners preserve their dimensions and test their separate content against the combined foundation.

## Corrected behavior

- Removed #36's private whole-graph melee suppression. Admission, target fit, AI range and `reach: edge` again use the original clearance radii. Removed the frozen-channeler fixture modification and the test that asserted the private release rule. No new melee counterplay policy is selected here. Integration must preserve #20's compiled release checks, miss events and rider fixes, and explicitly select `contact: body` on its Strike sector.
- Direction projectiles retain a sampled aim offset, independently of their cleared payload target. `CastState.AimOffset` and `EffectContext.AimOffset` do not acquire or lock a victim. Registered `aim` choices are `horizontal`, `target` and `point`; content selects `point`. Explicit streamed ground points clear the body offset; existing held-focus tracking refreshes it. A regression moves the original target during windup and verifies that the shot hits a bystander at the pressed aim instead.
- Direct launch elevation is the authored aim-volume center, or half the legacy body height. This replaces the old universal +1 m launch; the player profile launches at 1.098 m and the legacy kindler at 0.9 m. Explicit point/inherited origins still use the authored +1 m origin lift. The flight slab is 0.2 m, centered at its Z, replacing the old 0.5 m transform/band. These are intentional contact and presentation changes.
- Targeted launches, homing, chaining and returning use body aim elevation. A launch's slope persists through piercing and is inherited by forks, so a descending shot may later strike the ground. Orbit height follows the owner. Gravity keeps its existing lob solver. This does not fix nested emission-origin ownership or replace terrain occlusion.
- Damage volumes normalize all axes by stature, preserving their authored rest silhouettes within authoring precision. Navigation-radius tuning alone no longer widens them. Uniform rarity/stature scaling still applies; nonuniform anatomy needs authored proportions. Legacy bodies remain feet-to-top clearance cylinders. Mass and movement code are unchanged.
- Continuous disk contact, first-contact/entity-key ordering, contact-point hit origins and chain/return path truncation remain. Non-disk or hollow projectile shapes retain endpoint contact. Rounded square/triangle/line/bar/arc/sector boundaries remain exact; the sector apex gets only actual body padding, with no extra allowance. The #20 support audit found no authored requirement for one. Cleave continues to opt into body contact; other areas/fields retain their chosen center policies.
- Enclosing bounds are cached per tick, invalidated by pattern/player creation. Same-tick resizers must call `HitBodies.InvalidateBounds`; position/facing do not require invalidation. Sweeps reject distant volumes first and solve constant-height disk entries analytically. Tests cover same-tick spawn/resize invalidation and allocation-free cached reads.
- `body.player_hit_profile` is validated. Catalogs/ids are sent only to diagnostic views requesting `hit_bodies_v2`, whose recipes use height on all axes. The earlier unmerged radius-normalized v1 is no longer granted. Both clients request v2 only when the developer option is enabled before connecting; enabling it later requires reconnecting. Normal projectile rendering independently negotiates `projectile_slabs_v1` and needs no catalog.

These mechanisms follow the grammar's choices/registries and shared cast context. There is no species/form-name branch in collision or aiming, no new modifier fold, and no presentation read by simulation. The grammar's later instrumentation commands are not present in this base and are not claimed as passing.

## Evidence and gates

Evidence is under [experiments/entity-hitboxes/evidence/correction](../../experiments/entity-hitboxes/evidence/correction/).

| Gate | Result |
|---|---|
| `dotnet build HollowWake.sln` | Final incremental build: 0 errors, 0 warnings. A fresh test compilation still reports the three inherited xUnit2031 warnings in PortedProjectileTests. |
| `dotnet test HollowWake.sln --no-build` | Core 1570, server 43, native logic 116, all pass. |
| Real projectile matrix | 42 profiles × three terrain reliefs (-0.5, 0, +0.5 m) × seven forms/presets = 882 passing cases. Real `TryBegin`, projectile birth, damage and retained contact-ledger assertions; dimensions unchanged. Includes firebolt/Missile, piercing_arrow/Shot, Glaive, Orb, Volley, Quiver and Drawn Shot. |
| `validate`, `protocol-ts` | 0 errors/0 warnings; generated module current and contract test passes. |
| `load`, `fight` | 200 ticks, no system errors. Orc/Skeleton: Skeleton dies at 11.35 s. Restored moving Channeler/Duelist fixture passes; the duel leaves Channeler at 51/116. Wolf/Hob leaves Wolf at 69/96; Kindler/Boar leaves Kindler at 112/112; Skirmisher/Caller both die. Traces are retained without claiming unchanged outcomes. |
| Private server plus probe | Both v2 anatomy and slab features granted; catalog, movement, deltas and interface checks pass. |
| Browser tests/build | 237 tests pass. Production build passes; approximately 1621.5 kB main chunk with the large-chunk advisory. |
| Background smoke | `run-csgTBC`: browser 29/29, native 25/25, no cleanup errors. Native pixels explicitly untested. Shutdown still reports dummy texture/font/CanvasItem/ObjectDB allocations, the same counts recorded in the initial baseline. |
| Geometry presentation | Regenerated shared native/browser fixture; front and turned browser galleries visually inspected. No native graphical-session claim. |

An initial full test attempt found the expected stale protocol module before regeneration and an allocation assertion failure in the unchanged pathfinder test (7584 bytes). That test passed in isolation and in subsequent complete runs; no assertion was relaxed or excluded.

## Performance

All correction samples used normal process priority. The new `bench --only hit-bodies --json` roster resolves all 42 profiled patterns into the same 120-foe workload. Before optimization, projectiles cost 4.122 ms/tick; after, two samples cost 1.855 and 1.846 ms/tick. Total workload cost changed from 5.382 to 3.981/4.180 ms/tick. Allocation changed from 35.972 to 36.179 KB/tick, including casts/hits; the enlarged cast context has a small allocation cost. Each reports about 39 live flights. This is a local timing comparison, not a guarantee under arbitrary concurrent load.

The first full benchmark exceeded several budgets, including unchanged expression/fold channels: expression 215 ns, extra fold 678 ns, behavior 3.441 ms and snapshot 1.241 ms. It is preserved. The repeat passes the published named budget metrics: p95 11.073 ms, behavior 1.990 ms, snapshot 0.961 ms, full/delta payloads 23.229/7.765 KB, extra fold 403 ns, expression 131 ns, chunk generation 0.935 ms/14.378 KB. Interface snapshots measured 1.044 ms separately. Budgets were not changed, and no priority adjustment was used. Integration should repeat these measurements on its combined stack.

## Integration and review request

Claude rereview requested for corrected geometry, v2 capability gating, performance evidence, and reconciliation with #20. Do not restore the removed private release gate during conflict resolution. Preserve the sampled aim offset in shared cast/context constructors. This correction depends on the existing #36 geometry foundation and is not a standalone cherry-pick onto #34.

Kobold #38 and elemental #40 have separately authored projectile relief matrices. Their new content is not in this branch; their owners will run those gates on a temporary combined stack, retaining separate content diffs. Corrected entity mass/shove/corpse/site work remains with its owner. #20's release/rider and grammar integration still require combined-stack review; this record does not claim that integration is complete.

Acknowledged by: pending Claude rereview.
