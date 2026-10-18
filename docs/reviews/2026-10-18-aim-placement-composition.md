# Aim and placement composition: reviewed forward integration

Date: 2026-10-18. PR #36. Runtime source **5e12dbc28a6e073a3739df69e5a0a496e26252f0**. Previous verified checkpoint **8a15fd43d8e025bdc3ce770d48d51fb34f23b9dd**. Complete foundation **a3eafab043d38a3acf43d24f4c8f31c255668bd0** remains an ancestor.

## Owned sequence and review

The #20 owner implemented the correction once. The following reviewed scoped commits were applied with `-x` provenance, without merging the full #20 or downstream #34 branches backward:

| Owner commit | #36 commit | Scope |
|---|---|---|
| 70f3ace0e6dfbd04bfa09cf824b6d44aed7e9214 | 0a2b212e | Retained offset-body contact and frozen bearing |
| 783fe37cd32fd5c7a211ac2551008cd4cac6465a | 735e8b20 | Independent point-vector rotation under nested supports |
| 85ee039781b46025ca8631d5f23786636ab95f1a | 5e12dbc28a6e073a3739df69e5a0a496e26252f0 | Separate placement from sampled target aim, including delay and salvo paths |

The earlier owned elevation correction 8e3a5d1 remains applied as 8107599. One field-placement conflict in EffectContext was resolved by retaining every existing #36 property and adding SampledTargetPoint plus its reviewed copy assignment. The resulting diff only adds those four lines. The #20 observation pool and #50 CastSource field were not imported early; their eventual complete-source union must retain them alongside Aim, AimOffset, LaunchLift and SampledTargetPoint.

The [independent exact-source review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20#issuecomment-6255782966) cleared the three-commit sequence for this scope. Its 30 behavioral cases comprise the original 11 contact/placement cases and 19 further assertions covering straight/gravity/converge/guide flights, moved or despawned targets, delayed shots, three-shot salvos, live re-aiming and a range-clamped origin with a moving caster. The reviewer reported no remaining finding in this aiming correction. Those are the peer's independent probes, separate from this branch's integration gates and from Claude's final review.

Earlier failures remain recorded in the owner commits and the prior [elevation record](2026-10-18-aim-wrapper-elevation.md). The earlier 70f3ace and 783fe37 checkpoints were not treated as final acceptance after their subsequent findings.

## Resulting behavior

Effect placement Point, original Aim, frozen target center SampledTargetPoint, body-relative AimOffset, planar direction and launch lift have distinct roles. A bearing wrapper rotates the actual placement vector and sampled target vector independently around its origin, rotates XY body offset while retaining elevation, and releases the entity lock. It does not relocate an explicit point placement to a retained body. Zero turns preserve placement, nested turns compose, and opposite turns restore the trajectory.

Projectile bearing is resolved from the actual launch origin toward the live or frozen body aim. A committed targetless direction stays explicit. This preserves offset anatomy even for `origin:point`. Ordinary detached copies retain the frozen sample; explicit point/target changes clear it. New placement contexts clear it by default, while the aim wrapper explicitly passes its rotated sample. Live salvo re-aiming releases the old target and sample. Each salvo beat resolves its authored origin, including the limit sampled once at cast time.

No body dimensions, collision radius, clearance reach, content vocabulary, balance band or performance budget was changed to make a test pass. No skill/body-family/child-handler exception was introduced.

## Integration evidence

The [flight-correction evidence directory](../../experiments/entity-hitboxes/evidence/flight-correction/) contains this cycle under `placement-`. Raw logs and screenshots remain under the ignored artifacts directory; committed text normalizes line endings and trailing whitespace only. The source manifest lists exact commands and unchanged content/client tree hashes.

- Debug solution and Tools Release builds: zero warnings/errors.
- Full tests: **2360 pass**, comprising core 1900, server 59, native client 132 and browser 269. No .NET failures or skips. The focused aim/launch selection passes **93/93**: 39 aim/placement cases and the original 54 launch cases. All authored-body actual-cast tests remain in the full suite.
- Content validation: zero errors/warnings. Starter zone load completes 200 ticks. The literal census remains exactly 499 keys. All 777 lint finding keys match the previous checkpoint (580 smells, 197 notes).
- All seven Release Windows replay hashes remain identical to the committed baseline. The complete proving report is structurally identical to the previous aim-wrapper report, including all per-fight outcomes and hashes. Proving still exits 1 for the same three inherited win-rate ceiling breaches: Bulwark, Plaguer and Pitfighter each win 10/10 against 0.8. This is not a passing balance gate. No baseline or band was rewritten.
- The unchanged replay hashes do not prove the new aim metadata is unchanged. Actual contact, origin, trajectory and placement assertions supply the targeted behavior evidence, including the independent probes above.
- Private background run `run-qBp4Rq` passes browser 30/30 and native 36/36; cleanup errors are empty. The isometric screenshot shows the expected world. The card screenshot still shows the separately owned blank-world/tiny-viewport defect in this branch; its renderer and harness fixes are in downstream #34 and were not merged backward. Native rendered pixels remain untested, and the previously recorded shutdown diagnostics remain in the native log.
- Content, web and script trees remain identical to the prior checkpoint, so its production web build remains applicable. The earlier generated-protocol and six-feature probe evidence retain their recorded sources; they are not relabeled as fresh runs. The full server suite and private client smoke ran on this corrected runtime.

## Downstream evidence and handoff

The foundation owner independently validated the earlier 8a15 checkpoint in kin source **04853680073419fe0fea9dfd16d7a05535c0d04f**, with evidence commit **356155fed9d523447c24b27507aa9a55bd567d84**. Its `experiments/entity-review/evidence/37-combined-focused-after.txt` records **178/178**: 132 child-only Tide Orb/Frozen Orb cases across 22 original-size kin and three target terrain offsets, plus 46 world/population/anchor checks. The 132 pre-fix failures, initial golden failures, paired maps and reviewed replacements remain preserved in that owner's record. Parent-orb contacts cannot satisfy the child assertion. These kin use fallback cylinders; this is not a claim that they gained authored anatomical volumes.

That earlier downstream evidence supports the launch-origin correction; it is not a substitute for forwarding this final source through **#36 → #34 → #37**. The complete published ref is handed back for that progression. All local heavy processes were stopped after this cycle; no benchmark or performance trace ran. Coordinated performance verification, actual combined #20 acceptance, downstream renderer acceptance and native rendering remain separately scoped work.

Claude rereview is requested on the final published source and the context-field union. Hosted CI for that exact publication is reported on the PR; the prior green checkpoint is not relabeled as a new result.

Acknowledged by: pending Claude rereview of the final forward integration.
