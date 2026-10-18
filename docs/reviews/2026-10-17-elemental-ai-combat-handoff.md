# Elemental AI and combat integration ledger

2026-10-17. Content scaffolding on `codex/elemental-entity-foundation`, stacked on fauna PR #34. This is a design handoff, not an implemented AI engine. Read the [fauna AI handoff](2026-10-18-fauna-ai-handoff.md) first. The machine-readable per-body translation is [the provenance ledger](../../experiments/elementals/provenance.json).

## Implemented boundary

The Unbound Elements are a native faction hostile to players, allied internally and otherwise neutral except for hostile relations to tangled and remnant bodies. This is attack permission, not sensory knowledge, temperament or a food web. The prototype describes some elementals as slow to anger; these current aggressive bindings do not implement that temperament. There is no change to AI, collision, networking, elemental interaction or displacement runtime.

All combat uses shipped presets through the normal weave compiler, AI feasibility checks and authoritative effects. Ember uses existing Ember strikes, bolts and blasts; Tide uses Brine Bolt, Tide Bolt and Ice Spear; Storm uses Storm Bolt and Storm Nova; Stone bodies use Claw or Warden Slam. Bloom uses Venom Spit, Umbra uses Shade Bolt and Umbra Field, Lumen uses Lumen Presence, Mind uses Mind Lash, and Void uses Void Lash and Unmaking Pulse. Triune casts separate Ember, Tide and Storm bolts. These are actual capabilities, not a promise of the source kits. Lumen Presence is an existing toggled aura with damage and its existing domain modifier, not a projectile or shield-shred attack.

No assembled golems or legacy `stone_warden` are changed. Geode Shellback is the prototype's low free-form mineral body, not an assembled humanoid. No new summon preset is added; existing generic summoning can use these summonable patterns. Wild rewards are residue, floss and the existing common table. Summons lose wild drops and experience. All bodies are corpseless under the existing rule.

## Explicit translation choices

| Body or group | Current supported kit / body | Deferred source or design intent |
|---|---|---|
| Cinderling, Emberwisp | Ember Strike or Cinder Spit; live coal or ethereal mass | fire-front births, lava immunity/path costs, levitation, coordinated swarm pressure |
| Ember Elemental | Ember Bolt and Ember Blast; rising forked flame form | cinder swarm, fuel seeking, heat escalation, flame propagation |
| Tide Surge | Brine Bolt; water-density curl and basal pool | swimming, liquid flow through gaps, wetness, water displacement, drowning |
| Rime Star, Frost Elemental | Tide Bolt and Ice Spear; solid ice mass | ice armor fracture, melt/refreeze, terrain icing, geometry-aware shard positioning |
| Gale Elemental | Storm Nova; open loops with low mass | flanking orbit, stun claim windows, friendly coordinated burst timing |
| Storm Confluence | Storm Bolt / Storm Nova | conductive target choice, chain value, projectile interception and wind fields |
| Silt Crawler | Claw; loose sediment mass and poise | terrain borrowing, engulfing, sinking, sediment cohesion, splitting |
| Geode Shellback | Warden Slam, authored armor/poise and mineral mass | prototype energy-shield shell/recharge cycle, exposed core phases, stone-spire kit |
| Bloom Spore | Venom Spit; spore bell and tendrils | healing allies, rooting, spreading growth, nutrient sensing, poison immunity |
| Umbra Drift | Shade Bolt / Umbra Field; finite ethereal body, not undead | occlusion-aware ambush, concealment, phasing, source-specific decay interactions |
| Lumen Wisp | Lumen Presence close-range aura | prototype lightning spark and shield-first defense; no shield shred or revelation mechanic |
| Mind Knot | Mind Lash; crystal cage | predictive aim, hallucination, intent reading, mind control |
| Void Ravel | Void Lash / Unmaking Pulse; broken loops and ordinary collision | teleportation, portals, unmaking world geometry, invulnerability |
| Triune Elemental | independent existing bolts of three Threads | shatterchord, reactive attunement, damage conversion after being struck, sovereign phases |

Affinity tags and palette are separate from attack Threads. Explicit non-Stone affinity resistance is 0.35; Ember and Tide bodies also carry -0.15 vulnerability to the opposing Thread. These are ordinary attributes in the existing modifier and damage pipeline, not density-derived defenses or immunities. Stone is armor-mitigated, not granted a new resistance channel. No material adds status or terrain immunity. Tests exercise positive damage through every Thread on every body.

## Physical and tactical questions for the next reviewed AI pass

- Use authoritative dimensions, mass, poise and shared combat forecasts when evaluating approach positions, shove opportunities and escape. Do not derive hit volumes from open loops or painted cores. The separate precise-hitbox chat owns collision fitting.
- Very light ethereal bodies travel far under the existing shove model. Record actual impulse travel and whether path/aggro recovery remains readable before changing mass or rules. Density values are authored game estimates. Heavier water and mineral bodies still move under impulse; they are not flagged immovable.
- The current ranged state machine uses its longest skill range and flees inside 2.5 m. It cannot deliberately close for a nova, hold a choke, predict a moving target or time ally support. Individual skills and whole kits must keep passing actual AI-hit and sustained-resource tests during migration.
- Lumen Presence needs tactical aura positioning and domain value assessment without introducing a second modifier engine. Triune needs action-value comparison using each compiled Thread's real mitigation, costs and travel time, not a custom damage estimate.
- Reactive attunement requires a validated content/state contract, event timing, duration and precedence before a body can acquire it. Resistances and visuals must agree through published state; a color change alone is never proof of a mechanic.
- Flight, swimming and phasing each require a separate movement/targeting contract and truthful protocol representation. Do not promote author intent from `meta.ai_handoff` into simulation configuration.

## Proposed acceptance scenarios

Compare each new planner against the committed state-machine baseline using fixed seeds and the existing runtime: a cold spear caster behind a wall; a storm caster between an isolated target and a clustered group; an Emberwisp pushed beyond aggro range; a Geode Shellback with intact versus broken poise; a Lumen Wisp deciding between proximity to allies and escape; a Triune body against targets with different authored resistance values; a Tide body next to deep water; a Void body blocked by a closed corridor. Assert feasible casts, readable reaction times, exact repeat digests, bounded candidate/path work, and no access to unseen inputs. Keep the fauna handoff's budget and observability requirements.

Claude review requested: approve or revise the substitutions, elemental temperament/faction assumptions, low-mass displacement implications, biome-site constraints and scenario order. This pass supplies bodies and baseline evidence; it does not select or implement the future planner.

Acknowledged by: pending Claude review.
