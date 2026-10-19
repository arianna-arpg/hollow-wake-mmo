# Minions and constructs

A minion is a body that belongs to someone: a summon, a raised corpse, a construct. It fights on its owner's side (relations follow the owner), its kills credit the owner, it inherits what the owner invests in minions, and its AI is ordinary behavior content that follows the owner, takes up what the owner fights and obeys orders. Players, monsters and constructs summon through the same effects, and minions cast through the same weave runtime as everyone else.

Code: `Minions/` (the minion service, the summon recipe, contracts, corpses, crew forwarding), `Effects/Handlers/SummonHandlers.cs` (`summon`, `deploy`, `spawn`, `gather_corpses`, `consume_corpse`), `Effects/Handlers/MinionHandlers.cs` (`recall_minions`, `command_minions`, `minion_cast`, `consume_minion`), `Ai/MinionActions.cs` (minion and construct AI actions, command kinds), `Systems/MinionSystem.cs`, `Components/Minions.cs`. Rules live in group `minions`.

## Units

Distances and speeds are world units (one cell). Numbers ported from the earlier prototype were converted once, at about thirty of its screen units to one of ours, a factor read off body radii and walking speeds; nothing converts at runtime.

## The minion component

`Minion` holds the owner, when the body expires, its **group** (what a summon's capacity counts and what effects and commands address; by default the pattern, or the summoning weave for a pool), an optional **pool group** and **exclusive group**, the key of the summoning weave, its birth order (`Sequence`; the oldest goes first), whether it is a **construct**, whether it **inherits its caster**, and its inheritance, contract, decay and presence. Every summoned body wears the tags rule `minions.tags` lists, every construct those of `minions.construct_tags`.

The **summoning weave** is the context's host when a meta, trigger or `cast` runs the summon for a slot's weave (`EffectContext.Host`), else the running weave. Its key names the bodies' weave and their contract, its tags and modifiers fold their inheritance and pool capacity, and its glyphs board their weaves; a contract binds to the context's weave slot.

## Summoning

`summon` raises bodies for the context's origin. Its parameters:

- what: a `pattern`; a weighted `pool` of `{pattern, weight, tags, behavior}` re-rolled per body (entry tags join the body's tags, an entry's behavior overrides the summon's); or `from_corpse` (`nearest` to the focus within `search_radius`, by default `minions.corpse_search`, or the context's `target`), raising each corpse as its own kind and consuming it; a pattern beside `from_corpse` is the fallback. `raise_as: pattern` instead transforms a found corpse into that pattern, with no fallback birth; it requires `from_corpse` and excludes `pattern`/`pool`. Every repeat or delayed wave must find an available corpse again
- how many and how long: `count` (default `weave.summon.count`, at most `minions.max_per_cast`), `duration` (default `weave.duration`; zero or less lasts)
- how many at once: `max_active` (default `weave.summon.max`) counted over the group; with a `pool_group` the count is over the pool and the capacity is the owner's `minion.max`, based on `max_active` and folded with the weave's tags plus `pool.{group}`, so investments can raise one pool. Past the capacity the oldest bodies are dismissed (despawned, not killed), and one press raises at most the capacity: a count past it raises only what the group can hold, publishing births, running `on_birth` and spending corpses for those bodies alone. An `exclusive_group` dismisses the owner's bodies of the other groups in it, and ends their contracts.
- where: `place` with `at` (`focus`, the default: the target else the point; `caster`; `point`, clamped to `range` from the caster; `target`), and `shape`: `around` (the default: a ring of `radius`, by default `minions.ring`, at a random bearing per body), `scatter` (in a disc of `radius`; `scatter: r` is shorthand), `line` (across the facing, `spacing` apart) or `circle` (evenly around at `radius`)
- what it runs: `behavior`, else the pool entry's, else rule `minions.behavior`
- what it inherits: on unless `inherit: false`
- riders: `on_birth` effects run on each newborn body (the context's target is the body); `on_death` effects run where a body falls when it dies (not when it is dismissed or expires), as its owner while the owner stands, one deeper than the killing hit, with `body_life_max` set; glyphs extend both through the `on_birth` and `on_death` hooks
- terms: `contract`, `decay`, `waves`, `presence`, `replenish` (below)

Every roll comes from the cast's random stream: a single-pattern summon draws one bearing per body, a pool one pick more. Births publish `MinionSummoned`.

### Reusing encounter bodies

A summon keeps its pattern's anatomy, classifiers, base attributes and weave kit. The summon recipe supplies the minion behavior and owner relation; it removes the pattern's wild `Drops` component at birth, including experience, so summons cannot be farmed for encounter rewards. This also applies to contract respawns. A wild spawn retains those rewards. At birth the identity and `faction.*` stat tag use the owner's effective faction. Rule `minions.encounter_tags` removes encounter tag roots and their descendants while keeping anatomy, species and role tags. The pattern definition retains the native wild faction. Public snapshots resolve the same effective owner faction as combat relations. See [entity content](entities.md).

### Inheritance

Rule `minions.inherit` is a table of lines `{ from, attribute, form }`. At birth, and again whenever the owner's attribute version has moved (checked every `minions.inherit_interval` seconds), each line folds the owner attribute `from` in the context of the summoning weave's tags and the body's own tags, with the weave's modifiers (its glyphs') joining the fold, subtracts the owner attribute's declared base, and lands what is left on the minion as one modifier of `form` on `attribute`, under source `owner:{owner key}`. So `minion.damage` (based at one) lands as `damage more` of its excess, `minion.regen` (based at zero) as flat `life.regen`. A line may carry placeholders (`minion.status.{status}.chance`), expanded over the attribute catalog at load. Nothing is copied: the minion's numbers stay its own fold, and a refold touches the minion only when a value moved. Bodies are filled to their inherited maximums at birth.

### Contracts

`contract: { reserve: { resource: expr }, respawn: expr, toggle }` holds the summon: its reservation sits on the owner as flat modifiers on `{resource}.reserved` under `contract:{weave key}`, its weave slot reads `State["contract"] = 1`, its bodies never expire, and each that falls returns after `respawn` seconds times the owner's `minion.respawn_time` (folded with the weave's tags), re-rolled from the pool, at the owner's side (`ContractRespawned`). Casting the weave again ends a toggled contract; replacing the weave in its slot, an exclusive summon of another group and the owner leaving end it too. Ending dismisses the bodies and releases the reservation (`ContractStarted`, `ContractEnded`). A contract cannot also decay, come in waves or replenish.

### Decay, waves, presences, replenishment

- `decay: { delay, fraction, growth }`: after `delay` seconds the body loses `fraction` of its maximum life a second, multiplied by `growth` each second; unmitigable, and the death credits no one.
- `waves: { count, interval, cost_factor }`: one cast raises `count` waves `interval` seconds apart; each later wave pays `cost_factor` of the weave's cost, and the first that cannot be paid ends the rest.
- `presence: [modifiers]`: while a body of this summon lives, the owner's minions of other groups wear the modifiers under `presence:{group}`.
- `replenish: { interval }`: while the weave sits compiled in a slot of a living owner, a body is born every interval when the group has room, with no cast and no cost; a full group never evicts. The minion system looks for such slots every `minions.replenish_scan` seconds.

## Corpses

A dead body is a corpse while it lies there, unless it is a player, a minion or tagged `no_corpse`; it remembers what it was through its identity, so a death adds nothing, and the lifetime system clears it after `combat.corpse_seconds` (`Corpses.Is`, `Corpses.PatternOf`). `summon` with `from_corpse` raises them, `spawn` with `as_corpse` lays fresh ones down without a death, and `gather_corpses { radius }` drags those around the focus into a pile; `consume_corpse { radius }` spends the corpse a cast chose (its target, else the one lying at the point) without raising it, for weaves that burn or hook the dead, and publishes `CorpseConsumed`. A kill hook (`on_kill`) holding `summon { from_corpse: target }` raises the slain.

Two target kinds join the weave runtime's registry: `corpse` (a corpse that can still be raised or consumed) and `minion` (one of the caster's living minions or constructs). A form's `target: { kind: corpse, search_radius, consume }` refuses the weave before its cost is paid when no corpse lies in reach, and aims it at the one it found; since a cast drops a dead target and aims at where it lay, `from_corpse: target` takes the corpse lying within `minions.corpse_target_reach` of the aim. Raising consumes the corpse unless the summoning weave's form says `consume: false`, so one corpse can rise again. A summon with no such form whose search finds nothing publishes `SummonFizzled`.

## Minion AI

Minions think with behavior content like every other AI (`docs/design/ai.md`). The shipped minion behaviors follow the owner, take up what it fights, notice foes near themselves (passing over bodies tagged `prey`), heel back past their leash range, and obey orders; the actions:

- `follow_owner { distance, stop }`: back to within `stop` once farther than `distance` (default `minions.follow_distance`).
- `assist_owner { range, switch }`: take up the owner's foe when it stands within `range` of the owner (default `minions.assist_range`); the owner's foe is its AI's target, else what it last hit or cast at within `minions.assist_memory` seconds; with nothing to fight, turn on whoever last hurt the owner.
- `return_to_owner { stop }`: head back to the owner. The leash recall teleports a heeling minion to `minions.recall_radius` of its owner when it is farther than `minions.recall_distance`, or has made less than `minions.stuck_progress` of headway for `minions.stuck_seconds`, at most once per `minions.recall_cooldown`.
- `obey_command`: step the standing order (below).

Minions perceive `has_owner`, `owner_distance`, `owner_has_target`, `owner_target_distance`, `commanded` and `command_distance`, with the owner's stats under `owner.*`.

## Orders

A command is content: rule `minions.commands` maps names to `{ kind, duration, mark_radius, arrive, pin_radius }`, and kinds are code registered in `MinionCommands`: `assault` (march on the mark and kill whatever holds it, the pinned quarry first; an empty mark reached is done), `hold` (stand the mark and repel what comes until the order expires) and `recall` (break off and keep to the issuer's side). Issuing writes a `MinionOrder` (command, kind, mark, pinned quarry, issuer, expiry, geometry) into each recipient's behavior, dropping its target and plan; each recipient heeds by its `obedience` plus the issuer's discipline (the effect's `discipline` and the issuer's `command.discipline`), rolled on the issuer's random stream. The quarry is the given target when it is a foe, else the nearest foe within `pin_radius` of the mark. Ordered minions are exempt from the leash. `MinionsCommanded` reports how many heeded and defied.

Orders arrive two ways: the `command_minions` effect, and `MinionCommandRequested` events, which the wire's minion command intent publishes and the minion service handles (an unknown command name is ignored and logged).

## Effects over minions

Each addresses the context origin's minions: those of `group` when given; otherwise, when the summoning weave (the host, else the running weave) summons or deploys, only that weave's own, so a summon's meta orders its own retinue; otherwise all of them.

- `recall_minions { radius, group }`: mobile minions blink to an even ring around the owner; constructs stay.
- `command_minions { command, duration, affects, radius, discipline, group }`: orders at the context's point; `affects` is `minions`, `squad` (thinking bodies of the caster's faction within `radius`, by default `minions.earshot`, that answer to no owner) or `both`.
- `minion_cast { weave, at, range, group }`: every addressed minion executes a preset weave at once, free, at the point or at its own nearest foe within `range` (default `minions.cast_range`), with its own numbers at its host's level. It raises no cast of the minion's (`MinionOrdered` instead).
- `consume_minion { count, pick, group, radius, self, of, effects }`: spends minions (`of`: minions, constructs or any; `pick`: oldest, newest, nearest, or `target`, the context's target when it is one of the caster's minions, as a form's `minion` target kind chooses; `self`: the source itself), running `effects` where each stood with `consumed_life` and `consumed_life_max` set, then killing it with no credit (`MinionConsumed`). A minion's own self-destruct is a `minion_cast` of a weave holding `consume_minion { self: true }`.

## Constructs

`deploy` takes the summon's parameters (bar contracts and corpses) plus `lifetime`, `inherit_caster` and `intercept`. Constructs keep their pattern's behavior, stand where they were placed, and are not recalled or commanded; a breakable one bursts through `on_death`, and one that answers blows reads `has("recently_hurt")` in its behavior. Device actions:

- `construct_cast { weave }`: begin a ready weave at the target.
- `spring { radius, weave, consume }`: when a foe comes within `radius`, begin its weave at it; with `consume` (the default) the trap is spent, and goes once that cast is over.
- `echo_owner { damage_scale }`: replay each eligible weave the owner resolves after the echo appeared, from where the echo stands, damage scaled; the lists `minions.echo_excluded_tags` and `minions.echo_excluded_mechanisms` control eligibility. The core excludes summons, constructs, movement and body spending. The registered `spends_bodies` probe finds corpse/minion consumption throughout the graph, including held releases and guard bashes.

An echo subscribes when it is born, before its first think. Each echo keeps its own queue of resolved owner uses, in resolution order, retaining the compiled weave view, actual resolved effect section, initial cast variables and aim even if the owner removes or replaces its slot. Channel releases and guard bashes replay their own payloads; damage rolls are newly seeded for each replay. A think drains the uses already pending when it started; uses triggered by those replays wait for the next think. No older history is copied to a newly born or replacement echo. Death or removal of the echo or its owner clears pending uses. Only behaviors containing the registered `echo_owner` action retain queues; ordinary minions do not. Stun and freeze immediately clear pending uses, including statuses that expire between thinks, and owner uses resolved during that incapacity are discarded. Recovery accepts new uses without replaying a backlog. Other behavior-state pauses retain uses until the action runs or the body is removed.

A device's cast is a real use on its own clock: `construct_cast` and `spring` begin it with the intent any caster sends (see [ai.md](ai.md)), so the weave's uses, charges, gates, target, cooldown and cast time all apply, its cost is paid through the ordinary costs, it shows as the device's cast, and a device begins each weave at most once per `minions.construct_interval`. Devices are free of cost, not of uses, charges or gates: the `device` pattern folds every cost to nothing (a `cost` modifier of 100% less) rather than code skipping it, and a device pattern without that modifier pays from its own pools. A construct that inherits its caster casts with its owner as the origin, and the numbers split: the effects run as the owner, so their numbers are the owner's and kills credit the owner, from where the construct stands; the reach, the cast time and the cooldown are the construct's own; and the cast is the construct's (it is paid and validated, and `WeaveCast` names it). The snare's burst is instant because its weave casts through `snare_blast`, the blast with no cast time: the blast's cast time is a number on its form, which no attribute a preset sets can change, so an instant trap takes a form of its own. An echo's replay is not a new use but a resolution of the use its owner paid for: it runs on the runtime's free path (`WeaveRuntime.Replay`), free of cost and cooldown and with no cast time, as the owner at the echo's `damage_scale` from where the echo stands, replaying only eligible resolved uses; `WeaveCast` names the echo. Root and selector misses also name the echo and retain a unique nonzero cast identity through nested effect contexts, while payload stats and credit remain with the owner. A spent trap's going and an echo's replay are world rules no intent asks for, so they stay service calls. An `intercept { radius, mode, slow }` dome catches hostile projectiles about to reach its radius, before projectiles move: `dissolve`, `deflect` (turned back, now the dome owner's) or `slow` (speed kept at `slow` while inside); `ProjectileIntercepted` reports each.

A paid device use also checks its release policy as the device: its reach and body radius, target conditions, height tolerance and phasing apply at release, and a miss names the device. Only an accepted payload switches to the living owner's effect numbers and credit. Active cast observation samples those same payload stats from the device position through the shared pure origin helper; timing and the forecast caster remain the device. An echo's free replay remains an owner use from the echo's position, with the owner's release numbers.

Queued replays retain the resolved direction, point and body-relative aiming offset. A targeted projectile also retains the target center sampled before the owner's payload runs, so movement during that payload or before the echo thinks cannot silently redirect its launch. The selected body remains available for the ordinary release checks and direct-body payloads. A committed direction keeps its chosen planar bearing, with no target center or body lock; other replays face the saved point from the echo's position. Later salvo shots and authored guidance read input from the physical echo, not from the owner supplying its stats; explicit homing still follows its authored live-target rules. See [deliveries](deliveries.md) for frozen samples, live input and trajectory semantics.

## Crew forwarding

The glyphs socketed in a summoning weave board the weaves of the bodies it raises, wherever they fit. Minions board once the summoning weave has the mechanism rule `minions.forward_requires` names (a resonant key's); constructs board without it, and their copies keep `minions.construct_forward_scale` of the glyphs' modifier values. A glyph boards unless its `forwards` is false; one with a trigger boards only when it says so, and the glyph granting the mechanism never boards. Riders join each weave after its own glyphs in the host's socket order, up to its form's glyph limit; one that does not fit stays dormant there.

## Mechanisms

Probes in `Weaves/Probes/MinionProbes.cs`: `contract`, `pool`, `corpse` (raises, lays down or gathers corpses), `orders` (commands, minion casts or recalls) and `unlife` (decay). The built-in probes answer `summon`, `minion`, `construct`, `periodic_construct` and `engagement`.

## The minion system

Runs in the think phase after spawners and before behaviors: refolds inheritance where an owner's attributes changed, drains decaying bodies, ends contracts whose owner or slot is gone and brings back bodies whose time came, applies presences for owners whose minions changed, lets domes catch projectiles, and looks for replenishing slots. It does no per-minion work beyond these checks, and nothing in a zone without minions or domes.
