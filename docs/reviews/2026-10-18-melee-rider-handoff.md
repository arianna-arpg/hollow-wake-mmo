# Melee rider and repeated-miss handoff, 2026-10-18

Local slice for the shared forecast coordinator, based on 3ca56ebbb981b56c536c0156d4a29a697012ef6e. No icon, protocol, client, release-policy or AI changes are included. The coordinator owns integration and PR #20.

Rending, Felling and Furious now use the registered damage handler's after_damage hook. It runs once per positive damage packet on the packet's actual target, after the existing on_kill list, at the payload's current depth. Its variables start with that packet's last_damage and amount in a separate dictionary; child writes do not overwrite the parent's result. This is a per-hit policy: multiple underlying damage packets can each run riders. Zero damage, a missing/dead target and an empty selector run none. Furious needs no extra last_damage condition because the hook guarantees positive damage. Felling cannot erase that guarantee by writing zero into last_damage. Added hook payloads are not recursively hooked. Kill hooks retain their existing depth and ordering. Reaping was already on_kill and stays there.

The damage-stage hook covers both selector payloads and direct root damage, including Eviscerate. A delivery-only on_hit migration silently dropped riders on that direct-damage preset; the final patch avoids that regression without naming a form or preset in engine code. The shared delivery compiler still owns the safety of admitted root payloads.

Only the primary area placement reports report_miss. Echoes and subsequent cascade steps still execute their payloads and retain their existing depth.

Evidence under experiments/melee-rider-review/evidence:
- dotnet build HollowWake.sln: zero warnings and errors.
- dotnet test HollowWake.sln --no-build: 688 core, 45 server, 127 client; 860 passed.
- validate: zero errors and warnings.
- load --x 0 --y 0: zone loaded and ran 200 ticks.
- fight loosed_kindler hob: actual damage and kill trace.
- Release bench: retained full output. This is the older forecast base; its forecast allocation is not evidence for the coordinator's newer allocation fix.

The dedicated regressions cover whiffs, a different selected body, movement during windup, Eviscerate, Reaping, repeated placements, payload depth, kill ordering, zero/dead/missing targets, per-hit firing and nonrecursive hook payloads.

Integration asks:
- Cherry-pick the local commit, then run the coordinator's combined gates and paired bench.
- Keep the coordinator's AreaHandler.Effect IReleaseTargetSelector/ChecksLineOfSight edit; this slice deliberately omits it because the interface is absent from its base.
- Add after_damage and the per-hit semantics above to docs/design/weaves-and-effects.md in the coordinator-owned contract change.
- Targeting, reach and approach decisions are unchanged by this slice.

Acknowledged by: forecast coordinator; cherry-picked 47de147 as 8ba3f27 on codex/shared-cast-forecasts-integration. Combined gates and the owner-selected approach policy are recorded in 2026-10-18-shared-cast-forecast-rereview.md.
