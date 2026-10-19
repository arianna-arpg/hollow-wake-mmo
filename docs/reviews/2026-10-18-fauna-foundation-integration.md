# Fauna foundation integration and shortest-body flight floor

Date: 2026-10-18. PR #34. Previous Fauna head `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`.

**Current status: complete functional foundation prepared for coordinator adoption.** Tested runtime `900928f9e3706a650f1255b098e05550bc137976` includes complete #36 `0c610e4715c5d32002d051eec55d4fa7f7f4f526`. Final evidence and fresh replay attribution are below. Claude review, exact publication-head CI and coordinated performance clearance remain open; the PR stays draft. Earlier sections retain their historical source identities.

**Historical scoped checkpoint, superseded by the complete functional handoff below.** Runtime/content source `6ca91cc026998edcb5df0645fdbbff5c253f8d9d` is ready for independent review of the Fauna correction, not final foundation handoff. Claude subsequently reopened #36 in [6256607271](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256607271): returning free shots lose their downward path after a pierce, and bridge/tower shots use base terrain instead of layered walkable ground. The coordinator has paused final full-suite, client/probe gates and sibling propagation until complete corrected #36 arrives. The already committed combined replay entry is provisional and must be checked again then; no further replacement is made under this hold.

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

Final combined gates, probe, smoke and screenshot inspection have **not** run after the scoped correction. The first full combined run and its single replay mismatch are retained as partial results. The focused correction suite and controlled Release comparisons above are the completed checks. Hosted CI on this checkpoint, if green, does not close the reopened upstream findings. No benchmark or performance trace is authorized; no main merge or sibling propagation occurs before this complete stable handoff. The separate frozen-aim/capture additions remain owned by #20.

The app could not restore the archived entity-main-audit checkout: `The archived worktree snapshot is missing.` A new managed `fauna-foundation-integration` worktree was created from published Fauna source instead. Merged source `22fc19f` remains reachable on main, and all 2,846 ignored historical evidence files remain independently verified in `D:/Games/MMOARPG/artifacts/entity-main-audit-preserved-20261018-22fc19f2.zip` (SHA256 `1c451082a8f281ff7e54ec7d123c340336e97728a3c91aefd8d7e2d76136b239`) with its per-file manifest. No app internals or #37 worktree were changed.

The exploratory diagnostic's first invocation passed build flags through to its arguments; the initial JSON and console output are retained separately. Invoking the built diagnostic directly with the correct source/output arguments reproduced the same 219/45 result. This is an evidence-command correction, not a gameplay retry.

## Independent review and helper visibility

The independent scoped review of `6ca91cc` and evidence follow-up `ec9a8f0` reports 1,112 passing checks with no gameplay finding in the floor correction. Its 264 separate analytical predictions agree with all 249 contacts and 15 airborne misses. Complete peer diagnostics remain local; the coordinator owns the public review summary. This does not clear the upstream returning/layered-ground findings or the final combined integration.

The review identified xUnit1013 on the public non-test helper `AssertHumanoidFreeShotContact`. This follow-up changes only that helper's accessibility to `internal`; both test callers remain in the same assembly. Production, content, clients, scripts, replay references, assertions and runtime behavior are unchanged.

`dotnet build tests/HollowWake.Core.Tests/HollowWake.Core.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` succeeds with zero warnings and zero errors. The log and exact one-line declaration diff are retained as `34-helper-visibility-build.txt` and `34-helper-visibility.patch` under the entity review evidence directory. Neither the 848-case runtime selection nor the independent 1,112-check review was rerun. Final integration gates and sibling propagation remain held pending complete corrected #36.
Acknowledged by: independent scoped floor review completed at6ca91cc with the visibility cleanup above; final Claude reciprocal review remains pending under the upstream integration hold.

## Complete functional foundation handoff

The complete #36 publication `0c610e4715c5d32002d051eec55d4fa7f7f4f526` was merged once into Fauna `20ddf5a37fea115cdcb521bc06e24608934e2f4b`, producing tested runtime **`900928f9e3706a650f1255b098e05550bc137976`**. Complete main `f7ef072cc322b93b628229916c5220024e74a89c` remains included. The incoming [layered-ground/return review](2026-10-18-layered-ground-return.md), isolated causal comparison and original failures remain in the branch. This publication changes only the replay fixture, this review record and evidence after that tested merge.

The deliveries conflict combines support at the previous flight height, exclusion of overhead decks, bounded admitted cursor input, fresh-launch rules and pure loaded-only snapshot observation with Fauna's minimum across all statted patterns, including fallback cylinders. The content floor stays 0.16 m. The internal test helper, all 15 intentional airborne misses, body-selected checks, card renderer, interaction smoke and background harness corrections remain intact. Fauna definitions, physical dimensions, habitats, faction assignments and existing AI are unchanged. No #37-40 or separate #20 source is imported backward.

