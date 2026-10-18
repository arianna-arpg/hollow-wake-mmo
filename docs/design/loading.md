# Identity and the Crossing

Hollow Wake's loading surface is a rewardless, playable crossing. Its reference is the loading mini-game in `arianna-arpg/arpg-game-world` at `64819bd4351001a34c92eed13b63e1f55f0c52ef`. The Mu scene with vessels is character selection and is a separate future migration.

## Identity

`assets/branding` is the canonical insignia, original hand-sized Windows icon frames, favicon, and website mark. `provenance.json` records their earlier-project source. `scripts/import-legacy-branding.mjs` imports that source and its menu glyph vocabulary. The glyph layer patches the existing icon set, preserving MMO pool, status, look, and slot mappings. An optional `accents` path adds filled details to a stroked glyph in either renderer.

Run `node scripts/sync-branding.mjs` after an identity or loading-content edit. It copies the canonical assets to the desktop project, browser public files, and launcher. `--check` requires byte equality and runs in CI. The launcher and Windows export use the original ICO, including its small frames, rather than rasterizing the website mark. Login and loading headers use the website mark.

The shipped application name, operating-system icons, installer icon, browser favicon, login, and startup artwork identify Hollow Wake. Engine diagnostic messages and required third-party notices retain their technical identity. Godot's pre-scene boot splash is a static Hollow Wake PNG. Interactive play starts when the scene tree exists; the engine's earlier native initialization cannot execute this scene. This uses the engine's supported project and export settings, without an engine fork.

## Shared presentation content

`content/core/ui/loading.json` declares `ui_loading_screens`. Exactly one definition is default; every activity parameter lives under `meta`, outside simulation state. It declares the activity, seed sequence, preparation timeout, readiness radius, text, colors, ordered rendering layers, runic alphabet, wisp part recipe, gate profiles, spirit pairs, and motion/collision tunings. The content validator checks required fields, bounds, openings, palette, and supported activity/part/layer names.

The native boot cover reads a synchronized small copy at `Branding/loading.json`, before loading the full content registry on a worker. Once validated, the loaded default supplies subsequent preparations. The browser imports the same canonical document into its small boot module, then dynamically imports the world client. A bounded initialization stage can still occupy the main thread; loading animation is not a claim that arbitrary work has become asynchronous.

Activity factories and ordered layer painters are registration seams in each client. The crossing model, look recipe, and readiness lifecycle have separate responsibilities. New crossing variants change content. A new activity requires its model/view adapter and content validator to register together. The native renderer uses Godot canvas primitives; the browser retains the prototype's Canvas renderer. They share composition and geometry, with normal font, glow, and rasterization differences between backends.

## Mini-game contract

Entry travels down; travel may go left or right. Steering moves across the travel direction with mouse/touch, WASD/arrows, or controller stick/D-pad. Every direction uses the same logical coordinates and inverse pointer transform.

The original seeded generator, openings and central piers, staggered soul pairs, Wild Wisp pace increase, current refresh and easing, speed limits, and impact reset order are preserved. A hit takes precedence over collecting a current in the same step. Choosing one soul releases its companion. There is no score, reward, persistence, account progression, server intent, or consumption of the world's random stream. Each preparation derives a fresh deterministic visual stream from the content seed and session sequence.

The appearance uses the original spirit's disc, halo, eyes and live wisps, soul-flame silhouettes, gate inscriptions, currents, ribbons, abyss and mote strata. Existing MMO account cosmetics are not inferred from the old project's wardrobe; the shared part recipe is the appearance extension point. Reduced motion removes ambient travel, pulses and impact ornament while preserving steering and encounters.

## Work owns a lease

Beginning a preparation yields a token-owned lease. It can update a label/detail and actual completed/total work, yield a paint, fail with an optional retry callback, or finish. Superseded leases cannot change or close a newer cover. Unknown work stays indeterminate; there are no invented percentages or minimum display times. Failure remains visible and playable until retry or cancellation.

Startup covers content/client preparation. Character entry and authoritative world re-entry cover receipt of the hero and building the nearby terrain. The progress count is ready chunks plus the hero, and the cover closes as soon as those requirements are met. Ordinary seamless chunk streaming and `zone_changed` notices do not create artificial loading stops. A future explicit travel job can use the same lease.

Retry restarts local preparation and its timeout. The reliable connection still owns the original character-selection delivery; retry does not send a duplicate selection while its response is delayed. Cancellation closes the connection, discards pending entry, and returns to login. Old socket callbacks cannot install cancelled world data into the next connection.

The cover clears movement, blocks gameplay input and owns focus. Browser siblings are inert, then restored to their previous state. Keys and controller inputs held across completion are quarantined until release or neutral. Retry/cancel retain keyboard and touch access. Controller confirmation requires the client window or document to have focus; button edges are still tracked while unfocused so a held press cannot activate a button on return. Hiding the browser or losing native window focus pauses toy advancement. This does not pause the authoritative server or promise invulnerability during preparation.

## Verification

`scripts/loading-reference.mjs <prototype-checkout>` extracts and runs the original model from the pinned commit to regenerate `tests/fixtures/loading-reference.json`. The C# and TypeScript tests compare their full courses against these independent reference traces, including gate geometry. Do not generate the baseline from either port.

The normal client and browser smoke courses verify world entry and gameplay after the cover. Additional checks:

```sh
node scripts/sync-branding.mjs --check
godot --path src/HollowWake.Client -- --loading-smoke --shots ABSOLUTE_OUTPUT_DIRECTORY
npm --prefix web run dev -- --port 17880
# In web/, with Vite and the normal local game server running:
node scripts/loading-smoke.mjs --url http://127.0.0.1:17880
node scripts/loading-entry-smoke.mjs --url http://127.0.0.1:17880 --server ws://localhost:7778
```

The browser loading course exercises every direction and input family, stale leases, progress, retry, held-input release, focus restoration, portrait layout and reduced motion. The entry course delays actual server frames, cancels them, reconnects, times out, retries, and completes when terrain arrives. The native loading course captures every direction and exercises lease failure/retry/cancel through the real cover. `--loading-preview down|left|right` and `?loadingPreview=down|left|right` allow manual inspection without slowing ordinary loads.

Packaging uses `--boot-check` and requires both successful exit and `BOOT PASS`, after validated content and client initialization. The PR release dry run verifies exported assets, bootstrap JSON, and launcher packaging.
