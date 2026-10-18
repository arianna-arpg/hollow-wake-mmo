# Verification and limitations

## Passed

- Solution Debug build, including native client: zero errors; three inherited xUnit2031 warnings in PortedProjectileTests. Release Tools build: zero warnings/errors.
- Full .NET suite: 738 core + 116 client + 39 server = 893 passed. This adds 26 core cases and one full-roster native fixture gate. Web: 276 passed; porter: 10 passed.
- Content validation: zero errors and warnings. Generated protocol has no diff. Production browser build passes with its existing advisory chunk-size warning.
- Zone load and Gnoll/Gorer duel complete. The duel forces opposing teams for its arena; normal faction content allies these clans. The Gorer wins this seeded duel. Positive-damage kit checks cover every new entity separately against a long-lived target.
- Private browser smoke: 29/29. Native headless smoke: 25/25; rendering/theme pixels explicitly skipped. Godot retains baseline RID/ObjectDB shutdown diagnostics. Harness cleanup completed without errors.
- Both gallery cameras, grayscale walk poses and shared world scale inspected. Complete native/browser plan parity and actual gear attachment pass. Native rendered pixels are not claimed.

## Snapshot timing gate remains open

All measurements below are full Release benchmarks using the same runtime. The comparison baseline is fauna commit 90cb7fc exported into ignored artifacts/kin-baseline/content/core. No benchmark, snapshot or simulation runtime was changed. Initial snapshot time exceeded 1 ms; a baseline/candidate comparison also straddled it. A predeclared three-pair batch alternated order (base/candidate, candidate/base, base/candidate), retaining every result rather than selecting a passing run.

| Run | Tick p95 ms | Behavior ms | Snapshot ms/player |
| --- | ---: | ---: | ---: |
| bench.json | 10.171 | 1.937 | 1.059 |
| bench-baseline-comparison.json | 12.147 | 1.969 | 0.981 |
| bench-final-comparison.json | 11.784 | 1.977 | 1.037 |
| bench-paired-baseline-1.json | 15.341 | 2.153 | 1.039 |
| bench-paired-baseline-2.json | 12.139 | 1.910 | 0.983 |
| bench-paired-baseline-3.json | 13.999 | 2.486 | 1.329 |
| bench-paired-candidate-1.json | 13.677 | 2.038 | 1.124 |
| bench-paired-candidate-2.json | 15.054 | 2.538 | 1.089 |
| bench-paired-candidate-3.json | 13.519 | 2.211 | 0.975 |

The paired-batch snapshot medians are 1.039 ms baseline and 1.089 ms candidate. Both exceed the 1 ms budget; the candidate range is 0.975-1.124 ms and baseline 0.983-1.329 ms. These shared-host measurements do not establish a stable regression or a stable budget pass. Keep this PR in draft pending a controlled performance review. No budget was relaxed and the passing final individual sample is not used to hide the other runs. All runs report zero simulation errors; full snapshot/delta bytes remain 27.234/8.738 KB. Detailed measurements are preserved in JSON.

## Existing card-view capture issue

The browser smoke counts geometry/cards and verifies interactions but does not validate rendered world pixels. Its 16_cards capture shows sky and labels with the world absent, in both the preceding fauna run (run-QwdBg5) and this run (run-Q7cgqw). Paired images are retained here. The initial 01_isometric capture in the current harness shows the world and bodies correctly, and the separate production-mesh entity gallery renders correctly. This pass does not change the card renderer. Claude review should separately investigate that existing mode/capture failure and add a pixel-level assertion before treating smoke counts as visual proof.
