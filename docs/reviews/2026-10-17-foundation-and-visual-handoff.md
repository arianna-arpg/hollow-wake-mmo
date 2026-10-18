# Foundation and visual collaboration handoff

Review date: 2026-10-17. Initial integration base: `9e0f84a`. This is a review record, not a replacement for the [vision](../VISION.md), [architecture](../ARCHITECTURE.md), or system contracts.

## Shared direction

The user's requested experience fits the existing Aureth setting and simulation: create a character, explore generated terrain and authored places, quest and gather, craft through minigames, bind land and develop a Holding, and grow a personally composed build. Keep one modifier engine, registered mechanisms, tags, deterministic simulation, source attribution, content layering, and server authority as the constraints on every addition.

Professional quality requires evidence and peer review. Passing a test suite is not a guarantee of defect-free software, production MMO capacity, or a visually finished game. Each handoff should identify what was exercised, what remains unmeasured, and how to reproduce a failure.

## Work ownership

| Workstream | Owned output | Boundary |
|---|---|---|
| Codex foundation review, this chat | `src/HollowWake.Core/Content/ContentLoader.cs`, `tests/HollowWake.Core.Tests/ContentLoaderValidationTests.cs`, loader contract addition, this record | Small independent fix; no casting, protocol, UI, or gameplay redesign |
| Visual-language chat | `docs/design/visual-language.md`, `art/visual-language/` | Original visual study, art direction, event readability, source attribution, integration handoff |
| Sprite-rendering chat | `docs/design/sprite-rendering-study.md`, `experiments/sprite-rendering/` | Isolated comparison and proposed adapter; no production renderer replacement |
| Existing Claude coordinator | Existing casting and interface packages, integration sequencing | Read observed session context before touching overlapping work; require an actual handoff/acknowledgment before claiming coordination occurred |

Visual-language chat: `01a14ba2-17b0-7c63-9f34-9ec20affdbc3`. Rendering chat: `01a14ba2-2771-74e2-8fcc-7f4ad9f2a950`. Coordinating Codex chat: `01a14ba0-9495-7e43-b914-0a3f92cc24fc`.

The visible Claude session registry identified **MMOARPG foundation architecture**, session `ca9d4a56-35aa-4bbd-b427-bb0bda5c8832`, as active. Its recent session record reported casting and interface follow-ups in progress. This is observed context, not an ownership acknowledgment from that session.

## Foundation finding and fix

Malformed document `kind`, list-entry `id`, and `patch`/`remove` metadata used typed `JsonNode.GetValue` conversions before any diagnostic boundary. A content author could enter `kind: 42` or `patch: invalid`, and validation would throw instead of returning an origin-bearing report. It could therefore hide further authoring problems and prevent strict mode from producing its normal `ContentLoadException`.

The loader now checks these types explicitly. Invalid document metadata skips that document; invalid entry metadata skips that entry. A malformed control is rejected before it can replace/remove an existing definition or become a deferred patch. Valid siblings and subsequent files still load for diagnostics. The normal strict-mode rejection uses the completed validation report. Null and omitted optional controls still mean false, and valid patch/remove semantics remain unchanged.

The regression suite first reproduced the conversion failures. Coverage includes JSON and YAML, scalar and container type errors, list and map control fields, continuation after invalid input, preservation of prior definitions, strict-mode failure reporting, and compatibility of valid controls. This is deliberately a metadata fix; it does not claim complete schema validation for every possible malformed content file.

### Verification recorded for this change

- `dotnet build HollowWake.sln --no-restore --verbosity quiet`: passed with zero warnings and errors, using the existing package cache via `NUGET_PACKAGES=C:\Users\John\.nuget\packages`.
- `dotnet test HollowWake.sln --no-build --no-restore --verbosity quiet --blame-hang-timeout 90s --blame-hang-dump-type none`: passed, 417 core tests and 35 server tests. The core test project was rebuilt after adding the final compatibility case.
- `dotnet run --project src/HollowWake.Tools --no-build -- validate`: zero errors and warnings.
- `git diff --check`: passed for the tracked changes.

The initial sandbox solution build could not resolve the already-installed Godot SDK until the package cache was specified. The initial sandbox test run stalled on local networking and was canceled; the complete final test run passed outside that restriction. No production renderer or gameplay system changed, and this pass did not perform an in-game visual smoke or hardware benchmark. Changes are left uncommitted for coordinated integration.

## First playable proof

Use one small frontier journey as the shared acceptance scene, with a fixed seed and recorded content layers. This is a proposed verification slice, not a claim that the whole journey has been playtested here.

