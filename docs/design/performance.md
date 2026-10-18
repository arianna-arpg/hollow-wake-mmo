# Performance

The foundation must stay pristine as the game scales, so performance is measured, budgeted and gated rather than guessed.

## The benchmark

`hollowwake bench` (in Release) measures the hot paths:

- `attribute.*`: the modifier fold with and without caching, with weave extras, and its allocation.
- `expression.*`: formula evaluation, allocation and uncached parsing.
- `worldgen.*`: zone planning, chunk generation time and allocation, climate sampling.
- `zone.*`: a loaded start zone with several hundred monsters and a few walking, casting players, ticking for a stretch: average, median, 95th percentile and worst tick, allocation per tick, events per tick, per-system time and allocation (the simulation's profiler), and errors.
- `snapshot.*`: what the server builds and serializes per player per snapshot on the server path: the one-time burst on entering a zone, then steady state with only changed entities as full states, then the same with deltas opted in.

`--only <group>` runs one group; `--monsters`, `--players`, `--ticks` and `--minions` (pooled summons per player, so minions think, follow and fight) size the zone run; `--json` emits results for machines.

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
| worldgen.chunk | at most 4 ms and 400 KB | Zones load in well under a second; clients stream without hitches |
| attribute.apply.extra | at most 500 ns and 200 bytes | Every hit folds damage several times |
| expression.evaluate | at most 150 ns, no allocation for small calls | Formulas run everywhere |

When a change crosses a budget, the change is wrong until the budget is restored or the budget is argued down in this file.

## Profiling

`Simulation.Profile = true` records each system's time and allocation per tick in `Simulation.Costs`. The benchmark prints the top systems by allocation. Use it before optimizing anything: the first run of the benchmark showed one system responsible for almost all allocation, and a profile is what found it.

## Rules of the road

- Measure first; keep the benchmark honest; add a benchmark group when a new hot path appears.
- Determinism is not negotiable: an optimization that changes generated terrain or simulation outcomes is a bug, so golden tests pin outputs.
- Prefer removing work to making work faster: do not re-path when the path is still good, do not send what did not change, do not evaluate what no one reads.
- Allocation in steady state is the enemy of a smooth tick. Pool, reuse, iterate without copying, avoid closures and LINQ in per-tick code.
- The server thread owns the simulation. Parallelism comes from zones on worker threads behind the job scheduler seam, not from threads inside a zone.
