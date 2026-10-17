# Cast forecasting

Status: bounded design and executable experiment. Production integration is proposed. This chat owns only this document and `experiments/cast-forecasting/`; it does not own core, protocol, server, clients, content, or the earlier visual studies. No commits, pushes, publishing, or messages to another chat were performed.

## Player contract

Every actor uses the same readable preparation and delivery rules. A warning answers **where an attack can apply, when its next application is expected, and which parts can still change**. It never promises that an actor inside will take damage or that a body outside one warning is safe from other attacks. Moving beyond the tested region avoids that delivery. Remaining inside lets armor, resistance, guard, evasion and other build mechanics resolve through the existing damage pipeline.

An attack's presentation has independent preparation, emission, delivery and outcome lifetimes. Reaching the end of a bar is not evidence of emission. Emission is not evidence of contact. A failed or interrupted preparation must not show an impact. Projectiles and fields may continue after the cast ends. A later interruption never erases a previously emitted payload unless its actual delivery rule cancels it.

Use **Flame** as the player-facing Thread name, associated with **Fire**. Keep persisted IDs such as `ember`, `ember_bolt`, attribute scopes and tags compatible. The experiment demonstrates a display mapping; it does not rename gameplay keys. Display names must come from resolved content/localization, not string replacement in identifiers or messages. Existing documents and read-only references may still use the older name.

## Inspected implementation and limits

The following describes source inspected on 2026-10-17, including the coordinator's in-progress lifecycle additions. The checkout was changing during this work; evidence records the compiled probe's module ID and a digest of core sources and content. A Git HEAD alone does not identify these uncommitted changes.

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
| `CastPresentationEvent`, `CastCueMsg`, `World/CastCues.cs` | New opt-in `cast_cues`: sequence, cast ID, phase, source/target, identity/tags, server times, positions, aim point, fired/reason. Interest entry emits `sync`; hidden targets are redacted. | Reuse confirmed lifecycle correlation. These messages do not yet carry forecast shape, explicit committed direction, policy, revisions, surface clipping or a complete forecast baseline. |

At inspection `CastPresentation.Present` derives its aim **point** from target/point, not `CastState.Direction`, and `sync` intentionally uses the source point. Do not calculate a committed cone bearing from those points. The new geometry seam must serialize the actual resolved direction separately. Reinspect this before integration because the coordinator is editing it.

The current lifecycle phases observed in source are `start`, `sync`, `release`, `impact`, `end`, `interrupt`. `Fired` records whether this cast has emitted any release, not a pulse index. Impact currently requires positive, unblocked, unevaded damage; blocked, evaded and other combat results still use `CombatEventMsg`. Do not interpret absence of impact as a miss, derive damage amounts from it, or turn every `end` with `Fired=true` into another release. The experiment verifies lifecycle correlation for its direct-area scenarios only, not every delayed or triggered effect path.

The client shape adapters currently fall back to a circle for unknown names and apply some minimum dimensions. That fallback is unsuitable for an exact warning. Unknown/unrepresentable forecast shapes must become an explicitly uncertain envelope or a body timing cue; never silently paint a precise circle. Add parity checks at boundary points, including tiny widths, extreme inner fractions and aliases.

## Configurable contract

The [example configuration](../../experiments/cast-forecasting/forecast.example.json) is a proposal, **not loadable production content**. Content selects a registered forecast provider/profile on a form or effect; glyph patches compose those fields through the existing compiler. Rendering profiles cannot change hit rules. Do not introduce a parallel attribute or formula engine.

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
| `locked_on_start` | Direction commits with accepted start. Current timed Cleave. It says nothing about anchor or dimensions. |
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

Use the coordinator's `cast_cues` lifecycle for confirmed start/release/end/interrupt/impact. Add negotiated `cast_forecast_v1` state separately, or extend messages only after that owner agrees. Keep `CastId`; introduce an explicit zone epoch or an equally strong connection/world-entry reset guarantee. Do not identify a cast using only `(source, weave)` because repeats, reflex casts and delayed contacts may overlap.

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

Relation comes from source attribution and the actual delivery filter relative to the viewer, including ownership, summons, allied harmful `all` effects, PvP and changing allegiances. Thread grain conveys identity only. A Flame effect can help or harm. Ally is not synonymous with harmless; a hostile actor's healing effect is not necessarily a threat to the viewer.

