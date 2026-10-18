# Forecasting and body-contact integration

Status: integration in progress for PR #20. Not ready for merge, performance acceptance or final visual approval.

## Source and ownership

Reviewed main `17ca2f13a4c193d4b65d99ca4545aae267581092` was merged as `7aa78a4`. The complete fauna/body foundation `3d339d2ac71af6ac8bcff8e4f767e1f7c44ce2d8` brings #33 and full #36 `c07b6df`, rather than an isolated hitbox correction. Its prior evidence is preserved and does not certify this combined source. The original #20 replay fixture is deliberately retained until the outstanding flight defects are corrected and combined replay changes can be explained.

The #36 owner retains projectile launch, aiming and nested-origin corrections. This integration owns AreaHandler contact forecasting, shared cast observation, the wire and both forecast adapters. Coordination: [#36 comment](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6254933679), [#20 comment](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20#issuecomment-6254933860). #50 remains a separate dependent request with its own paid-device correction, review and performance evidence; retarget it before deleting #20's branch.

## Combined behavior

- Strike, Measured Strike and Bash author `contact: body` on the same compiled sector selector. Aim commitment, source reach, costs, selected-recipient riders and release policies remain shared and configurable. Supports change the compiled graph and attribute fold, not a skill-name renderer.
- Body candidate observation translates the existing anatomy to the supplied estimated position. This restores meaningful pre-commit movement estimation after the body-contact merge without changing live Transform, facing or collision reach. Actual release still uses the body's actual position.
- The wire adds optional world-space `bottom`/`top` and sends `target_test: body` for anatomical contact. Both clients require finite ordered bounds, draw both slab perimeters, retain the directional commitment cue and decline unsupported contact modes. Legacy center forecasts keep their height-tolerance presentation. An older forecast client declines body coverage rather than drawing a false center boundary.
- The incoming precise annular-sector predicate is retained, with zero central angular allowance. Combat-derived fixtures are regenerated; tiny-sector and hollow-boundary cases remain present even without that allowance. The generator excludes the separately built trace helper's source and output.
- Held release aim returns point, bearing and body aim offset as one pure sample. Observation cannot clear the live cast's offset. #54's original `Aim` remains separate from current delivery `Point`.
- Snapshot delta reuse requires the exact previous and current state objects. A viewer enabling hit-body diagnostics cannot receive another viewer's ordinary delta merely because they share history.

Coverage is still primary candidate geometry. Unsupported wrappers and dependent graphs retain timing with unavailable spatial coverage; this is not certification of all future travel, secondaries, prospective damage or every legacy spell's full presentation.

## First integration checks

`dotnet build HollowWake.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false` passed with zero warnings/errors. The first `dotnet test HollowWake.sln -c Release` run passed 2,414 core, 66 server and 147 client cases and failed five core cases. The failed output is retained, not relabeled:

- AI approach's old center-only fixture expected a body still touching the enlarged contact boundary to be ineligible. Its observed speed is now 1.5 units/s: with the fixture's one-second windup the farther body leaves contact, while closing by 0.6 units restores it. A first correction at 2 units/s also exceeded contact after closing; that failed focused run is retained.
- The release-policy validation fixture now registers its pattern before invoking all validators, as the incoming anatomy validator resolves inheritance through the registry. No production validator is bypassed.
- The point-anchored shove's empty-ground projectile and seeded delivery fight still fail on the inherited flight source. Claude identified default empty-ground dive and nested flight lift defects on #36; its owner is correcting these. Do not weaken those tests or claim their correction before integrating the reviewed source.
- The combined tournament baseline differs, expected to need explained regeneration after flight correction. No fixture hash is replaced at this stage.

The corrected focused core run passes all 57 approach, release-policy, cast-observation and hit-body cases. The first browser invocation found missing executables in the existing shared dependency junction; the junction itself was preserved under ignored artifacts and this worktree received its own pinned dependencies without modifying the shared target. Browser tests then passed 404 cases and the production build passed with its existing bundle-size warning. The subsequently restored tiny-sector fixtures expand that browser matrix and require the next adapter check.

New regressions cover translated anatomy and vertical misses without live movement, held-aim purity, diagnostic versus ordinary snapshot deltas, exact wire contact/slab fields, and both adapters' rejection of malformed slabs. The incoming species/contact matrix ran in the full core suite; that does not excuse the two flight failures.

Raw outputs stay under ignored `artifacts/forecast-body-integration`; sanitized first build/test logs are under `experiments/forecast-rendering/evidence/body-integration`. No new timing or trace run occurred. Earlier default-budget misses, diagnostic samples and their uncertainty remain intact. Full functional/client/probe/replay gates and one separately coordinated diagnostic capture remain pending the corrected combined source. Native pixels still require an isolated graphical session.

## Client and viewer follow-up

On runtime source `0eac5dc`, the restored tiny-sector matrix passes all 422 browser cases. The content pack validates with zero errors/warnings. Seven targeted server/history cases pass, including a live loopback viewer whose center is outside the sector while its anatomy overlaps: wire eligibility is candidate, then becomes not-candidate after the viewer moves wholly above the same slab. The server sends unchanged bounds in both cases.

`node scripts/test-background.mjs --suite all --calling holdfast --forecasts --out artifacts/forecast-body-integration/melee-client` passes 32 browser and 37 native behavior checks, returns to character selection and reports no harness cleanup errors. The browser screenshot `forecast_primary_candidate.png` was inspected: it shows the two slab perimeters and the actual Cleave progress bar. Crowded labels and foliage remain readability limits; it is not final production art. The screenshot remains local with its hash recorded in `melee-summary.json`.

