# Precise entity hitboxes

## Claim and baseline

Codex owns this additive entity-hitbox foundation on `codex/precise-entity-hitboxes`, in the new managed `precise-entity-hitboxes` worktree. Base: `8296c3ca34add6042691731a4e02bb1aa6aef0b1` (Orcs #33, stacked on Goblinkin #32 and undead #31). Remote recheck found all three open at the requested heads. The root checkout and the other entity chat's worktree are not used for edits.

Before editing, inspected open #20 at `da8e549547bea62538e9aba875e74bd7938f139e` (cast forecasts and positional hits) and #22 at `0de2d29de38bb4ddc57112d52fe4deeb433cddaa` (public equipment/day). This base contains the area-plan foundation, but not #20's integrated forecast wire and positional release changes. Integration must preserve those behaviors.

Publication recheck: #33 remains open at the exact requested head, #20 remains open at the recorded head, and #22 is now merged. This branch deliberately retains the requested #33 base; it does not claim testing a merge of #20 or the newer main containing #22.

Claimed overlapping seams: core delivery handlers, projectile system, area-plan candidate geometry, targeting/reach helpers; public entity snapshot/message/delta geometry; both entity views and shared diagnostic geometry; affected contracts and tests. Appearance/equipment and forecast lifecycle ownership remains with those requests. No cosmetic bounds are used by the simulation. This record is notice, not an acknowledgement by another engineer.

Additional seam claimed during bundle verification: `web/src/ui/logic/contentFiles.ts` excludes the new simulation-only anatomy definitions from static UI content. The negotiated server catalog supplies them; baking a second copy into the application would add size without serving the interface.

## Scope

Author simulation damage volumes through registries, independently of conservative movement clearance and mass. Cover facing, height, tiny and broad/long bodies, live scaling, visible misses and grazing contacts. Carry resolved geometry to both clients for an understandable diagnostic overlay. Keep ground areas' existing center/height policy unless a delivery explicitly selects body contact. Do not implement mounting, rider control or rider mass aggregation.

## Verification

Evidence lives in `experiments/entity-hitboxes/evidence/`.

| Gate | Result / evidence |
| --- | --- |
| `dotnet build HollowWake.sln --no-restore` | Pass, including native client. Zero errors; the three existing xUnit2031 warnings in `PortedProjectileTests` remain. `build-final.txt`. |
| `dotnet test HollowWake.sln --no-build` | Core 688, server 42, native client 116 passed, none skipped or failed. `tests-final.txt`. |
| `hollowwake validate` | Zero errors, zero warnings. `validate-final.txt`. |
| `hollowwake load --x 0 --y 0` | 200 ticks without logged errors. `load.txt`. |
| `hollowwake fight orc_grunt skeleton_warrior` | Both bodies deal damage; skeleton dies at 11.35 s. `fight.txt`. |
| Server plus `probe --deltas --interface --features hit_bodies_v1` | Private harness server; feature granted, catalog received, movement/chat/interface checks pass. `probe.txt`. |
| `protocol-ts` | Regenerated module; server protocol contract test passes. `protocol.txt`. |
| `npm --prefix web test` | 237 tests in 33 files pass. `web-tests-final.txt`. |
| `npm --prefix web run build` | Pass. Main bundle 1,621.22 kB / 376.90 kB gzip; Vite reports its chunk-size warning. The redundant local profile payload was removed; the server catalog remains authoritative. `web-build-final.txt`. |
| `node scripts/test-background.mjs --suite all` and final `--no-build` run | Browser 29/29, native headless 25/25. Native rendering/theme pixels explicitly skipped. `smoke-result.json`, `smoke-web.txt`, `smoke-godot.txt`. Final web-only smoke after bundle exclusion also passes 29/29 in `smoke-web-final-result.json` / `smoke-web-final.txt`. |
| `node experiments/entity-hitboxes/render.mjs --write-fixture` | Both front/turned views render without page errors; both inspected. `render.json`, `bodies-front.jpg`, `bodies-turned.jpg`; shared fixture passes in both clients. |
| Release `hollowwake bench` | Final values below meet the documented budgets; normal-priority repeat variability is preserved and explained. `bench-final.txt`. |

Native shutdown logs contain the same dummy-renderer texture/font/CanvasItem and 23 ObjectDB leak warnings as the committed Orc baseline at `experiments/orc-warbands/evidence/gates/smoke-godot.txt`. They are not hidden by the passing smoke summary. No launcher/release pipeline changed.

### Performance conditions

Default workload, final Release binaries, no parallel build or smoke from this chat. The final benchmark process alone used Windows `AboveNormal` priority; no unrelated process was changed. It measured zone p95 10.952 ms, allocation 70.233 KB/tick, behavior 2.229 ms / 22.9 KB, snapshot build 0.820 ms/player, full snapshot 24.435 KB, delta 7.831 KB, chunk generation 0.771 ms / 14.378 KB, attribute extra fold 331.370 ns and expression evaluation 106.305 ns. The small expression allocation reading remains the benchmark's 0.017 bytes/eval; the steady-state delivery allocation regression passes. There were zero system errors. Delivery projectile work was 0.466 ms/tick including payload effects.

Normal-priority measurements on this shared machine varied substantially: an earlier run measured snapshot 0.854 ms; preserved runs measured 1.121, 2.259 and 1.543 ms (`bench-first.txt`, `bench-repeat.txt`, `bench-repeat-2.txt`). The slowest also exceeded unchanged expression/attribute budgets. Scheduling contention is a plausible explanation, not a proven attribution. The final controlled sample passes without changing workload or budgets; repeat the normal-priority gate on the integration host rather than treating this as a guarantee under arbitrary desktop load.

## Behavior and evidence

- Registered `hit_bodies` definitions and `hit_body` components separate damage contact from clearance and mass. Authored player, skeletal, Goblinkin, Orc and Gnasher bodies use compounds of cylinders and round ellipsoids. `AuthoredAnatomyKeepsStatureClearanceAndMassIndependent`, invalid-content tests and live-scaling tests cover the contract. No new mounting/rider behavior is included.
- Anatomy rotates with authoritative facing and scales from live Transform dimensions. Tiny bodies, empty gaps, broad/long bodies and large-scale broad-phase completeness have focused regressions in `HitBodyTests`. Targeted bolts can descend to a whelp; an untargeted horizontal bolt passes overhead. Cosmetic meshes never enlarge contact.
- Disk projectiles sweep continuously and sort by first contact/entity key. Existing ledger, pierce, rehit, fork, chain and return tests pass. A new transition regression checks that chain/return starts at contact instead of striking the rest of the old path. Shape padding uses round distance at corners, including a degenerate sector rim.
- Target admission/AI use body reach; melee payload release checks it again. `AnActualMeleeReleaseMissesABodyThatLeftDuringWindup` checks actual runtime damage and cast publication. Cleave explicitly selects `contact: body`; ground areas/fields otherwise retain center policies. `AreaObservationAndExecutionShareOptInBodyAndVerticalContact` pins execution/observation agreement.
- The existing angle/reach fixture moved its side target outside the newly valid body-edge graze. The proc-order duel now holds its kiting target still: under actual release checks the moving target had left melee range at every windup. This preserves the test's proc-order purpose; it does not assert the previous moving-duel damage trace is unchanged.
- The additive `hit_bodies_v1` catalog comes from the server; snapshots, fingerprints, deltas and clears carry the profile id. Loopback tests cover opt-in/off and wire round-trip. Shared line fixtures check both clients, including an unknown-shape refusal. Actual browser meshes and overlay were inspected in both small JPEG captures.

## Limits and integration asks

The damage body is authored rest-pose anatomy, not animated bone/triangle collision. Source melee reach is a directional extent, not a swept weapon. Circular convex flights sweep; other projectile shapes retain endpoint collision. Existing wall/ground and bridge/surface policies remain separate. The current broad phase derives live maximum body bounds for each query to stay correct under resizing; larger crowd/projectile budgets should profile this work before introducing a cache with invalidation.

Point/inherited projectile origins retain their legacy lift, now in a content rule. Nested emission origins are not solved here. This branch has no forecast wire integration or spatial-miss event from #20; the coordinator must reconcile those seams without replacing that lifecycle. #22 equipment remains cosmetic and must not become hit geometry during integration. Native behavior and shared geometry are checked headlessly; native pixels still need an isolated graphical session.

## Review request

Claude review requested, especially simulation/presentation separation, deterministic contact, broad-phase completeness, scaling, the documented performance conditions and compatibility with #20/#22. Do not merge this request automatically.

Acknowledged by: pending.