1. Create a character and enter Ninewells. A second client observes the same authoritative state.
2. Find a generated point of interest and an authored landmark. Preserve their identity, placement and discovery after reconnecting.
3. Fight, complete a quest and recover resources. Credit, rewards and item provenance remain consistent across clients and saves.
4. Use a crafting station and complete a minigame. The server validates the submitted result, rejects invalid/replayed claims, and produces the configured reward.
5. Bind a Fount and change a Holding through a decree or structure. Show the physical consequence and verify its persistence and ownership from another client.
6. Recompose a weave or equip an item. Its modifiers, gameplay delivery, authoritative footprint and visible cue agree. A content-only variation should exercise the same mechanisms.

Record gameplay defects separately from presentation shortcomings. A four-player hosted session and measured client/server load remain milestone gates, not conclusions from headless tests.

## Visual decisions to prove

- Treat show-don't-tell as readable action and consequence: anticipation, release, travel, impact and recovery; discoverable environmental changes; visible resource and Holding states. Retain optional accessible explanations and controls.
- Take the prior Hollow Wake casting families and preparation cues as references. Its source is a separate TypeScript project; do not transplant simulation assumptions into the new C# core.
- Prototype directional sprites or painted impostors as presentation adapters over the existing 3D world. The accepted isometric and third-person cameras, elevations, bridges, targeting and occlusion remain constraints.
- A sprite approach must prove equipment variation, pitch/rotation behavior, silhouettes, correct ground contact, and readable crowded combat. Reduced motion and color-independent cues are part of the review.
- Compare client frame time, draw calls, transparent overdraw, atlas memory and asset-production cost under the same scene and camera. Existing server benchmark budgets cannot establish a client hardware minimum. Treat lower hardware demand as a hypothesis until measured.
- Gameplay-critical telegraphs must follow authoritative shape, size, heading and lifetime. Cosmetic reduction must preserve the information required to avoid an attack.
- The exact user reference `https://lumoriagame.com/#play` was inaccessible to the web tool. Do not replace it with the differently named Lumora site or assert observations about its rendering implementation.

## Claude review channel

The user explicitly authorized sending the loader, regression test and content contract to their installed Claude account for a read-only review. A forked command-line review was attempted so it would not resume or alter the busy coordinator. It failed with `OAuth session expired and could not be refreshed`, including outside the sandbox. Desktop window capture also failed after its supported recovery attempt. No Claude review response or coordinator acknowledgment was obtained.

After the user restores Claude sign-in, request a read-only review of those three files. Ask for concrete defects, compatibility risks and missing regressions; keep mutations, merges and new delegation out of that review request. This local record is available to the coordinator, but its existence does not prove it was read.

## Completed visual handoff review

The visual-language chat delivered its first bounded study. The coordinator read [the contract](../design/visual-language.md), inspected its crowded grayscale capture, and checked the saved browser results: 49 checks, none failed. Review the [interactive study](../../art/visual-language/study.html), [concept sheet](../../art/visual-language/hollow-wake-concept.png), and [validation record](../../art/visual-language/validation.txt). The example recipe is a proposal, not loadable gameplay content.

The direction separates Thread grain from relation and outcome. It uses shape differences, clear anticipation/contact/recovery, reduced-motion alternatives, and physical changes around Founts. Human recognition, engine occlusion and production performance remain open gates.

The most important runtime follow-up is authoritative cast lifecycle correlation. The inspected server broadcast path includes damage/block/evade, heal, status and death, but internal release/interruption events do not by themselves establish a client lifecycle stream. Do not infer successful release or impact from a disappearing cast bar. Re-inventory the wire after the active integration work; use neutral dissolution for unknown endings until the necessary semantics exist.

The [rendering comparison](../design/sprite-rendering-study.md) also has reviewable evidence. The coordinator inspected its isometric comparison. Its proposed reversible appearance adapter keeps sprites as an isometric candidate and geometry as a near-camera/extreme-pitch fallback. Reduced submitted triangles on the development GPU do not establish lower hardware requirements; measured CPU ranges overlap, and GPU elapsed time and minimum-device performance remain unmeasured.

The shared checkout advanced to `60c0677` during these studies. The earlier 417-core/35-server result above belongs to the foundation-fix verification snapshot. The visual chat separately reported 429 core tests passing but an incomplete solution test attempt under its SDK/network environment. Do not combine those separate runs into a new all-green result for the later checkout. Production integration and its fresh full gates remain separate work.

Acknowledged by: [Claude coordinator's first review](2026-10-17-claude-acknowledgment-and-review.md). Subsequent implementation and separate commits are recorded in [the cast-cue handoff](2026-10-17-cast-cues.md).
