# Visual convergence from Hollow Wake ARPG

The original ARPG is a convergence requirement, not a disposable reference. Its skills, attacks, supports, defenses and delivery mutations must eventually have equivalent readable presentation in both MMO clients. Preserve reusable visual language and source attribution while expressing it through registries, authoritative events and content. No renderer dispatches on an individual skill, support, Ember or Cleave ID.

## Source and evidence

The read-only capture tool and evidence live in [experiments/visual-convergence](../../../experiments/visual-convergence/). The census imports the actual exported skill/support catalogs, reads delivery/effect/cast-mode schema unions, records nested delivery variations, and preserves casting, combat and defense profile data. It records repository revision, individual source hashes and a before/after-verified TypeScript tree fingerprint, so a dirty working tree is not falsely described as identical to its HEAD.

| Original sources | Inheritance obligation |
|---|---|
| `src/data/castingCues.ts`, `src/engine/castingCues.ts`, `src/render/vis/castingCueLayer.ts` | Common actor-independent cast language; readiness based on actual held state; interruption fracture and fizzle collapse; discoverable timing without false completion. |
| `src/data/combatCues.ts`, `src/render/vis/combatCueLayer.ts` | Distinguish contact, inward restoration, defensive response, harmful application and expiry. |
| `src/data/defenseCues.ts`, `src/render/vis/defenseCueLayer.ts` | Persistent plates, spiral, carapace, brace and guard follow durable state rather than a guessed lifetime. |
| `src/render/vis/effectVoice.ts` and further registering modules | Reuse authored silhouettes and effect voices through shared primitive/sprite adapters. The census's base-module list is not an exhaustive registration inventory. |
| `src/render/vis/aoeTrace.ts` | Damage warnings use the same resolved shapes and timing as simulation, never approximate bounds from names or decoration. |
| `src/data/skills.ts`, `src/data/supports.ts`, `src/engine/skills.ts` | Supports, grafts, trees, sequels and triggers change the compiled result. Inherit presentation from that result and its executed outcomes. |

The current Thread palette is authoritative content. Preserve original motif and material semantics where they fit; the old game's five damage colors are not automatically the MMO's nine Thread taxonomy. Ember remains the display name of persisted `ember` content.

## Coverage by family

[coverage.json](../../../tests/fixtures/cast-visuals/coverage.json) is the machine-readable classification. Each family has a status, concrete coverage statement and explicit missing work. It covers original delivery/effect/cast-mode vocabulary, observed skill/support fields and nested variant paths, plus the current MMO effect handler registry. Categories distinguish metadata, intentional absence of decoration, implemented shared cues, partial coverage and pending families. Counts and source hashes belong in dated evidence.

The implemented foundation composes Thread and mechanism layers, uses paired primitive registries, and carries confirmed lifecycle/contact and outcome events. It covers current presets with a validated shared baseline. It does **not** mean the original catalog is migrated or every mechanic's complete visual meaning is present:

- Mode-specific readiness art and staged timing require shared state bindings.
- Shared active-cast timing, primary area candidate shapes, held-aim policies and spatial misses now use [resolved forecasting](../cast-forecasting.md). Secondary placements, dependent wrappers, arbitrary delivery families and surface clipping remain partial.
- Persistent defenses, stacks, stored resources and transforms require durable state adapters.
- Complex trails, tethers, construct/companion states and authored effect voices require reusable rendering families.
- World interaction, crafting, territory and audio need additional family contracts beyond this combat census.

These are mechanism-sized obligations. Implementing a family must benefit every skill/support composing it. Do not create a backlog row or code branch per catalog item.

## Extension and migration gate

1. Refresh the source census when relying on changed original content. Capture remains read-only and aborts if source files change during capture.
2. Run the audit. New schema discriminants, skill/support fields and recorded nested variant paths require explicit classification. The current effect registry and both primitive vocabularies must remain classified.
3. Identify original profiles/assets and the authoritative event or durable state expressing the mechanic. Preserve provenance. A cosmetic capability accent is not proof that a conditional branch ran.
4. Prefer existing layers, templates and primitives. A novel family needs a shared adapter and both client implementations; mark only verified portions complete.
5. Add appropriate conformance scenarios: players/non-player actors, composed supports, interruption/expiry, unknown vocabulary, reduced motion and truthful timing/geometry. Supply captures and relevant budget measurements for new visual families.

CI audits the committed census; it cannot observe future edits in the separate ARPG checkout automatically. The engineer authoring each migration or visual-family PR owns refreshing the census at that boundary, recording its revision and source-tree fingerprint in the PR, and reviewing the resulting classification changes. The opposite-side reviewer verifies that provenance before accepting a parity claim. A captured fingerprint describes that snapshot only; it does not certify the current external checkout. Passing classification proves no **unclassified captured mechanism**; it never turns a partial/pending family into implemented coverage. Runtime unknowns fall back safely while content/contract review catches missing families before claiming parity.

Run `node experiments/visual-convergence/audit.mjs` from this repository. To refresh, invoke the original repository's installed tsx CLI on `capture-prototype.mjs`, passing that repository root and this experiment's `evidence/prototype-census.json`. No original files are copied back, normalized or modified.

## Coordination

PR #6 owns the confirmed action-event stream, composable cosmetic recipes and convergence gate. The forecasting integration owns the shared resolved delivery, positional release checks, reliable forecast baselines and both-client boundaries; the probe opts into the lifecycle stream when present. Claude's PR #9 owns body appearance, world materials, procedural body animation and camera feel. Preserve both initialization paths in `Main.cs` and `web/src/app.ts` during integration and rerun combined smokes.

The broader mechanics migration remains in [the parity plan](hollow-wake-gap.md), with this contract supplying visual acceptance requirements.
