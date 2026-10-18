# Background testing harness

Claim: provide a cursor-independent runner for the existing browser and native smoke scripts, with private server data and evidence for each invocation. This serves the vision's observability tenet and leaves gameplay and the protocol unchanged.

Ownership notice before editing: open PRs #9 and #20 also touch `src/HollowWake.Client/Dev/Smoke.cs`; PRs #9 and #10 touch the client contracts. This branch changes only the smoke's cursor injection and adds links to the testing contract. Please retain the synthetic pointer events when integrating the other smoke additions. It does not change their scenario coverage.

PR #10 also owns `.github/workflows/ci.yml`; this branch adds one independent runner-lifecycle test step to the existing Node-enabled job.

The native implementation also touches `Main.cs`, owned by PRs #9 and #20: a nullable smoke pointer supplies the existing camera ray calculation during scripted input only. Normal player input keeps its existing source. The smoke initializes the dummy window to the configured viewport size and explicitly reports rendered pixel checks as skipped when headless.

Additional ownership notice before editing: PRs #9 and #22 touch `web/scripts/smoke.mjs`, and #22 also touches the native smoke and `Main.cs`. The browser change adds an opt-in physical controller isolation mode for this runner. A diagnostic run confirmed a connected controller cancelled loading through `navigator.getGamepads()` even in headless Chromium. Scenario coverage stays unchanged.

The shared `CLAUDE.md` command and background-testing guidance overlap PRs #9, #10, #18, #19 and #22. They are additive instructions for all sessions; retain the other requests' system-specific commands during integration.

Verification on Windows against main `91f234f`, with sanitized evidence under [experiments/background-testing](../../experiments/background-testing/README.md):

- `dotnet build HollowWake.sln`: clean, no warnings or errors. `dotnet test HollowWake.sln --no-build`: 667 passed (531 core, 98 client, 38 server).
- `hollowwake validate`: 0 errors, 0 warnings. `hollowwake load --x 0 --y 0` and `hollowwake fight wolf hob`: exit 0. No protocol changes; the generated contract test passed.
- Web build and type check passed; web unit tests: 174 passed across 26 files. Runner regressions: 9 passed, including physical controller isolation, unrelated-server rejection, timeout descendant cleanup and preservation of a neighbouring process.
- `node scripts/test-background.mjs --suite all`: browser 25/25; native headless 23/23, including zero visible physical controllers. Native rendering is explicitly excluded; theme state is checked and rendered pixel comparison is logged as skipped. The browser screenshot was inspected.
- `pwsh -NoProfile -File experiments/background-testing/check-focus.ps1`: both suites passed with no owned process observed in the foreground across 1,924 samples over 129.5 seconds. The probe observes focus only; it never changes focus. Transitions shorter than the sampling interval are not covered.
- Two simultaneous browser invocations use distinct loopback ports, data folders and accounts; their reports and timestamps are retained in the evidence directory. Both leave no cleanup errors.

The broader machine search found a matching WinGet .NET Godot installation. The runner detects it and invokes the engine directly with hidden child windows and piped logs. A real controller cancelled the browser's loading screen during an earlier run despite Chromium being headless; the new `--isolated-input` page setup blocks controller polling and connection events without affecting other applications.

Limits: graphical native rendering and OS integration were not exercised on the owner's active desktop. Build and Godot import outputs still require separate worktrees for concurrent native runs; browser `--no-build` runs can share one built checkout. macOS native user-data isolation is unsupported and rejected. CPU/GPU usage is shared. Forced termination of the runner cannot guarantee cleanup. The final native run exits cleanly; some earlier native runs emitted Godot RID/ObjectDB shutdown warnings, which this work does not diagnose.

Claude review is requested for process isolation, cleanup, native pointer behavior and preservation of the overlapping smoke additions. No review or merge is claimed.

Acknowledged by: pending Claude review.
