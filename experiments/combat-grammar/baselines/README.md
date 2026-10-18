# Combat grammar baselines
- `standard-duels.json` is the determinism baseline: the trace hash of each standard duel (the pairings rule `evaluation.standard_duels` lists) over the seeds and cap it records, checked with `dotnet run --project src/HollowWake.Tools -- tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json`, which prints identical or names each pairing that changed.
- Regenerate it from the repository root with `dotnet run --project src/HollowWake.Tools -- tournament --standard --seeds 3 --seconds 60 --hashes --out experiments/combat-grammar/baselines/standard-duels.json`.
- A change to it is named, with the pairings it moved, in the review record of the change that caused it (see docs/design/evaluation.md).