Preserve high-contrast boundaries, timing and direction under particles, sprites, bloom and scenery. Use a dark under-stroke and light edge, outward teeth for harmful regions, inward ticks for beneficial regions, and a separate uncertain pattern. Avoid hue-only meaning. Tracking direction uses an open/dashed marker; a closed chevron identifies confirmed directional commitment. Live-size uncertainty remains visible even with that chevron. Reduced motion retains a steady boundary and linear meter without radial sweep, repeated flashes, shake or pulsing.

Sort warning presentation by actionable relation, deadline, overlap with the local actor and stable ID. Merge fills only on compatible surfaces and semantics; preserve individual critical boundaries/deadlines. Limit decorative effects first. If a density budget is exceeded, show a conservative combined danger envelope, earliest deadline and multiplicity/uncertainty indication; never erase a hazard to meet a cosmetic cap. Keep beneficial, hostile and unknown layers distinguishable, and test grayscale, bright/dark terrain, isometric/shoulder cameras, offscreen sources, bridges and dense overlapping attacks. Unknown geometry must never become a precise harmless icon.

## Actors that dodge or tank

Humans receive the same server-resolved warning information regardless of whether the source is a player, monster, summon or construct. AI can optionally consume this same plan through registered perception/action mechanisms. Give it authored reaction delay, observation limits, navigation feasibility, escape cost and a risk preference; it cannot inspect a hidden future target, unrevealed RNG or a client's future movement.

An avoidance action searches reachable space outside the eligible danger region for the application window, accounting for speed, roots, cast movement and collision. Walking outside is a spatial miss; an `EvadeStage` success is a different outcome and should have a different confirmed cue. A dodge animation grants no invulnerability by itself. Current leap untargetability is a mechanic that its cues must describe accurately.

A tank decision compares authorized damage bounds (when available), current resources/mitigation, other incoming threats and the benefit of holding position or continuing an action. If bounds are unknown it is a policy choice with uncertainty. Do not add an automatic dodge override to player input. The existing AI currently chooses/chases/casts; a forecast-aware avoidance action is a follow-on, not implemented by this document.

## Both client adapters

The authoritative region DTO is engine independent. Godot consumes the existing `UI/Logic/ShapeOutline.cs` and `World/ShapeMesh.cs`; web consumes `world/shapes.ts`. Convert coordinates only in existing `Coords`/`coords` seams. Reuse shape caches/material pools and keep immutable base geometry separate from a per-instance transform/timing record. Neither renderer may evaluate damage or rerun compiled gameplay expressions.

Add shared JSON golden cases for shape name, wire proportions, origin, direction, point membership, expected outline and vertical policy. Run the C# hit predicates and both client adapters against them. Tesselation error must be bounded and reported; an outline is a rendering approximation, not a new collision test. Protect unknown-shape handling rather than inheriting the current circle fallback for critical forecasts.

Expose semantic progress, relation, uncertainty and confirmed ending to optional accessible UI and audio. Reduced motion must preserve the same information. Body animation, sprite clips and Flame VFX may subscribe to lifecycle events while region geometry stays in its own world frame. One profile can serve different render styles without changing area dimensions.

## Executable evidence

Run from the repository root:

```powershell
./experiments/cast-forecasting/run.ps1 -PackageCache 'C:\Users\John\.nuget\packages'
```

The cache argument is optional when the normal NuGet cache is accessible. The isolated project uses the already-required YamlDotNet package and disables package feeds in its own directory. It compiles current core source into its own executable/output folders, loads shipped content, and leaves shared binaries and production source untouched. The wrapper checks that core/content did not change during the run. An unavailable cache is a build prerequisite failure, not a passing scenario.

Inputs: [scenario data](../../experiments/cast-forecasting/scenarios.json), [core probe](../../experiments/cast-forecasting/Program.cs), [proposed contract checks](../../experiments/cast-forecasting/contract-model.test.mjs). Outputs: [actual event/position traces](../../experiments/cast-forecasting/evidence/core-results.json). The fixture uses two brigand bodies through the common runtime; it does not exercise character creation, real AI choices, a network session or either production renderer.

For the read-only replay, run `node experiments/cast-forecasting/serve.mjs` and open `http://127.0.0.1:8794`. The [viewer](../../experiments/cast-forecasting/index.html) scrubs the recorded frames; it has no damage formula, gameplay loop or automated camera motion. It shows numeric damage only after a recorded damage event. Its simple planar outline is diagnostic, not a production renderer. It does not implement the proposed relation/uncertainty grammar or vertical surface projection.

