# Cast and attack forecasting

This section is the implemented contract for shared cast forecasting and positional hits. The proposal and diagnostic study below retain explicit follow-on scope. Implementation evidence and integration review live in the [shared forecasting handoff](../reviews/2026-10-17-shared-cast-forecasts.md).

## Production shared forecasting

Every active cast uses `WeaveRuntime.DescribeCast`. It observes the compiled delivery after forms, Threads, glyphs and cast-local modifiers compose. It does not dispatch by skill identity. The area capability and hit resolution share the same resolved geometry and candidate predicate; modifiers change the live sampled region through the existing attribute fold. Every active mechanism supplies timing, with explicit unavailable spatial coverage when its graph cannot be safely described. A primary region never claims the whole effect graph, secondary emissions, damage or future victim selection.

`CastForecastBuilder` caches a pure observation once per entity per simulation tick across viewers. It converts dimensions without snapshot rounding and evaluates current viewer candidacy through the shared predicate. Known-source interest is required; a hidden stored target suppresses geometry conservatively. No hidden target coordinates or inferred future rolls are sent. Positional misses are logged without disclosing a hidden target identity.

Clients negotiate `cast_forecast_v1`, which also grants `cast_cues`. `EnterWorldMsg.forecast_epoch` identifies the zone-instance lifetime. A reliable ordered `CastForecastMsg` contains complete active membership, increasing sequence, sample time, expiry, cast identities, independent emission/readiness/max-hold deadlines, committed-direction policy, optional primary candidate regions, and an explicit omitted count. Changed/new members carry full records; unchanged members reference the exact sequence of their last full record. Both clients refuse a missing revision and report the unknown member until periodic full-record repair. Geometry changes are never quantized away. Complete membership replaces old state at snapshot cadence, including empty frames. End/interruption suppress preparation immediately; a miss or channel release does not terminate an ongoing channel. World entry/reconnect clears old epochs and resets the clock. Despawn removes that source. A local timer never releases a cast. Expired samples lose their spatial outline and retain an explicit delayed/unknown indication until repaired.

Both clients consume the same `ui_cast_forecasts` content for colors, stroke, uncertainty dashes, directional marker, dimensions, labels and segment quality. Timing panels use screen-space font/bar dimensions; boundaries retain world units. They use the existing shape adapters with exact sampled dimensions, bypassing cosmetic defaults and clamps. Tessellation increases with resolved dimensions to bound chord error; an over-budget outline becomes unknown rather than inaccurate. Unsupported shape names decline spatial presentation rather than falling back to a circle. Combat-derived fixtures exercise every built-in shape, including thin hollow variants, against both adapters and the live combat predicates. The registered vocabularies must match. To add a new shape, register its combat predicate and both drawing adapters and extend this parity gate; individual spells and supports need no renderer edits. A custom shape needs a distinct wire name: replacing the collision meaning of an existing name without matching adapters violates the contract.

The primary perimeter stays broken because origin, size, LOS, target caps and requirements can still change. A closed direction marker means direction is committed; an open marker means aim can change. Short vertical markers show the actual upper and lower center-height limits. This is a candidate envelope in the authoritative world frame, not terrain/bridge clipping; wall and surface eligibility are still conditional. The boundary remains readable through scenery, while decorative cast cues keep their own depth policy. Reduced-motion mode retains the same steady boundaries and linear progress with no pulsing or sweep.

Server `cast_forecasts.max_casts` bounds each frame; `valid_seconds` bounds stale knowledge, with at least three snapshot intervals of repair allowance. `refresh_seconds` bounds full-record repair for unchanged members. Overload selects nearest known casters deterministically and displays an explicit additional-casts warning in both clients. This is an overload warning, not a certified combined danger envelope; crowded-world release still requires density/readability work. Geometry meshes are reused while only their authoritative transform changes.

### Current coverage and extension gate

- All current/future active casts inherit lifecycle, timing, expiry, interest handling and the common presentation settings.
- Supported direct area roots inherit actual shapes and modifiers, for melee, spells, players, monsters and summons through the same resolver.
- Already emitted projectiles and fields continue using their authoritative entity shape stream. Preparing an unsupported delivery, dependent multi-root graph or unsafe/random geometry displays unknown coverage; its future travel, secondary scatter/pulses or acquisition targets are not fabricated.
- A new delivery family implements `IDeliveryPlanSource` only if it can describe its semantics safely. Wrappers must preserve sequencing, placement, conditional state and scope. The migration coverage record remains partial until these capabilities exist and tests prove them. A neutral fallback does not satisfy full visual parity.
- Forecasts do not yet include prospective damage, guaranteed survival, per-surface clipping, AI dodge decisions, latency compensation or a comprehensive hazard union under overload.

The integrated review record lists the tested baseline, complete gates and remaining rollout limits. Benchmark `snapshot.forecast.*` measures the actual production builder and JSON serialization separately from entity snapshots, with the active observation count so empty work cannot masquerade as a loaded measurement.

## Original study and follow-on design (historical)

