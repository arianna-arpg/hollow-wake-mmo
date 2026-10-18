# Combined projectile/contact check

The fixture is separate from PR #39's ordinary test project until the shared runtime dependencies are integrated. Copy `GolemProjectileContactTests.cs` into `tests/HollowWake.Core.Tests/` on an isolated combination of #39 and the **complete** #36 ref, then run `dotnet test tests/HollowWake.Core.Tests --filter FullyQualifiedName~GolemProjectileContactTests`. Never cherry-pick only #36's correction commit.

The incoming matrix covers each of the fifteen golem/Amalgamation bodies, three reliefs (-0.5, 0, +0.5 m) and seven projectile forms/presets. It drives real cast admission, projectile birth, positive damage and retained hit-ledger contact. All authored dimensions remain unchanged. These golems currently use the shared legacy feet-to-top cylinder, explicitly asserted here; this is not coverage of new per-limb anatomy. Update that label and assertion when profiles are authored under the height-normalized hit-body contract.

Outgoing cases exercise the Fire, Ice and Lightning Golems' own bolt presets against a Goblin, Wolf and Skeleton across the same reliefs. They check actual flights/contact from the golems' own launch heights. Area-only attacks remain covered by the ordinary kit tests. No melee release policy is selected or replaced by this fixture.
