# Combat grammar

A cast is a point in a space of nine dimensions. Every combat mechanic is a quantity on an axis of one of them, a word from a registry or an effect list at a named moment, never a flag and never a special case for one weave. This page is the test for every combat change, as [VISION.md](../VISION.md) is the test for every change: it names the dimensions and their units, the four operators that compose them, the rules of composition and the invariants, how the AI plays the space as a player would, how the harness measures it, and what a change proves. The systems keep their own contracts: [combat.md](combat.md) (the damage pipeline), [deliveries.md](deliveries.md), [weaves-and-effects.md](weaves-and-effects.md), [attributes-and-modifiers.md](attributes-and-modifiers.md), [cast-forecasting.md](cast-forecasting.md), [minions.md](minions.md), [ai.md](ai.md) and [evaluation.md](evaluation.md) (the harness).

Every rule and invariant below is marked **holds** or **target**. A rule that holds names the instrument that pins it, and a change that breaks it is a regression. A target names the instrument that will pin it and the baseline of exceptions the instrument carries today, and a change is gated on adding no new exception; the review record of the pass that closes a target says so, and the mark changes with it.

## Terms

- An **axis** is a quantity with a unit: an attribute of the weave, or an expression over attributes. A **dimension** is one of the nine groups of axes below. `hollowwake weave <id> --axes` places a compiled weave on them.
- A **node** is an effect list at a named moment. A **choice** is a word from a registry.
- A **seat** is one weave in a body's book, with its own cooldown, uses, banks and faces. A **use** is a press or a trigger that starts a seat's cast and pays for it. A **resolution** is anything a use causes: a hit, a tick, a pulse, a sequel.
- A **bank** is a quantity a seat or a body accumulates to spend: uses, charges, stacks, and **brim**, the bank a channel fills while it is held.
- The **proving pairs** are the pairings rule `evaluation.proving_pairs` names, patterns built to be even so bands apply to them; the **standard duels** are the pairings rule `evaluation.standard_duels` names, whose seeded traces are hashed as the determinism baseline.
- The **morph test** is the generated test that samples points of the space, compiles each as content, runs it in a seeded arena and asserts the invariants; the **linter** is `hollowwake lint`, which reports composition smells in content and code.
- A **doctrine** is a content definition that tells an AI body how to score and choose among what its weaves afford.

## The four operators

The compiler owns four operators, and the grammar has no fifth.

- **Quantities fold.** Every number a cast reads is folded through the one modifier engine on the body that owns the number: the weave's axes in the caster's fold under the weave's tags, so glyphs, passives, items, statuses and weave trees move them by tag scope; the victim's defenses, caps and resistances in the hit scope. The handler reads the folded value when the effect resolves, not when the weave compiles. *Holds* (`attributes-and-modifiers.md`, the pipeline tests).
- **Choices patch.** Every word a parameter takes comes from one registry that validation and the handler both read. Glyphs and trees set a word with a patch: the first glyph to set a parameter wins over the form and over later glyphs, `default` fills only a missing parameter, `append` adds to a list, and trees patch after glyphs so a glyph wins. A word never hides a number: a firing style that spaces its shots is a spacing axis with a style word, not a word that means a spacing. *Holds* for the words validation knows (`weaves-and-effects.md`); *target* for the words still hiding numbers, which the linter's frozen-axis check and `weave --axes` list.
- **Riders hook.** Every moment a delivery passes through that content may ride is a hook the handler's signature declares. A hook list runs with the moment's context, and an effect a hook added is never itself hooked. *Holds* for declared hooks; *target* for the plain effect lists at moments the signatures do not declare as hooks, which a signature test lists.
- **Graphs nest.** An effect list may cast another weave, run a list later, repeat, branch or wrap. Depth rules bound the nesting, and the compiled weave states what its graph can do through mechanism probes, so a glyph gates on capability rather than on a weave's name. *Holds* (the depth rules, the probe tests).

## What an axis is

An axis is real when the compiled weave states its value and every value composes with every value of every other axis without a special case. A handler's signature declares each parameter as an axis with its unit and the attribute it falls back to, a node, or a choice from a named registry; until the signatures carry those declarations, the `AxisBindings` registry carries them beside the compiler, and `weave --axes` reads them.

