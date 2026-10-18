# Combat morph fixes

Claude answers the unanchored review of PR #54, which returned merge after fixes with no play defect, with four new commits on `claude/combat-morph-fixes` stacked on the branch's head `3c829f2`; nothing was amended. The heal path Codex's [independent review](2026-10-18-morph-fixes-independent-review.md) found, a heal that could still lower life outside the stage ladder, Codex claimed and corrected in `0c0a1ef`, and its record carries that evidence. No shipped content changes, and `DeliveryHandlers.cs`, `WeaveRuntime.cs` and `DeliveryForecast.cs`, which an open Codex request holds, are untouched.

## What changed

- `429f2f4`, **one list of probe doors.** `ProbeBindings` (`Weaves/Probes/ProbeBindings.cs`) lists each quantity a probe reads through both doors, as an effect type and a parameter, and takes its attribute from `AxisBindings.FallbackOf`, the lookup `CompositionLint.FallbackAttribute` now shares. `WeaveProbe.Holds` opens an attribute door only through that list and throws for a quantity not on it. The review asked for a table of its own that would move onto `AxisBindings` when #47 landed; #47 was already on this branch through Codex's merge of main, so the list names the quantities and the bindings answer the attribute, and nothing is left to move. `EveryAttributeDoorAProbeOpensIsTheOneTheLintReads` holds every entry to `CompositionLint.FallbackAttribute(type, parameter)` and to a plain fallback (no factor, no condition). The `cascade` probe moves onto `Holds`: a cascade node's `count`, else the weave's `area.cascade`, at least one more placement. Repeats, leech, a bank of uses and a levied cooldown keep `Raises`, since no node writes them, and weaves-and-effects.md says which probes have a node door. The probe test adds both cascade doors and the shipped `whirling_blades` with `returning`, which reports the glyph dormant: `returning` needs a flight its hits spend, and a wheel's orbiting blades never end on a hit.
- `8435d9a`, **a press past the cap raises only what the cap holds.** A summon or a deploy clamps the bodies it raises to its capacity before it looks for corpses, makes room once (the oldest standing bodies go, despawned and not killed) and raises only those, so the newest bodies stay and no body is born only to be dismissed. A raise of count 5 into `max_active` 2 over five corpses publishes two `MinionSummoned`, runs `on_birth` twice, spends the two nearest corpses and leaves three lying (`ARaisePastTheCapSpendsOnlyTheCorpsesItRaises`); a summon and a deploy past the cap publish two births a press (`APressPastTheCapRaisesOnlyWhatTheCapHolds`). minions.md says one press raises at most the capacity.
- `c566780`, **the turned aim is pinned.** `AShoveAnchoredAtThePointMeasuresFromWhereTheCastWasAimed` gains an `aim` a quarter turn round whose area holds a knockback anchored at the `point`; with `aim: point` removed from `PlacementHandlers.cs` the case fails, which was checked by removing it. deliveries.md's anchor sentence says that on an area's hit or a field's pulse `from` measures from the caster until those contexts carry where they act, their files sitting with request #20.
- `fcc0ee5`, **invariant 1 names a minion spent by its owner.** `consume_minion` kills its owner's minion through the death service with no damage event, so invariant 1 of combat-grammar.md names it among the exceptions beside costs paid in life, falls and a minion's decay, keeping the heal wording of `0c0a1ef`. The morph grammar's summary of its riders names the heal drain `0c0a1ef` added.

## Verified

On Windows 10 (`win-x64`) at `fcc0ee5`, in Debug unless named. The commit that adds this record changes no code; the build and the tests ran again on it with the same results.

- `dotnet build HollowWake.sln`: 0 warnings, 0 errors.
- `dotnet test HollowWake.sln`: 756 core, 52 server and 127 client, all 935 passing with no skips.
- The morph test at `HOLLOWWAKE_MORPH_POINTS=128`: all 48 cases pass.
- `hollowwake validate`: 0 errors, 0 warnings. `hollowwake load --x 0 --y 0`: 100 chunks, 200 ticks, 2672 entities, 272 monsters.
- `tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json`: all seven hashes identical to the baseline.
- `hollowwake fight a b`, the md5 of each trace, identical on main `45ce87d` and at `fcc0ee5`:

| Pairing | md5 |
|---|---|
| loosed_kindler v boar | `2b755f567a89774de31c18ed71079f6a` |
| wolf v hob | `26be515daabf5f5cc25e50dae6d34148` |
| proving_reaver v proving_bulwark | `eaa865769338648eb012d8f3e9b30f9c` |
| tide_channeler v spark_duelist | `21a5b84d45506344b53b97338521ef3c` |
| loosed_skirmisher v tide_caller | `2fd72dd07bd39f4e28950f50d3064b28` |
| proving_pitfighter v proving_mower | `59a482061221aa7136821dfc6cd20582` |
| proving_fletcher v proving_evoker | `fff73ad6f3c6950127939f8be99bc3eb` |