Status: shared-system design with a production core primary-area plan and executable experiment. Cast lifecycle, wire and production client integration remain proposed. See the [foundation handoff](../reviews/2026-10-17-delivery-plan-foundation.md) for current ownership and verification, and the [original proposal handoff](../reviews/2026-10-17-cast-forecasting.md) for its provenance.

## Player contract

Every actor uses the same readable preparation and delivery rules. A warning answers **where an attack can apply, when its next application is expected, and which parts can still change**. It never promises that an actor inside will take damage or that a body outside one warning is safe from other attacks. Moving beyond the tested region avoids that delivery. Remaining inside lets armor, resistance, guard, evasion and other build mechanics resolve through the existing damage pipeline.

An attack's presentation has independent preparation, emission, delivery and outcome lifetimes. Reaching the end of a bar is not evidence of emission. Emission is not evidence of contact. A failed or interrupted preparation must not show an impact. Projectiles and fields may continue after the cast ends. A later interruption never erases a previously emitted payload unless its actual delivery rule cancels it.

Keep **Ember** as the player-facing Thread name, following the later owner decision. Persisted IDs such as `ember`, `ember_bolt`, attribute scopes and tags remain compatible. Display names must come from resolved content/localization, not string replacement in identifiers or messages. The earlier proposal's Flame alias is superseded.

## One casting foundation

Preparation, aim commitment, release, interruption and delivery observation belong to the common weave runtime for every actor and every composed skill. There is no Cleave casting subsystem or Cleave forecasting renderer. Content selects existing casting modes, targeting rules, dimensions and delivery mechanisms; the forecast describes their resolved behavior. Skill, form, glyph and species IDs are identity and test data, never runtime dispatch keys.

Use one lifecycle view for timed attacks, instant actions, charges, channels, toggles, projectiles and persistent deliveries. An instant action has no invented windup. A single-target or nonspatial effect may have a timing/target cue without an area. A beneficial effect follows the same lifecycle with its real relation semantics. Missing geometry support must not require a separate casting method or fabricate a harmful footprint.

Register geometry providers by effect/delivery mechanism and shapes by their established shape registry. One direct-area provider must cover source-centered, point-centered and directional forms, including glyph-modified versions. It reads the compiled graph after mutators, the actual targeting context and the shared attribute fold. Presentation metadata under `meta` may choose art or sound but cannot alter duration, aim policy, hit membership or damage.

A skill-specific mechanical difference is justified only by an explicit skill mutator requirement that existing composition cannot express. First implement the difference through glyph patches, modifiers, hooks and registered mechanisms. If a genuinely new capability is required, add that reusable mechanism with execution and forecast contracts together; the mutator selects it through content. Do not add a skill-name exception, a parallel cast state machine or a duplicate damage formula. Absence of an existing second consumer is not a reason to hard-code the first consumer's name.

Acceptance must span different forms and targeting modes before rollout. Cleave is one regression fixture, alongside a source circle, a ground-point circle, a directional line, a second consumer of the cone mechanism and a compiled glyph that changes shape and reach. Their shared behavior must remain shared as the number of skills grows.

## Inspected implementation and limits

The executable experiment runs against the clean integration baseline identified in its review record. Its output includes the compiled probe's module ID and a digest of core sources and content. The coordinator's unmerged lifecycle work was inspected separately as an integration reference; it is not copied into or required by this branch.

| Existing seam | Observed behavior | Forecast implication |
|---|---|---|
| `Effects/Shapes.cs`, `ShapeSize` | Registered circle, square, triangle, sector/cone, crescent, line/band, bar, arc/wave predicates. Core angles are degrees; `Inner` is a radius fraction. | Use the registered predicate and resolved proportions. Do not define a second Cleave shape. |
| `DeliveryHandlers.Area` and `EffectHelpers` | Area membership tests body centers, adds melee reach for anchored melee, applies placement scale and angle modifiers, filters relation/untargetability, checks line of sight and optional requirements/target caps. Dimensions are evaluated when the effect executes. | A sector is the candidate region, not a guaranteed target list. Live modifiers can change the eventual boundary. |
| `content/core/combat/forms.yaml`, `weaves.yaml` | Cleave is a timed directional area, cone angle 110 degrees, radius from `weave.area.radius`; the base radius is 2.8. Its attack-time and weapon-damage expressions remain authoritative. `gore` composes the same form. | Do not key forecasting or rendering on `cleave`, `gore` or an actor species. |
| `WeaveRuntime.ValidateTarget`, `Advance`, `Run` | Directional targeting clears the target and normalizes initial direction. Ordinary timed Cleave retains it despite later `Aim` calls. Its source center is read at execution. | Current Cleave commits direction at start; it does **not** freeze its world origin or dimensions at start. |
| Held modes | Channels may track aim; held release calls `AimAtRelease`; finite and indefinite holds differ. | A general delayed lock policy is a new mechanic, not something a client can invent from Cleave behavior. |
| `SimPhase`, `ZoneBuilder` | Casting resolves in Act, before movement in Move. Scheduled work runs before systems. | Movement queued for the release tick may occur after the hit. Preserve current ordering in the first slice. |
| `SpatialGrid.Within` and `LineOfSight` | Area queries use an implicit height tolerance of 4 around the anchor. LOS checks structure walls and rising base ground, not an explicit bridge/floor identity. | Never claim a bridge deck protects someone merely because it is a different rendered floor. |
| `Snapshots`, `Messages` | Delivery entities already send shape, resolved size, heading, spin and expiry. Wire angles are radians; wire `inner` is a distance. Remote casts have basic casting/progress state; owner `CastInfo` is richer. | Reuse these units and renderers. Remote clients must not reconstruct missing form/glyph or owner state. |
| Coordinator's unmerged `CastPresentationEvent`, `CastCueMsg`, `World/CastCues.cs` | Proposed opt-in `cast_cues`: sequence, cast ID, phase, identity, times, positions and emission/reason. Interest entry emits `sync`; hidden targets are redacted. These files are absent from this branch's baseline. | After that separate work merges, adapt its confirmed correlation. It still needs resolved forecast geometry, commitment, revisions and complete forecast repair. |

