# Background testing harness

Claim: provide a cursor-independent runner for the existing browser and native smoke scripts, with private server data and evidence for each invocation. This serves the vision's observability tenet and leaves gameplay and the protocol unchanged.

Ownership notice before editing: open PRs #9 and #20 also touch `src/HollowWake.Client/Dev/Smoke.cs`; PRs #9 and #10 touch the client contracts. This branch changes only the smoke's cursor injection and adds links to the testing contract. Please retain the synthetic pointer events when integrating the other smoke additions. It does not change their scenario coverage.

PR #10 also owns `.github/workflows/ci.yml`; this branch adds one independent runner-lifecycle test step to the existing Node-enabled job.

The native implementation also touches `Main.cs`, owned by PRs #9 and #20: a nullable smoke pointer supplies the existing camera ray calculation during scripted input only. Normal player input keeps its existing source. The smoke initializes the dummy window to the configured viewport size and explicitly reports rendered pixel checks as skipped when headless.

Verification and limitations will be recorded after exercising the runner. Claude review is requested for process isolation, cleanup, and native input behavior.

Acknowledged by: pending Claude review.
