# Aim wrapper elevation follow-up

Date: 2026-10-18. PR #36. Supersedes the aim-rotation acceptance blocker raised after the green 46997d2 checkpoint; the original launch corrections and their evidence remain in place.

## Source and ownership

The #20 owner published the scoped correction **8e3a5d1be04606995cc260ff0ff68e077a11b6cb** with the helper, wrapper, regression tests, contract and failure evidence. After review, that exact commit was applied with provenance as **8107599a73856a37d069e33c0297179a3f03cdd4**. No competing implementation or full #20 backward merge was made.

The final complete-source merge is **a24936a805296a8fd18d2ce6f07e74cc60a78e3d**, adding complete #33 **a3eafab043d38a3acf43d24f4c8f31c255668bd0**. That last follow-up changes only documentation, one anchored-radius test row and evidence. Its src, content, web and scripts trees exactly match the prior #33; the final combined runtime also exactly matches tested 8107599. All prior foundation history is retained.

## Defect, correction and independent evidence

The bearing wrapper cleared a retained target without sampling its body elevation, and EffectHelpers.At did not carry an existing AimOffset. A supported upward shot therefore became horizontal. The owner's expanded 13-case fixture was copied unchanged temporarily onto standalone **46997d23fd4d73aba89ade15ca7669038f9420f9**: **10 failed and 3 controls passed**. The six original cases expected vertical velocity 3.75 but observed zero. Two offset/anchor cases and two actual elevated-step contact cases also failed. The source hash and complete failing output are retained under the `aim-wrapper-before-` evidence prefix; the temporary fixture was removed after that run.

The wrapper now samples the retained body's aim offset before releasing its lock, or uses the already sampled offset. It rotates the XY part with the bearing and preserves Z, the inherited From anchor and LaunchLift. EffectHelpers.At accepts that offset explicitly; unrelated new placements keep their existing default. Original Aim and source/ownership/cast identity behavior stay separate. The #20 version retains its CastSource assignment; #36 does not acquire that independent #50 field early, and the eventual full-source union must preserve it.

All **13 aim regressions plus 54 launch regressions pass (67/67)**. Coverage includes retained and sampled elevation at zero and positive/negative turns, actual contacts above a terrain step, nonzero XY body offsets, inherited elevated launch anchors, and empty-ground horizontal controls. No dimension, contact radius, range, balance band, test exception or performance budget was relaxed.

## Verification and remaining acceptance

Evidence is in [flight-correction](../../experiments/entity-hitboxes/evidence/flight-correction/) with the `aim-wrapper-` prefix. Committed logs normalize line endings and trailing whitespace only; raw output remains in the ignored artifacts directory.

- On corrected runtime 8107599, Debug solution and Tools Release builds pass with zero warnings/errors. Full .NET tests pass: core 1873, server 59, native 132 (2064 total, no failures/skips), including all authored-body and launch cases.
- After the final docs/test-only #33 merge, the affected population and anchored quest suite passes **36/36**, including the new reach-40 case where the resident is outside the giver's reach but inside the selected site's search. Unrelated gates were not restarted for that unchanged runtime.
- Content validation reports zero errors/warnings; starter load completes 200 ticks. The literal census matches 499 keys, and all 777 lint finding keys match the prior correction (580 smells, 197 notes).
- All seven Release Windows replay hashes are identical to the committed baseline, without rewriting it. The complete proving report is structurally identical to the prior final33 report, including all outcomes and hashes. Proving still exits 1 for the same three inherited win-rate ceiling failures; it is not a passing balance gate.
- Corrected-runtime background run `run-jmGKBJ` passes browser 30/30 and native 36/36, with cleanup errors empty. Native pixels are skipped and the previously recorded shutdown diagnostics remain. Card screenshot inspection still shows the known blank-world/tiny-viewport issue in this branch; the foundation owner has the separate renderer and harness fixes in #34, which are not merged backward here. Raw screenshots remain with the smoke artifacts.
- Web source and content did not change in this follow-up. The 269 browser unit tests and production build from the preceding 46997d2 source remain applicable; they are not relabeled as a new run. Likewise, earlier protocol and six-feature probe evidence retains its recorded source.

The complete correction is ready to propagate **#36 → #34 → #37**. Claude rereview is requested for the owned patch and final foundation union. Actual combined #20 release/contact/forecast acceptance, the final downstream renderer check, native rendering and coordinated performance verification remain pending. No new performance sample was taken. Hosted CI for the newly published head is reported on the PR separately from the prior green head.

Acknowledged by: pending Claude rereview of the final correction.
