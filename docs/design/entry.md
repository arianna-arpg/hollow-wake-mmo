# Account entry and Mu

After authentication, the server chooses the account's entry stage. The shipped stage is Mu: a spirit drifts through an abyss among standing vessels, and stillness near one opens its Wake card. The same content, spatial model, body families, spirit parts and input bindings drive both clients. Mu is local presentation. It creates no simulation entity, provisional character, equipment, loot, combat, world position or account progress.

## The current waking

Remembered characters occupy the inner rank; unclaimed Calling vessels follow it. With no remembered lives, the unclaimed rank takes the inner radius. Vessels wear their Calling's existing world appearance and starting gear, and turn toward the wisp. A remembered vessel names the character, Calling, level and saved zone; it previews the Calling's kit, not a snapshot of saved equipment. A new vessel describes the Calling and asks for a name. Wake resumes the owned character or creates a new one and selects only the character returned for that request. The existing Crossing covers actual world preparation.

Every shipped Calling is available. The prototype's dealt hand, slot unlocks, Vault purchases, unknown cowls, run contracts and death-to-new-run economy are not MMO mechanics and are not fabricated. Existing characters remain resumable if later availability rules change. The account's capacity limits creation; it never disables a remembered life. MMO death and respawn keep their existing behavior.

Quick play authenticates and focuses a remembered vessel. It waits for an explicit Wake. Ordinary login, quick play, returning from the world and a future tutorial threshold all consume the same character-list entry state.

## Content and registries

`entry_flows` defines the default account flow, `character_limit` and ordered stages. A stage has an `id`, registered `kind`, presentation id, and optional `completed_by` account tags. A stage whose completion tags are all satisfied is skipped. An empty completion set never skips. A flow ends at a persistent stage that permits character selection.

`CallingDef.account_requirements` is an optional set of account tags, with the core's hierarchical matching. Empty means available. These are server conditions, separate from presentation. The server filters the legacy Calling list, reports availability to opted-in clients, and rechecks creation and entry rather than trusting a rendered card.

`ui_entry_scenes` contains presentation under `meta`: the activity id, ring and dwell rules, movement speed, wrapping, camera, body sizes and opacity, labels, palette and the source of the spirit recipe. Changing these requires no account or world logic. `wispSource` refers to a loading presentation's part recipe; the smaller Mu radius is authored here. The spirit and abyss painters are shared with loading.

Open sets have registration points:

- `EntryStageKinds.Register` declares a server stage kind's selection capability. A new gameplay stage must also implement its authoritative lifecycle before content uses it.
- `EntrySceneSchemas.Register` validates an activity's metadata.
- Native `EntryViews.Register` and browser `registerEntryView` bind a stage kind and activity to a view. Each view owns its lifecycle and input, receives the server's list, and emits existing shell actions. A future tutorial view receives its own metadata and messages; it need not inherit spatial selection.

Unknown stage or activity presentations fail closed with a compatibility message. They never fall through to the old picker. Presentation numbers never affect character stats or the simulation.

## Spatial selection

`SpatialSelection` is the engine-free mechanism in each client. Its ring layout preserves the prototype's crescent, then widens and grows it to preserve the minimum seat gap. Ranks stack outwards. The wrap rim grows with the outermost seat; crossing it re-enters at the antipode with the authored clearance. This remains usable as the roster grows.

The nearest vessel within the dwell reach is engaged. Movement resets progress. An available vessel opens once after the authored stillness interval. Closing it does not repeatedly reopen it: leave its reach to rearm. An unavailable vessel can be inspected through the accessible controls but never wakes. A roster refresh invalidates old cards and targets. Opening another surface cancels a travel target and dwell progress.

Mouse and touch choose a ground destination. Keyboard and stick motion use the player's movement bindings. The compact vessel controls provide direct inspection for keyboard and assistive use. Tab stays inside an open Wake card; controller confirmation, navigation and back operate the same card. Text entry never moves the wisp. Suspended, hidden or unfocused entry surfaces do not process movement; held movement must return to neutral after a card or suspension. Reduced motion stills ambient movement while preserving selection and vessel attention.

## Protocol and ownership

The optional hello feature `entry_flow` adds `CharacterListMsg.entry`, containing the authoritative flow, stage, kind, presentation, creation capacity and Calling availability. It also enables a `create_character.request_id` to be echoed with `created_character_id` on the resulting list. All additions are optional. Legacy clients receive their original fields and still face the same server validation.

A view retains only its outstanding creation request. Only the matching successful list may trigger automatic selection after Create and Wake. Cancelling that card clears the request: the server may already have created the character, but a late result only refreshes the roster. Rejection makes the card editable. Closing the connection disposes the view and clears pending input and creation state. Connection generation guards reject replies from old sockets. Account facts have no client-writable completion or unlock message.

## Tutorial migration boundary

The reference is the earlier project's scene fabric, not just its last Mu stage: `data/scenes.ts`, `data/tutorialObjectives.ts`, `data/commanders.ts`, `engine/scenes.ts` and `docs/engine/scenes.md`. Its faction journey supplies an unfinished road or survival objective, drills, clashes and assaults, then an interruptible commander reckoning or an early fall. Both routes reach a wake card and the same Mu threshold.

Bring these parts together in a later implementation:

- An authoritative transient staging instance, with save stand-down, reward suppression, no normal world exits, temporary actors and teardown. The account ledger is durable; the staging character is not a saved MMO character.
- Registered stage handlers for cards, measured movement/casting drills, authored clashes, waves, the commander and the fall. Use the existing intent, weave, combat, hit, death/downed and event systems. Do not simulate tutorial combat in the UI.
- A seeded, persisted faction/journey choice; authored road and sky progression; objective and attention markers; ordinary cast forecasts, interruption, body-part counterplay and the early-fall route. Completion cannot depend on an uninterruptible cutscene or killing an invulnerable actor.
- A covenant that contains the tutorial's fall and rewards, with real bodily feedback and graceful timeout/abort handling. Temporary abilities and UI/cast seals must be policies on the staging instance, not branches keyed to Mu or a class.
- A versioned account migration policy for veterans and new accounts. Stamp the server's completion tag atomically at the Mu threshold, after the authoritative outcome. Aborting earlier leaves the tutorial due; disconnecting from Mu does not replay it. A newly introduced tutorial must not unexpectedly capture existing accounts.

The present flow intentionally authors no tutorial stage and grants no unlocks. `EntryFacts` persists server-owned tags with an empty default for older accounts. Tests register a synthetic preceding stage and demonstrate refusal of direct character creation/selection until the server records its completion, replay after abort, and the shared return to Mu. That pins the handoff contract without claiming that the tutorial systems are already ported.

## Evidence

Both clients consume `tests/fixtures/mu-rings.json`, produced by `scripts/mu-reference.mjs` from the prototype function rather than a new implementation. Paired tests cover dwell interruption, latching, wrap, roster refresh and capacity. Server tests cover feature negotiation, legacy compatibility, authority, persistence and a preceding stage. The background smoke uses actual native and browser Wake cards. `experiments/mu-selection/check.mjs` adds browser touch, keyboard, controller, focus, cancellation, refusal and portrait checks against a private server.
