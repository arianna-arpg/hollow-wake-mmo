# Corrected kin performance comparison

The coordinator granted an exclusive local measurement window. Source/content head: `6a65ed186ce17ce7edaebf5681034e4041ef619f`. Baseline content: exact `26c50de03c62f434c517564906a54c0287b281ce` exported with git archive. Both full runs used the same current Release binary, default parameters, Normal process priority, baseline then candidate. The authored-profile group followed. Every first sample is retained; no retries, discarded samples or budget changes. Per-run metadata records arguments, source, priority, process ID and SHA256 hashes for the runtime binaries. All processes exited zero and stopped before the next owner's window.

| Named measure | Baseline | Candidate | Budget |
| --- | ---: | ---: | ---: |
| zone.tick.p95 (ms) | 10.8920 | 11.2207 | 25 |
| zone.tick.alloc (KB) | 75.7812 | 75.7843 | 2048 |
| system.behavior (ms / KB) | 1.730381 / 22.8 | 1.725977 / 22.8 | 3 / 1024 |
| snapshot.build (ms/player) | **1.0848485** | 0.9532005 | 1 |
| snapshot.bytes (KB/player) | 29.295835 | 29.295835 | 30 |
| snapshot.delta.bytes (KB/player) | 8.889854 | 8.889854 | 10 |
| worldgen.chunk (ms / KB) | 0.637989 / 14.3352 | 0.598158 / 14.3308 | 4 / 400 |
| attribute.apply.extra (ns / B) | 408.074 / 0.1392 | 378.216 / 0.1392 | 500 / 200 |
| expression.evaluate (ns) | 116.2353 | 119.6584 | 150 |
| expression.alloc (B/eval, process-wide) | 0.0168 | 0.0168 | see below |

The candidate passes the named numeric timing/size budgets in this controlled sample. The baseline snapshot sample fails; this does not establish a performance improvement or a stable absence of regressions. Historical samples remain retained. Snapshot timing headroom is only0.0468ms and full-state traffic headroom only0.7042KB. `snapshot.interface.build` is1.046196ms baseline and1.025935ms candidate; these are not hidden by the named `snapshot.build` result. No separate interface budget currently exists.

The CLI process-wide allocation counter is nonzero and is not described as zero. The separate passing `ExpressionAllocationReviewTests` warms and measures400,000 actual calls across four small formulas with `GC.GetAllocatedBytesForCurrentThread`, asserting zero bytes on the executing thread. Neither the benchmark nor its counter was changed. Both full runs report zero simulation errors.

The authored hit-body workload contains42 resolved profiled patterns among120 foes: total2.092649ms/tick, projectiles1.025015ms/tick,37.592715KB/tick. This is measured coverage, not an invented delivery budget. Kin and fauna still use fallback cylinders and have separate real-cast contact tests.

Zone planning remains a separate shared-foundation review blocker: baseline67.3142ms, candidate62.4137ms. Claude's independent #31/main comparison identified excessive synchronous chunk generation by the local population search. The #31 owner is correcting that search and will propagate it; this run does not close that blocker. Review: https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6253471494 . #20's separate release-policy performance findings remain independent.