A flag is not a kind. A flag the engine carries resolves one of four ways, and a new handler never adds one: it becomes an axis whose default is today's behavior (a lobbed flight is an arc height above zero; a field that strikes a body once is a rehit lockout), a choice from a registry (which bodies an effect reaches is one word from one vocabulary, for every effect that picks bodies), a node (a list at the moment the flag used to gate), or it is dropped because a tag or another axis already says it (a hit that cannot crit carries the tag that skips the crit step; a delivery that ignores walls has the weave's phasing). *Target*: the flags the engine carries today are listed with their resolution in the pass-zero capability map, and a signature test holds the count at that baseline until each one goes.

A literal number in a graph is a frozen axis: no modifier reaches it. Content writes an attribute reference or an expression where an axis exists, and a rule's default is the base of an attribute a glyph can move, not a constant a system reads. *Target*: the linter's frozen-axis check and `weave --axes --all` carry the baseline of literals in shipped content.

**Reading the axes.** `hollowwake weave <id> --axes` places a compiled weave on the nine dimensions (`AxisReport`): one line per axis with its value, its unit, who stated it and where it sits in the compiled graph or the cast layer. Who stated it is one word: `attribute` (the weave's attribute folded with its own bases and modifiers, where the graph writes a reference or nothing), `literal` (a number the form's or preset's graph writes), `expression` (a formula, with its number when it reads the weave's attributes alone), `patch` (a glyph's or tree node's patch), `hook` (a rider a glyph hooked), `form`, `preset` or `glyph` (a cast-layer field and its owner), `rule` (a rule read where the graph gives nothing) or `probe` (a mechanism probe that holds). A constant no attribute reaches is a frozen axis, whoever wrote it. The report lists the literals after the dimensions, each with its writer (the form, the preset or a glyph), the path inside the writer's own definition and the attribute it shadows where one exists; `weave --axes --all` totals them over every form, preset and glyph, and `--literals` writes their keys to the fixture the core tests hold equal as the baseline, so a new frozen literal fails a test until the record admits it. What each handler parameter is (an axis with its dimension, unit, the attribute it falls back to or is multiplied by, the fold that reaches it on the weave, the caster or the bearer, and the condition under which the handler reads the fallback; a node; a choice with its vocabulary read from its registry; or a flag) is declared in `AxisBindings` until the signatures declare it. The signature tests hold the flags, the effect lists no signature declares a hook, and the parameter names split across handlers at their baselines.

Choice defaults supplied by content rules are reported as words with rule provenance. Damage-body contact declares its slab height and elevation only when body contact is selected; an unwritten height comes from the live source body and is not reported as a fixed weave number. Projectile launch-aim words come from the projectile-aim registry. Their elevation policy remains distinct from the nested cast handler's orientation vocabulary, recorded in the split-name baseline.

## Units

Content writes world units (a cell is one unit) for distances and radii, degrees for angles and degrees a second for turning rates, seconds for times, fractions for shares (an inner radius, a fill, a movement factor), and counts for things counted. Oscillations are written as cycles a second and spacings as world units. Time resolves on the simulation tick, so a time shorter than a tick means one tick. The wire may carry other units (radians, a distance for an inner edge) and converts only at the wire, in the protocol and the clients' coordinate files, never in content or the simulation. A modifier moves a quantity in its own unit; a conversion is never a modifier.

## The dimensions

| Dimension | Decides | Axes and units | Set by | Moved by |
|---|---|---|---|---|
| **Geometry** | where a resolution applies | a shape word from the shape registry; radius, width and length in world units; angles in degrees; the inner fraction of a radius left empty; counts of strikes | the form's shape words and area attributes, the delivery's parameters | shape patches; the `area.*` fold |
| **Kinematics** | how deliveries and bodies move | world units a second; degrees a second for turning trajectories, cycles a second and world units for oscillating ones; counts of pierces, forks, chains and bounces; distances shoved or leapt | the `projectile.*` and movement attributes | the `projectile.*` fold; firing-style and return patches; immovability and roots on the struck body |
| **Payload** | what lands on a struck body | damage per Thread; status magnitude, duration and stacks; resources; displacement; bodies summoned; weaves cast as sequels | graphs, hooks and Threads | the damage fold, `added.*`, `convert.*`, `ailment.*`, `status.*` |
| **Economy** | what a cast costs and what it banks | resource costs and reservations; cooldown seconds; uses; charges with caps and decay; brim as a fraction; stacks; corpses and minions spent | the cost fold, uses, charges and spends | `cost.*`, `cooldown.*`, `uses.*`, `charge.*`, reserve and upkeep |
| **Timing** | the time from press to last resolution, and the exposure it costs | cast seconds; beats; holds; stages; fill as a fraction; windows; fuses and telegraphs; movement kept as a fraction | the cast mode and its block; `cast.time`, `channel.*`, `repeat.*` | speeds; stuns, freezes and forbidden tags on the caster |
| **Targeting** | what is selected, and when the selection is checked again | the targeting mode; target kinds; filter words; pick orders; caps; requirements; aim spread | the form's target block, `pick` and `filter` | `aim.spread`; taunts and untargetability |
| **Agency** | who acts after the press: the caster, an inert carrier (a flight, a field, a tether), a reactive device (a trap, a totem) or a deliberating proxy (a minion) | autonomy as a behavior and a doctrine; live count; lifetime; leash | summon and deploy effects, including those that raise from a corpse | `summon.*`, `minion.*`, contracts and orders |
| **Causality** | what starts a resolution and how far it cascades: press, reflex, release, trigger, proc, follow-up, repeat, cycle, meta, sequel, hook | depth; chance; internal cooldown; procs a minute; repeat counts | triggers, hooks, follow-ups and repeats | `proc.*`, `trigger.chance`, status hooks, the depth rules |
| **Coupling** | state one cast writes on a body and another cast reads | status, charge, gauge, flag and victim-tag keys with magnitudes and caps | the hooks that write | the `vs.*` scopes, gauges, `require`, `spend`, consume and rupture effects that read |