In the inspected unmerged lifecycle, `CastPresentation.Present` derives its aim **point** from target/point, not `CastState.Direction`, and `sync` uses the source point. Do not calculate a committed bearing from those points. Serialize the actual resolved direction separately. Reinspect that independently owned work before integration.

The unmerged lifecycle phases observed were `start`, `sync`, `release`, `impact`, `end`, `interrupt`. `Fired` records any emission, not a pulse index. Impact required positive, unblocked, unevaded damage; other combat results use `CombatEventMsg`. Absence of impact is not proof of a miss, and `end` with `Fired=true` must not replay release. On the clean baseline, the probe verifies existing `WeaveStarted`, `WeaveCast`, `WeaveInterrupted` and `DamageDealt` events. Optional compile-time checks exercise the additional lifecycle only when that source is present; the evidence explicitly reports availability. Passing baseline checks does not validate the unmerged wire contract.

The client shape adapters currently fall back to a circle for unknown names and apply some minimum dimensions. That fallback is unsuitable for an exact warning. Unknown/unrepresentable forecast shapes must become an explicitly uncertain envelope or a body timing cue; never silently paint a precise circle. Add parity checks at boundary points, including tiny widths, extreme inner fractions and aliases.

## Implemented core foundation

`DeliveryForecasts.Describe` observes a compiled effect, or a single-root compiled graph, through the optional `IDeliveryPlanSource` capability. Registered effect handlers supply that capability after the compiler applies glyph patches and hooks. The built-in area handler implements it for all registered shapes, with no skill, form, glyph or species dispatch. New delivery families opt in on their own compiled effects; wrappers must preserve their real placement, timing and variable semantics before they can opt in.

Area execution and observation call the same resolver to produce `ResolvedAreaPlan`. It carries the resolved shape and dimensions, anchor mode and position, normalized direction, relation filter, excluded center target, LOS policy, height tolerance and whether a requirement or target cap can restrict candidates. `ContainsCenter` includes the spatial query's vertical band; `IsCandidate` also applies the real relation, untargetability, exclusion and LOS rules. Execution uses that same candidate predicate before requirements, selection and payload effects. Angles are core degrees and inner radius is a fraction. A sampled plan never freezes future execution: sample again after movement, aim or modifier changes.

`DeliveryForecast.PrimaryRegion` is explicitly **primary candidate coverage only**. It does not enumerate victims, evaluate per-target requirements or random selection, execute payloads, schedule echoes, predict damage, describe secondary scatter/pulses, assign a cast identity, or certify emission time and commitment. Being outside this region says nothing about other graph nodes or secondary deliveries. Multiple roots and unsupported wrappers return no region with a reason instead of treating dependent effects as independent. `UnavailableReason` means unknown coverage, never empty or safe space. Client adapters must still establish that they can represent the returned shape accurately.

Observation first checks every geometry dependency without evaluating it: expressions, attribute base formulas, applicable modifiers, conditions, gauges, single-hop links, resource fractions and flags. Only then does it use the existing attribute fold and resolver. Random expressions, untrusted functions/curves, opaque mounted scopes, dependency cycles/depth overflow, missing inputs and nonfinite dimensions decline coverage. Both branches of a conditional are checked, so some currently deterministic cases conservatively decline. This is dependency analysis, not a second numeric resolver. `Expr.IsPreviewSafe` alone proves only tree-level function purity; it does not prove that referenced attributes are safe.

Custom functions and curves must explicitly opt into the purity contract described in [content and expressions](content-and-expressions.md). Shape implementations likewise honor the existing mathematical predicate contract: dimensions, reach and membership are deterministic functions of their arguments. Evaluate on the simulation's owning thread, with registrations complete before content runs. Forecasting uses a detached effect context so it cannot initialize the caller's lazy time/distance scope. Ordinary attribute memoization is allowed; gameplay state, resources, events, scheduled work and random streams remain unchanged. No background observer or per-tick forecast generation is introduced by this core API.

