# Combat playtest acceptance

Completion evidence for PR #20 is summarized in `docs/reviews/2026-10-19-combat-playtest-integration.md`. The production source measured by `bench-final.json` is the source committed at `1ce0ce38`; the later native smoke change corrects only test input, and the remaining changes are evidence and documentation.

Run the unchanged default Release `bench` on Windows for the budget rows; `bench --only hit-bodies --json` exercises the authored compound anatomy workload separately. `evidence/` retains every ordinary measurement from this completion, including failures, rather than keeping only the final pass:

- `bench-first.json`: isolated populations, before performance changes.
- `bench-optimized.json`: initial small repeated-work reduction, still over budget.
- `bench-wire.json`: generated entity writer and lazy water-distance sampling; full snapshots improved, zone planning and combined snapshots still exceeded their budgets in that run.
- `bench-registry.json`: published content indexes and reused coordinate cache, within budget.
- `bench-final.json`: final validator publication ordering, within budget.
- `bench-hit-bodies-final.json`: authored anatomy workload.
- `gates.json`, `replays.txt`, `validate.txt`, `lifecycle.txt`: acceptance results and explicit limitations.

Raw logs, screenshots, the fixed-source snapshot diagnostic and process-scoped trace remain under the integration worktree's ignored `artifacts/combat-acceptance/`. The initial snapshot diagnostic and later trace are diagnostic evidence, not ordinary budget results. No uninstrumented run overlapped another owned heavy test. Machine-wide quiet and arbitrary hardware are not certified. The full-state traffic budget has limited remaining headroom.

The inactive population proposal is preserved separately under `experiments/parked-populations/`. Production server configuration continues to load only `content/core`.
