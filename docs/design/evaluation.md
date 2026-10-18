# Evaluation: tournaments, metrics and bands

Combat is measured, not asserted. The evaluation harness runs seeded duels across pairings of patterns and reports what the simulation's own events say happened: who won and when, what each side dealt, cast and used, and how varied its casting was. Designers keep the bands a matched pairing must stay inside as content, and gates keep the reports as evidence beside the benchmark rows.

Code: `src/HollowWake.Core/Evaluation/Tournament.cs` (the runner and the report records), `src/HollowWake.Tools/TournamentCommand.cs` (the `hollowwake tournament` command). Rules live in group `evaluation`.

## What a fight is

A fight is one pairing in a fresh arena (`ZoneBuilder.CreateArena`) seeded from the pairing and the fight's index, the two bodies spawned four units apart under hostile factions, ticked until one of them dies or the cap in seconds runs out. Nothing is read from a body beyond its pattern's name: every number comes from `DamageDealt`, `WeaveCast`, `StatusApplied` and `EntityDied`, the mechanisms from the compiled weaves in the caster's book that match the keys it cast. The same seed gives the same fight, so a report repeats exactly and a change in it means a change in content or code.

## What a report holds

Per fight, per side: damage dealt, hits and crits, casts, the distinct weaves cast and their mechanisms, the size of its kit, the spread of its casts, the statuses applied, and the fraction of life left. Per pairing: win rates for each side and the draw rate, the median and quartiles of time to kill over the fights that ended, each side's variety (the share of its kit it cast, averaged over fights) and spread (how evenly its casts fell over the weaves it did cast, the entropy of their shares over the most it could be), the union of weaves and mechanisms each side used, and the bands it broke. The whole report serializes to JSON (`--out`), so a pull request can keep it under the evidence folder.

## Bands

Rules group `evaluation` holds the bands a matched pairing must stay inside: `ttk_min` and `ttk_max` for the median time to kill, `win_rate_ceiling` for either side's share of wins, `draw_ceiling` for the share of fights that end in a draw, and `variety_floor` for the share of its kit each side must use. With `--bands` the command names every breach and fails. Bands are for pairings built to be even, such as the proving pairs; an uneven pairing is measured but not banded.

## Using it

```bash
dotnet run --project src/HollowWake.Tools -- tournament                                   # the proving pairs, ten seeds, sixty seconds
dotnet run --project src/HollowWake.Tools -- tournament --pairs wolf:hob,proving_reaver:proving_bulwark --seeds 20 --seconds 45 --bands --out report.json
```

A gate on a content or AI change runs the proving pairs with bands, keeps the JSON, and compares the mechanisms each side used before and after: a doctrine or a weave that never shows in a report is not proven to work. The report is not a playtest: a human rubric and recorded sessions say whether the fights are good, the harness only says whether they stay inside what the designers wrote down.