The earlier ARPG's `skillPreview.ts` requires previews to use engine resolvers, and `warningCues.ts` shares `guardBashGeometry` between warning and application. This foundation adopts that same ownership of geometry. The earlier game's actual-clock readiness, fixed committed placement, separate fizzle/release/contact, defensive outcomes, charge growth and repeated emissions remain integration requirements. They are not inferred from a sampled region or implemented as a special Cleave method. Existing movement and defenses still resolve through their current runtime and damage pipeline.

## Configurable contract

The [example configuration](../../experiments/cast-forecasting/forecast.example.json) is a proposal, **not loadable production content**. Delivery mechanisms select registered forecast providers; visual profiles live under `meta.forecast`. Forms and glyphs compose mechanics through the existing compiler. The provider must observe those mechanics rather than requiring every skill to repeat a forecast definition. Rendering profiles cannot change hit rules. Do not introduce a parallel attribute or formula engine.

Register providers by delivery mechanism, initially `area`. A provider exposes a pure description from a shared resolved delivery plan. Execution and forecasting consume that same plan or the same pure plan builder. Extending to a new shape registers its hit predicate, resolved wire description and client outline adapters; extending to a new effect family registers a provider. Neither operation requires a switch over weave IDs.

### Resolved preparation state

Proposed `CastForecastState` is a reliable, versioned presentation view, linked to the existing cast ID. It is not an intent and never authorizes a hit. All numeric values are finite, server-resolved values in core coordinates (X east, Y north, Z up).

| Field | Contract |
|---|---|
| `zoneEpoch`, `castId` | Unique zone-instance lifetime plus existing cast ID. IDs never identify a new use twice in one epoch. Avoid assuming a zone's small sequence survives restart or transfer. |
| `revision`, `sequence`, `serverTick`, `serverTime` | Per-cast revision, stream ordering/baseline watermark and authoritative observation time. A lifecycle adapter may reuse existing sequence when its ordering guarantee covers both streams. |
| `source`, `weave`, `displayName`, `mode`, `tags` | Authorized identity from existing content. Do not send hidden loadouts, targets or future RNG. |
| `phase`, `reason` | Preparation/hold/recovery/ended state. The lifecycle event remains the authority for why a cast ended. Recovery exists only when the runtime actually has that state. |
| `startsAt`, `endsAt`, `nextEmissionAt`, `validUntil` | Server simulation seconds; nullable deadlines when unknown or indefinite. `validUntil` bounds stale knowledge, not payload lifetime. Finite bars, charge readiness and channel pulse cadence have distinct meanings. |
| `emissionOrdinal` | Confirmed monotonically increasing payload/beat counter; it is proposed and is not existing `Fired`. A baseline establishes the current count without replaying past flourishes. |
| `direction` | Actual normalized planar direction, policy and commitment state/time. Serialize the direction used by delivery, not body yaw or an inferred target vector. |
| `anchor` | Resolved origin plus policy: source now/at release, locked point, tracked target or delivery transform. Source motion and rotation commitments are independent. |
| `dimensions` | Resolved shape dimensions, sample time, revision and whether they are live or frozen. Existing ordinary areas are live until execution. |
| `regions[]` | Stable per-delivery-node/placement identity, delivery kind, shape/size/frame, active window, membership/filter rules and certainty metadata below. |
| `viewerEligibility` | Server-filtered relation/eligibility class and observation time, never guessed solely from Thread or faction color. Unknown is distinct from harmless. |
| `capabilities` | Supported shape/projection descriptions and schema version, negotiated before sending. Unsupported clients keep the old cast display, without a falsely exact region. |

### Direction, anchor and timing policies

These are **simulation policies**, resolved by registered handlers; the presentation mirrors them. Existing semantics must be represented first. New values require content validation and simulation tests before content can select them.

| Policy | Meaning |
|---|---|
| `locked_on_start` | Direction commits with accepted start, as in current ordinary timed directional casts. It says nothing about anchor or dimensions. |
| `track_until_commit` | Server samples accepted aim or perceived target, with a configured turn rate, until a server deadline. On commitment it stores and publishes the exact direction. A client timer alone cannot claim commitment. |
| `track_to_release` | Aim can change through release; communicate that continued tracking. Releasing a held cast may resample aim under existing rules. |
| `per_emission` | Every channel beat, salvo shot or repeated placement has its own aim/plan identity and any associated warning window. |
| `source_at_release` | Origin follows the actual source at execution, including authoritative shove/teleport. A rooted cast does not imply displacement immunity. |
| `point_locked` | A ground point is committed; later target or source movement does not drag the warning unless the effect explicitly specifies follow. |
| `target_tracking` | Origin follows a target while valid; target loss has a declared cancel/last-point/reacquire policy. Never silently select a new victim. |

Proposed lock controls are seconds before release or a normalized windup fraction (choose one, reject conflicting fields), turn rate, aim source, and fallback-on-target-loss. Quantities may use existing attributes and expressions. The example's 0.3-second pre-release commitment and rate are **experiment settings**, not a Cleave balance change. Validate `0 <= lock <= finite duration`; reject a normalized lock for indefinite holds without a finite emission cycle. Define whether a duration change recomputes a future lock; an already confirmed commitment never unlocks accidentally.

