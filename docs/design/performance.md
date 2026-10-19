# Performance

The foundation must stay pristine as the game scales, so performance is measured, budgeted and gated rather than guessed.

## The benchmark

`hollowwake bench` (in Release) measures the hot paths:

- `attribute.*`: the modifier fold with and without caching, with weave extras, and its allocation.
- `expression.*`: formula evaluation, allocation and uncached parsing.
- `worldgen.*`: zone planning, chunk generation time and allocation, climate sampling.
- `zone.*`: a loaded start zone with several hundred monsters and a few walking, casting players, ticking for a stretch: average, median, 95th percentile and worst tick, allocation per tick, events per tick, per-system time and allocation (the simulation's profiler), and errors.
- `snapshot.*`: what the server builds and serializes per player per snapshot on the server path: the one-time burst on entering a zone, then steady state with only changed entities as full states, then the same with deltas opted in.
- `snapshot.forecast.*`: incremental production forecast observation, interest filtering, exact revision reuse and JSON encoding. The note reports active observations so an empty scene cannot masquerade as a forecast measurement.
- `snapshot.combined.*`: entity deltas, self/interface state, forecasts and encoding in the same snapshot. This shares the existing snapshot time and delta-traffic budgets; forecasts do not receive an additional allowance.

`--only <group>` runs one group; `--monsters`, `--players`, `--ticks` and `--minions` (pooled summons per player, so minions think, follow and fight) size the zone run; `--json` emits results for machines.

Expression allocation has explicitly different scopes. The historical `expression.alloc` row retains its original whole-process counter and loop; keep its nonzero readings when comparing old records. `expression.alloc.calling_thread` measures allocations on the synchronous evaluator's thread after a fixed forty warmup loops of the exact sample delegate, then one measured loop of one hundred thousand evaluations. `expression.alloc.sample_process` records whole-process allocation over that same new sample, including other runtime threads. No sample is retried or chosen for being zero, and no estimated overhead is subtracted. The zero-allocation small-call contract applies to the warmed calling-thread sample; runtime bookkeeping on that thread is still counted and a nonzero result must be reported and investigated. The process rows remain diagnostics and must not be relabeled as zero or omitted. These counters do not certify asynchronous/custom functions that allocate on other threads.
`--only hit-bodies` repeats the deliveries workload with a deterministic roster of resolved patterns carrying authored hit profiles. This exercises compound-volume contact and broad-phase bounds; the ordinary unprofiled deliveries workload is not a substitute for it.

## Budgets

Budgets are for the benchmark's default zone run in Release on a development machine, per server tick at 20 Hz.

| Measure | Budget | Why |
|---|---|---|
| zone.tick.p95 | at most half the tick (25 ms) | Headroom for network and saves on the same thread |
| zone.tick.alloc | at most 2 MB | Keeps gen0 collections rare enough not to show as hitches |
| system.behavior | at most 3 ms and 1 MB | Hundreds of monsters must think cheaply |
| snapshot.build | at most 1 ms per player | Ten snapshots a second per player |
| snapshot.bytes | at most 30 KB per player per snapshot | Full states are the fallback for a client that does not negotiate deltas; in a melee of hundreds of monsters their life, stamina and cast progress change every snapshot |
| snapshot.delta.bytes | at most 10 KB per player per snapshot | Every shipped client negotiates deltas, so this is the number players feel |
| worldgen.zone.plan | at most 40 ms | Bounds synchronous zone planning, including local quest population inspection |
| worldgen.chunk | at most 4 ms and 400 KB | Zones load in well under a second; clients stream without hitches |
| attribute.apply.extra | at most 500 ns and 200 bytes | Every hit folds damage several times |
| expression.evaluate | at most 150 ns, no allocation for small calls | Formulas run everywhere |

When a change crosses a budget, the change is wrong until the budget is restored or the budget is argued down in this file.

The full-state, delta and interface groups run sequential stretches of the same workload. Do not add their averages and call that an end-to-end result; use `snapshot.combined.*` for simultaneous cost. Both shipped clients request forecasts, so protocol negotiation is compatibility protection, not evidence that forecast cost is optional in normal play. The added forecast epoch/day fields and changed combat outcomes can also increase ordinary snapshot traffic; report remaining headroom in both full and delta groups. Run `--only tick --cast-cues true` to include confirmed cast-event production, and add `--minions` for a summon-heavy stress report. Default budgets remain default-workload gates; stress reports must explicitly retain any over-budget measurements and do not establish crowd or minimum-hardware certification.

## Profiling

`Simulation.Profile = true` records each system's time and allocation per tick in `Simulation.Costs`. The benchmark prints the top systems by allocation. Use it before optimizing anything: the first run of the benchmark showed one system responsible for almost all allocation, and a profile is what found it.

For snapshot attribution, `hollowwake bench --only tick --snapshot-profile <new-file.json> --json` additionally records each viewer in each full, delta, interface and combined phase. The output directory must exist and an existing file is never overwritten. Ordinary runs retain their existing timed loops. The diagnostic invokes the same builders and serializer once in the same order, and retains the summary rows with an explicit diagnostic label. Capture costs are split into entities, self, interface panels and the optional forecast stream; encoding costs, message counts and bytes are recorded separately for each. Build and encoding counts describe the same messages, not two streams. Update/delta counters count entity states, not messages. Per-sample records include viewer order, simulation tick, known entities, self cadence, emitted counters and process-wide GC collections.

All probe overhead remains included in diagnostic totals, with no estimated subtraction. The preallocated record buffer is sized before the run. Calling-thread allocation counters cover each stage; phase wall time, process CPU and GC also cover intervening simulation ticks and probe work. These contexts cannot establish contention or its cause. Preserve the first ordinary budget result alongside the diagnostic; the diagnostic does not replace it or certify a budget pass. Phases sample different simulation stretches, and viewers share caches in their existing order, so neither phase means nor viewer means alone establish a causal regression. Use a coordinated quiet window for a single fixed-source diagnostic and investigate its evidence before deciding whether another measurement is warranted.

Profile schema 2 adds monotonic start/end timestamps to measured stages. When the `HollowWake-SnapshotProfile` EventSource is enabled, one completed-frame marker relates those timestamps to the trace's native thread and time. The marker runs after the measured frame, and its before/after timestamps bound correlation uncertainty; its process overhead can affect later frames and is not subtracted. The optional source is Tools-only and never participates in gameplay. Older raw profiles retain their original schema.

The bounded [trace preparation](../../experiments/forecast-rendering/snapshot-trace-plan.md) combines process-scoped managed stacks, compilation/GC/wait events and, when an elevated Windows session is available, a limited scheduler recording. Managed thread-time sampling alone does not prove CPU use, preemption or external contention. Keep raw binary traces, process identities and absolute machine paths in ignored local evidence with hashes; publish only inspected, sanitized summaries. A capture is diagnostic evidence on its exact validated source, never a replacement for the first ordinary budget result.

## Rules of the road

- Measure first; keep the benchmark honest; add a benchmark group when a new hot path appears.
- Determinism is not negotiable: an optimization that changes generated terrain or simulation outcomes is a bug, so golden tests pin outputs.
- Prefer removing work to making work faster: do not re-path when the path is still good, do not send what did not change, do not evaluate what no one reads.
- Zone placement samples distance to water only after cheaper candidate constraints pass and only when an authored water-distance constraint needs it; candidates at the same point share that sample. This preserves placement, RNG and quest output. Entity snapshots reuse the requested captured variant across nearby viewers and use a generated writer for the repeated entity payload, with byte-equivalence tests against the reference serializer.
- Allocation in steady state is the enemy of a smooth tick. Pool, reuse, iterate without copying, avoid closures and LINQ in per-tick code.
- The server thread owns the simulation. Parallelism comes from zones on worker threads behind the job scheduler seam, not from threads inside a zone.
