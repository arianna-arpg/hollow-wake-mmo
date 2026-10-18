# Elemental bounded-search and level integration

PR #40 consumes the owning foundation's minimal corrections `b1dd4d9`, `097c526` and `192e073` with provenance as `ebb50b4`, `5b14728` and `016f29b`. The only conflict was the shared review notes: both the first missed planning target and subsequent query implementation record are preserved. No owning golden constants were copied. The final upstream evidence head is `3007003`; its measured source is `ec144c3`. The owner reports planning 57.6400 to 36.6863 ms against the original foundation, within the proposed 40 ms target but above Claude's earlier 21-23 ms main reference. That is upstream evidence, not an elemental performance result.

The shared runtime caps each local population slot at the content-authored nearest 16 chunks, uses actual spawn facts without flora placement, and rejects impossible candidate chunks without consuming RNG. The shared equivalence tests include overlays and their removal. Default inhabitants and landmark cells now preserve authored pattern levels. Elemental inhabitants explicitly set `use_site_level: true`, retaining their intended region-level difficulty; their stamps contain no separate spawn cells. Eight new cases exercise each manifestation's actual initial births and respawns at level 25. No elemental physics, geometry, combat kit or renderer changed.

## Generation audit

Evidence under `experiments/elementals/evidence/review-fixes/bounded-*` compares preserved `4b7d0aa` Release runtime (gameplay source unchanged at `9f04932`) with this integration, using exact prior elemental content. The preserved Core DLL SHA256 is `B8D8ED2CBE56506A18ACF6907DCDDED0558B81610AC7D50505CDF359DA3FD159`. The two JSON audits record eight zones, sites, per-pattern counts, explicit/effective resident levels and quest targets. Four before/after CLI maps are retained.

All audited site identities/levels and per-pattern spawn/body counts are unchanged. The starting town now offers Gather Floss, Cull Hob and Scout Ruin, all level 1. Clear Knot is omitted; Scout moves from `poi.1_1.ruin.305.334` to `poi.0_1.ruin.213.450`. The other seven zones still have no generated offers. These are this branch's actual targets, which differ from the owning foundation's content. Starting guards and quartermasters return to uniform level 5; the knot's cinder imp/shade/tangle return from site level 3 to pattern levels 4/2/6. The northern knots similarly return from site level 7 to 4/2/6. Warband and other site residents also regain authored levels; the complete per-site changes are in `bounded-world-review.txt`.

The own goldens were refreshed only after this audit. Origin info changes for the offers; the other three info hashes remain unchanged. Chunk hashes change because the existing format includes explicit spawn levels. Climate remains unchanged. The five expected old-golden failures are retained; the first build's missing test namespace import is also retained alongside the corrected zero-warning build.

## Verification status

The focused run passes 195 cases, including all elemental and shared bounded-search/level/query cases, with only the five expected old-golden failures before refresh. After the audited refresh, the full Release solution passes Core 2319, native client 121 and server 48 (2488 total), including all 168 elemental cases. Solution build is clean with zero warnings/errors. Content validation is 0/0; the 200-tick load, elemental duel, generated protocol check and private TCP anatomy/delta/interface probe pass. Browser tests pass 284/284; typecheck and production build pass with the existing large-chunk advisory.

Fresh private run `run-uzwrsN` passes browser 31/31 and native state 26/26, with no cleanup errors. Card and grayscale frames were inspected: terrain and bodies render, and legacy guards/quartermasters show the restored level 5. Native headless output retains existing shutdown allocation warnings; native pixels and controller/desktop integration remain outside coverage. The smoke/probe servers and owned child processes have stopped. Evidence files carry the `bounded-` prefix; original card acceptance remains preserved.

Earlier level-dependent benchmarks are historical. The controlled measurement below uses the restored resident levels and preserves every first sample. #20 release-policy integration stays separate. The PR remains draft; this record does not authorize merging.

## Validated foundation ancestry

The final #34 tip `745015e5a0849a201e6857e47c9e23435618fb0a` is merged to reconcile the stacked PR's ancestry. Conflicts were limited to two shared review records and the branch-specific goldens. The shared records are the complete incoming historical records; the independently audited elemental golden file is retained byte-for-byte from tested `b6b3e3018beb47a1b4118337b7cc5962d587eff8`.