Forms and presets set every axis; glyphs, passives, items, statuses and weave trees move them. Geometry, kinematics, payload, economy, timing and targeting describe a single cast. Agency, causality and coupling describe how casts make a build. A proxy casts on every dimension with its own stats plus the owner's investment the inheritance rule names (`minions.inherit`; a construct marked to cast as its owner casts with the owner's). Causality is budgeted: depth rules, internal cooldowns, chance caps and repeat caps bound what one use can cause, however the content nests. Coupling is the join: a setup writes a key, and a payoff on any other dimension reads it (a radius that grows with stacks, a release scaled by a bank, a pick that prefers the marked); the archetypes live there.

Two faces of a cast are derived, never authored. **Legibility** is where and when a cast can apply, read from the resolved plan the forecasts share with execution ([cast-forecasting.md](cast-forecasting.md)): a warning and the hit read the same plan, a warning states its certainty and never promises a hit or safety, and a delivery without a forecast provider shows its timing only. **Value** is output per cost and per second of exposure, read from the harness ([evaluation.md](evaluation.md)). A telegraph that is not the plan, or a power number that is not measured, is a second truth, and the grammar admits one truth.

## Composition

Geometry with kinematics is a swept region; with timing, an area over time; with targeting, a set of candidates. Economy meets timing where costs land (at the press, the beat, the stage, the upkeep) and meets causality where a triggered use pays as a pressed one does and a free proc is capped by chance, internal cooldown and depth. Glyphs gate by capability, through the mechanism probes and descriptive tags; identity tags (`form.*`, `preset.*`) may scope a modifier, as an item that improves one weave needs, but a glyph gate that names one is a smell.

The rules of composition:

