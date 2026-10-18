# Final pre-main kin performance sample

The coordinator granted an exclusive window for the exact retained source/content `c6e117a362b552f9865155789fdf020d979f97d2` (runtime `ce3bcf569fb27426991ec69931c950e6951c74e1`). Read-only preflight caught that this worktree's Release binary was still the earlier #31 `ec144c3` binary; it was rejected without measuring it as #37. A fresh single-worker Release tools build at the retained source succeeded with zero warnings/errors. Its full output, all binary SHA256 values, content tree, exact arguments and Normal process priority are retained in `37-final-bench-*`.

One default full benchmark ran, followed by one authored hit-body workload. Both exited zero and stopped before the next owner's window. These are the first samples of this source/content; there were no reruns, omitted rows, changed parameters or budget changes. This is pre-main evidence: the separately prepared #31 main merge and audit repair were not present in these inputs.

| Named measure | First sample | Limit | Outcome |
| --- | ---: | ---: | --- |
| worldgen.zone.plan | 40.8383 ms | proposed 40 ms | Miss; target acceptance remains pending |
| worldgen.chunk | 0.587121875 ms / 14.335205 KB | 4 ms / 400 KB | Pass |
| zone.tick.p95 | 12.7149 ms | 25 ms | Pass |
| zone.tick.alloc | 75.667956 KB | 2048 KB | Pass |
| system.behavior | 2.088379 ms / 22.9 KB | 3 ms / 1024 KB | Pass |
| snapshot.build | 1.0225075 ms/player | 1 ms/player | Miss |
| snapshot.bytes | 28.851885 KB/player | 30 KB/player | Pass |
| snapshot.delta.bytes | 8.881948 KB/player | 10 KB/player | Pass |
| attribute.apply.extra | 363.1175 ns / 0.1392 B | 500 ns / 200 B | Pass |
| expression.evaluate | 109.2474 ns | 150 ns | Pass |

The CLI process-wide expression allocation reads 0.0168 B/evaluation; it is not zero and is not relabeled as such. The earlier separate executing-thread allocation regression remains a different measurement. The full benchmark reports zero simulation errors, four POIs and three quests. `snapshot.delta.build` is 0.9688485 ms and `snapshot.interface.build` is 1.1430755 ms; neither substitutes for the failed named `snapshot.build` gate.

The authored workload contains 42 profiled patterns among 120 foes: total 2.24583275 ms/tick, projectiles 1.05414125 ms/tick, fields 0.1365235 ms/tick, allocation 37.592715 KB/tick. This is measured coverage without a separately invented delivery budget. Kin and fauna still use their tested fallback cylinders.

The two misses remain open for the coordinator and Claude. This run does not establish the cause of either miss, erase prior variability or certify performance after the upcoming main integration. No acceptance or main merge is claimed.
