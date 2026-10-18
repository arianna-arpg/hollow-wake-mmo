# The Living Wilds

Fauna and critter foundation: 19 new bodies and renewed wolf, boar and grove_stag IDs. Source roster and sprite/anatomy precedents were inspected from `arianna-arpg/arpg-game-world` at `118b510f8b5bfb781163c5bc13ea4908f698e75b`; `provenance.json` records adaptations. Generated polygon anatomy is intentionally reinterpreted, not a pixel-exact conversion. Existing native/browser body builders and animation rigs render the same reviewed plans.

Run `node experiments/fauna/render.mjs` from the repository root after web dependencies are installed. It creates a private Vite server and headless Edge page, renders both angles and a grayscale walking pose, then shuts them down. `--write-fixture` explicitly refreshes the shared reviewed plan fixture. Review images before accepting fixture changes. Build/cache output lives under ignored `artifacts/`.

Cards enlarge each body independently; the top lineup uses one world scale with 1.8 m player and skeleton references. Card measurements are authoritative nominal W/D/H and authored physical mass. Ears/horns can extend beyond stature. The gallery renders actual production geometry with preview lighting, not gameplay or native pixels.

The biome tables and direct legacy populations place all bodies; small critters have no attack or drops and cannot be selected by the beast-cull template. Current behavior assignments are scaffolding. See the [AI handoff](../../docs/reviews/2026-10-18-fauna-ai-handoff.md) for Claude review and proposed future simulations. Neither ecological AI, flight, swimming, climbing nor precise model-fitted hitboxes is implemented here.

Generation evidence uses seed `aureth`. All four before/after ASCII terrain/site maps remain identical. Only the origin's cull quest text changes, Grey Wolf to Wolverine, from the expanded eligible catalog. Chunk hashes change from new population entries, while the climate hash is unchanged. The global cull chooser's existing lack of local/level constraints is called out in the handoff.
