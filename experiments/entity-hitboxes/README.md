# Entity hit-body evidence

The implementation is in Core and the production clients. This folder contains reproducible authoring and visual evidence, not a second gameplay implementation.

- `author-profiles.mjs` writes the simulation profiles and additive pattern bindings from explicit rest-anatomy recipes. It reads dimensions/classification for authoring only; the simulation never reads appearance or runs this script.
- `node experiments/entity-hitboxes/render.mjs` uses the real browser body builder and diagnostic overlay in a private headless Edge session. It saves `evidence/bodies-front.jpg` and `bodies-turned.jpg`; both were visually inspected. `--write-fixture` refreshes the shared native/browser line fixture and should only accompany an intentional geometry change.
- The cyan union approximates rest-pose anatomy. Yellow is movement clearance. Decorative ears, tusks, gear, banners and empty saddles are excluded. Common-scale views preserve the small whelp and adult-height skeleton/Orc comparison; the larger cards inspect individual parts.
- The background smoke uses `node scripts/test-background.mjs --suite all`. Its full run directory remains under ignored `artifacts/testing`; copied summaries/logs here are the review evidence. Native headless mode does not certify native rendering.
- `evidence/bench-first.txt` and `bench-repeat*.txt` preserve over-budget measurements. `bench-final.txt` is the serial Release measurement after the final simulation changes with only the benchmark process at `AboveNormal` priority. The review record explains the variability and exact conditions; no budget was relaxed.

The [review record](../../docs/reviews/2026-10-17-precise-entity-hitboxes.md) names commands, counts, behavioral tests, integration boundaries and remaining limitations. The authoritative contract is [physical bodies](../../docs/design/physical-bodies.md#damage-anatomy).