1. **One mechanism, one door.** Two parameters that mean the same thing on two handlers share a name, a unit and a default: a radius is a radius, an interval an interval, a duration a duration, a pick order a pick order, on an area, a field, a scatter or a status alike. A second door for an existing mechanism is not added; the existing door is widened. *Target*: a bindings test asserts one unit and one attribute per parameter name across handlers, with the duplicated doors of the capability map as its baseline.
2. **One `who`.** Which bodies an effect reaches is one vocabulary, in two layers: target kinds say what a cast may be aimed at, filter words say which bodies a resolution reaches, and a word means the same in both. No handler carries its own self, ally or exclude flag. *Target*: validation refuses unknown filter words (holds); the two layers still disagree on whether an ally includes the caster, and the flags remain, both in the signature test's baseline.
3. **Every moment is a hook.** A delivery declares in its signature every moment content may ride: a hit, a first hit, a tick, a landing, an end, an expiry, a return, a turn. A plain effect list at a moment is a hook validation cannot see. *Target*: a signature test lists plain effect-list parameters, with the expiry list of flights as its baseline.
4. **The same number is written once.** A scaling law lives in `rules.yaml` as a named basis or in an attribute's base, and graphs reference it. *Target*: the linter's copied-expression check, with the weapon and level formulas of shipped content as its baseline.
5. **A rule a glyph cannot reach is a frozen axis.** A delivery rule (a fork angle, a chain range, a homing radius) is the base of an attribute, so content can fork into three or chain across a room. *Target*: `weave --axes` reports a value whose source is a rule, with the delivery rules of the capability map as the baseline.
6. **The runtime reads the compiled weave, never the form.** A cast mode, a ramp, a guard, a timing window, a release check or a toggle is a block the compiled weave states, so a preset, a glyph or a tree can grant one. A form is where the defaults live, not where the truth lives. *Target*: an architecture test counts the runtime's reads of the form, with today's count as the baseline; the held-mode blocks already come through the compiled weave.
7. **Agency inherits by rule and lives under a cap.** A proxy's casts are casts on every dimension. A body left behind by a delivery is a summon under its owner's cap, ending by a lifetime, a contract or its owner (the cap replaces it, the owner dismisses it, or the owner goes), never a bare spawn that outlives its purpose. *Target*: the morph test asserts every body a cast leaves behind is gone, contracted or standing under a cap at the end of the scenario; the spawn effect, which has no cap, is the baseline exception.
8. **Causality continues the budget.** A follow-up, a status tick or a cast event continues the depth of the use that caused it rather than restarting it, and a hook-added effect is never hooked. *Target*: the morph test's fan-out bound; the restarts at follow-ups, status ticks and cast events are the baseline.
9. **No system names content.** A status that stuns is read through a flag the status declares and a rule names; a tag that widens a reach is read through the attribute it modifies. The handlers, probes, stages, trigger kinds, intent handlers, curves and filter words are registries, each growing from its own file. *Target*: the linter's string-switch check over the simulation's source, with the switches of the capability map as its baseline.
10. **A glyph moves one axis freely and pays for the rest.** A glyph may move one axis without a price; every further axis it moves costs on payload or economy. A glyph that moves several axes is admitted with its measurement in the record. *Target*: `weave --axes` on each glyph's patches and modifiers; no instrument yet counts the price.

## Invariants

These hold at every point of the space, for every actor, player or AI.

