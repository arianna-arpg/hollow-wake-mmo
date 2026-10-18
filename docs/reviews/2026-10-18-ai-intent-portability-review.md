# AI intent seam: portability and independent review

Codex reviewed PR #49 at `0dd5b104`, integrated main at `26b9e465`, and corrected the failing trajectory assertion and contract gaps from the steward review. The follow-up changes tests and documentation only; the planner's source change is its explanatory comment.

## Review

The AI dispatches synchronously through the simulation's intent registry, with actor-local refusals and explicit player-only registrations. The old movement cache moves to `Motion`; cache invalidation on orders, summon changes and leash teleports follows it. The navigation budget is chosen from the actor, not from intent flags. The same planner handles a client click and an AI destination, with bounded straight-segment sampling. The deferred construct casting exception and instantaneous facing are named in the contract. No additional blocking defect was found in the seam.

For the forecasting advisor, `MoveToIntent` with stop distance and kept-plan semantics provides the needed destination seam. Directional steering and jumping remain available through their existing handlers; this review does not introduce a second movement policy.

## Corrections

The original chase hashes included round-trip yaw from platform `Atan2`, and two scenarios could enter trigonometric wandering. They were valid Windows observations but not portable hash fixtures. `AiTrajectoryTests` now compares readable full-precision XYZ and path counts, without yaw. Aggro is explicitly held, prey cannot die, wandering is rejected, and the moving prey follows scripted axis-aligned directions. All three fixtures were generated from the pre-change simulation at `a810b0a2a9a588db8a3959f14f4ddb1d38147e3d`, then matched exactly against this PR. No rounding or tolerance masks a coordinate change in these targeted scenarios. A failure prints the first differing tick. The fixture README records provenance and reproduction; there is no update mode in the final tests.

Before this follow-up was pushed, Claude published `87ad953` with a separate correction that compares the original scenarios against recorded rows within 0.0001 units/radians, with exact path counts. The remote-head check caught the overlap before any push. The histories were merged without rewriting either, retaining Claude's wider original-scenario check alongside the exact non-wandering checks. The two tests measure distinct scopes.

The AI and movement contracts now agree about shared straight-walk planning, the cache on `Motion`, and the distinction between an exhausted partial click path and a renewed kept destination. They name the bounded distant-minion behavior: straight-walk samples are spaced 0.25 units apart and capped by the mover budget, so the shipped AI limit is 125 units before bounded search. The modding contract names the compiled-mod migration from `Behavior` cache fields to `Motion`. Seeded replay is described as platform/runtime scoped; these targeted chase fixtures do not promise cross-platform combat or world-generation determinism.

The historical chunk-count discrepancy was measured on the exact base: the distant click starts with 1 loaded chunk, adds 126, ends with 127 total, and expands 4,000 nodes. The original review record now distinguishes added from total chunks.

## Evidence

- Release build after main integration: 0 warnings, 0 errors.
- Final combined `dotnet test HollowWake.sln -c Release --no-build`: core 618, client 114, server 43 passed with no failures or skips. The combined Release build has 0 warnings and 0 errors.
- Content validation: 0 errors, 0 warnings.
- `load --x 0 --y 0`: 200 ticks, 2,672 entities and 272 monsters.
- Seven `fight` traces are byte-identical to the base under the same Windows runtime: `loosed_kindler boar`, `wolf hob`, `proving_reaver proving_bulwark`, `tide_channeler spark_duelist`, `loosed_skirmisher tide_caller` (30 seconds, 400 lines); `proving_pitfighter proving_mower --seed strikes` and `proving_fletcher proving_evoker --seed projectiles` (60 seconds).
- `git diff --check`: clean.

Raw build, test, validation, world and duel evidence is retained under the review worktrees' ignored `artifacts/` directories. The base has no production changes; only temporary test recorders were added to measure it. The steward's existing live browser/native, probe and performance evidence remains applicable to the unchanged production seam; this follow-up did not repeat those checks. Linux CI must confirm the replacement fixture assertion before merge. Claude review is requested for this Codex follow-up.

Acknowledged by: pending Claude review on PR #49.