The controls disable crit, evasion, block and regeneration through ordinary modifiers to isolate geometry and armor. They do not set weapon damage or replace the damage expression. The tank case overrides armor to a documented experimental value. Each scenario starts from the same seed and actor creation order at a given tick rate. Different tick rates may yield different damage rolls because existing streams include tick identity; comparisons are paired within a rate.

| Scenario | Required proof |
|---|---|
| Stand through windup | No damage before actual duration; one start, release, impact and terminal transition for one cast ID. |
| Walk out | Real movement leaves the footprint before resolution; release still occurs, no damage/impact. |
| Committed aim | A late accepted aim update does not rotate ordinary Cleave or change the paired seeded hit. |
| Interrupted | Existing stun handling interrupts before release; no payload or damage. |
| Tank | Same seed/position/attack, higher armor, lower but nonzero actual damage. |
| Friendly | Same faction inside the candidate region is excluded by enemy filtering. |
| Body edge | An actor's edge inside but center outside an area does not get hit. |
| Elevated bodies | At 3 units above the anchor the current area may hit; at 5 it does not. These are stationary synthetic bodies, not bridge fixtures. |

The probe's geometry reader intentionally accepts only one direct source-centered deterministic area and refuses other graphs. It uses the real registered shape and core size helpers; it is not the extensible production provider. A production implementation must extract the common plan evaluation from the handler, including every modifier/wrapper and selection condition.

Verification snapshot, 2026-10-17:

- Isolated probe: all 27 real-core scenario/rate combinations passed, including correlated confirmed lifecycle transitions. Ten proposed-contract checks passed. [Recorded output](../../experiments/cast-forecasting/evidence/validation.txt). The wrapper confirmed stable core/content during this run.
- At 20 ticks/second the paired stand and tank cases resolved approximately 9.8600 and 2.7889 damage respectively. Moving out and interruption resolved no damage. These are fixture observations, not prospective damage guarantees or balance targets.
- Replay inspected in the in-app browser: windup at 67%, walk-out before release, unchanged direction after late aim, interrupted preparation without release, and armor with actual post-release damage. Saved captures: [windup](../../experiments/cast-forecasting/evidence/windup.png), [move out](../../experiments/cast-forecasting/evidence/move-out.png), [interruption](../../experiments/cast-forecasting/evidence/interrupted.png), [tank](../../experiments/cast-forecasting/evidence/tank.png). This is visual inspection of the diagnostic viewer, not a production client smoke.
- Full solution build with outputs isolated under the experiment passed with zero warnings/errors. Content validation reported zero errors/warnings. Headless zone load and a five-second brigand/boar duel passed.
- Full solution tests were **not all green** at the concurrent integration snapshot: core 429 passed/1 failed (old `Ember Bolt Codex` expectation versus `Flame Bolt Codex`); client logic 78 passed; server 30 passed/2 failed before the WebSocket test timed out. Server failures were missing `CastCueMsg` in the sample roster and stale generated web protocol. The timeout occurred in `WebSocketTests.ServesTheWebClientBesideTheSocket`; it is not established as a forecast defect. These shared files were left with their owner. Detailed attempt notes: [verification record](../../experiments/cast-forecasting/evidence/verification.txt).

Coordinator update: the later complete casting implementation suite resolves the concurrent Flame-name/protocol failures and passes its live transport tests. See [the current verification record](../reviews/2026-10-17-flame-cast-events.md); the earlier snapshot above remains provenance for this experiment.

Do not use the isolated contract checks as evidence that proposed tracking locks, network repair, bridge clipping, AI avoidance, prospective damage bounds, production performance or client visuals already work. Shared runtime integration and its fresh complete gates remain the coordinator's next responsibility.

## Ownership and integration handoff

Coordinator: Codex chat `01a14ba0-9495-7e43-b914-0a3f92cc24fc`. That chat owns the active confirmed lifecycle and first Flame sequence across core/protocol/server/both clients. This is a reviewable proposal for its next slice, not an agreed shared-file edit or a claim that a message was sent/read.