1. **One pipeline.** No body loses life to another body's effect except through the stage ladder of [combat.md](combat.md); costs paid in life, falls and a minion's decay are the named exceptions and are not another's effect. *Target*: the morph test asserts every life drop on a struck body has a damage event; the `resource` effect, which may lower any resource directly, is the baseline exception.
2. **Termination.** Every flight, hold, field and placement ends by its duration or with its source; a toggle ends with its bearer, its slot or its upkeep; a summon ends with its lifetime, its contract or its owner; every bank caps, and decays unless its kind says it holds (a manual bank, a brim floor). *Holds* for flights, fields, placements and capped summons (the morph test) and for holds (the held-mode tests); *target* for bare spawns (rule 7).
3. **Bounded fan-out.** The resolutions one use causes are bounded by a named rule set: today the depth rule of each kind, the internal cooldowns, the chance caps and the repeat caps; the target is one causal budget per use. *Holds* for the delivery and agency families against today's caps (the morph test derives the bound from the sampled point and the rules); *target* for repeats, procs and chance caps, which no instrument samples yet, and for the single budget.
4. **Determinism.** Every roll draws from a seeded stream. Reading a bank, a forecast, a hint or an explanation must change nothing. Seeded replay *holds* within the tested platform and runtime (the morph test runs each point twice; the standard duels hash against their platform's baseline). An identical tournament with explanation on and off remains a *target* until a differential check runs both modes; replay alone does not prove that claim.
5. **A target is an aim, never a guarantee.** Each application re-tests position, arc and line of sight when it resolves; the weave's phasing is the only wall skip. *Holds* for deliveries (the release-time checks); *target* where a release check is a form field that may omit line of sight.
6. **Quantities fold, choices patch.** A literal in a graph is a frozen axis. *Target*: the linter and `weave --axes` (the baseline of "What an axis is").
7. **Execution and forecast share one plan.** A warning and a hit read the same resolved plan; a warning states its certainty and never promises a hit or safety. *Holds* for the deliveries with a forecast provider (the forecast tests); *target* for the deliveries without one.
8. **A use has paid before anything resolves.** A press or a trigger pays its cost and cooldown before its first resolution; a sequel, follow-up or meta is a resolution of the use that paid, not a use. Interrupting a hold cancels its terminal discharge and future held resolutions; channel pulses that already resolved remain paid resolutions of that use. *Holds* (the runtime tests).
9. **Steady state stays inside the budgets of [performance.md](performance.md)**, and the delivery systems allocate nothing per tick. *Holds* (the bench).

## The playstyle space

An archetype is a setup verb, a payoff verb and the economy between them. The rows are the archetypes the content is built to support; the columns are the mechanic families a cast can carry: flights; areas and chains; fields, auras and tethers; placements (scatter, cascade, delay, detonate); displacement; status coupling; banks; held and timed modes; causality (triggers, procs, repeats, sequels); proxies; toggles.

| Archetype | Its economy |
|---|---|
| Banks and vents | charges, uses and brim gained by acting, spent in a burst |
| Holds and releases | exposure traded for a scaled release |
| Marks and detonates | stacks written on a victim, paid out by a second cast |
| Spreads and infects | crowds and deaths carry a status onward |
| Commands and sacrifices | minions and corpses as currency and as hands |
| Pulls and punishes | position forced, then struck |
| Places and denies | area over time the foe must respect |
| Guards and answers | the foe's tempo turned into a reply |
| Echoes and cascades | depth and chance multiplying one use |
| Strides and strikes | angle and cooldowns traded through movement |
| Reserves and sustains | a reservation held for a standing effect |
| Wards and mends | allies as the targets: shields, heals and domains that others fight under |

Each archetype has defining cells and supporting cells in that matrix. A defining cell holds at least three exemplar weaves that differ on two axes or more as `weave --axes` reports them, each played by a doctrine in a scenario test; a supporting cell holds at least one. *Target*: the archetypes, their defining cells, the payoff that counts as engagement for each, and the proving pairs are content the harness reads; the coverage is measured by the harness and recorded in the review record of each pass. This page keeps only the rows, the columns and the targets. Prototype content is ported only as exemplars where the matrix shows a gap.

Every mechanic has at least two counters a player can execute, from five families: avoid it in space, interrupt it in time, mitigate its numbers, strip its state, or punish its commitment. The current table of counters, with the empty cells, is kept in the review record of the pass that last measured it.

## The AI as a player

The AI is a player of this grammar, not a system beside it. Its inputs are what a client could show, what the compiled weave states (mechanism probes, the resolved plan, costs, banks, holds) and doctrines authored as content. Its outputs are validated uses and moves. [ai.md](ai.md) describes the behavior machine as it stands; this section states what any AI in this engine must and must not be.

A decision is made of registered parts:

- **Affordances**: the verbs a weave offers an actor, emitted at compile time by a provider registered beside each mechanism probe or casting block (begin, release at a fill or stage or bash, press in a window, aim, toggle off, reload through a face, spend a bank, summon while there is room, command, move to a landing point), with derived facts (reach band, root seconds, costs, delivery family). A new mechanic registers its probe, its provider and its perception names; the chooser does not change.
- **Perception**: registered names computed lazily when a doctrine reads them, each read recorded for explanation. A name's value is what a player's session standing in that body's place would receive on the wire, after the actor's reaction delay; a name that reads more declares itself a trait, and a doctrine that uses a trait names it, so an elite's sharper senses are authored and telegraphed, never assumed.
- **Options**: an affordance bound to a target, a point or a direction this think. **Vetoes** are the runtime's own pure checks (readiness, cost, gates, forbidden tags, the target kind and its requirement, reach by forecast), each refusal keeping its reason. **Considerations** are registered scorers over an option, each shaped by a named curve from the expression library and weighted by the doctrine.
- **Doctrines**: a content kind. Per-verb consideration lists with curves and weights, reserves, temperature, reaction delay and a movement section. Every doctrine field is an expression over the actor's attributes (`ai.*` among them), so statuses and passives bend minds through the one modifier engine and no doctrine number is a constant a modifier cannot reach. Behaviors stay the outer mode machine and name a doctrine when engaged.
- **Explanation**: every decision can publish an event carrying the wake reason, the target and why, the perception read, the chosen option with its score terms, the runners-up, the vetoed options with their first failed check, the movement advice and the outcome. A rule turns publishing on; the decision is the same either way. It is never on the wire by default; the fight trace and the tournament read it.

The AI must never:

- **read what a client could not show**: random streams, unapplied aims, another body's cooldowns, banks or numbers the wire does not carry, forecasts of casts not yet begun, except through a declared trait;
- **bypass validation**: a body a client could drive acts through the intents a client sends; a body no client drives (a construct, a trap) acts through the same validated entry a handler would use, never through raw effect execution; what the AI cannot express as a use or a move (facing, a leash, a dismissal) is a rule of the simulation, not a decision; difficulty is content and modifiers, never extra rolls, free costs or hidden stats;
- **carry bespoke code**: no switch on a pattern, weave, form or glyph id; a new mechanic becomes playable by registering an affordance, a perception name or a consideration and by writing content;
- **break determinism or the budget**: randomness from the simulation's per-entity streams, early exit on a veto, a think cadence that is content and that the harness pins (throttling by distance from a player is a rule the harness sets to none).

## Measurement

Combat is measured, not asserted. The harness ([evaluation.md](evaluation.md)) runs seeded fights over pairings from content and reports what the simulation's own events say happened. What it reports today, per pairing: the win and draw rates, the median and quartiles of time to kill, each side's kit coverage (the share of its seats it used, a use being a press or a trigger) and spread (the effective number of seats it used, over its uses), the mechanisms used, and the bands broken; and for the standard duels, the trace hash against the baseline. Bands are content, in the rules group the harness reads, written before the run they judge.

What the passes add, each with its own definition when it lands:

- **Scenarios and archetypes**: fights in arenas with walls, crowds and allies, and against the world's packs, elites and a boss, aggregated by archetype with intervals; in those, a proficiency score that averages the win, the time left, the life left and the foe's life taken.
- **Damage taken per kill**, as medians, spreads and high quantiles.
- **Engagement**: the share of fights in which the archetype's declared payoff happened, read from the events the content names. Its control is **restricted play**: the same pattern with the mechanic's weaves removed from its kit, or its doctrine forbidden the verb; how often it beats the full build. Near one half, the mechanic does not matter.
- **Decision entropy**: the entropy of the AI's choices over a fight, reported as an effective number of choices (distinct from spread, which is over a side's uses of its seats).
- **Win-rate band and counter cap**: each archetype's share of wins against the field inside its band, no matchup past a cap, and mirrors at one half as the harness's own check.
- **Effective archetypes**: the effective number of viable archetypes, from the equilibrium mass over the win-rate matrix summed by archetype, so a duplicated build does not count twice (distinct from kit coverage, which is per side).
- **Causal fan-out**: the most resolutions one use caused.

