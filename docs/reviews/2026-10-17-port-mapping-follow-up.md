# Port mapping review and integration follow-up

Codex reviewed PR #14 at `b4909b8` while the owner requested closure of the open pull requests. This record claims the mapping script, its regression tests and the ledger's mapping contract before editing them. The existing PR retains ownership; no other worktree is changed.

Two defects reproduced on a synthetic extension of the existing fixture, with the original six tests passing and two new checks failing:

- Distinct projectile shells emitted duplicate `missile` IDs because the collision counter read an unstored `stem` field. A spell with `castMove: 0.5` must have a distinct shell from an otherwise identical stationary spell.
- Damage proportions followed source object order while Thread names followed descending damage order. A spell with fire 1 and cold 9 emitted Tide 10% and Ember 90%. Shares must stay paired with their mapped types. A third type is folded proportionally into the two retained Threads, preserving total damage.

The fixes preserve the data-only runtime boundary. These scripts emit drafts for manual authoring; they never execute in the game. Main's repository map and required aggregate CI gate are preserved during conflict resolution.

Verification against main `6feec67`: `dotnet build HollowWake.sln -c Release --nologo -v quiet` passed with zero warnings/errors; `dotnet test HollowWake.sln -c Release --no-build --nologo -v quiet` passed 491 core, 86 client and 36 server tests; content validation reported zero errors/warnings; `node --test scripts/port/port-prototype.test.mjs` passed all eight checks. The added regressions failed on the reviewed base and pass with the fixes. No production content, simulation or wire behavior changed.

The unit anchors, role-based Umbra mapping and weapon-dice policy remain the ledger's documented authoring choices. This review checks the focused transformations and their regressions; it does not certify every generated catalog row. The existing hand-maintained ledger tables were not regenerated from an external snapshot. Authors must regenerate affected drafts before adopting them; authored content in later slices still needs its own proofs.

Claude: please check the focused generator fixes and the mapping contract; the original ledger's approvals did not review these fixes.

Acknowledged by: pending Claude review.
