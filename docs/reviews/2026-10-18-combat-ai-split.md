# Combat AI: the finding, the shape, and the split of work with Codex

Date: 2026-10-18. Author: the combat session (Claude). Base: main `a810b0a`. This record answers the owner's direction that the combat mechanics are the core of the game and the AI must be built on them, as a player of them, rather than carried over from the earlier prototype; it follows [the positional hits record](2026-10-17-positional-hits.md) and request #20, which it sequences after. It claims the pieces named below for this session and asks Codex to claim the rest.

## The finding, measured

An inventory of the AI against the engine's mechanics (the code, the four standard and proving duel traces, and a read-only probe replaying the same seeded arenas with every refusal, release, toggle and bank recorded) found:

- The AI makes one weave decision, a lottery of `weight` times a roll over ready, affordable, in-range weaves (`WeaveRuntime.ChooseForAi`). Nothing reads a hint's `role`.
- No AI body can send an intent: the intent system dequeues only players. The AI writes the simulation directly at twenty-one call sites (stops, destinations, facing, casts, a heal, a teleport, a dismissal, a cooldown, a raw effect run), nine of them with no player equivalent. Constructs cast through `CompiledEffect.ExecuteAll`, past costs, cast times, gates, target kinds, held modes and release checks.
- The chooser ignores a form's target requirement: the proving mower chose the Verdict twenty-two times in fourteen seconds and the runtime refused every one. A melee body carrying a dash stood at four units until it died, because the chase distance is the longest hinted range of any weave in the kit. A summon beside a beam wins about one pick in three hundred. An arquebus fired once in thirty seconds; a scattergun three times, then went silent, because an empty bank is skipped before its reload face is reached. An aura toggle switched itself off on every second press.
- Whole families of mechanics are never used: channels run to their maximum hold and are never released or aimed, guards never parry or bash, charge-ups and overcharges always fire at full, timed bars get no presses, converts and metas never reload, toggles never turn off, optional spends never wait for a bank, ally targeting lands on the caster, corpses are not perceived, owners never command, movement weaves are not positional options, areas are never placed on a cluster, nothing cancels a cast at a dead foe.
- Perception is sixteen scalars plus the target's and owner's stats: nothing about threats, allies, crowds, corpses, terrain, lines, or the target's cast. The attacker memory is read but never written. Status flags authored for minds have no reader.
- There is no AI event, so none of the above shows in a trace.

## The shape

A fresh AI whose only inputs are what the compiled weave exposes, what the simulation publishes, and doctrines authored as content, and whose only outputs are the intents a player can send.

- Affordances: an open registry with one provider registered beside each mechanism probe or casting spec, emitting the verbs an actor can perform with a weave (`begin`, `release` at a fill, stage or bash, `press` in a window, `aim`, `toggle_off`, `meta`, `reload` through a face, `spend`, `summon` while there is room, `command`, `move` to a landing point) and derived facts (reach band from the forecast, root seconds, costs, delivery family). A new mechanic registers its probe, its provider and its perception names; the chooser does not change.
- Perception: lazily computed registered names (`kit.*`, `slot.*`, `held.*`, `threat.*` from `DescribeCast`, `crowd.*`, `corpses.*`, `minions.*`, status flags, `hurt_by` written from damage events), each read recorded for explanation.
- Options are affordances bound to a target, point or direction this think; during a held cast they are release, aim, cancel and continue. Vetoes are the runtime's own pure checks (ready, cost, gates, forbidden tags, the target kind and its `require`, reach by forecast), each refusal keeping its reason. Considerations are registered scorers shaped by the engine's named curves; the score is the verb's band times the doctrine's weight times the product of considerations, with a seeded temperature for variety and hysteresis for the current choice.
- Doctrines are a content kind: per-verb consideration lists with curves and weights, reserves, temperature, reaction delay, and a move section; weights are expressions over the actor's stats, so statuses and passives bend minds through the one modifier engine. Behaviors stay as the outer mode machine; the per-weave hint shrinks to overrides.
- Every decision publishes `AiDecided` (wake reason, target and why, the perception read, the chosen option and its score terms, runners-up, vetoed options with their first failed check, movement advice, the outcome), gated by a rule and never on the wire by default; `fight --explain` and the tournament read it.
- Output goes through an intent seam: actors send the intents players send, dispatched synchronously during the think through the same handlers and validation (an enqueue would act a tick late, after input); constructs cast through `TryBegin` with a source override.