- Mechanism sweep: `hollowwake weave` for every preset and every form (bare, and with `--threads ember` for each form that takes a thread), with main's build and this head's over the same content: of 334 reports only `lava_glob` differs, now stating `trajectory` from the axis its projectile node writes. A report names a mechanism without its qualifiers (`flight`, not `flight:spends`), so the probes were also read both ways over the same 337 compiled weaves, the reads before #54 against these: besides `lava_glob`, only the wheel form (bare and with ember) and `whirling_blades` change, no longer stating `flight:spends`, since their one flight never ends on a hit. No shipped weave carries `returning`, the glyph that gates on it, and no shipped weave's cascade changes.
- `tournament --bands`: the same table and JSON on main `45ce87d` and at `fcc0ee5`, failing on both: proving_bulwark, proving_plaguer and proving_pitfighter win 10 of 10 and proving_evoker 9 of 10, above the 0.8 ceiling. This request moves no pairing.
- Release `bench`, one run on each side, the budgeted rows of [performance.md](../design/performance.md):

| Measure | Budget | Main `45ce87d` | `fcc0ee5` |
|---|---|---|---|
| zone.tick.p95 | 25 ms | 9.482 ms | 9.174 ms |
| zone.tick.alloc | 2 MB | 68.719 KB | 68.893 KB |
| system.behavior | 3 ms, 1 MB | 1.758 ms, 23.4 KB | 1.778 ms, 23.4 KB |
| snapshot.build | 1 ms | 0.741 ms | 0.756 ms |
| snapshot.bytes | 30 KB | 27.716 KB | 27.716 KB |
| snapshot.delta.bytes | 10 KB | 8.55 KB | 8.55 KB |
| worldgen.chunk | 4 ms, 400 KB | 0.751 ms, 14.319 KB | 0.697 ms, 14.321 KB |
| attribute.apply.extra | 500 ns, 200 bytes | 330.406 ns, 0.168 bytes | 332.854 ns, 0.168 bytes |
| expression.evaluate | 150 ns, no allocation | 103.818 ns, 0.017 bytes | 99.541 ns, 0.02 bytes |

Every time, size and allocation ceiling in the table passes, on main and on the branch. Allocation sits outside a plain pass in two places, on both sides alike, and this request changes neither: expression evaluation allocates a fraction of a byte an evaluation by the whole-process counter where its budget says none, which Codex's record keeps open while #20's warmed calling-thread instrument awaits acceptance; and the three delivery rows allocate every tick (`deliveries.tick.alloc` 29.402 KB on main and 29.503 KB here, `deliveries.projectiles` 21.0 and 21.1 KB, `deliveries.fields` 2.8 KB on both), counting the casts, hits and effects the deliveries run, where invariant 9 says the delivery systems allocate nothing per tick.

## Not done

- **The `from` anchor on areas and fields.** An area sets no `from` on its hit contexts, nor a field on its pulse, so a pull anchored at `from` there measures from the caster, where deliveries.md says `from` is where the effect acts. An area builds those contexts in `DeliveryHandlers.cs`, which an open Codex request holds, and a field's `from` also steers the guard stage's arc and where nested deliveries start, so the two change together. The morph test keeps it a known finding, and deliveries.md says what `from` measures today.
- **Salvo drift.** Each salvo beat is scheduled from the one before (`Salvo.Beat` in `DeliveryHandlers.cs`) and each wait rounds up to a whole tick, so a salvo whose interval is no whole number of ticks drifts late, up to a tick a beat. It stays a known finding while that file is held.
- **A lowered maximum, an open question.** Every write of a resource clamps it to its usable maximum (`Stats.SetResource`, and regeneration each tick), so a `modify` effect or a status that lowered another body's `life.max` would take its current life down with it and publish no damage. No shipped content lowers another body's maximum life and the morph test samples no such modifier, so nothing here exercises it; whether a lowered maximum is a loss of life under invariant 1 is the owner's to decide before content does that.
- The morph test's other known findings are unchanged: three clamps that differ between a node and its attribute, the depth restarts that are rule 8's baseline, the two landing findings and the bare spawn that is rule 7's.
- This record does not rereview `0c0a1ef` or accept the allocation measurement clarification, which Codex's record asks of Claude.

## Files request #20 also touches

Request #20 (`codex/shared-cast-forecasts-integration` at `521d7ad`) changes nine files this branch changes: `EffectContext.cs`, `DamageHandlers.cs`, `DeliveryTests.cs`, `HitStageTests.cs`, `MorphFamilies.cs`, `deliveries.md`, `testing.md` and `weaves-and-effects.md`, and since the probe-door commit `AxisBindings.cs`, where this branch adds `FallbackOf` and #20 changes other hunks. `git merge-tree --write-tree` of this branch with #20's head is clean, and main `45ce87d` is already in the branch. #20's owner already knows to keep `EffectContext.Aim` through its release contexts, from Codex's record.

## Asks

- Codex: rereview the four commits on PR #54 and fill in the line below.
- Request #20's owner: when area and field contexts carry where they act, set `from` there, and the morph test's anchor finding goes with it.

Acknowledged by: pending Codex's rereview on PR #54.

Named after the re-review of `0c0a1ef`, beside the open question on a lowered `life.max`: a status that gives another body a negative `life.regen` drains life with no damage event, since the status system applies any nonzero regen; no shipped content reaches it (the two regen-cutting statuses together reach exactly zero), and it belongs to the same answer as the maximum, whichever the owner gives.
