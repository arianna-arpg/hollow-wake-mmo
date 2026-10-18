# Elemental integration of the final reviewed foundation

PR #40 consumes the exact complete #34 `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`, including runtime `26431c48`, reviewed #36 `01b2884`, #33 `a3eafab`, main `17ca2f1` and the complete main `fa7f5f0` / PR #56 test-only lifecycle correction. No duplicate cherry-pick or competing runtime implementation is added. The existing shared renderer and moving-villager harness corrections are retained.

Merge conflicts are limited to the population contract and branch-specific world goldens. The reviewed population contract is accepted, preserving pattern-level defaults and explicit site-level opt-ins, including the shipped Knot. The elemental golden file initially retains this branch's prior values so the first comparison can expose changes for audit; foundation golden constants are not copied. Elemental body dimensions, content, site-level opt-ins and AI/anatomy boundaries remain unchanged.

The combined runtime is `3f414d7da5f65212d6272a6bab042141f4127adb`. Evidence uses the `final-foundation-` prefix under `experiments/elementals/evidence/review-fixes/`. Old evidence at `ce4c992` remains tied to that source.

## Own world audit

The eight-zone audit compares `main-levels-after.json` from `ce4c992` with `final-foundation-levels-after.json`. All site identities, site levels and per-pattern spawner/body counts are unchanged. All four terrain maps and POI lines are identical. Only Knot resident levels change: cinder imp, remnant shade and tangle now explicitly use their own site's level instead of pattern levels 4, 2 and 6. Other residents retain their prior levels, including level-5 starting guards/quartermasters and the elemental opt-ins. The full per-site changes are in `final-foundation-world-review.txt` and `final-foundation-level-diff.json`.

The starter offers are now all level 1: Clear Knot reaches `poi.0_0.knot.200.60` and kills Cinder Imp; Gather Floss and Cull Hob retain their targets; Scout Ruin moves from `poi.0_1.ruin.213.450` to `poi.1_1.ruin.305.334`. The Knot's three resident types are level 3, within the shipped two-level quest window. The other seven zones still have no generated offers. The original missing offer is therefore resolved on this branch without changing ordinary residents to suit the quest.

The initial unchanged-golden run records four failures and three passes. After auditing the intended level and offer changes, only the origin info hash, three affected chunk hashes and overlaid-world hash are refreshed from this branch's generated output. The other three info hashes, `(1,0)` chunks and climate hash remain unchanged. The origin and overlay values differ from the foundation's constants; no foundation golden file was copied. Full functional gates are in progress.

No benchmark is authorized or run. The previous snapshot miss (1.222392 ms/player against 1 ms), original process-wide expression allocation counter (0.0168 bytes/eval), and inherited proving-pair balance breaches remain open. Shared #20/#50 combined acceptance belongs to the coordinator. This PR remains draft; no merge into main is authorized.

Acknowledged by: pending Claude rereview.