At a deadline, resolve pending accepted inputs, interruption and commitment in a documented server order; retain current Act-before-Move ordering initially. If movement-before-hit or input grace is wanted later, make it an explicit combat rule with deterministic replay tests. Do not add client-side backdating or silently rewind a successful dodge into a hit.

### Regions and truth levels

A region is a **potential application region** for one delivery during a time window. It contains:

- `shape`, resolved `ShapeSizeInfo`, origin and heading in the existing wire units; `targetTest` (`center` or `body_edge`) and any authoritative padding semantics.
- Earliest/latest application times, whether this is a single application, pulse, swept path, or an envelope of possible future paths. Times may be unknown. Projectile emission and impact time are different.
- Filter, required predicates and target-cap selection summarized as conditional eligibility. Membership alone does not promise selection, hit chance, immunity bypass, damage or life loss.
- Height rule, relevant surfaces or vertical interval, and LOS/phasing policy. A clipped surface mesh is a drawing adapter; it cannot change the simulation's predicate.
- Independent certainty for direction, anchor, dimensions, schedule, occlusion and selection. Use `committed`, `tracking`, `conditional`, `envelope`, `unavailable` only for the dimension they describe. A locked direction does not make live dimensions exact.

The first provider supports a direct deterministic area. A compiled effect graph traversal must account for wrappers, branch conditions, repeats, delays, cascades, scatter, charge scales, placement scale, glyph patches and triggered child casts. Do not execute the effect graph in a second simulation to obtain a preview. That would spend resources, fire hooks or consume RNG.

For random placement/size, either expose an honest possible envelope or create an authoritative committed plan at the same deterministic sampling point the attack will consume. A forecast must not advance a gameplay random stream or reroll a cached result. Do not disclose unrevealed rolls merely to make a warning look exact. Unsupported conditionals or delivery types downgrade certainty; validation refuses a profile claiming exact coverage it cannot supply.

Extend by mechanism: fields use actual delay/pulse/exposure/expiry and follow rules; projectiles show their current collision shape and a bounded path envelope, widened when guidance/homing can turn; scatter publishes each committed placement or its distribution envelope; cascades preserve individual placement windows; chains show potential acquisition ranges rather than fabricated future targets. Newly spawned delivery entities take over the active geometry without a second overlapping warning for the same node/placement.

### No invented damage

The first production slice should show geometry, preparation, relation and confirmed outcomes. It should not print an exact prospective damage number. `DamageDealt.Total` is an actual resolved damage result; it is not necessarily life removed because pools and other stages intervene.

To support deliberate tanking quantitatively later, add an opt-in **side-effect-free analysis mode of the existing damage stages**, not a client damage formula. It returns a server-authorized interval or `unknown`, the evaluated defender state/time, Threads, possible crit/block/evade branches and pool effects. Random rolls are bounded, never consumed. Each registered stage must supply bounds or mark the analysis unknown; conditional statuses, future buffs, simultaneous hits and unsupported effects prevent a survival guarantee. Revalidate when relevant state changes. An expected value cannot replace a worst-case bound, and a defensive readiness cue cannot promise immunity.

The current experiment proves a narrower statement: with the same deterministic setup and actual attack, remaining inside with more armor receives less nonzero damage. It does not implement prospective damage bounds or prove that the defender will survive every encounter.

## Network and lifecycle adapter

Once the coordinator's lifecycle has merged and been verified, reuse its confirmed start/release/end/interrupt/impact correlation. Add negotiated `cast_forecast_v1` state separately, or extend messages after seam agreement with that owner. Keep the shared cast identity; introduce an explicit zone epoch or equally strong connection/world-entry reset guarantee. Do not identify a cast using only `(source, weave)` because repeats, reflex casts and delayed contacts may overlap.

1. On accepted start, send a current resolved view. A local input may display a distinct unconfirmed aiming preview, but no public cast, cost, release or damage is confirmed until the server accepts it. Rejected intents clear that preview.
2. Update relevant geometry on accepted aim, commitment, movement/displacement, size/rule changes and relation changes. Rate-limit/coalesce unchanged views; commitment and terminal events are reliable transitions. Rate limits cannot turn a tracking estimate into a committed warning.
3. Release transfers the correct node/ordinal to spawned delivery state. A direct area may resolve immediately. Preserve ordering with the correlated combat result but do not require an impact to finish a cast. Impact may precede an end for a direct hit and arrive long after end for a projectile.
4. Interrupt/cancel/death/target loss clear future preparation only according to the actual termination reason. A missing state, visibility loss, timeout or source despawn is not proof of a successful release or a particular failure. Show neutral dissolution for unknown endings.
5. Send a complete active-forecast baseline with a watermark on entry/reconnect and at bounded repair intervals. Joining mid-cast reconstructs elapsed progress and current commitment without replaying old releases. Leaving interest removes presentation. Retain terminal tombstones until the next safe watermark/epoch; never allow a delayed start to resurrect an ended cast.

