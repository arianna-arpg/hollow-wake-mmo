# Deliveries

A delivery is how a weave reaches its targets: projectiles that fly, areas that strike at once, chains that leap, fields that linger, placements that scatter or cascade, and movements that carry or shove bodies. Every one is an effect type whose behavior is its parameters plus the attributes of the weave that delivers it; nothing about a delivery is special-cased by weave. Choices (a firing style, a return, a shape) are parameters that glyphs set with patches; quantities (forks, homing, cascade steps) are weave attributes that forms set as bases and glyphs, passives and items modify; riders at named moments are hook lists. Handlers live in `Effects/Handlers/DeliveryHandlers.cs`, `PlacementHandlers.cs` and `MovementHandlers.cs`, their components in `Effects/ProjectileComponents.cs` and `Components/Body.cs`, and the systems that carry them over time in `Systems/ProjectileSystem.cs`, `FieldSystem.cs` and `DisplacementSystem.cs`.

Distances are world units (a cell is one unit), angles are degrees in content and rates of turn are degrees per second, times are seconds. Parameters that default to a weave attribute fall back to a plain number when a delivery runs without a weave (a status's effects, a tool). Whatever a delivery runs later (its hits, hooks, echoes and steps) keeps the slot and host weave of the cast, so effects that address their host, such as restoring its uses or sparing its cooldown, act on the weave that cast it.

## Shapes

Projectiles, areas and fields collide where they are drawn, through one registry of named hit shapes (`Effects/Shapes.cs`, `Shapes.Register` for mods). The client draws a delivery by the same name. A shape is tested in the delivery's own frame: it sits on an anchor (a projectile's position, an area's center) and faces a direction (a projectile's heading turned by its spin, an area's or field's facing).

A shape's proportions are a `ShapeSize`: `radius` (its main extent), `angle` (full angle in degrees), `width` (full width or thickness) and `inner` (the fraction of the radius left empty at the anchor). Each shape reads the proportions it has and supplies defaults for the rest.

| Shape | Placement | Proportions | Defaults |
|---|---|---|---|
| `circle` | centered | radius; inner leaves a ring | inner 0 |
| `square` | centered, turned with the facing | radius is half the side | |
| `triangle` | centered, apex along the facing | inscribed in a circle of the radius | |
| `sector` (alias `cone`) | anchored, opening along the facing | radius, angle; inner leaves only the far edge | angle 90 |
| `crescent` | anchored | a sector's far band | angle 180, inner 0.5 |
| `line` | anchored | radius is its length, width across | width from the weave's `area.width` |
| `band` | anchored | a line | width equal to its length |
| `bar` | centered, laid across the facing | radius to each side, width is its thickness | width a quarter of `area.width` |
| `arc` | centered | a blade bowed forward: part of a circle centered one radius behind the anchor, angle wide as seen from that center, width thick | angle 90, width a quarter of `area.width` |
| `wave` | centered | an arc bent half as tightly | angle 120, width a quarter of `area.width` |