| Owner / proposed slice | Exact seam and boundary |
|---|---|
| This deliverable | Entire `docs/design/cast-forecasting.md` and `experiments/cast-forecasting/`. No edits elsewhere. |
| Existing coordinator | Current `CastPresentationEvent`, `CastCueMsg`, core release/contact/end correlation, server subscription/interest sync, both lifecycle consumers and Flame display/VFX changes. Finish and validate those before extending the wire. |
| Next core owner, after seam agreement | Extract a pure resolved area plan from `Effects/Handlers/DeliveryHandlers.cs` and `EffectHelpers.cs`; expose it to execution and a registered forecast provider. Extend `CastState`/runtime only for genuine required observation or new mechanics. Do not add delayed commitment mechanics just to draw Cleave. |
| Next protocol/server owner | Add resolved direction, node identity, size/anchor certainty, eligibility and observation/deadline fields; negotiate forecast capability; maintain lifecycle correlation, redaction and interest repair in `World/CastCues.cs`/snapshot seams. Regenerate web protocol. |
| Next client owners | Feed those regions into Godot `ShapeOutline`/`ShapeMesh` and web `shapes.ts`, with safe unsupported-shape behavior, current core coordinates and shared golden fixtures. Keep decorative Flame VFX separate. |
| Follow-on mechanics owners | Explicit vertical/surface eligibility, tracking commitment, delivery-family providers, quantitative damage bounds and optional AI avoidance. Each needs its own scope and tests; none is authorized as a runtime edit by this bounded deliverable. |

**Next safe implementation slice:** after the coordinator accepts the shared plan/DTO seam, support the direct deterministic area provider and expose existing Cleave semantics in a flat two-client arena. Preserve start-locked direction, source-at-release origin, live dimensions, existing filters and center tests. Show a conditional boundary where future dimensions/origin/eligibility can change. Add one content-composed variant using the same provider to prove there is no weave-name branch. Keep all unsupported providers honest with timing-only or envelope cues. No damage estimate is required for this slice.

Acceptance: same committed cone for local player, remote player and enemy; move out before Act resolves; move on release tick follows documented ordering; late aim cannot bend Cleave; source displacement updates origin; a mid-cast size modifier updates the live region; interruption does not flash release; armor reduces the real result; release without a target produces no impact; late join shows elapsed preparation; reconnect/transfer/stale messages cannot resurrect it; unknown shapes fail safely. Run the repository build/test/content gates, core/server loopback and both production client builds/smokes when these shared files change. Bridge and crowded-world rollout waits for their explicit eligibility/clipping and readability gates.

Unresolved decisions: whether future authored attacks use delayed aim commitment; whether size/origin should ever freeze; vertical volume versus same-surface gameplay; supported notice/latency envelope and any input grace; target-cap/random-placement forecast policy; whether prospective damage intervals justify their complexity; quality-budget overload representation and minimum-device cost. The conservative defaults above allow the first slice without pretending those decisions are made.

## Sources read

- Repository [vision](../VISION.md), [architecture](../ARCHITECTURE.md), [combat](combat.md), [deliveries](deliveries.md), [movement](movement-and-z-levels.md), [networking](networking-and-persistence.md), [AI](ai.md), [JSON wire ADR](../adr/0004-wire-format.md), [browser ADR](../adr/0006-browser-client.md), and `CLAUDE.md`.
- Actual source under `src/HollowWake.Core/Weaves`, `Effects/Shapes.cs`, `Effects/Handlers/DeliveryHandlers.cs`, `EffectHelpers.cs`, `Combat/DamagePipeline.cs`, `Combat/Stages`, `World/SpatialGrid.cs`, `World/LineOfSight.cs`, `Sim/SimSystem.cs`, `Sim/Events/CastPresentationEvents.cs`; `src/HollowWake.Protocol/CastCueMsg.cs`, `Messages.cs`; server `Snapshots.cs`, `CastCues.cs`, `ZoneInstance.cs`; both shape adapters; shipped forms/weaves/rules and existing cast/delivery tests.
- Current [visual-language study](visual-language.md) and [sprite-rendering study](sprite-rendering-study.md), read-only. Their anticipation/contact distinctions, quiet nonessential effects and shape-first readability inform this proposal; their rendering/performance results are not recertified here.
- Prior project, read-only: `D:\Games\Claude\ARPG Game - World\AGENTS.md`, `CLAUDE.md`, `docs/design/casting-cues.md`, `combat-threat-cues.md`, `combat-readability.md`. Transferable principles: actual-clock readiness, fixed committed placement, distinct fracture/fizzle, relation-independent identity and no invented defensive guarantees. The older TypeScript implementation is not the new server's authority.