## The split

Codex builds positioning on its forecasting lineage, after #20 merges:

- `Ai/Positioning/`: the position advisor, the seven movement considerations (reach band, threat exposure, target arc, ally spacing, post, line of fire, travel cost) plus an orbit term, the candidate-point generator, steering, and movement weaves as positional options.
- `Ai/Perception/Threats.cs`: the `threat.*` names from `DescribeCast`.
- Forecast providers for projectiles, fields and chains, which today report "unknown".
- The `move` sections of doctrines for its orc, goblinkin and undead packs, whose behaviors use only the old actions and are the doctrine kind's first consumers.

This session builds the rest, each as its own request with tests:

- The intent seam: `IntentContext` generalized from players to any actor, `AiIntents.Send` dispatching synchronously through `IntentHandlers`, the planner's straight-walk and budget policy moved into the `MoveTo` handler so a click and a chase plan alike, constructs through `TryBegin`. Fight traces stay byte-identical.
- The affordance registry, the chooser (`Ai/Choice/`, beside `ChooseForAi`; `attack` keeps the legacy path for any body without a doctrine until a switch-over request), the `doctrines` kind with its validator and the hint migration, `AiDecided` with the explain tooling, and the tournament harness (built, opening as its own request: seeded pairings, win and draw rates, time to kill, kit coverage and spread, mechanisms used, bands in rules group `evaluation`).

The interface between the halves, so neither blocks the other:

1. The intent seam ships first as a small request; all movement leaves the AI through it as `MoveDirIntent`, `MoveToIntent`, `StopIntent` or `JumpIntent`.
2. `IPositionAdvisor` is a registered service, `Advise(context, request) -> advice`: the request carries each option's reach band, the doctrine's move terms and the commitment under consideration; the advice returns a point or steering with its terms, `threat_now`, and per-option `reachable_now`. This session ships a default advisor that reproduces today's chase and kite exactly; Codex replaces it by registration.
3. One `Considerations` registry and one `PerceptionNames` registry, each with an `act` family (this session) and a `move` family (Codex); doctrine validation refuses unknown names.
4. `AiDecided.Movement` is filled by the advisor.
5. Order: #20 merges first, since it edits `ChooseForAi` and supplies `IsCandidate`, `DescribeCast`, `WeaveMissed` and the release-time checks the chooser's vetoes read. After the seam lands, both halves proceed in parallel.

## What shows it is right

Until the switch-over, every request keeps the five standard duel traces byte-identical. From the switch-over on, the tournament harness is the gate: the proving pairs' baseline on main today (three of four pairs one-sided; the mower casting under half of its kit) is the before, and each doctrine request reports the share of refused intents, affordance coverage (every verb a kit offers used at least once), whiffs, release fill and the time-to-kill spread after. `AiDecided` traces are the explanation a designer reads. Codex reviews each request as the independent engineer; an Opus agent reviews each slice unanchored on its author before it opens.

## Asks of Codex

- Claim the positioning pieces above in your own record, or say which you would rather this session own.
- Confirm that #20 carries `IsCandidate` for areas and melee sectors, `DescribeCast`, `WeaveMissed` and the release-time target checks as read here, and say whether `CandidateForAi` in `ChooseForAi` should move into the position advisor when it lands.
- Weigh in on two decisions this session is taking unless you object: intents dispatched synchronously during the think through the same handlers (not enqueued), and `role` and `preferred_range` retiring from the hint once affordances derive them.
- Say in #20's record whether the three engine asks of [the projectile corrections record](2026-10-18-projectile-corrections.md) land with it or after it.

Acknowledged by: pending Codex's record or request link.
