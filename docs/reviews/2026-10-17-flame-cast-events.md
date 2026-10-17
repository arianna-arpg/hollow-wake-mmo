# Flame cast events: implementation and peer-review handoff

Codex implemented the owner-requested confirmed cast lifecycle and first Flame presentation slice in both clients, based on main `07a2497`. Work is isolated in branch `codex/flame-cast-cues`, in the registered `flame-cast-cues/MMOARPG` worktree. The original integration checkout is clean after the owner's explicit approval of the verified transfer. The loader fix is its own commit/branch (`44b8a54`, `codex/content-loader-validation`), and the earlier visual/sprite proposals are a separate commit/branch (`4c68391`, `codex/visual-studies`). Review these in order; the feature is based on both.

Codex has read and acknowledges [Claude's first review](2026-10-17-claude-acknowledgment-and-review.md) and the new collaboration contract. Claude approved the loader and reviewed the proposed direction; Claude has not yet reviewed this completed feature. The owner explicitly selected **Flame** in the current request, resolving the naming question in that first review.

## Scope and seam for Claude

- `CastPresentationEvent` in Core: stable zone-local cast id; start, release, impact, end and interrupt semantics; exact simulation timestamps. Release means a non-empty effect list is executing, never that it hit. Impact requires confirmed positive, non-evaded, non-blocked damage. A held channel can release multiple payloads under one cast id.
- `CastCueMsg` in Protocol with reliable opt-in `cast_cues` negotiation. ZoneInstance sends only to viewers who know the source, and for impacts the target too. Unknown targets are redacted. Entity spawn reconstructs ongoing preparation via `sync`, not old flourishes.
- Both clients consume a bounded/deduplicated timeline and the same `content/core/ui/cast-visuals.yaml` profiles, registered as `ui_cast_visuals` through the interface family. Profiles select by tags and author geometry, stroke width, phase durations, color, height, motion and opacity. Crossed ribbons preserve stroke thickness in both camera projections with one mesh per cue. Neutral fallback is mandatory; reduced motion removes decorative spin, expansion and fading. These are cosmetic cues, never hit regions or cooldown authority.
- Player-facing `Ember` becomes `Flame`; the stable `ember` ids, tags, attribute paths and save references remain compatible. The Thread description explicitly associates Flame with Fire. No internal-key migration is attempted.

Current file ownership includes core cast state/context/runtime and damage event emission; protocol messages; server negotiation/snapshot/event dispatch; client Main/app wiring; new cast timeline/render files; content visual definition/validator/recipes; display wording in content and lore; associated tests and design contract. Please review through this handoff before changing these same files. The core gameplay rules, timing and hit geometry are not being redesigned in this slice.

## Separate forecasting chat

The user-requested forecasting chat `01a14bbb-0128-7a93-850c-bee1d1dfea41` completed an initial `docs/design/cast-forecasting.md` and `experiments/cast-forecasting/`. The coordinator reviewed that contract and reran its proof against the completed feature: 27 real-core scenarios and 10 proposed-contract checks passed. Cleave preserves its existing committed direction and live resolution; moving out and interruption deal no damage, and paired armor reduces actual damage. The next proposed runtime slice is one shared resolved area plan feeding both the hit test and both clients. No production forecast hitbox, damage estimate, bridge eligibility or AI avoidance is claimed by this cosmetic slice.

Publication-time coordination: that chat subsequently published the expanded shared-provider proposal in [PR #3](https://github.com/arianna-arpg/hollow-wake-mmo/pull/3), on `codex/cast-forecasting` at `c99bfec`. **PR #3 owns the current forecasting proposal and supersedes the initial copies preserved in this branch.** During integration, take its versions of the forecasting design and experiment; do not overwrite them with these older copies. Its reported 63 scenario/provider checks and 10 contract checks ran on its own baseline, without this branch's lifecycle extension. The 27/10 rerun above remains the evidence for the initial proof against this implementation; it does not certify the expanded proposal against this branch.

Codex also read Claude's `docs/reviews/2026-10-17-positional-hits.md` on integration main at `0604d90`. The direct-target release-time reach/visibility gap is real and separate from cosmetic impact truth: a cue reports the damage the current simulation confirmed, while that simulation still needs positional-hit rules for direct-target roots. This pull request does not implement release revalidation, melee sectors, miss reasons or AI avoidance. Claude's acceptance list is acknowledged as the runtime follow-up; this record does not claim that work is already implemented. Existing Cleave direction is committed for the proof above; comparison with the original prototype's melee facing remains unverified.

## Verification evidence

- `dotnet build HollowWake.sln --no-restore`: zero warnings/errors. `dotnet test HollowWake.sln --no-build`: 446 core, 85 client and 38 server tests passed. The latter includes real opt-in lifecycle flow, ongoing-cast reconstruction, hidden-target redaction and a hidden-source exclusion.
- `npm --prefix web test`: 142 tests passed. `npm --prefix web run build`: passed. Generated protocol contract tests pass. Renderer regression checks world-space contact anchoring, lethal contact visibility, reduced motion, depth testing and disposal.
- `hollowwake validate`: zero errors/warnings. `load --x 0 --y 0`: generated the start zone and ran 200 ticks. `fight loosed_kindler boar --seconds 20`: completed; the saved trace is byte-identical to the loader-only baseline.
- Live TCP probe on the isolated local server: `PROBE OK`. Browser smoke with `--calling kindler --flame`: 26/26 passed, no page errors. Godot smoke with the same optional arguments: 23/23 passed, empty error log. Both recorded start, release, recovery and later confirmed impact from ordinary player input. The final captures use configurable ribbon strokes.
- An initial Godot cast legitimately missed, so the initial impact assertion failed. The smoke now makes at most three real attempts without inventing a hit. One browser attempt raced server startup and got connection refused; the final harness waited for HTTP readiness before running. These setup/encounter failures are retained in local logs, not counted as passes.
- `node art/visual-language/review-study.mjs`: 49 standalone artifact checks passed after capture relocation. The older sprite study retains its measured evidence and its explicit low-end/GPU measurement limitations.

Portable evidence is under `experiments/flame-casting/evidence/`: final engine logs, baseline/feature benchmark JSON and the identical duel traces. Larger captures remain local under `artifacts/flame-review/godot-ribbons/` and `web/artifacts/flame-review/web-final/`; they were inspected. The captures show intentionally depth-tested decoration in the existing placeholder world. They do not certify crowded readability, future assets, minimum-device performance or final art quality.

## Measured cost and review asks

Release `bench --only tick --json`, default 300-monster/4-player/600-tick workload, run sequentially on this machine:

| Measure | Loader-only baseline | Feature | Repository budget |
|---|---:|---:|---:|
| Zone tick p95 | 8.82 ms | 9.23 ms | 25 ms |
| Zone allocation | 67.49 KB/tick | 74.20 KB/tick | 2 MB/tick |
| Casting system | 0.658 ms/tick | 0.762 ms/tick | No separate budget |
| Casting allocation | 31.5 KB/tick | 36.1 KB/tick | No separate budget |
| Snapshot build | 0.75 ms/player | 0.66 ms/player | 1 ms/player |
| Full/delta snapshot payload | 26.97 / 8.06 KB | 26.97 / 8.06 KB | 30 / 10 KB |

The first review's request for unchanged active-casting allocation is **not met**: lifecycle records add measured allocation on transitions/contacts. No event is emitted every tick merely to animate preparation. All measured repository budgets remain satisfied, and the duel is unchanged. Please explicitly assess this bounded event cost; the timing difference is an observation from one paired run, not a statistical regression claim. Snapshot byte figures do not include the new opt-in event stream.

Please review event ordering, originating cast identity through `EffectHelpers.At`/context copies, end-versus-hit semantics, reliable feature negotiation, interest/reset behavior, and client content parity. New regressions caught and corrected the initially missing projectile-context identity and guard raise ordering. Broader review of the earlier integration and the full seeded player journey requested in Claude's record remain separate follow-ups; they are not represented as completed here.

Publication status: the owner explicitly authorized all three pushes and review requests to `arianna-arpg/hollow-wake-mmo`. The prepared branches were published and these requests opened, in dependency order:

1. [PR #4: content-loader validation](https://github.com/arianna-arpg/hollow-wake-mmo/pull/4), `codex/content-loader-validation` into `main`, prepared commit `44b8a54`.
2. [PR #5: visual studies](https://github.com/arianna-arpg/hollow-wake-mmo/pull/5), `codex/visual-studies` into `codex/content-loader-validation`, prepared commit `4c68391`.
3. [PR #6: confirmed cast events and Flame cues](https://github.com/arianna-arpg/hollow-wake-mmo/pull/6), `codex/flame-cast-cues` into `codex/visual-studies`, prepared implementation commit `6feb2c6`, followed by this publication/coordination record.

Each requests Claude review. Retarget the next request when its predecessor is integrated so the remaining diff stays focused. No merge was performed. The earlier automatic approval rejection was resolved by the owner's explicit authorization; it is no longer a publication blocker.

## Owner-requested architecture audit

The owner asked whether Flame introduced a bespoke feature or shared infrastructure. Inspection of `CastPresentation`, both `CastCueTimeline` consumers and both `CastVisuals` renderers confirms no dispatch on Flame, Cleave, a weave key, or an actor role. The runtime supplies common cast facts, tag-selected client content supplies appearance, and neutral profiles supply unsupported-tag fallback. Flame is one content recipe. This is a reusable cast-cue layer, not a completed game-wide presentation system.

The limits now appear explicitly in `docs/design/cast-presentation.md`: one selected profile per phase, a ribbon primitive, and no presentation vocabulary for healing, defensive outcomes, persistent statuses or world interactions. Independent layers for forms, Threads and glyphs are not implemented. Extending coverage must add shared mechanisms and conformance cases, not per-skill branches or a bespoke profile for every possible combination.

Added `tests/fixtures/cast-visuals/` with a separate content layer and ten selection cases consumed by both client test suites. These verify new content-only looks, misleading Flame names/keys, hierarchical and compound tags, deterministic precedence, phase fallback, unknown vocabulary, and removal of the Flame recipe. The .NET suite validates the fixture through the real content loader. This proves profile selection and recipe reuse, not rendered parity for every future effect family.

Audit-update verification: solution build clean (zero warnings/errors); 446 core, 86 client and 38 server tests passed; 152 browser tests passed; browser build passed; content validation zero errors/warnings. The change adds only tests, fixtures and documentation, so the earlier smoke/benchmark evidence still describes the unchanged runtime; it was not rerun or newly certified here. A TypeScript inference issue in the imported heterogeneous JSON fixture was corrected before the successful browser build.

Claude's review at `c07113b` has since requested changes before merge: reconcile the older forecasting snapshot with merged PR #3; eliminate cue allocation with no listeners and measure the subscribed cost; confirm naming authorization; and strengthen reset/interruption coverage. Those runtime/integration requests are not represented as fixed by this architecture audit. Naming is already directly authorized by the owner's instruction: "But let's instead just use \"Flame\" rather than \"Ember\", as we want to standardize or normalize nomenclature for consistency's sake". No additional naming approval is needed from this chat. The published branch must retain its history under the collaboration contract; integration should avoid an unapproved rewrite of fetched commits.

Acknowledged by: Codex, for Claude's collaboration contract, initial review and the changes requested at `c07113b`. Claude: completed-feature review received; requested changes remain before merge.
