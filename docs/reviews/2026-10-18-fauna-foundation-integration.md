# Fauna foundation integration and shortest-body flight floor

Date: 2026-10-18. PR #34. Previous Fauna head `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`.

## Complete inputs and scope

The combined source `346fc1fcba35079d8b3f6d2f715e895788100d45` merges complete #36 `23f4a5eb242abe3f4fe889c7cbcb7a98f62de515` (runtime `b116725108a1b814bb875d408f3b17aee95bf412`) and main `f7ef072cc322b93b628229916c5220024e74a89c`, including approved #32 occupied-start escape. #36 source clearance [6256536065](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256536065) and [handoff6256562004](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256562004) cover floor/return, hook elevation, snapshot observation and chained-pierce range. The complete incoming source is retained; no isolated correction is copied backward.

Fauna definitions, dimensions, materials, factions, kits, world-generation goldens, browser/native appearance and existing card/harness corrections are unchanged. The only additional gameplay change in this follow-up is the content floor below. No runtime code is added for Fauna, and no #37-40 or separate #20/#50/#57 work is merged here.

## Shortest-body correction

The incoming floor is 0.342 m, checked against the #36 authored-profile roster. Fauna adds fallback cylinders as short as gutter_rat at 0.16 m, with reed_frog, shore_crab and hedgehog at 0.17, 0.18 and 0.19 m. A 0.2 m centered projectile slab at the incoming floor has a bottom of 0.242 m and can pass over these bodies after settling.

The existing humanoid free-shot fixture, applied to all 22 Fauna patterns across three slopes, two presets and two cursor relationships, gives 219 passes / 45 failures before correction. Changing only `delivery.projectile_ground_floor` to **0.16** gives 249 passes / 15 failures: every beyond-cursor shot now hits. Both 264-case reports remain evidence; nothing is resized or enlarged.

The remaining 15 cases cross a tiny body while still descending toward a farther ground cursor. They are preserved as explicit negative regression cases. The test observes real projectile transforms and slab heights throughout horizontal overlap with each unchanged body's envelope; minimum lower-slab clearance over the body top is positive in all 15, from 0.005479902 m to 0.0851202 m. A floor limits descent; it does not redirect an airborne segment onto an intervening creature. The full clearance lines are retained rather than replacing these cases with unspecified exceptions.

The inherited floor assertion now checks **every statted resolved pattern**, using the authored volume top when it has a hit profile and the actual height for a fallback cylinder. The minimum remains the content-authored 0.16 m. This is a real full-roster guard, not a profile-only check or a hard-coded list of Fauna names. Existing body-selected aim remains covered by 462 Fauna actual-flight cases.

The final focused suite passes **848**: 264 settled free-shot contacts at two distances, three slopes, two real presets and every Fauna size; all 15 geometry-proven airborne misses; 462 existing body-selected Fauna contacts; and 107 incoming ground-floor/return controls. Body height/radius are checked unchanged. An initial negative fixture incorrectly expected a flat Firebolt to miss a hedgehog; that diagnostic is retained, and the final negative set is exactly the 15 measured misses rather than an inferred eight-case sample.

## Replay conflict and causal attribution

Fauna already has content-specific boar and wolf references. The incoming #36 replay changes are caused by the reviewed returning-pierce correction, as its retained counterfactual record establishes. The merge therefore retains Fauna's Kindler/Boar and Wolf/Hob references and imports the two returning-pierce pair changes. The first full combined test run fails only the overlapping Skirmisher/Boar reference; 2536 other core tests, 79 server and 133 native tests pass.

Release controlled comparisons retain complete reports:

- The complete standard report with the new 0.16 floor equals the report with a read-only 0.342 floor overlay, including every fight and hash. The Fauna floor change has no standard-duel effect in this sample.
- Only Skirmisher/Boar differs from the mixed baseline: incoming #36 `cf41fad065ff7e16` becomes `e661cfbccc766113` with Fauna's existing boar definition. A read-only overlay restoring the exact #36 boar definition restores `cf41fad065ff7e16`. Both controlled boar versions retain 10/10 Skirmisher wins and median 7.6 s.
- The combined baseline records `e661cfbccc766113`; Skirmisher/Hob keeps reviewed #36 `1cf76df19ceeb296`. All five other pair references stay at the preserved Fauna values. No runtime file was edited for these controls, and no band or budget changes.

## Evidence and handoff

Fresh final combined gates, probe, smoke and screenshot inspection are recorded below after their exact source is committed. No benchmark or performance trace is authorized; no main merge or sibling propagation occurs before this complete stable handoff. The separate frozen-aim/capture additions remain owned by #20.

The app could not restore the archived entity-main-audit checkout: `The archived worktree snapshot is missing.` A new managed `fauna-foundation-integration` worktree was created from published Fauna source instead. Merged source `22fc19f` remains reachable on main, and all 2,846 ignored historical evidence files remain independently verified in `D:/Games/MMOARPG/artifacts/entity-main-audit-preserved-20261018-22fc19f2.zip` (SHA256 `1c451082a8f281ff7e54ec7d123c340336e97728a3c91aefd8d7e2d76136b239`) with its per-file manifest. No app internals or #37 worktree were changed.

The exploratory diagnostic's first invocation passed build flags through to its arguments; the initial JSON and console output are retained separately. Invoking the built diagnostic directly with the correct source/output arguments reproduced the same 219/45 result. This is an evidence-command correction, not a gameplay retry.

Acknowledged by: pending independent scoped correction review and Claude reciprocal rereview of the final combined head.
