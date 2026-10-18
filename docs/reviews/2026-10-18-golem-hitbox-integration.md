# Golem contact integration evidence

2026-10-18. Supplemental check requested through the owner's authorized PR coordination. The published golem content remains separate from shared runtime ownership.

## Exact combination

- Golem #39 correction: `17c14121b7d8adfd3021bfc1f423381e1a53c5cc`.
- Complete hitbox #36 ref: `724c5ed57d7a6082f9c485f66b8dd7001acbe2fb`, including its foundation and correction. The correction was not cherry-picked alone.
- Isolated merge: `6c698de05de5ba4a6db1c974e816e221885a4812` on `codex/golem-hitbox-validation`. Merge was clean; no shared runtime or release-policy edits were needed.

The fixture source is `experiments/golems/combined-tests/GolemProjectileContactTests.cs`, copied into the temporary core test project. Its README gives reproduction instructions. The separate location keeps a supplemental combined-runtime check distinct from PR #39's presently unintegrated base.

## Results

`experiments/golems/evidence/combined/` records:

- **342/342 actual cast/contact cases pass.** Incoming: fifteen golem/Amalgamation patterns, three reliefs (-0.5, 0, +0.5 m), seven forms/presets (Firebolt, Piercing Arrow, Glaive, Orb, Volley, Quiver and Drawn Shot), totaling 315. Outgoing: Fire/Ice/Lightning Golems' own bolt presets against Goblin Scavenger, Wolf and Skeleton Warrior over the same reliefs, totaling 27.
- Each case requires successful cast admission, a real projectile, positive damage to the intended target and its retained hit-ledger entry. Incoming target height/radius and all relief elevations are checked against authored content. No dimensions or attack rules were adjusted to pass.
- **83/83 existing golem tests pass** against this runtime: actual kits, seeded replay, owner allegiance, shared reservations/replacement, reward-free respawn, explicit resistances/leech, physical profiles, population references, stances and legacy Warden stats. The separate Bone Golem shared-slot regression also passes (**1/1**).
- Combined content validation: **0 errors, 0 warnings**. These commands compiled the relevant core/test/tool projects; this is not a new full-solution/client/performance claim.

## Limits and next integration gate

All fifteen golem/Amalgamation targets currently use #36's feet-to-top navigation-cylinder fallback. The matrix explicitly checks that fact. It does not claim per-limb authored damage anatomy, gaps between assembled limbs or matching animated meshes. The Goblin/Skeleton outgoing targets use #36's profiles; Wolf still uses its current fallback. Future golem profiles belong with entity content and must use the shared height-normalized volume contract, excluding decorative geometry.

The five live-site level regressions remain separate, awaiting the entity owner's corrected #34. Shared mass/shove and quest corrections are not present in this temporary combination. #20's chosen release/miss/rider policy is also not integrated here. #36's removed private gate was not restored. Its shared Warden Slam preset is unchanged and remains usable by other species.

Do not merge PR #39 on this evidence alone. Consume the corrected shared stack, rerun these matrices and all applicable gates, then request Claude rereview of that exact combined head. Full Bonewright hunt-and-graft behavior remains unimplemented by the owner's decision.

Acknowledged by: pending Claude combined-stack rereview.
