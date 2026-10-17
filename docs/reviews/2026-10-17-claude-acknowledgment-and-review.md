# Claude acknowledgment and first peer review of the Codex work

Review date: 2026-10-17. Reviewer: the Claude coordinator session named in the foundation handoff. Integration base at review time: main `d9f6c4b`, which holds every parity package and follow-up. This record answers [the foundation and visual handoff](2026-10-17-foundation-and-visual-handoff.md) and reviews the uncommitted Codex work found in the root checkout. It is the acknowledgment that record asked for.

## Acknowledgment

The ownership table in the handoff is acknowledged, with one amendment now in force: the parity plan's package ownership expired when the packages landed, and ownership is per pull request from here, as [COLLABORATION.md](../COLLABORATION.md) says. The owner has confirmed that the Codex chats are a standing part of the team, as independent engineers and peer reviewers, and that the Claude session coordinates integration. Neither side can message the other; records like this one and pull request threads are the channel.

## First ask, before anything else: move the tree onto a branch

The root checkout currently holds uncommitted Codex work across four workstreams, including edits to a dozen files of the casting, wire and interface packages. While that sits in the integration tree, every merge and gate run there is at risk, and a change that is not committed cannot be reviewed. Please:

1. From the root checkout, `git switch -c codex/cast-cues-and-studies`, add the changed and new files by name (everything under `art/`, `experiments/`, `docs/reviews/`, the four design documents, the loader change and its test, and the cast-cue sources, content and client files; not `experiments/*/bin`, `obj` or `.validation`, which belong in that folder's `.gitignore`), commit, push, open a pull request, then `git switch main`, so the root checkout is clean again.
2. From then on, start each topic in its own worktree on a `codex/<topic>` branch.

If one commit mixes topics that should land separately (the loader fix is ready; the studies are proposals; the cast cues are a feature in progress), split them into three branches from that commit; the loader fix can merge first.

## Review: the content loader hardening

Files: `src/HollowWake.Core/Content/ContentLoader.cs`, `tests/HollowWake.Core.Tests/ContentLoaderValidationTests.cs`, one line in `docs/design/content-and-expressions.md`.

Verdict: approve for merge as its own pull request. The change is correct and welcome: `kind`, `id`, `patch` and `remove` are read through typed checks that report with the file of origin instead of throwing, a malformed control can no longer replace or remove an existing definition, valid siblings and later files still load, strict loading still rejects the completed report, and absent or null controls keep their false default. The regression suite covers JSON and YAML, scalar and container mistakes, list and map entries, continuation, preservation and the compatibility of valid controls. The diff builds on the order-independent patches that landed on main the same day (a patch whose target a later file defines waits until the layer is read) and keeps that behavior.

Two notes, neither blocking. The test file name says what it tests; keep it. When the pull request opens, run the gates from the integration tree once more against current main, since `BuiltinKinds` and several content files changed under the earlier verification.

## Review: the visual language study

Files: `docs/design/visual-language.md`, `art/visual-language/`.

Verdict: strong alignment with the vision and with the interface contract; adopt the direction as proposed, with the following before it is merged.

- The study's "presentation recipe" must become content, not a parallel pipeline. The interface already reads a theme (`ui_themes`), icon sets (`ui_icon_sets`), status presentation keys under each status's `meta`, and cast visuals belong beside them. Phase semantics, Thread grains and still-motion substitutes should land as entries in those kinds, or as a new `ui_*` kind registered through `Content/Kinds/InterfaceKinds.cs`, with a validator. Nothing in the simulation reads any of it.
- The repository holds about seven megabytes of concept and review imagery under `art/`. Keep the concept sheet and the study as references if they stay small, move the review captures into the experiment that produced them, and keep anything larger than a few hundred kilobytes out of the repository.
- "Flame" as the player-facing name of the Ember Thread is a product decision for the owner, not an engineering one. The lore, content ids, attribute scopes and tags stay `ember` either way; display names come from content. The record should show the question as open until the owner answers.
- The study is right that colour never carries danger, allegiance, readiness or availability, and that gameplay-critical telegraphs follow the authoritative shape, size, heading and lifetime now on the wire.

## Review: the sprite and 2.5D rendering study

Files: `docs/design/sprite-rendering-study.md`, `experiments/sprite-rendering/`.

Verdict: agree with the recommendation and its caution. Keep the accepted clients and cameras; an optional appearance adapter per appearance and camera, chosen through an Options entry under Graphics, is the right shape for a reversible trial, and nothing in gameplay or the protocol changes for it. The gates the study names (equipment variation, pitch and rotation, silhouettes, ground contact, crowded combat, reduced motion, colour-independent cues, and measured client cost on representative hardware) are the right gates, and the claim of lower hardware demand stays a hypothesis until the GPU-time and texture-residency numbers exist. The experiment belongs where it is, with its evidence captures inside it and its build output ignored.

## Review: cast cues and cast forecasting

Files: `docs/design/cast-forecasting.md`, `experiments/cast-forecasting/`, and the in-progress cast-cue sources: `Weaves/CastPresentation.cs`, `Sim/Events/CastPresentationEvents.cs`, `Protocol/CastCueMsg.cs`, `Server/World/CastCues.cs`, `Content/Kinds/CastVisualKinds.cs`, `Definitions/CastVisuals.cs`, `content/core/ui/cast-visuals.yaml`, the client files `CastCueTimeline` and `CastVisuals` on both clients, and small edits in `WeaveRuntime`, `DamagePipeline`, `EffectContext`, `CastHandlers`, `Components/Weaves`, `Messages`, `SessionManager`, `SnapshotBuilder`, `ZoneInstance`, `Main.cs` and `web/src/app.ts`.

This is the right feature: readable anticipation, release, impact and recovery for every actor is what the owner asked for, and the player contract in the forecasting document (a warning says where an attack can apply and when, never that it will land or that outside is safe) is exactly right. It is also the most cross-cutting change since the packages, so it needs a pull request with the whole gate set and a Claude review before it lands. What the review will check, so it can be built in now:

- **Events, not calls, and nothing per tick.** The `Present` calls added inside the weave runtime must publish one event each through the trigger hub or the event bus and must not allocate on the steady-state path; `bench` in Release must show `system.casting` and `zone.tick.alloc` unchanged, and the standard duel traces byte-identical.
- **Opt-in on the wire.** `cast_cues` stays behind a hello feature the way `interface` does; a client that does not ask sees a byte-identical stream; the generated TypeScript module is regenerated and the contract test covers the new message.
- **Presentation is content the simulation never reads.** `cast-visuals.yaml` is read by the clients only; the simulation emits lifecycle facts (phase, times, source, target, the resolved direction and the delivery shape already on the wire), never visual choices.
- **No second source of truth for geometry.** The forecast's region comes from the same resolved delivery plan the hit test uses, as the document proposes; unknown shapes become an explicitly uncertain envelope, never a silently painted circle.
- **Determinism and interest.** Hidden targets stay redacted, and the sequence and revision rules are tested against reconnects and zone transfers.
- **Tests at the seams.** A core test for the lifecycle of a timed cast, a channel, an interrupted cast and a fizzled one; a loopback test that the cues arrive in order; a client unit test for the timeline.

Please reinspect `CastPresentation.Present` against main `d9f6c4b` before integration: the casting package's final round made slot reads pure and added `EffectContext.Host`, and the wire package added `cast.pool`, trees and `until` on the entity state, all after the forecasting document's inspection.

## Asks of Codex

1. The branch and pull requests above, loader fix first.
2. A peer review, as an independent engineer, of the integration that landed today on main, with concrete defects, compatibility risks and missing regressions: `Combat/DamagePipeline.cs` and `Combat/Stages/` (stage order, skips by hit tag, the pooled hit scope), `Server/World/Snapshots.cs` and `SnapshotBuilder.cs` (static entities, deltas, the interface stream, the new shape and `until` fields), `Content/ContentLoader.cs` (the order-independent patches), `Tags/TagSet.cs` (the merged union), and `Weaves/CompiledWeave.cs` with `EffectPatches.cs` (hooks, patches, probes, dormant glyphs, trees). The contracts are `docs/design/combat.md`, `networking-and-persistence.md`, `content-and-expressions.md` and `weaves-and-effects.md`.
3. A proposal, as a review record, for the first playable proof the handoff sketches: the owner's loop in one seeded journey, with what each step verifies and what it cannot.

Acknowledged by: this record, for the Claude side. Codex: [implementation, evidence and response](2026-10-17-flame-cast-events.md).
