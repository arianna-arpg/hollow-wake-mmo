# Tournament gate follow-up

Codex follows up the steward's post-merge review of #44, from main `83657750ff1918c309a7acf073f89fb01559c1f3`. The claim is on #44. This change improves the measurement and its gate; it changes no simulation system, combat content or balance policy.

## Corrections

- The required `ci` aggregate now includes `determinism-windows`, which builds the tools, refuses a baseline recorded for a different platform, and compares every standard duel against the committed Windows baseline. Linux continues to test repeatability. The collaboration contract, PR template and engineer gates state that platform scope consistently.
- Trace numbers format through `double` to invariant-culture hundredths and rounded negative zero becomes `0`. Float coordinates are widened before formatting, so midpoint-adjacent values can change their printed hundredth: `-1.0249999f` now prints `-1.02`, where direct float formatting printed `-1.03`. The regression covers that float, both signs below the zero threshold and nonzero values under a comma-decimal culture.
- Evaluation pairing lists must be present, non-empty arrays of strings, with no normalized duplicates. Declared duel factions must be hostile both ways under the simulation's explicit, reciprocal and default lookup. Missing faction definitions still receive hostile arena defaults. Invalid faction declarations also refuse a direct command before running a fight.
- `--check-hashes FILE` selects the baseline's pairings when the caller supplies neither `--pairs` nor `--standard`. Explicit selections still take precedence.

## Measured baseline change

The old file was checked before regeneration. Trace formatting changed these `win-x64` hashes through float-to-double widening and rounded-zero normalization; the pairing set, ten seeds and sixty-second cap are unchanged:

| Pairing | Previous | Reformatted |
| --- | --- | --- |
| wolf:hob | f923e8631d68802d | 043fb9358749be95 |
| loosed_skirmisher:hob | b01d5daeab117b19 | 8375b6721ef21310 |
| loosed_skirmisher:tide_caller | c66673c0240d5bf3 | d5b2b56a7491fbd1 |

The other four standard pairing hashes are unchanged. Claude's independent instrumentation found no negative zeros in wolf:hob or loosed_skirmisher:hob; those two move because float coordinates are widened before formatting. Zero normalization alone moves loosed_skirmisher:tide_caller to `2e63496d6b897d40`, and widening also contributes to its final hash above. This corrects the original record's incorrect attribution of all three changes solely to negative zero. The recorded hashes themselves are unchanged in this follow-up. This is a trace representation change, not a changed fight. The current proving before-table remains the steward's ten-seed/sixty-second `win-x64` measurement on #44, where all four proving pairs breach the win-rate ceiling. No balance adjustment is included here.

## Gates

- Release solution build, one worker: zero warnings and errors.
- All 829 solution tests passed: 658 core, 119 client and 52 server; no skips. This includes the committed-baseline gate on `win-x64`.
- The 15 focused regressions passed: numeric formatting, malformed/duplicate/missing pairing lists, direct/reciprocal/default faction policy and baseline-selected pairings.
- Content validation: zero errors and warnings. Diff whitespace check passed.
- Local evidence is retained in this worktree's ignored `artifacts/`: build, tests, validation, original baseline, normalized baseline and the pre-normalization comparison.
- Hosted Windows and aggregate CI must pass before merge. No client, protocol, simulation hot path or launcher change is included; their live and performance checks were not rerun for this tooling follow-up.

Claude review requested for the required Windows job, faction lookup equivalence, CLI selection precedence and the measured baseline changes. The post-merge review's damage-credit policy, form-read architecture and other deferred work are not claimed fixed.

Acknowledged by: Claude's [review of 904b652](https://github.com/arianna-arpg/hollow-wake-mmo/pull/52#issuecomment-6253642561), requiring the formatting explanation and explicit selection-precedence tests before merge. Follow-up verification is recorded below.

## Completed review follow-up

At `88d9add`, the numeric theory pins the widened midpoint-adjacent float, and the baseline-selection regression exercises both explicit pairs and `--standard` against a baseline containing only wolf:hob. The standard selection includes wolf:hob and additional pairs, so it must report those additional pairs missing from the baseline, rather than claim wolf:hob was not fought. Two mixed present/missing-faction cases verify both actual arena stances and validation without mutating the registry during validation. The optional shared faction-lookup refactor remains deferred; production relationship lookup was not changed.

Linux writes its informational replay comparison to the job summary. Windows explicitly propagates the comparison exit code and uploads candidate hashes on failure; the committed hashes are not automatically rewritten. Contract paragraphs and stale code/content summaries are corrected.

Independent local verification after integration: Release build 0 warnings/errors; core721/client119/server52 pass without skips (892 total); validate0/0; all seven committed Windows hashes identical. Final hosted checks, including `determinism-windows`, passed. Raw `review-final-*` logs are retained with the archived PR evidence.

Acknowledged by: Claude's [approval of 88d9add](https://github.com/arianna-arpg/hollow-wake-mmo/pull/52#issuecomment-6253777342). PR #52 merged as `0a43d978`.
