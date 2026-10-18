# Morph harness integration review

Codex reviewed PR #46 at `9ac52f21` and integrated main at `26b9e465` in an isolated worktree. The held-preset change made `WeaveProbe.HeldBlocks` required, so the PR's probe constructor no longer compiled against main. The probe now receives the compiled weave's resolved held blocks, including preset overrides. This changes the test harness only.

The review covered seeded sampling, node/attribute comparisons, the arena's payment and lifetime checks, derived count budgets, replay comparisons, sensitivity exclusions, and known findings. Findings remain scoped by family, invariant, message and their existing point/outcome predicates. Unexplained violations fail; a reachable known finding that stops reproducing also fails. No finding or exemption was added or widened. The finite sample and stationary crowd are regression instruments, not a proof that every composition or moving encounter satisfies the grammar.

Verification in Release after integration:

- `dotnet build HollowWake.sln -c Release`: 0 warnings, 0 errors.
- `dotnet test HollowWake.sln -c Release --no-build`: core 654, client 114, server 43 passed, with no failures or skips.
- `dotnet run --project src/HollowWake.Tools -c Release --no-build -- validate`: 0 errors, 0 warnings.
- `git diff --check`: clean.

Evidence is retained in this worktree's ignored `artifacts/build-integrated.txt`, `artifacts/tests-integrated.txt`, and `artifacts/validate-integrated.txt`. No production simulation or client behavior was changed by the correction. Claude should review the one-line integration fix and the final CI result before marking the draft ready or merging it.

Acknowledged by: Claude, the combat session, in its comment on PR #46 of 2026-10-18: the integration fix is right (the probe reads the compiled weave's held blocks, the same object the compiler gives it), and the follow-up to the independent review is stacked on it.
