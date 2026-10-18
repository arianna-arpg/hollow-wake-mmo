# Occupied-home displacement correction

Date: 2026-10-18. PR #32. Prior published source: `08831970d0241656ba6b91a86d1e71b34baf72f5`.

## Review and scope

Claude's [review6255443142](https://github.com/arianna-arpg/hollow-wake-mmo/pull/32#issuecomment-6255443142) and [confirmation6255604301](https://github.com/arianna-arpg/hollow-wake-mmo/pull/32#issuecomment-6255604301) block approval because the swept displacement trace rejects its own occupied start cell. The coordinator requested a narrow correction and evidence before any dependency propagation. The same `DisplacementSystem.cs` blob `7facf5fa3e4a4b47da2a7f094fc136f36f733bc8` exists at #32 `0883197`, #36 `01b2884` and #37 `60e6e8a`; no later dependency already repairs it.

The trace now accepts failed clearance samples only within the cell containing the original position. A resident may move inside and out of an occupied home cell without a false collision. Valid ground samples retain their normal height handling; invalid home-cell samples retain the previous height. Every newly entered cell, including an endpoint exactly on a boundary, still requires clearance. This serves the shared, server-owned movement pipeline and does not change content, force tuning, AI, body geometry or wire shapes.

## Regression evidence

`DisplacementEscapeTests` creates the obstruction before invoking the real `SpawnerSystem`, verifies the resident is born in a cell `CanOccupy` refuses, then checks impulse and repeated sub-cell nudge escape in both horizontal directions, placed and terrain blockers, spatial-index updates and no false collision. Further cases retain the next-wall stop and collision, exact endpoint boundary protection on both axes, and the existing deep-dip far-wall stop for nudges and impulses. The latter is an explicitly named behavior change from the old endpoint-only integrator, not a new change in this follow-up.

The original 16-case escape/boundary suite on unchanged runtime fails 12 and passes 4. The initial test-authoring compile errors are preserved separately, not represented as behavioral evidence. The final focused suite passes 38 cases (18 new and 20 existing movement/physicality checks), including the previously authored short diagonal crossing control. Raw logs will accompany the final gate record.

`deliveries.md` now lists `mass`, `shove.authority` and the `physicality` rule group. `physical-bodies.md` documents the 80 kg reference/base/formula alignment, fixed numeric expression bindings and missing policy-expression load validation. Existing global faction-table coverage and center-distance melee stopping limitations remain as named in Claude's review; this narrow movement repair does not redesign them.

## Integration and verification

Full solution, content, load, duel, replay and proving comparison results are recorded after the runtime commit below. No benchmark is authorized in this pass, so no timing or performance acceptance is claimed. Original evidence is retained. No automatic propagation to #33, #36, #34, #37 or the sibling content requests is performed; the coordinator owns the integration sequence after rereview.

The separate #33 small-body direction-flight blocker remains with #36. Its existing 882-case anatomy/relief matrix includes direction-form Firebolt/Piercing Arrow with a body sampled at press time, plus a moved-target/bystander regression; this does not prove empty-ground horizontal shots can reach a whelp. The hitbox owner confirmed review6255777391 and owner decision6255794640 now require its separately owned short-body/slope-floor and hovered-Glaive-return correction. No competing fix is made here.

Acknowledged by: pending Claude rereview of the published occupied-home correction.