Projectiles test the authored [damage anatomy](physical-bodies.md#damage-anatomy), including the delivery's vertical slab. Round padding measures contact with shape edges and corners, not an enlarged bounding rectangle. Areas default to body centers and may explicitly opt into body contact; fields retain their center policy. Every shape reports its enclosing reach. Body-contact broad phases add live anatomy bounds, so large or resized bodies are not lost to fixed padding.

## Areas

`area` strikes every body inside its shape at once and runs its `effects` (the `on_hit` hook) on each, one level deeper.

Area execution resolves a `ResolvedAreaPlan` and uses its shared candidate predicate. The same resolver is available for observation through the compiled effect's `IDeliveryPlanSource` capability and `DeliveryForecasts.Describe`. This yields current primary candidate coverage only, with the selected contact/height policy, relation, exclusion and LOS semantics; requirements, capped/random selection, payloads and secondary echoes remain execution work. Unsafe or unsupported dependencies decline observation without affecting execution. See [cast forecasting](cast-forecasting.md#implemented-core-foundation) for the purity contract, live sampling and integration limits.

| Parameter | Meaning |
|---|---|
| `shape` | a registered shape; default `circle` |
| `radius` | default the weave's `area.radius` |
| `angle`, `width`, `inner` | the shape's proportions; the angle of shapes that have one is scaled by the weave's `area.angle` |
| `center` | `focus` (the target, else the point), `source` (the cast's start, else the source's body) or `point`; default `source` for anchored shapes and `focus` for the rest |
| `reach` | `center` (default) or `edge`: anchored shapes reach further by the source's navigation clearance radius |
| `contact` | `center` (default, existing height tolerance) or `body` (intersection with authored anatomy) |
| `height`, `elevation` | for body contact, vertical slab height (default source height) and offset above the anchor (default zero); sampled once when resolving |
| `filter` | `enemy` (default), `ally`, `ally_or_self`, `self`, `not_self`, or `all` |
| `include_center` | false spares the context's target |
| `max_targets`, `pick` | over the cap, `pick` chooses: `random` (default, shuffled on the context's stream), `nearest`, `farthest`, `lowest_life`, `highest_life` (by fraction of maximum life) |
| `require` | an expression each body must meet, read with the body as `target` (`target.life_pct < 0.5`) |

Anchored shapes of weaves tagged `melee` reach further by the weave's `melee.reach`. After an area resolves, the weave's `area.scatter` sets off that many aftershocks at random points within its radius, each smaller by rule `delivery.aftershock_scale` and following at `delivery.aftershock_delay` apart, and `area.pulses` repeats the area where it stood every `delivery.pulse_interval`. Echoes run one level deeper and never echo again.

## Occlusion

Deliveries stop at walls. A wall is a cell of a structure that cannot be walked; rising ground blocks a line too (`World/LineOfSight.cs` walks the cells a segment crosses, drawing lines a unit above the ground). An area or a field reaches only bodies with a clear line from where it acts (its anchor or center); a flight that enters a wall ends against it (or glances off while it has bounces). A delivery with `occlusion: free`, or of a weave whose `phasing` is above zero, passes through. Bodies flagged `untargetable` are passed over by every delivery.

## Projectiles

`projectile` launches flights that the projectile system carries.

The flight's Transform height comes from `delivery.projectile_height` and is centered on its Z coordinate, matching the slab both current clients draw. Circular flights without an inner hole sweep continuously against convex damage primitives between tick positions, ordered by first contact then entity key. This catches a tiny body between endpoints without enlarging it. Other delivery shapes retain endpoint collision. Contacts keep the existing hostility, untargetability, hit-ledger, pierce, fork, chain and return policies; chain/return transitions discard the rest of the old path and resume from the contact point next tick. Existing wall/ground checks still own obstacle handling; this is not a replacement terrain collision solver.

Direct caster launches use the body's authored aim elevation, or half its height for a legacy cylinder. The registered projectile `aim` choice selects launch elevation: `horizontal` keeps Z velocity zero; `target` uses a retained body's aim point or the body-relative offset sampled by a direction press, and otherwise aims toward the cursor's ground point with a terrain-relative floor; `point` explicitly aims at the context point plus any sampled body offset. `delivery.projectile_aim` supplies the content default (`target` in the shipped layer). For a free ground shot, `delivery.projectile_ground_floor` holds the flight center at or above that height over the terrain before body contact is tested. Content sets the floor to the shortest statted body's top across both authored anatomy and fallback cylinders; the projectile slab can overlap that body. A body crossed while the shot is still descending toward a farther cursor may remain below its flight and be missed. Body-selected aim continues to aim at that body's actual height. The flight descends toward the cursor and continues along its bearing after reaching the floor, so near input does not shorten its range and smooth slopes remain hittable. This policy does not acquire a body. Its raw integration pitch remains intact for downhill continuation; snapshots separately report the locally constrained motion described in [networking](networking-and-persistence.md#zones-and-interest). Explicit `point`, `horizontal` and gravity choices retain their own trajectories; walls still stop flights.

A released target can keep a frozen center in `SampledTargetPoint` without retaining its entity. Launch bearing and elevation use that center plus `AimOffset` from the actual launch origin; gravity, convergence and guide aim retain the center semantics of an ordinary body target. A committed direction cast has no sampled target center and keeps its chosen planar bearing. Detached copies preserve the sample; explicit re-aiming, retargeting and new placement contexts clear it.

A direction cast samples its body offset on press without retaining a damage target or changing its planar bearing. A moving victim can leave that line and a bystander can be hit. Held casts update their offset when their existing tracking updates focus; explicit streamed points clear it. An ordinary targeted launch keeps its slope after piercing and forks inherit it. A returning flight instead levels any downward pitch at the pierce contact and uses the content floor on the rest of its outward leg, so a low hit does not bury it before the return. A trajectory change discards the unused segment of that tick from both travel counters. A newly consumed chain resets the leg counter; an existing chase that merely pierces does not. Chains and return turns clear the floor before steering to their live destination. Homing, chaining and returns steer toward live body aim elevation; an orbit follows its owner's aim elevation. Gravity retains its lob solver.

Ground placements and explicit point origins use `delivery.projectile_origin_height`. A flight hook supplies its exact world position with `LaunchLift = 0`, so children start there without another ground lift. In-flight positions can be elevated; `on_land` and ground-clamped endings are at ground height, and their children start at ground height too. A targetless hook child keeps its emission elevation instead of treating that position as fresh cursor-ground input. Detached copies, aim wrappers, casts at that same emission point and later salvo beats preserve the inherited launch anchor and lift. New placement anchors reset that fact and use their own ground-placement lift. The original caster, credited owner, cast identity and damage-body dimensions are unchanged. No rendered weapon socket supplies simulation coordinates.

### Launching

| Parameter | Meaning |
|---|---|
| `count` | flights per launch; default the weave's `projectile.count`; an expression, so `randi(2, 4)` rolls on the cast's stream |
| `aim` | registered launch elevation choice; default `delivery.projectile_aim` (`target` when absent); built-ins `horizontal`, `target`, `point` |
| `speed` | ground speed; default `projectile.speed` |
| `range` | how far a leg flies; default the weave's `range`, unbounded when `duration` is given alone |
| `duration` | seconds a flight lasts, whatever its range |
| `pierce` | bodies a flight passes through before it is spent; default `projectile.pierce` |
| `radius`, `shape`, `shape_size`, `shape_angle`, `shape_width` | the hit shape: a circle of `radius` (0.5) unless `shape` names another; `shape_size` defaults to `radius` |
| `fire` | `fan` (default), `volley`, `ring` or `salvo` |
| `spread` | degrees a fan spreads across (15), scaled by the weave's `projectile.spread` |
| `spread_by_aim` | `{ near, far, range }`: the spread runs from `near` to `far` degrees as the aim point moves from the caster out to `range` |
| `volley_spacing` | units between flights of a volley, side by side across the aim |
| `ring_jitter` | degrees a ring may turn at random, rolled on the cast's stream |
| `salvo_interval` | seconds between the shots of a salvo; default rule `delivery.salvo_interval` |
| `origin`, `origin_range` | `caster` (default) or `point`: flights start at the aim point, at most `origin_range` from the caster |
| `gravity` | a lob: the flight arcs to land at the aim point, or at its range when the point lies beyond |
| `look` | what clients draw |

A flight aims at its live or sampled target when it has one, else along the cast's direction, else toward the point. A fan spreads `count` flights across the spread; a volley fires them side by side; a ring sends them evenly all around from the aim; a salvo fires one now and one each interval after, each aimed afresh at the live aim the caster has sent since the salvo began (an `AimIntent`, kept on its weave book and arriving at most rule `casting.max_aim_rate` times a second), else at the cast's target where it now stands, else along the cast's own aim, and stops when the caster dies. Every salvo beat uses the same origin resolver as the first shot; an authored origin-range expression is sampled once for the salvo. Explicit streamed aim clears both the old body lock and frozen sample before resolving the new placement and bearing.

### Trajectory axes

Each axis is a parameter defaulting to the weave attribute of the same name under `projectile.`, so a form sets a base and glyphs raise it from nothing or dampen it. Rates of turn are degrees per second.

| Axis | Attribute | What it does |
|---|---|---|
| `homing` | `projectile.homing` | turns toward a quarry: the cast's target while it is fair game, else the nearest hostile body within rule `delivery.homing_radius`, looked for every `delivery.homing_reacquire` seconds |
| `guide` | `projectile.guide` | turns toward the live aim its caster has sent since it left, else the point it was aimed at |
| `erratic` | `projectile.erratic` | the heading wanders by up to the rate each second, rolled on the flight's own stream |
| `spiral` | `projectile.spiral` | wheels around where it was launched while flying outward |
| `orbit` | `projectile.orbit` | circles its caster at `orbit_radius` (rule `delivery.orbit_radius`) |
| `spin` | `projectile.spin` | turns the hit shape; the path is unchanged |
| `wave` | `projectile.wave` | sways across the path this many times a second, `amplitude` units (rule `delivery.wave_amplitude`) to each side |
| `accel` | `projectile.accel` | units per second gained each second; never below rule `delivery.min_speed` |
| `bounce` | `projectile.bounce` | times it glances off the ground (a lob keeps rule `delivery.bounce_restitution` of its fall) or off rising ground instead of ending |
| `recurve` | `projectile.recurve` | bends steadily, mirrored across a fan so a fan closes in |
| `converge` | `projectile.converge` | bends toward the point it was aimed at until it reaches it |
| `zigzag`, `zigzag_every` | | swings its heading this many degrees one way then the other every so many units (2) |

The integrator is deterministic: the same flight from the same seed draws the same path. Only erratic flights roll, each on a stream derived from the cast's stream and the flight's entity.

### Hits and spent flights

A flight strikes each hostile body once; its ledger remembers whom. A flight with `rehit` (the weave's `projectile.rehit`, seconds) never ends on bodies and may strike one again once that lockout passes. Otherwise each body struck counts against `pierce`; past it the flight is spent, and in order it:

1. forks while it has `forks` (`projectile.forks`): it ends there and rule `delivery.fork_children` children (2) fly on, fanned `delivery.fork_angle` degrees each side, each with every number of its parent, one fork fewer, a fresh leg of range, and its parent's ledger so they spare the body that split them;
2. chains while it has `chains` (`projectile.chain`): it turns toward the nearest fair body it has not struck within rule `delivery.chain_range` and flies straight at it on a fresh leg;
3. turns back when it has a `return`;
4. ends.

`return` is `none` (default), `origin` (back to where it was launched) or `caster` (back to the caster wherever the caster stands, or to its origin when the caster is gone). A flight also turns back when its outward range or duration runs out. Turning clears the ledger, so bodies struck going out are struck again coming back; the way back passes through everything and ends when the flight is home (within rule `delivery.return_arrive`) or after rule `delivery.return_max_seconds`. Lobbed and orbiting flights do not return. Flights with ordinary ground collision end against the ground and rising ground; the free-ground floor and the outward leg after a returning pierce apply the policy described above.

### Hooks

| Hook | When | Context |
|---|---|---|
| `on_hit` | each body struck | the body as target |
| `on_first_hit` | the first body this flight strikes; fork children never run it | the body as target |
| `on_tick` (with `tick` seconds, 0.5) | every beat in flight | no target |
| `on_travel` (with `travel` units, 2) | every so many units flown | no target |
| `on_land` | a lob coming down | no target, at the ground |
| `on_return` | a returning flight arriving home | no target |
| `on_expire` | range, duration or the ground ending the flight | no target |
| `on_end` | the flight ending for any reason but forking | no target |

Each runs where the flight is, facing its heading, with deliveries it starts beginning there, at the flight's depth (one deeper than the cast). A flight that runs `on_tick` or `on_travel` lists keeps its own copy of the cast's variables, so a rotating `cast` turns per flight. Emitters are `on_tick` lists with a `cast` or a nested `projectile`; a carried aura is an `on_tick` area; sequels are `on_end` casts; a flight that strikes again from the body it struck is an `on_hit` cast of the host (`cast { weave: host, at: target }`); catching a returning blade for a charge is an `on_return` list.

### State the systems keep

A flight's entity has a `Transform` (position and heading), an `Identity` (look `look`, tag `projectile`, its caster's faction) and a `Projectile` with the numbers above, the shape and its size, its ledger and its steering state. The projectile system moves it, publishes `EntitySpawned` and `EntityDespawned` with pattern `projectile`, and keeps the spatial index current.

## Chains

`chain` leaps from the struck body `count` times (default the weave's `chain.count`) to another body within `range` (6), never the same one twice, running its `effects` (`on_hit`) on each one level deeper per leap. `pick` chooses the next body: `nearest` (default), `farthest`, `lowest_life` (the most wounded, by fraction of maximum life, as heals want), `highest_life` or `random`; ties keep the nearer body.

## Fields

`field` places a standing area that pulses its `pulse` list (the `on_hit` hook) on the bodies inside every `interval` seconds (0.5) for `duration` seconds (default the weave's `duration`).

| Parameter | Meaning |
|---|---|
| `radius`, `shape`, `angle`, `width`, `inner` | its shape, as for areas; default `radius` the weave's `area.radius` |
| `filter` | who pulses reach, as for areas |
| `follow` | moves with its caster, and ends (running `on_end`) when its caster dies |
| `delay` | lands this many seconds later; until then it is marked and does nothing |
| `exposure` | seconds a body must stand inside before pulses reach it |
| `hit_once` | each body is pulsed once; `rearm_on_exit` lets a body that leaves and returns be pulsed again |
| `pull` | units per second the bodies inside are drawn toward its center (negative pushes) |
| `drift` | units per second it moves along its facing |
| `seek` | units per second it moves toward the nearest enemy within rule `delivery.seek_radius` |
| `rotate` | degrees per second its shape turns |
| `radius_curve` | `{ from, to, curve }`: its radius runs from `from` to `to` times its cast radius over its life along a named curve |
| `domain`, `domain_filter` | modifiers every body inside that passes the domain filter (default the field's filter) carries while it stays, removed when it leaves or the field ends |
| `exclusive` | a key: placing a field with the key ends the caster's older fields with it |
| `segments` | `{ count, spacing, axis }`: a row of fields `spacing` apart, `along` the facing (default) or `across` it |
| `pulses` | ends after this many pulses |

Hooks: `on_land` when a delayed field arrives, `on_tick` once each pulse at its center before the pulse reaches the bodies (emitters), `on_enter` and `on_exit` for bodies passing its filter, `on_end` when it ends. A field faces the cast's direction, else its caster's facing. A field keeps track of who is inside only when it needs to (entering, leaving, domains, exposure, strike-once, pull); others look only when they pulse.

## Auras

`aura` places a field that goes where its source goes: `radius` (the weave's `area.radius`), a `domain` of modifiers for the bodies inside that pass `domain_filter` (default `ally_or_self`), a `pulse` (the `on_hit` hook) on the bodies passing `filter` every `interval` (1), `on_tick` at its center, and `on_enter`, `on_exit` and `on_end`. One aura per source and `key` (default the weave's): casting it again replaces it, or with `toggle` turns it off; a toggled aura lasts until turned off, others `duration` (the weave's). An aura ends when its bearer dies, running its `on_end`. Reserving mana while it runs and paying upkeep belong to the cast modes.

## Tethers

`tether` draws a live line from the source to its target (`link: target`, the default), to the point (`point`) or to the source's owner (`owner`, for constructs and minions). Every `interval` (0.5) it runs its `pulse` (the `on_hit` hook) on the bodies passing `filter` that it crosses within half its `width` (1); it lasts `duration` (the weave's) and snaps when stretched past `max_length` or when a bound end dies or is gone, running `on_end`. Its entity sits at the line's middle, facing along it, its radius half the line's length.

## Placements

`scatter` rains strikes in a disc around the focus. `count` strikes (default the weave's `area.strikes`) land within `radius` (default twice `area.radius`), one every `interval` seconds or all at once. `at: enemies` places the first under the nearest bodies passing `filter`, within `jitter` of where they stood, and the rest at random; `random` (default) places all at random. With `telegraph` each strike is marked that many seconds before it lands. Each strike runs `effects` at its point, one level deeper, with variable `placement.step` its index. The points draw from a stream seeded once from the cast's stream.

`cascade` is a wrapper: it runs its `effects` where it is, then `count` more times (default the weave's `area.cascade`) at points `step` units apart (default one and a half times `area.radius`) along `dir`: `forward` along the facing, `backward`, `axis` (forward and back in turn), `out` (away from the caster) or `random`. Each later placement waits `interval` seconds (0.15) more than the last, plus `interval_step` more each time; is `scale_step` larger and deals `damage_step` more. Glyphs wrap a form's placement in it with a patch (`wrap: { type: cascade }`).

Areas, fields and strikes placed under a cascade or an aftershock multiply their radius by variable `placement.scale`. A telegraphed strike or delayed field is marked by an entity with look `telegraph` (and a radius) that lives until it lands, and by a `PlacementTelegraphed` event.

`aim` is a wrapper that turns the bearing its `effects` are delivered along: by `offset` degrees, by the next bearing of a `sequence` each time the same source runs it (the place in the sequence is kept on the source, so it goes with the body), and by a random turn within `spread` degrees (scaled by the weave's `aim.spread`) rolled on the cast's stream. The point turns about the source with it, and its deliveries go along the bearing rather than at a target. Before releasing a body target, it samples that body's aim offset; an already sampled offset follows the same path. Horizontal offsets turn with the bearing and elevation stays intact. A retained body supplies a frozen target center (`SampledTargetPoint`) and body-relative `AimOffset` before that lock is released. They are independent of placement `Point`, so an identity wrapper preserves a direct projectile's bearing and contact without relocating point-centered areas or point-origin projectiles. An already committed direction stays independent of the sampled body offset and turns only by the authored bearing change. Empty-ground input keeps the rotated bearing and cursor-ground floor policy. The existing launch anchor and lift are preserved; a bearing change is not a new ground placement. Glyphs graft it onto a form's delivery with a wrap patch.

The placement point, sampled target center, body offset and bearing rotate independently by the authored turn. The point is never reconstructed along the bearing: they may differ for a committed cast or a retained body with offset anatomy. Nested turns compose, zero turns preserve placement, and opposite turns restore the sampled point and trajectory. This also applies to area children centered at the point and projectile children originating there.

`detonate` sets off what the source owns: its projectiles in flight, its fields and tethers, and anything else it owns carrying one of `tags` (default `projectile` and `field`), all of them (`pick: all`, the default) or only the one nearest the point (`nearest`), within `radius` of the point when given. Each runs `effects` where it is, then ends: a projectile with its `on_end` as if its flight ran out there, a field or tether with its `on_end`, anything else despawned.

## Movement and shoves

`move` moves a body: the source for `teleport`, `dash`, `leap`, `blink` and `recall`, the target for `knockback`, `pull` and `buffet` (`who` chooses otherwise).

A `teleport`, and a `dash` without a `speed`, moves straight toward the target or point by up to `distance`, stopping before the first cell it cannot stand in. A dash with a `speed` travels there over time instead: the body stops walking and wears flag `dashing`, bodies passing `filter` within half its `width` (1.5) of its way are struck once each by `on_contact` (the `on_hit` hook), `on_travel` runs every `travel` units, and it ends at its destination or against what stops it, running `on_end`; with `phase` it passes the cells placed things block, never walls. A `leap` arcs to the target or point over `air_time` seconds (0.6), rising `arc` units (2) at its middle, landing where a body may stand on anything up to the top of its arc; in the air it wears `leaping` and `untargetable`, and it lands with `on_land` and `on_end`; with `telegraph` its landing is marked for its air time. A `blink` arrives after `delay` seconds (at once without one), with `behind` just behind its target. `mark` stores where its source stands (or the point, `at: point`) under a `key`, and `recall` with the same `mark` key returns there and spends it. Carried bodies are moved by the displacement system, and the movement system leaves them be until they land.

`knockback` (away from the source), `pull` (toward it, stopping a unit short) and `buffet` (a random way, rolled on the context's stream) shove the target. A shove measures from its `anchor`: `from` (default) is where the effect acts, a flight's hit point or a field's center, so a pull on a flight's hit draws the body to where it was struck; `caster` is the source's own position, so a hook on a flight draws toward the caster, subject to resistance and the authored distance; `point` is the original aim point, preserved when a hit moves the context onto a struck body. Shoves ride an impulse on the body's `Motion` that the displacement system integrates on top of the body's own movement: the starting speed targets the physically adjusted distance on clear flat terrain, with integration stopping slightly short at the speed threshold, as it decays at rule `delivery.impulse_decay` per second, and it settles below `delivery.impulse_min`. Knockbacks and buffets include the weave's `knockback` before the common final force ceiling. Every newly entered cell is tested along an impulse or field nudge, rather than only its endpoint. A body already inside a blocked cell may move within and leave that starting cell; the exemption never extends into an adjacent cell. A dip whose far rise exceeds `move.step_up` stops swept travel even when the final destination is level with the start. A shove that runs a body into something it cannot enter (a wall, a cliff, a placed blocker) stops there; at rule `delivery.collision_speed` or more the body collides: the move's `on_collide` list runs on it, a `BodyCollided` event is published and the `collision` trigger kind fires for the body and for its shover. `status` names a status the shoved body wears until the shove settles. Bodies flagged `immovable` are not shoved.

Shove distance is adjusted by the shared [physical body model](physical-bodies.md) before conversion to impulse. Knockback, pull and buffet read victim mass and remaining poise, source authority and the ordinary `shove.authority` fold. Pull clamping follows that adjustment. Direct velocity displacement and voluntary travel do not repeat it. Continuous field pull also uses this shared policy, with immovable rejection and its own per-tick anchor clamp.

Code carries bodies through `DisplacementSystem.Carry` and shoves them through `DisplacementSystem.Shove`, which mark the body `Displaced` so the system visits only bodies it is moving; `SpeedFor` gives the impulse for a distance and `Nudge` moves a body a small validated step (fields pulling bodies in use it).

## Attributes

The delivery channels are declared in `content/core/combat/attributes-delivery.yaml`: the projectile lifecycle (`projectile.spread`, `.forks`, `.chain`, `.rehit`, `.bounce`), the trajectory axes, the area and placement channels (`area.angle`, `area.width`, `area.cascade`, `area.scatter`, `area.pulses`, `area.strikes`, `melee.reach`, `knockback`), `phasing` and `aim.spread`, beside the core ones in `attributes.yaml` (`projectile.count`, `.speed`, `.pierce`, `area.radius`, `chain.count`, `range`, `duration`).

The shared body channels `mass` and `shove.authority` are declared in `content/core/combat/attributes-physicality.json` and read by impulse shoves and continuous field pulls.

## Rules

Rule group `delivery` (`content/core/combat/rules-delivery.yaml`): `fork_children`, `fork_angle`, `chain_range`, `homing_radius`, `homing_reacquire`, `min_speed`, `return_arrive`, `return_max_seconds`, `bounce_restitution`, `salvo_interval`, `wave_amplitude`, `orbit_radius`, `aftershock_scale`, `aftershock_delay`, `pulse_interval`, `seek_radius`, `impulse_decay`, `impulse_min`, `collision_speed`.

Rule group `physicality` (`content/core/combat/rules-physicality.json`) supplies `weight` and `shove` expressions, their mass/poise/authority constants and final scale bounds, and the anchoring pool and broken-state flag. See [physical-bodies.md](physical-bodies.md) for the policy and current authoring limits.

## Mechanism probes

`Weaves/Probes/DeliveryProbes.cs` registers probes glyphs gate on: `returning`, `homing`, `trajectory` (any axis that bends or paces a flight), `chain` (a chain effect or chaining projectiles), `shape` (a shape other than a circle, or `shape:<name>`), `fire` (a style other than a fan, or `fire:<style>`), `cascade`, `storm` (scattered strikes), `telegraph` (delayed or marked placements) and `shove`. The shared probes `projectile`, `flight` (`flight:children`, `flight:spends`), `area`, `surface`, `landing` and `displaces` read the same graph.

## Events

`Sim/Events/DeliveryEvents.cs`: `ProjectileForked`, `ProjectileChained`, `ProjectileBounced`, `ProjectileTurned`, `ProjectileReturned`, `PlacementTelegraphed`, `PlacementLanded`, `DisplacementStarted`, `DisplacementEnded`, `BodyCollided`.

## When the source is gone

Every delivery's effects read its source's stats and weave. A delivery whose source no longer exists (a caster despawned, a minion dismissed while its arrows fly) therefore ends quietly, as delayed effects do: a projectile is checked once a tick before it tests hits and despawns with none of its hooks; a field or an aura ends without its `on_end` and takes its domain off the bodies inside; a tether ends without its `on_end`; scheduled strikes, cascade steps, aftershocks, salvo beats and telegraph marks never land; a collision runs no `on_collide`; a body carried by a move whose caster is gone simply stops. A source that is dead but still lies there is still a source.

## Order and cost

In the move phase, walking runs first, then displacement, then projectiles, then fields, then tethers. The delivery systems allocate nothing per tick in steady state: they iterate the spatial grid in place and copy out only the bodies a shape touches before running effects, rent pooled lists for areas, chains and placements, read their rules once per rule set (rule sets never change in place), and evaluate a launch's numbers once per launch rather than per flight. A flight spent on a hit leaves the spatial index and despawns once.

## Planning around placed blockers

The pathfinder plans around the cells a simulation's placed things block (trees, veins, structures in its blocked-cell index), the cells walking refuses: it never stands in one. A diagonal step may pass such a cell's corner, as walking allows. A goal inside one (a wander point or a home that fell in a tree) is reached from a cell beside it, which counts as arriving, so a search never spends its budget on a cell it cannot enter.