Use `clamp((estimatedServerNow - startsAt)/(endsAt - startsAt), 0, 1)` only for a finite known interval. A bar at 100% may await confirmation; it must not auto-release. Indefinite channels show ongoing state and separately known next pulse. Charge reaching full is readiness unless the runtime confirms auto-release.

Clock estimation uses existing ping/server-time observations plus a monotonic local clock and a bounded offset estimate. Track uncertainty (RTT/jitter/queue delay) instead of asserting zero latency. Never restart a late cast from zero or speed up animation to manufacture a readable windup. Do not interpolate authoritative boundaries with actor visual delay if that would misplace the warning; use a current authoritative/predicted frame and communicate any mismatch. Keep purely visual actor smoothing separate from the hit footprint.

Document the playable network envelope and measure readable warning time: authored notice minus observation age, clock uncertainty, client scheduling, upstream input time and next server tick. If insufficient, show urgent/late state without extending the authoritative attack. Fairness improvements require authored longer notice, earlier interest, server scheduling or a separately reviewed input policy. Forecasts do not solve latency by themselves.

The [contract model](../../experiments/cast-forecasting/contract-model.mjs) tests proposed revisions, complete baselines, tombstones, clock-based progress and emission deduplication. It is deliberately **not** an adapter for the current `CastCueMsg`; some of its fields do not exist there. Production must bound memory, packet sizes, number of regions and update rates, and negotiate shape support. An unordered reliable transport needs revision-aware buffering; sequence gaps alone cannot imply loss because interest filters skip other actors' events.

## Elevation, occlusion and crowded encounters

Do not project everything onto `HeightAt(x,y)` and call it correct. An actor on a bridge and one underneath share XY but have different surfaces. A forecast needs the source/delivery height rule and affected surface slices. Ground-attached warnings belong on those receiving surfaces; airborne/projectile volumes need vertical information too. Terrain/chunk revisions invalidate stale clipping. Missing terrain yields uncertain coverage, not a safe-looking hole.

The current 4-unit area tolerance and base-ground LOS do not establish deck occlusion. The synthetic elevated-body scenarios intentionally expose that limitation; they are not bridge integration tests. First ship accurate current semantics in a flat acceptance arena. Before general world rollout, either depict all presently eligible vertical slices or implement a separately reviewed surface/volume rule used by both hit resolution and warnings. Do not silently make legacy attacks floor-limited in the renderer.

Walls/terrain that actually block the delivery may clip its warning. Decorative canopy, camera occluders, props and bridges must not be assumed to block hits merely because they block vision. Phase/free-occlusion deliveries retain their true coverage. If exact clipping is unavailable, draw the full candidate envelope with a conditional boundary; do not draw a false safe pocket. Server-side targeting still evaluates current LOS and eligibility.

Relation comes from source attribution and the actual delivery filter relative to the viewer, including ownership, summons, allied harmful `all` effects, PvP and changing allegiances. Thread grain conveys identity only. An Ember effect can help or harm. Ally is not synonymous with harmless; a hostile actor's healing effect is not necessarily a threat to the viewer.

Preserve high-contrast boundaries, timing and direction under particles, sprites, bloom and scenery. Use a dark under-stroke and light edge, outward teeth for harmful regions, inward ticks for beneficial regions, and a separate uncertain pattern. Avoid hue-only meaning. Tracking direction uses an open/dashed marker; a closed chevron identifies confirmed directional commitment. Live-size uncertainty remains visible even with that chevron. Reduced motion retains a steady boundary and linear meter without radial sweep, repeated flashes, shake or pulsing.

Sort warning presentation by actionable relation, deadline, overlap with the local actor and stable ID. Merge fills only on compatible surfaces and semantics; preserve individual critical boundaries/deadlines. Limit decorative effects first. If a density budget is exceeded, show a conservative combined danger envelope, earliest deadline and multiplicity/uncertainty indication; never erase a hazard to meet a cosmetic cap. Keep beneficial, hostile and unknown layers distinguishable, and test grayscale, bright/dark terrain, isometric/shoulder cameras, offscreen sources, bridges and dense overlapping attacks. Unknown geometry must never become a precise harmless icon.

## Actors that dodge or tank

Humans receive the same server-resolved warning information regardless of whether the source is a player, monster, summon or construct. AI can optionally consume this same plan through registered perception/action mechanisms. Give it authored reaction delay, observation limits, navigation feasibility, escape cost and a risk preference; it cannot inspect a hidden future target, unrevealed RNG or a client's future movement.

An avoidance action searches reachable space outside the eligible danger region for the application window, accounting for speed, roots, cast movement and collision. Walking outside is a spatial miss; an `EvadeStage` success is a different outcome and should have a different confirmed cue. A dodge animation grants no invulnerability by itself. Current leap untargetability is a mechanic that its cues must describe accurately.

A tank decision compares authorized damage bounds (when available), current resources/mitigation, other incoming threats and the benefit of holding position or continuing an action. If bounds are unknown it is a policy choice with uncertainty. Do not add an automatic dodge override to player input. The existing AI currently chooses/chases/casts; a forecast-aware avoidance action is a follow-on, not implemented by this document.

## Both client adapters

