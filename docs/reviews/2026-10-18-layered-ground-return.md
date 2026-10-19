# Returning descent and layered-ground aiming

Date: 2026-10-18. PR #36. Production **d9ac853bed4368ee9ac732bc420f0d6ff8a50957**; tested follow-up **560adf01fe8c47667ad16ccc2b5c8aae668211b9** changes tests only. Includes complete main **f7ef072cc322b93b628229916c5220024e74a89c** by coordinator authorization; the accepted occupied-start movement implementation and tests remain unchanged. This publication adds only evidence and two attributed Windows baseline hashes.

## Scope and behavior

Addresses [Claude's blocking review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256607271) and [follow-up](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256782754). Ownership was declared before editing on [#36](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256689611), [#34](https://github.com/arianna-arpg/hollow-wake-mmo/pull/34#issuecomment-6256689865) and [#20](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20#issuecomment-6256690026). Content owns the aim policy and numeric floor; clients pick surfaces while the server resolves admitted input.

Default returning free shots now receive their ground floor at launch and keep raw descent through piercing hits. This reaches later short bodies and permits a return after a hovered target moves aside. A returning flight without an existing floor retains the prior chained-pierce leveling behavior. Gravity, orbit and explicit launch lift preserve their choices. Existing chained-leg distance accounting is preserved.

Floor support uses base terrain or a walkable layer below the previous flight position. Overhead decks do not pull a shot up from underneath. The loaded-only observation query shares support semantics, preserves raw pitch and never generates terrain. Both client cursor rays now intersect visible base terrain and walkable layer tops, including oblique and under-deck picks. Native chunk loading makes already-generated neighbors available for border interpolation without cursor-triggered generation.

Accepted server ground/direction inputs resolve Z hints to actual surfaces; hovered body height remains authoritative. Independent review and own regressions exposed a third defect in the first correction: distant points could generate remote chunks before admission. The final source rejects nonfinite input, validates the cast first and bounds accepted input before terrain resolution, reusing authored Ground range or the existing Direction aim limit. Refused Raise samples no terrain. No new range constant or wire shape was introduced.

Documented policy consequences remain: a far-cursor line can pass over a close short body; fresh placements, cascades and point origins use the default free-ground policy; base terrain retains uncapped floor rises; flight-hook children preserve exact inherited position/lift. The separate unshipped on-hit ring-child target-retention issue is unchanged.

## Verification and attribution

[Commands, failures, resolutions and provenance](../../experiments/entity-hitboxes/evidence/layered-ground/source.txt) retain initial evidence. Before fixes: returning cases 6 failures / 1 pass; actual production-stamped watch_tower/wreck_hull cases 6 failures; browser deck picks 2 failures / 2 passes; admission cases 3 failures. Final tests include real-landmark cross-level shots, layered snapshot purity, hovered misses, both client ray implementations and finite distant-point bounds.

[Independent scoped review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256927120) found no remaining blocker at d9ac853 and inspected the tests-only 560adf01 follow-up. It freshly passed 290 focused checks (252 core, 4 additional admission cases, 34 server); earlier 16 native and 24 browser checks were carried forward explicitly, not counted as fresh. Coordinator static review agrees. The peer's raw private payload is not republished.

The final owner gate cycle on 560adf01:

- Debug solution and Tools Release builds: zero warnings/errors. Validate: zero errors/warnings. Starter load: 200 ticks. Fight, generated protocol and six-feature private probe pass.
- Full .NET run: core 2049 pass / 1 baseline failure, server 89 pass, native 136 pass. After the justified two-entry baseline update, the sole failing test passes on rerun; no runtime source changed. Browser 275 pass. This accounts for **2550 distinct cases**, with original failure logs retained. Background-runner tests separately pass 10.
- Fresh production web build passes with its existing chunk-size warning. Private smoke run-5HVo2p passes browser 30/30 and native 36/36; no cleanup errors. All task-owned processes are stopped.
- All 777 lint keys remain unchanged (580 smells, 197 notes), as do all 499 literal keys. The initial literal command accidentally used one path for output and its redirected log; its IOException is retained, followed by a successful run with separate paths.

Only two standard duel hashes changed: Skirmisher/hob **1cf76df19ceeb296 -> 465a4807b0fe94f2** and Skirmisher/boar **cf41fad065ff7e16 -> 7dd259e21c8ac0e8**. The other five remain identical. Both win rates remain 1; median kill times remain 5.85s and 7.6s. Only these two committed baseline values were replaced; the final seven-pair Windows replay passes.

Causal attribution uses an isolated 560adf01 archive with exactly two production hunks reverted: returning floor adoption at launch and preserving the existing floor at pierce. It retains layered support, cursor/authority admission fixes, snapshot observation and current main. Blob comparison checks 402 archived production/content files and identifies only those two source files as changed. This counterfactual restores **all seven previous standard hashes** and makes its full proving report structurally identical to b116's prior report. The initial counterfactual relative-baseline-path error and corrected absolute-path run are both retained.

The final proving report changes only four Hash fields: two individual fights, their pair, and overall **6673881dfab95df6 -> 4730bef23767a558**. Every non-hash value is unchanged. Fletcher/Evoker stays 0.5/0.5 with 7s median kill time. Proving still exits 1 for the same three inherited ceilings: Bulwark, Plaguer and Pitfighter each win 10/10 above 0.8. This is not a clean balance result. [Exact differences](../../experiments/entity-hitboxes/evidence/layered-ground/proving-differences.json) and [comparison](../../experiments/entity-hitboxes/evidence/layered-ground/comparison.json) are committed.

The isometric browser screenshot renders the world. Card mode still shows the known blank-world/tiny-viewport issue owned downstream in #34. Native pixels are skipped in headless mode. Native shutdown retains 5 dummy textures, 2 shaped texts, 1 font, 1 CanvasItem and 26 ObjectDB leak diagnostics. These limitations are unchanged and not cleared by smoke pass counts. No benchmark or performance trace ran; ordinary harness timing is not performance acceptance.

## Handoff

Claude reciprocal rereview and exact final-head hosted checks remain pending at publication. Keep the PR draft until their disposition. Coordinator receives the definitive evidence commit for one downstream #34 merge; this record does not itself propagate or merge branches. #34 owns the full-roster content-floor correction. #20 owns sample-first input, frozen echo/captured Direction/targeted sector/physical CastSource and the separately declared changing-layer held-surface forecast extension. Those remain downstream union work. Coordinated performance acceptance is still open.

Acknowledged by: independent scoped review and coordinator static acknowledgment [6256927120](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256927120); Claude reciprocal rereview pending.
