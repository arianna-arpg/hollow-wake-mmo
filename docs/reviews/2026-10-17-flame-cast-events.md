# Shared action presentation and ARPG convergence: review handoff

PR [#6](https://github.com/arianna-arpg/hollow-wake-mmo/pull/6), branch `codex/flame-cast-cues`, owns the common action-presentation stream and recipes. The owner required that Flame remain an example and that the original Hollow Wake ARPG be treated as eventual convergence scope. Work is isolated in the registered worktree; the shared integration checkout remains clean.

## What changed

The runtime assigns stable cast identity, emits confirmed start/release/impact/end/interruption events, and preserves identity through delayed work and actual projectile contact after interruption. Both clients consume a reliable opt-in stream with sequence, expiry and interest handling. No visual recipe controls simulation.

The extension composes one winning profile per authored layer instead of choosing one look for an entire action. Shared Thread templates instantiate from current or content-added Thread definitions. Flame overrides one Thread layer. Compiled mechanism capabilities can contribute body-sized accents; they do not assert that conditional paths executed or describe hit bounds. Segments, rings/arcs, rays and corners use paired registries and shared numeric fixtures. Budgets bound active layers and layers per event.

An open server event-adapter registry adds distinct confirmed damage, healing, block, evade, status application/expiry and revival outcomes. These do not invent cast IDs. Beneficial status application differs from harmful application; correlated damage is not decorated twice; source/target interest checks apply. Outcomes anchor to the affected body, and content can retain lethal-contact/revival decoration while a dead snapshot is present.

The read-only original-project census captures 948 skills, 457 supports, 18 delivery discriminants, 52 effect discriminants and 9 cast-mode discriminants. Source revision is `118b510f8b5bfb781163c5bc13ea4908f698e75b`; the before/after-stable TypeScript tree fingerprint is `6b9e8dab28f65957d0ff39231584fff813917d513b7cc52899db8a1446629275`. Individual source hashes and original casting/combat/defense profile data are retained. Its eight effect voices are only those registered by the base module, not an exhaustive painter inventory.

The coverage manifest groups these into 12 reusable families, explicitly distinguishing implemented, partial and pending work. Tests reject unclassified refreshed vocabulary, skill/support fields and nested variant paths, unclassified current effect handlers, and mismatched client primitive registries. All 79 shipped presets have valid shared lifecycle presentation. New skills using existing mechanisms inherit that infrastructure without renderer branches.

## Claude review requests and coordination

- Integrated reviewed main with merge commit `5e82195`, preserving published history. The obsolete forecasting files were replaced by the merged PR #3 versions; its current proposal is preserved. The only new probe adjustment explicitly enables lifecycle observation.
- `EmitCastPresentation` defaults false; zone Add/Remove tracks negotiated observers. Identity bookkeeping survives late observers, but lifecycle/impact records and mode-string allocations do not occur without them. Mode strings are cached.
- Added real channel-projectile interruption coverage and repeated client watermark reset coverage. A released projectile still hits with the same identity after interruption.
- Naming is directly authorized in this chat: “But let's instead just use \"Flame\" rather than \"Ember\", as we want to standardize or normalize nomenclature for consistency's sake”. Stable internal `ember` references are unchanged. Main's newer `b910e94` review note asserts a contrary owner decision (“Ember stays”). That inconsistency is recorded in [the PR thread](https://github.com/arianna-arpg/hollow-wake-mmo/pull/6#issuecomment-6242621216); this branch follows the direct instruction here and does not edit Claude's record to claim agreement.
- Claude's [PR #9](https://github.com/arianna-arpg/hollow-wake-mmo/pull/9) owns appearance, world materials, body animation and camera feel. [Coordination posted](https://github.com/arianna-arpg/hollow-wake-mmo/pull/9#issuecomment-6242580606): preserve both initialization paths in Main/app, and rerun combined checks when integrated. This is not an approval or a combined runtime verification of #9.
- Loader PR #4 and the visual studies (recreated as #7 after #5 closed) are merged. This request targets main. No feature merge or history rewrite is performed here.

## Verification and evidence

- Release solution build: zero warnings/errors. Release solution tests: **454 core, 90 client, 38 server** passed.
- Browser: **168 tests** passed; production build passed, including generated protocol checks.
- Content validation: zero errors/warnings. Headless start zone: 200 ticks completed.
- The seeded `fight loosed_kindler boar --seconds 20` trace remains byte-identical to the recorded loader baseline; SHA256 `9816503a03c8629c8cc12e6fbb09cd2c615f767662ae53cf39eeec826c26f8ce`.
- Forecast probe: **63** real-core scenario/provider checks plus **10** proposed-contract checks passed with lifecycle observation enabled.
- Source census refresh and standalone coverage audit: no unclassified captured vocabulary. This proves classification, not that pending families are ported.
- Final live TCP probe: **PROBE OK**. Fresh-save browser smoke **26/26**, no page errors; Godot smoke **23/23**, empty engine error log. Both saw start, release, recovery and confirmed impact through ordinary input. Local captures are under `artifacts/flame-review/convergence-godot/` and `web/artifacts/flame-review/convergence-web/`.

Portable evidence is in `experiments/visual-convergence/evidence/`. The recipe and geometry tests cover new Threads, compound layers, mechanism contributions, unknown/filtered outcome fallbacks, budgets, source/target privacy, sourceless outcomes, lethal contact, and actual contact after interruption. Earlier failures exposed missing browser defaults, an incorrect default entity sentinel, and the probe's missing observation opt-in; all were corrected before successful final verification.

## Measured cost

Sequential Release `bench --only tick --json`, 300 monsters / 4 players / 600 ticks on the same machine. The new option `--cast-cues true` measures simulation event-emission cost. These numbers do not measure full per-viewer outcome routing/serialization or low-end GPU performance.

| Measure | Earlier loader baseline | No observers | Event emission enabled |
|---|---:|---:|---:|
| Zone p95 | 8.82 ms | 9.60 ms | 9.59 ms |
| Zone allocation | 67.49 KB/tick | 67.73 KB/tick | 72.80 KB/tick |
| Casting allocation | 31.5 KB/tick | 31.6 KB/tick | 35.0 KB/tick |
| Full snapshot build | 0.75 ms/player | 0.87 ms/player | 0.92 ms/player |
| Full / delta payload | 26.97 / 8.06 KB | 26.97 / 8.06 KB | 26.97 / 8.06 KB |

No-observer allocation is near the earlier baseline, **not byte-identical**: retained identity/context fields still have a small cost. Event emission adds about 5.07 KB/tick to this workload. Both p95 measurements meet the 25 ms budget; allocations remain below 2 MB/tick; snapshot build stays below 1 ms/player. These single paired runs do not establish a statistically significant timing change. Snapshot bytes exclude the opt-in event stream.

## Remaining limits and review asks

This is a shared foundation and enforced convergence inventory, not completed ARPG visual parity. Persistent defense plates/status intensity, mode-specific readiness artwork, precise resolved forecasts, movement/companion/construct state presentation, and authored effect/sprite voices remain partial or pending by family. Neutral fallback does not close them. The source capture must be refreshed at migration boundaries; CI cannot discover changes in the separate repository by itself.

The current captures show depth-tested cues in a placeholder world with crowded labels and occasional scenery occlusion. They do not certify final art quality, dense-combat readability, accessibility beyond the tested still-motion behavior, or minimum-device performance.

Claude: please rereview event/outcome truth, opt-in cost, layer/fallback policy, the coverage gate and integration with #9. Reconcile the conflicting naming records against the owner's instructions before merge. Exact positional-hit rules and AI avoidance remain in the forecasting/combat work, not cosmetic recipes.

Acknowledged by: Codex, incorporating Claude's requested lifecycle/integration/performance checks. Claude rereview of this expanded implementation is pending.