The authoritative region DTO is engine independent. Godot consumes the existing `UI/Logic/ShapeOutline.cs` and `World/ShapeMesh.cs`; web consumes `world/shapes.ts`. Convert coordinates only in existing `Coords`/`coords` seams. Reuse shape caches/material pools and keep immutable base geometry separate from a per-instance transform/timing record. Neither renderer may evaluate damage or rerun compiled gameplay expressions.

Add shared JSON golden cases for shape name, wire proportions, origin, direction, point membership, expected outline and vertical policy. Run the C# hit predicates and both client adapters against them. Tesselation error must be bounded and reported; an outline is a rendering approximation, not a new collision test. Protect unknown-shape handling rather than inheriting the current circle fallback for critical forecasts.

Expose semantic progress, relation, uncertainty and confirmed ending to optional accessible UI and audio. Reduced motion must preserve the same information. Body animation, sprite clips and Ember VFX may subscribe to lifecycle events while region geometry stays in its own world frame. One profile can serve different render styles without changing area dimensions.

## Executable evidence

Run from the repository root:

```powershell
./experiments/cast-forecasting/run.ps1
```

Use `-PackageCache <path>` if the normal NuGet cache is inaccessible. The isolated project uses the already-required YamlDotNet package and disables package feeds in its own directory. It compiles current core source and the existing engine-free client shape adapter into its own output folders, loads shipped content, and leaves shared binaries and production source untouched. The wrapper checks that core/content did not change during the run. An unavailable cache is a build prerequisite failure, not a passing scenario.

Inputs: [scenario data](../../experiments/cast-forecasting/scenarios.json), [core probe](../../experiments/cast-forecasting/Program.cs), [mechanism providers](../../experiments/cast-forecasting/ForecastProviders.cs), [proposed contract checks](../../experiments/cast-forecasting/contract-model.test.mjs). Outputs: [actual event/position traces](../../experiments/cast-forecasting/evidence/core-results.json). The fixture uses brigand bodies through the common runtime and actual compiled forms/glyphs. It does not exercise character creation, real AI choices, a network session or either complete production renderer.

For the read-only replay, run `node experiments/cast-forecasting/serve.mjs` and open `http://127.0.0.1:8794`. The [viewer](../../experiments/cast-forecasting/index.html) scrubs the recorded frames; it has no damage formula, gameplay loop or automated camera motion. It shows numeric damage only after a recorded damage event. Its simple planar outline is diagnostic, not a production renderer. It does not implement the proposed relation/uncertainty grammar or vertical surface projection.

The controls disable crit, evasion, block and regeneration through ordinary modifiers to isolate geometry and armor. They do not set weapon damage or replace the damage expression. The tank case overrides armor to a documented experimental value. Each scenario starts from the same seed and actor creation order at a given tick rate. Different tick rates may yield different damage rolls because existing streams include tick identity; comparisons are paired within a rate.

| Scenario | Required proof |
|---|---|
| Stand through windup | No damage before actual duration; one authoritative start and payload resolution. |
| Walk out | Real movement leaves the footprint before resolution; payload resolves with no damage. |
| Committed aim | A late accepted aim update does not rotate an ordinary timed directional cast or change its paired seeded hit. |
| Interrupted | Existing stun handling interrupts before release; no payload or damage. |
| Tank | Same seed/position/attack, higher armor, lower but nonzero actual damage. |
| Friendly | Same faction inside the candidate region is excluded by enemy filtering. |
| Body edge | An actor's edge inside but center outside an area does not get hit. |
| Elevated bodies | At 3 units above the anchor the current area may hit; at 5 it does not. These are stationary synthetic bodies, not bridge fixtures. |
| Different consumers | The same provider supports cone, source circle, point circle and line; changing form does not select a new cast implementation. |
| Compiled mutator | A glyph changes shape and radius through normal compilation. A target outside original reach is hit; a target inside the new hollow center is excluded. |
| Unsupported mechanisms | Projectile and cascade graphs report no exact provider rather than being misrepresented as a direct area. |

The experiment's thin adapter now calls the production `DeliveryForecasts` capability on compiled effects; it contains no geometry resolver or expression parser. The viewer consumes the existing client's outline loops with no skill-name or shape switch and distinguishes the caster from a ground-point anchor. Its `planarInside` diagnostic remains a planar test; actual hit traces and core candidate checks establish height, eligibility and LOS. Primary coverage and unsupported graphs retain the limits of the core API above.

Current commands and results live in the [foundation handoff](../reviews/2026-10-17-delivery-plan-foundation.md). Original screenshots and historical checks remain in the [original verification record](../../experiments/cast-forecasting/evidence/verification.txt). Each record identifies its tested baseline; earlier results from a concurrently changing coordinator checkout are not evidence for this branch.

Do not use the isolated contract checks as evidence that proposed tracking locks, network repair, bridge clipping, AI avoidance, prospective damage bounds, production performance or client visuals already work. Shared runtime integration and its fresh complete gates remain the coordinator's next responsibility.

## Ownership and integration handoff