### Actual-union replay attribution

The baseline conflict initially retains the whole prior Fauna fixture. Both input fixtures and the conflict text are preserved under [34-final-union](../../experiments/entity-review/evidence/34-final-union/). The full solution run then reports exactly one failed test, the provisional baseline, naming Skirmisher/Hob and Skirmisher/Boar. No incoming hashes were installed without measuring the union.

Fresh Release controls on runtime900928f establish the following; the older 6ca91cc comparison above remains historical evidence only:

- Complete standard reports at 0.16 m and with a read-only 0.342 m overlay are structurally identical, including individual fights. This equality was checked again under the new returning policy that adopts its floor at launch.
- Actual Fauna Skirmisher/Boar is `fbf0ac20a5a74103`, with 10/10 Skirmisher wins and median 7.6 s. Restoring the exact original #36 boar definition produces incoming `7dd259e21c8ac0e8` at both floor choices; those two complete single-pair reports are identical. This distinguishes the existing Fauna boar contribution from the floor choice on the actual union.
- Skirmisher/Hob becomes `465a4807b0fe94f2`, matching the reviewed incoming return-policy correction. Incoming #36 preserves its independent two-hunk runtime counterfactual, which restored all seven previous isolated hashes while keeping layered support and admission corrections.
- Only the two prior Fauna references change: Boar `e661cfbccc766113 -> fbf0ac20a5a74103`, Hob `1cf76df19ceeb296 -> 465a4807b0fe94f2`. The other five stay unchanged. No runtime source was edited for these new content-overlay controls.

The corrected baseline test passes on its single-test rerun, and the required Release seven-pair Windows comparison prints `hashes: identical`. [Comparison](../../experiments/entity-review/evidence/34-final-union/replay-comparison.json), complete actual/floor reports, legacy-boar controls, exact overlay files and the original failed test log are retained.

### Final functional evidence

[Provenance](../../experiments/entity-review/evidence/34-final-union/provenance.json) records both parents, main, the runtime tree manifest, tested binary SHA256 values, smoke/probe identities and platform. [Verification](../../experiments/entity-review/evidence/34-final-union/verification.json) and the adjacent raw logs account for:

- Debug solution and Release Tools builds: zero warnings and errors. Initial full .NET run: 2,840 core pass plus the sole replay-fixture failure, 89 server pass, 137 native pass. The sole failed test then passes after the attributed fixture update. This accounts for 3,067 distinct cases; a second full-suite run is neither claimed nor needed for a fixture-only correction.
- Browser tests: 304 pass; prototype porter: 10; background-runner lifecycle tests: 10. Production web build succeeds with its existing chunk-size warning. Generated protocol is current.
- Content validation: zero errors/warnings. Starter load runs 200 ticks. Skeleton/Zombie fight passes. The audit retains all 777 lint keys (580 smells, 197 notes), and all 499 literal keys equal the committed fixture.
- Private smoke `run-n4vt04`: browser 31/31, native 36/36; cleanup errors empty. The separate private probe grants all six requested features and passes. Owned-process/lease checks find no remaining test processes or leases.
- The saved isometric and card screenshots were inspected: both show the full world viewport, terrain, bodies and interface. The isolated #36 blank-world/tiny-viewport defect is absent with the retained renderer fix. These captures do not replace anatomical rendering review of every creature.

The full proving report is structurally identical to Fauna runtime `26431c48a67ddd3b77811cf94363a589cdd50e69`, including hash `4730bef23767a558`. It still exits 1 for the same three inherited ceilings: Bulwark, Plaguer and Pitfighter each win 10/10 above 0.8. Fletcher/Evoker remains 0.5/0.5 with median 7 s. This is not balance acceptance.

Native pixels remain untested in headless mode. The run retains the known shutdown diagnostics: 5 dummy textures, 2 shaped texts, 1 font, 1 CanvasItem and 26 ObjectDB instances. Ordinary smoke timings are not performance acceptance. No benchmark or performance trace ran. No main merge or sibling propagation occurred. #37 remains at its prior source until the coordinator adopts this complete handoff.

One evidence-packaging command initially used the wrong Core framework directory; its diagnostic is retained. The manifest was corrected to hash the actual Core DLL beside the tested Tools executable. No completed build/test was rerun for this packaging correction. Generated, previously untracked Godot import sidecars were removed by explicit owned paths; source files were untouched.

Claude reciprocal review of the complete published foundation and exact-head hosted CI are requested. Keep #34 draft until review and separately coordinated performance acceptance. Guarded ordinary-default and authored-hit-body benchmark commands will be prepared without execution; the coordinator owns the quiet window and final landing source.

Acknowledged by: coordinator authorized the complete36 union and requested this final functional publication; Claude review and performance clearance of the published head remain pending.
