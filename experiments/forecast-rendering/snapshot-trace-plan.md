# Bounded snapshot trace

This is preparation for one diagnostic on the validated combined forecasting/body-contact source. It does not authorize treating a prior branch's measurements as combined evidence or repeating an unchanged workload until it passes. First budget failures and all 800 samples from the original stage diagnostic remain intact. No production optimization is selected by this plan.

## Prerequisites and tools

Finish combined correctness gates first, including the shared release predicate, contact selectors, actual origin, aim, damage-recipient riders and client eligibility. Record the exact source commit, then build that clean commit in Release. The capture runner rejects dirty source, a different commit, an executable with a different embedded source revision and an existing output directory. Heavy builds, smoke tests and other measurements stop for the agreed capture window.

Use the official Microsoft dotnet-trace package pinned to `10.0.750501`, installed into ignored `artifacts/diagnostics/dotnet-trace`, never globally. Its [NuGet package](https://www.nuget.org/packages/dotnet-trace/10.0.750501) and [CLI reference](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) describe startup collection and reporting. Restore from the official NuGet feed through a local config containing only that source:

```powershell
dotnet tool install dotnet-trace --tool-path artifacts/diagnostics/dotnet-trace --version 10.0.750501 --configfile experiments/forecast-rendering/trace-tools/NuGet.Config
dotnet build experiments/forecast-rendering/trace-tools/TraceTools.csproj -c Release
```

The inspector pins Microsoft's TraceEvent library separately. No trace is captured by building it. `TraceTools fixture` is a synthetic marker/stack control and not a game benchmark.

The managed capture enables `dotnet-sampled-thread-time`, explicit runtime GC/loader/JIT/compilation/exception/threading/contention/wait-handle events at verbose level, and `HollowWake-SnapshotProfile:0xFFFFFFFFFFFFFFFF:5`. Verbose runtime events are needed for detailed compilation attribution and add overhead. The isolated installed tool's profile descriptions are authoritative for that version; do not assume a sampling frequency from a different runtime's documentation. Sampled thread time includes waits and must not be renamed CPU time. No heap dump, application network provider or global runtime setting is enabled.

## One capture

Run `capture-snapshot-trace.ps1` in PowerShell 7 with the agreed 40-character commit and a new directory below ignored `artifacts/`. Without `-Run` it only checks prerequisites. Add `-Run` after the coordinator releases a quiet window:

```powershell
./experiments/forecast-rendering/capture-snapshot-trace.ps1 -ExpectedHead <validated-commit> -OutputDirectory artifacts/snapshot-trace/first -Mode ManagedOnly
./experiments/forecast-rendering/capture-snapshot-trace.ps1 -ExpectedHead <validated-commit> -OutputDirectory artifacts/snapshot-trace/first -Mode ManagedOnly -Run
```

The runner launches the existing default `bench --only tick --snapshot-profile ... --json` once through dotnet-trace. It uses Normal priority, a fixed 64 MB EventPipe buffer, 30-second trace limit and 45-second process watchdog. It does not warm or replay the workload separately. All console output, binary trace, frame/stage records, source/executable/tool identities, completion status and hashes stay local. The combined profile must contain all four phases and 800 frames, but capture exit alone is not evidence that all trace events or symbols survived. A timeout or failure preserves partial output and stops only its owned process tree; no automatic retry occurs. Release the heavy-work window as soon as capture and cleanup exit, before analysis.

`-Mode WithScheduler` adds the prepared `snapshot-scheduling.wprp` Windows recording in the same single workload invocation. The bounded 64 MB memory profile includes process/thread lifetime, context switches and ready-thread events. It deliberately omits file, registry and network tracing. Raw kernel metadata is machine-wide, so it stays local and analysis selects only the benchmark's process/thread identities from its markers. The runner requires an elevated session, refuses an existing or unknown WPR recording, and starts/stops only its unique named instance. A failed save attempts cleanup only of that owned instance. It never cancels another recorder.

The current agent session was verified non-elevated; no kernel recording was started. The WPR profile parses successfully, but live scheduler collection is not certified. Managed capture remains usable. If an elevated diagnostic session is unavailable for the agreed window, report the scheduling gap explicitly instead of attributing a stack gap to contention. [EventPipe's scope](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe), the [WPR command contract](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/wpr-command-line-options) and [kernel keywords](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/systemprovider) define these boundaries.

## Analysis and decision

Read the retained binary without collecting again:

```powershell
dotnet experiments/forecast-rendering/trace-tools/bin/Release/net10.0/TraceTools.dll inspect artifacts/snapshot-trace/first/managed.nettrace artifacts/snapshot-trace/first/events.local.json
artifacts/diagnostics/dotnet-trace/dotnet-trace.exe report artifacts/snapshot-trace/first/managed.nettrace topN -n 30
```

The inspector retains marker identities/times and event counts locally. Dynamic and typed runtime counters may overlap and are not summed. The official report supplies resolved managed stack names; unresolved frames and lost events must remain explicit. Verify one matching marker per raw frame, identical sample/phase/round/viewer/tick/start/end fields, complete phase coverage, and no event-loss or rundown warnings. Keep every frame, including first-use spikes. Whole-run stack percentages alone do not attribute an individual spike.

For a stage timestamp S, marker anchor bracket [A, B], frequency F and trace event time T, its trace-time interval is bounded by `[T - (B-S)/F, T - (A-S)/F]`. Convert units consistently. Marker emission can itself pause, so retain the bracket width and decline precise attribution when it is too wide. Use native thread identity to match runtime events and, if collected, scheduler intervals. The first interface/forecast frames and later Self spikes are the initial inspection targets; they do not predetermine the result.

Attribute only supported overlaps: sampled work inside entity capture/JSON, compilation on the measured thread, managed waits, or scheduler ready/off-CPU intervals when the kernel record exists. A zero GC count proves neither continuous CPU execution nor absence of another runtime pause. If the trace cannot distinguish the cause, preserve that uncertainty and state what additional evidence would distinguish it. Any optimization requires a concrete measured path, unchanged payload/correctness gates and its own controlled comparison; tracing does not waive the 1 ms budget.

Publish an inspected source-relative report with source/tool versions, raw artifact hashes, stage/stack findings, uncertainty and next decision. Do not publish raw traces, local usernames, absolute machine paths, process/thread IDs or unrelated processes. Sanitize before posting; local evidence remains available for reproduction.
