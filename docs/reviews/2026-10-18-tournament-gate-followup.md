# Tournament gate follow-up

Codex follows up the steward's post-merge review of #44, from main `83657750ff1918c309a7acf073f89fb01559c1f3`. The claim is on #44. This change improves the measurement and its gate; it changes no simulation system, combat content or balance policy.

## Corrections

- The required `ci` aggregate now includes `determinism-windows`, which builds the tools, refuses a baseline recorded for a different platform, and compares every standard duel against the committed Windows baseline. Linux continues to test repeatability. The collaboration contract, PR template and engineer gates state that platform scope consistently.
- Trace numbers that format to negative zero are written as `0`. Other numeric text is unchanged, including invariant-culture hundredths. The formatting regression covers both signs below the rounding threshold and nonzero values under a comma-decimal culture.
- Evaluation pairing lists must be present, non-empty arrays of strings, with no normalized duplicates. Declared duel factions must be hostile both ways under the simulation's explicit, reciprocal and default lookup. Missing faction definitions still receive hostile arena defaults. Invalid faction declarations also refuse a direct command before running a fight.
- `--check-hashes FILE` selects the baseline's pairings when the caller supplies neither `--pairs` nor `--standard`. Explicit selections still take precedence.

## Measured baseline change

The old file was checked before regeneration. Only trace zero normalization changed these `win-x64` hashes; the pairing set, ten seeds and sixty-second cap are unchanged:

| Pairing | Previous | Normalized |
| --- | --- | --- |
| wolf:hob | f923e8631d68802d | 043fb9358749be95 |
| loosed_skirmisher:hob | b01d5daeab117b19 | 8375b6721ef21310 |
| loosed_skirmisher:tide_caller | c66673c0240d5bf3 | d5b2b56a7491fbd1 |

The other four standard pairing hashes are unchanged. This is a trace representation change, not a changed fight. The current proving before-table remains the steward's ten-seed/sixty-second `win-x64` measurement on #44, where all four proving pairs breach the win-rate ceiling. No balance adjustment is included here.

## Gates

- Release solution build, one worker: zero warnings and errors.
- All 829 solution tests passed: 658 core, 119 client and 52 server; no skips. This includes the committed-baseline gate on `win-x64`.
- The 15 focused regressions passed: numeric formatting, malformed/duplicate/missing pairing lists, direct/reciprocal/default faction policy and baseline-selected pairings.
- Content validation: zero errors and warnings. Diff whitespace check passed.
- Local evidence is retained in this worktree's ignored `artifacts/`: build, tests, validation, original baseline, normalized baseline and the pre-normalization comparison.
- Hosted Windows and aggregate CI must pass before merge. No client, protocol, simulation hot path or launcher change is included; their live and performance checks were not rerun for this tooling follow-up.

Claude review requested for the required Windows job, faction lookup equivalence, CLI selection precedence and the measured baseline changes. The post-merge review's damage-credit policy, form-read architecture and other deferred work are not claimed fixed.

Acknowledged by: pending Claude review.
