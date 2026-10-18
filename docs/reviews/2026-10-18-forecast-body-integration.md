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

Acknowledged by: pending Claude review of the combined correction.