`foundation-ancestry-equivalence.json` records identical before/after Git trees for `src`, `content`, `tests`, `web`, `scripts`, `config`, `launcher` and `deploy`. The full staged comparison outside `docs` and `experiments` has no changed paths. Every retained Release output file has the same SHA256 before and after merging; Core is `3D2B7D065F19B2675CBDCDDC42BD462E7E4A490C55B1D8015BF48BD61D840238`. All application/test inputs are therefore unchanged, so the tested Release binary is retained without a rebuild or test rerun. The pending benchmark will use that binary with this reconciled source ancestry.

The original #34 228 bytes/tick allocation failure (threshold less than 200), isolated pass and full unchanged-suite retry pass are retained verbatim in the incoming `34-final-*` evidence and shared review. Their cause remains unestablished. Incoming historical duel traces retain their original trailing spaces; their preservation is not reported as a clean whole-merge whitespace check. Elemental edits pass their scoped whitespace check. Previous level-dependent measurements remain historical, and no fresh benchmark has run at this checkpoint.

## Controlled performance: snapshot target missed

The coordinator's next quiet window measured exact source `50f7dd639d95ade50b04987aa6f835cfef3f8304` using the retained `b6b3e30`-equivalent Release binary. Exactly two processes ran serially at Normal priority: one full default benchmark, then one authored hit-body profile. Both exited 0 and were reported STOPPED promptly. All first rows and process metadata are retained under `bounded-bench-*`; there was no repeat or separate worldgen-only process.

| Gate | First result | Limit / result |
| --- | ---: | --- |
| Zone planning | 35.962 ms | Proposed 40 ms target met; still above earlier main 21-23 ms reference |
| Tick p95 | 9.7455 ms | 25 ms, pass |
| Behavior | 1.576413 ms/tick | 3 ms, pass |
| Snapshot build | 1.222392 ms/player | 1 ms, **fail** |
| Full / delta snapshots | 28.851885 / 8.881948 KB | 30 / 10 KB, pass |
| Chunk generation | 0.572948 ms / 14.330811 KB | 4 ms / 400 KB, pass |
| Extra attribute fold | 337.728 ns / 0.1392 bytes | 500 ns / 200 bytes, pass |
| Expression evaluation | 101.5167 ns | 150 ns, pass |
| Original expression allocation counter | 0.0168 bytes/eval | Nonzero; no literal-zero pass |

Tick allocation is 75.885729 KB, behavior allocation is reported as 22.9 KB and system errors are zero. The shared 42-profile/120-foe workload reports total deliveries 1.869366 ms/tick, projectiles 0.919366 ms/tick, fields 0.114253 ms/tick and 37.592715 KB/tick allocated. These are shared authored profiles, not new elemental hit volumes. The original process-wide expression allocation row is preserved without substitution. Full snapshots have 1.148115 KB of size headroom.

**Performance is not cleared:** snapshot building exceeds its limit by 0.222392 ms/player (22.2392%). No cause is asserted, and no repeat was attempted to obtain a pass. `bounded-performance-review.txt` gives every numeric gate and exact process timestamps. Functional checks remain independently passing; the coordinator owns further shared performance decisions. #34's separate original allocation failure and retry evidence remain unchanged with cause unestablished.

## Lightweight snapshot inspection

There is no diff from validated #34 in `SnapshotBuilder.cs`, `Snapshots.cs`, `MessageSerializer.cs` or `Bench.cs`; this branch's only additional Core source change is appearance-family validation. The benchmark injects ordinary Hob/wolf/boar/wight/brigand patterns, and the audited starting-zone resident list contains no elemental patterns. This does not prove that all content effects are absent, but there is no elemental-specific snapshot implementation to correct.

The measured row in `Bench.Snapshot` combines entity-state building, occasional self-state building and JSON encoding across 50 rounds and four players. Simulation ticks occur outside its timer. `SnapshotBuilder` already shares fingerprints and captured dynamic states per tick across viewers; JSON encoding still occurs separately per message/view through `SerializeToUtf8Bytes`. The current aggregates do not split building from encoding or record snapshot-phase GC. Compared with the historical run, rounded known entities move 441 to 442 while full bytes decrease 29.295835 to 28.851885 KB. That does not establish a cause for the timing miss. Full/delta/interface measurements also advance through different simulation states, so they are not a matched A/B.

An actionable diagnostic, if separately coordinated after the current windows, is to retain the original combined row while splitting entity/self build from encoding, recording per-view times, changed/spawn/message counts and GC counts. It would distinguish interest/capture work from serialization/allocation before any runtime edit. No diagnostic run or implementation change has been made from this single sample, and no budget is relaxed. #20's separate calling-thread allocation instrumentation does not relabel this run's original process-wide expression counter.

Acknowledged by: pending Claude rereview.