The gate order is fixed: engagement with its restricted-play control comes first, because until the AI demonstrably plays each archetype's mechanic and withholding it demonstrably costs, win rates and the effective archetypes measure the AI's noise rather than the design. Until then, a change is gated on breaking no band it did not find broken.

People measure what the harness cannot. A playtest of a build uses a validated player-experience inventory plus five items of this game's own: I saw what the enemy was about to do; I could name its archetype, scored blind against the declared one; my last two builds played differently; when I lost I knew what I could have done; I want to try another build. The protocol and its thresholds join [evaluation.md](evaluation.md) when playtests begin. The evidence that the design is right comes from outside the team: blind archetype naming above chance, harness power and perceived power agreeing in rank, players' event logs falling inside the AI's distributions, and someone outside reproducing the numbers from the seeds.

## Extending the grammar

A new axis is added end to end, or not at all: an attribute with a base, a unit and a tag scope in the attribute catalog; its binding on the handler's signature (today in `AxisBindings`); a probe if it is a capability a glyph may gate on; a row in the morph test's table for its family; the linter's knowledge of the attribute it replaces; a forecast provider if it changes where or when a resolution applies; an affordance or a perception name if the AI can play it; a presentation decision in the cast-visual coverage manifest; and the contract of its system, with this page when the axis is a new kind of thing. A later layer or a mod may register new words, probes, handlers, curves and doctrines and may replace or patch definitions; it may not change what a unit or a dimension means.

## Proving a change

Every combat change shows, beyond the gates of `CLAUDE.md`, in its pull request and its review record the following evidence. An instrument becomes required when its implementation lands on the change's base; until then the record names it as pending and supplies the available gates and duel evidence.

- `hollowwake validate` with no errors and no new warnings;
- the standard duels (`hollowwake tournament --standard --check-hashes` against the committed baseline) identical to the recorded baseline, or the record naming each changed trace and the rule it serves;
- the morph test passing, with any new axis added to its tables;
- the linter adding no smell beyond its baseline, or the record naming each one it adds and why;
- the proving pairs breaking no band they did not break before, with the mechanisms each side used before and after;
- the bench inside its budgets;
- the contract of the system it touched updated with it, and this page when it changes a rule, an invariant or a mark.

Each slice of the work proves one dimension or one doctrine end to end, from content through compiler, runtime, forecast and AI to the harness, and ends with a review record under `docs/reviews/` in the form [COLLABORATION.md](../COLLABORATION.md) gives.