Native headless mode does not validate pixels. Its shutdown log includes six dummy textures, two shaped-text objects, one font, one CanvasItem and 29 ObjectDB instances reported as leaked. The inherited #34 log already contains the same warning classes with different counts; this comparison does not prove their cause or clear them. Both complete logs and the passing functional summary remain visible. No trace or new budget measurement was run.

A separate code-level compatibility finding was posted on [#50](https://github.com/arianna-arpg/hollow-wake-mmo/pull/50#issuecomment-6255062880): an inheriting device's execution uses owner stats from the device origin, while its unchanged forecast still samples device stats. That owner must reconcile the shared pure effect-context sample and test differing geometry stats before claiming construct forecast parity. #20 does not independently edit its owned Run/miss/replay paths. The forecasting chat is also reviewing this checkpoint independently.

## Area support inheritance correction

The forecasting peer found that active Ring and Wide supports did not expand the range-derived Strike, Measured Strike and Bash sectors. A generic `area.radius_base` parameter now supplies an external base to the existing `area.radius` fold, including caster and weave modifiers and catalog bounds. Those selectors author `radius_base: weave.range`. Source edge and melee reach remain later contributions. Existing `radius` expressions keep resolved semantics, and authoring both doors is rejected. Dependency preflight checks the external expression and modifier stack without sampling RNG.

The support regression verifies forecast eligibility and actual damage on a body between the original and enlarged boundaries, for each affected form, both supports and with/without a range modifier. Further cases cover flat/link/increased/more/override layers, bounds, resolved-radius compatibility and unsafe dependencies. The combined support, approach and grammar selection passes 102 cases; content validation reports 0 errors and 0 warnings.

An attempted expansion of this matrix to Beam failed four dormant-support checks. Inspection confirmed the existing mechanism contract deliberately excludes range-based lines from the area mechanism. That exploratory content change was removed; Beam retains its authored range and existing support compatibility. The failed log is retained as `support-line-policy-first-tests.txt`. No mechanism gate was weakened to force a support onto an ineligible delivery.

## Construct and paired renderer integration

Complete #50 `738bad67f3c1490b1f2b241dc970d55606d6d24d`, including payload-origin correction `d796f04`, is merged at `c0e275c`. The single conflict retains physical `CastSource`, shared `PayloadOrigin` and the pure sampled `AimOffset`. Original `Aim` remains independent. The combined construct, cast-forecast, positional-release and support selection passes 114 cases. A fresh full Release build is clean; full tests retain 2,474 core passes and the same three outstanding flight/replay failures, with all 66 server and 147 client cases passing. No failure was hidden or removed. The pre-flight standard tournament report is retained locally for the later behavior comparison.

The paired renderer contribution #55 `d15b3b72e69d1ff728adc2c9a3c9d49be9511429` is merged at `2c18e07`. Inspection confirms the live adapters call the tested segment builders and mesh-reuse predicates. Both retain the authoritative frame, lower/upper loops, holes and direction cue; translation and heading reuse local geometry. On this combined source, all 184 native client and 459 browser cases pass, including 37 paired geometry/reuse cases per client. The browser production build passes with its existing bundle advisory. These are engine-free geometry checks, not native pixel certification.

The prototype-port suite passes 10 cases. The visual-convergence inventory reports no unclassified vocabulary across 948 recorded legacy skills and 457 supports, while still explicitly reporting partial and pending presentation families. Classification is not completion of those families. No new trace or performance sample ran during these integrations. The final complete #36 foundation handoff, combined replay reconciliation, fresh client/probe checks and performance acceptance remain open.

## Final flight merge and aim-support correction

Complete #36 source46997d23fd4d73aba89ade15ca7669038f9420f9 is merged at4a95efe. The incoming original Aim, sampled AimOffset and LaunchLift coexist with #50 CastSource and PayloadOrigin. The point-anchored shove regression now passes. World-generation golden reconciliation waits for the complete fauna foundation; no existing combined fauna hash was replaced with an unrelated baseline.

The combined review found a generic aim-wrapper defect: clearing its target also discarded body elevation. Six focused cases failed before correction; expanded actual-contact cases independently reproduce the same defect on standalone #36. Isolated correction8e3a5d1 samples the retained body's offset before releasing it, rotates XY with the bearing and keeps Z. EffectHelpers.At takes that offset explicitly, leaving new placements' reset behavior unchanged. Thirteen new cases cover retained/sampled targets, actual contact above a terrain step, noncentral body offsets, airborne launch anchors and empty-ground horizontal control. The combined aim, launch, forecast, construct, support and grammar selection passes226 cases. The fix is published for forward adoption through #36 and #34.

The remaining seeded-delivery fixture exposed a changed assumption rather than a return-system regression: short authored boar anatomy makes the piercing glaive aim down, strike and then terminate on the ground before the outward leg ends. The full failing trace remains retained. This generic mechanism test now uses the existing stable, upright t_delivery_body fixture with rarity disabled. Its production skirmisher still casts the real seeking, splitting and returning weaves; fork, turn, return, intervening return hit and exact repeated seeded trace assertions all remain, and the test passes. Authored species remain covered by the body matrix and tournaments. Failure output now includes the complete trace for future diagnosis.

Fresh complete combined gates and performance acceptance are still pending. No benchmark or trace ran during this correction.

Acknowledged by: pending Claude review of the combined correction.