The coordinator owns the confirmed lifecycle and presentation across core/protocol/server/both clients. This foundation is an independent core slice explicitly authorized by the owner while cast cues and positional hits were still absent from its baseline. It does not claim those prerequisites landed or that another chat acknowledged the handoff.

| Owner / proposed slice | Exact seam and boundary |
|---|---|
| This foundation | Shared area resolution in `DeliveryHandlers.cs`, `DeliveryForecast.cs`, dependency preflight and expression purity metadata, focused core tests, contracts and the experiment adapter. |
| Existing coordinator | Current `CastPresentationEvent`, `CastCueMsg`, core release/contact/end correlation, server subscription/interest sync, both lifecycle consumers and Ember display/VFX changes. Finish and validate those before extending the wire. |
| Next core integration owner | Consume the shared primary-area capability when integrating confirmed cast state. Preserve positional-hit semantics when that separate work lands. Extend `CastState`/runtime only for genuine required observation or new mechanics; no delayed commitment mechanic is needed merely to draw Cleave. |
| Next protocol/server owner | Add resolved direction, node identity, size/anchor certainty, eligibility and observation/deadline fields; negotiate forecast capability; maintain lifecycle correlation, redaction and interest repair in `World/CastCues.cs`/snapshot seams. Regenerate web protocol. |
| Next client owners | Feed those regions into Godot `ShapeOutline`/`ShapeMesh` and web `shapes.ts`, with safe unsupported-shape behavior, current core coordinates and shared golden fixtures. Keep decorative Ember VFX separate. |
| Follow-on mechanics owners | Explicit vertical/surface eligibility, tracking commitment, delivery-family providers, quantitative damage bounds and optional AI avoidance. Each needs its own scope and tests; none is authorized as a runtime edit by this bounded deliverable. |

**Next safe integration slice:** after cast cues and positional hits land and their exact seams are rechecked, expose the common cast lifecycle and this shared area capability in a flat two-client arena. Demonstrate directional, source-centered and ground-point forms plus a glyph-mutated variant through the same provider and renderer. Preserve each compiled form's existing targeting, direction commitment, anchor, live dimensions, filters and center tests. Show a conditional boundary where future dimensions/origin/eligibility can change. Keep unsupported providers honest with timing-only or envelope cues. No new skill-specific casting method or damage estimate is required.

Acceptance: equivalent compiled behavior for local player, remote player and enemy; distinct shapes and anchors through common adapters; move out before Act resolves; movement on the release tick follows documented ordering; late aim cannot bend a start-locked direction; source displacement updates a source anchor without dragging a fixed point; a mid-cast size modifier updates the live region; a mutator changes the plan through composition; interruption does not flash release; armor reduces the real result; release without a target produces no impact; late join shows elapsed preparation; reconnect/transfer/stale messages cannot resurrect it; unknown shapes fail safely. Run repository build/test/content gates, core/server loopback and both production client builds/smokes when those shared files change. Bridge and crowded-world rollout waits for explicit eligibility/clipping and readability gates.

Unresolved decisions: whether future authored attacks use delayed aim commitment; whether size/origin should ever freeze; vertical volume versus same-surface gameplay; supported notice/latency envelope and any input grace; target-cap/random-placement forecast policy; whether prospective damage intervals justify their complexity; quality-budget overload representation and minimum-device cost. The conservative defaults above allow the first slice without pretending those decisions are made.

## Sources read

- Repository [vision](../VISION.md), [architecture](../ARCHITECTURE.md), [combat](combat.md), [deliveries](deliveries.md), [movement](movement-and-z-levels.md), [networking](networking-and-persistence.md), [AI](ai.md), [JSON wire ADR](../adr/0004-wire-format.md), [browser ADR](../adr/0006-browser-client.md), and `CLAUDE.md`.
- Actual source under `src/HollowWake.Core/Weaves`, `Effects/Shapes.cs`, `Effects/Handlers/DeliveryHandlers.cs`, `EffectHelpers.cs`, `Combat/DamagePipeline.cs`, `Combat/Stages`, `World/SpatialGrid.cs`, `World/LineOfSight.cs`, `Sim/SimSystem.cs`, `Sim/Events/CastPresentationEvents.cs`; `src/HollowWake.Protocol/CastCueMsg.cs`, `Messages.cs`; server `Snapshots.cs`, `CastCues.cs`, `ZoneInstance.cs`; both shape adapters; shipped forms/weaves/rules and existing cast/delivery tests.
- Coordinator's unmerged `docs/design/visual-language.md` and `docs/design/sprite-rendering-study.md`, read-only references outside this branch. Their anticipation/contact distinctions, quiet nonessential effects and shape-first readability inform this proposal; their rendering/performance results are not recertified here. The lifecycle source paths above are also separately owned, unmerged references.
- Prior project, read-only: `D:\Games\Claude\ARPG Game - World\AGENTS.md`, `CLAUDE.md`, `docs/design/casting-cues.md`, `combat-threat-cues.md`, `combat-readability.md`. Transferable principles: actual-clock readiness, fixed committed placement, distinct fracture/fizzle, relation-independent identity and no invented defensive guarantees. The older TypeScript implementation is not the new server's authority.
