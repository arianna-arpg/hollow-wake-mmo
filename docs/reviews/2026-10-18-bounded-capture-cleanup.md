# Bounded capture cleanup preparation

Runtime remains the integration checkpoint `295fc53510949f84725c3f4f394577bdd0678f3d`. The dependency hold for the complete fauna/layered-ground/returning-shot correction remains active. This change touches capture preparation only; no runtime or content is rebuilt or modified.

The coordinator found two parameterless `Process.WaitForExit()` calls in the tracked trace runner: after watchdog termination and again in final cleanup. Output collection also called `GetResult()` on an unbounded `ReadToEndAsync` task. A child retaining an inherited output pipe could therefore strand the script even after the direct process exited.

The runner now makes one owned-tree termination attempt, with a 10-second exit wait, in final cleanup. Both console streams copy asynchronously into new files while the process runs. Their shared drain wait is limited to 5 seconds; incomplete copies are cancelled without another task wait. Partial files remain. Completion metadata records timeout, direct-process exit, output completeness, the initiating failure and cleanup errors. Files that may still be growing are not assigned stable hashes. Cleanup failure cannot print a stopped confirmation or become a successful capture. The exact-source, clean-tree, binary revision, fresh-output, Normal-priority and no-retry guards remain.

The newly prepared ignored ordinary runner receives the same output and cleanup handling for its default and hit-body modes. The historical 225 ordinary runner is unchanged. The tracked plan now describes the sequential 90-second default, 90-second hit-body and 45-second ManagedOnly trace watchdogs, plus one 10-second termination and 5-second drain allowance per step: 270 seconds of explicit waits. Six minutes is an operational planning allowance, not an OS/file-I/O deadline or a granted quiet window. WithScheduler and its WPR commands remain outside this sequence.

Verification performed: PowerShell AST parsing reports zero errors for both changed runners. Static checks require exactly three waits with one explicit argument each (`45000,10000,5000` for trace; `90000,10000,5000` for ordinary), exactly one tree-termination call, two output-copy tasks, and no `ReadToEndAsync`/`GetAwaiter`/`GetResult` invocation. `git diff --check` passes on the changed tracked files. These are static checks, not a live timeout/pipe test or a capture certification. Final no-Run preflight remains deferred until the definitive source is published and rebuilt.

Prepared file SHA-256 values:

- Tracked `experiments/forecast-rendering/capture-snapshot-trace.ps1`: `158BDB3E8FF0BA1AC17B5DF3FC3D952BA73793D652AEBC630030893482BA9D20`.
- Local `artifacts/forecast-body-integration/run-correction-benchmark.ps1`: `BA2267102E478BFA05782595DA5988B0CFDBDE19DD04B6429D0C84288044894A`.
- Unchanged historical `artifacts/forecast-body-integration/run-final-default-benchmark.ps1`: `C75BD4E5273F03EFF94491B64316C0957C4D672821E6148E615EE64300D02AD4`.

The raw static result is local `artifacts/forecast-body-integration/bounded-runner-static-check.json`. Earlier 225 candidate records and reserved output paths, plus the partial 295 gate logs, remain intact. No workload, benchmark, trace, smoke, runtime build or timing measurement ran for this preparation. No quiet window is requested or claimed.

Acknowledged by: pending coordinator/Claude review of the script-only correction.
