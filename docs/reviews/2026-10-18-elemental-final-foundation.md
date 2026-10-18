# Elemental integration of the final reviewed foundation

PR #40 consumes the exact complete #34 `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`, including runtime `26431c48`, reviewed #36 `01b2884`, #33 `a3eafab`, main `17ca2f1` and the complete main `fa7f5f0` / PR #56 test-only lifecycle correction. No duplicate cherry-pick or competing runtime implementation is added. The existing shared renderer and moving-villager harness corrections are retained.

Merge conflicts are limited to the population contract and branch-specific world goldens. The reviewed population contract is accepted, preserving pattern-level defaults and explicit site-level opt-ins, including the shipped Knot. The elemental golden file initially retains this branch's prior values so the first comparison can expose changes for audit; foundation golden constants are not copied. Elemental body dimensions, content, site-level opt-ins and AI/anatomy boundaries remain unchanged.

Functional gates and the eight-zone world/quest audit are pending on this exact combination. Evidence will use the `final-foundation-` prefix under `experiments/elementals/evidence/review-fixes/`. Old evidence at `ce4c992` remains tied to that source. In particular, the missing Clear Knot offer is not declared resolved for this branch until its own generated targets and effective resident levels are inspected.

No benchmark is authorized or run. The previous snapshot miss (1.222392 ms/player against 1 ms), original process-wide expression allocation counter (0.0168 bytes/eval), and inherited proving-pair balance breaches remain open. Shared #20/#50 combined acceptance belongs to the coordinator. This PR remains draft; no merge into main is authorized.

Acknowledged by: pending Claude rereview.
