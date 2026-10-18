# Background lifecycle test: exited Linux descendant

Scope: fix only the lifecycle test's interpretation of an already-exited Linux descendant. Runtime process cleanup, client input, gameplay, content and protocol are unchanged. This serves the vision's observability tenet by removing a false CI failure without weakening the live-process assertion. Branch starts from main `17ca2f13a4c193d4b65d99ca4545aae267581092`. No open PR claimed `scripts/test-background.test.mjs` when ownership was checked.

## First failure

[PR #31 CI attempt 1](https://github.com/arianna-arpg/hollow-wake-mmo/actions/runs/39169124249/attempts/1), exact head `a918663ce5845474fb4347a11fcb663d337a9eb9`, failed the launcher job's lifecycle test. The original capture is preserved at `artifacts/entity31-contract-ci-first-failure.txt` in the integration checkout. Relevant excerpt:

```text
timeout stops the owned process and its descendant without stopping a neighbour
Error: ESRCH: no such process, read
    at async readFileHandle (node:internal/fs/promises:1261:24)
    at async TestContext.<anonymous> (.../scripts/test-background.test.mjs:114:22)
errno: -3, code: 'ESRCH', syscall: 'read'
tests 9, pass 8, fail 1
```

The later rerun succeeded on the same head; that does not erase the first failure. The test previously treated only `ENOENT` as an absent `/proc/<pid>/stat` entry. The captured read instead returned `ESRCH` after shutdown.

## Change and verification

The existing Linux assertion is extracted into a test-local helper with an injectable reader. Both `ENOENT` and `ESRCH` now classify the descendant as absent. A zombie remains acceptable while awaiting reaping. Readable running, sleeping, uninterruptible and stopped states still fail the assertion. `EACCES`, `EPERM`, `EIO` and errors with no code propagate as the same error objects. The neighbour-liveness and process-tree cleanup assertions remain in place.

On Windows with Node v24.9.0:

- With the original `ENOENT`-only catch, `node --test --test-name-pattern "Linux descendant check" scripts/test-background.test.mjs` failed deterministically: 0/1 passed, error code `ESRCH`.
- After adding only `ESRCH` to that catch, `node --test scripts/test-background.test.mjs` passed 10/10. The injected Linux cases run on every platform; the real Windows timeout/descendant/neighbour and interruption checks also passed.

These local results isolate the classifier race; they do not claim to reproduce Linux kernel timing on Windows. The existing Ubuntu CI launcher job runs the real `/proc` lifecycle check and must pass on this PR's exact head before acceptance. No benchmarks, graphical sessions or unrelated local builds were run. The PR #23 evidence archive is untouched.

Claude review requested: confirm only the two absent-process error codes are accepted, live descendants still fail, unexpected read errors retain identity, and the real Ubuntu lifecycle check passes.

Acknowledged by: pending Claude review.
