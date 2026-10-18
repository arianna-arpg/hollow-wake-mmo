# Shared ability icons

`scripts/import-ability-icons.mjs` extracts the pinned prototype branch into shared vector content. The exact revision and source catalog coverage are recorded in `assets/branding/ability-icons-provenance.json`. It reads Git objects without checking out or changing the prototype worktree. Regenerate with Node and the prototype's installed `tsx` dependency.

The C# `AbilityIconTests` and browser `abilityIcons.test.ts` exercise full imported-face coverage, stable item/seat identity, same-id preset/form separation, converted faces, future content and XML escaping. The protocol round-trip suite covers the additive identity fields.

Run Vite on port 8899, then `node experiments/ability-icons/render.mjs` for a focused production-renderer check. It uses the browser's actual item and seat components, compares their SVG layers and saves a gallery under ignored `artifacts/ability-icons/focused`. The native client's existing `--mockup` renders its staged inventory, supports, rack, cooldowns and dormant slots. Live full-client smokes run against a private server on separate ports. Review evidence and limitations are in `docs/reviews/2026-10-17-skill-support-icons.md`.
