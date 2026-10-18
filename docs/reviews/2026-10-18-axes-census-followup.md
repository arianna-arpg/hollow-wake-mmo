# Axes census and shared bindings follow-up

Codex addressed Claude's two required findings on PR #47 at `1b07c1e`. An active glyph can fit an empty shell because its mechanism is declared, without contributing a node. The census now tries subsequent forms and presets until a graph-writing glyph contributes values. A per-glyph assertion covers every core glyph with hooks, patches or added effects and explicitly requires Martyrdom's death-hook radius of 2.5.

The focused check first failed only for the newly exposed key `glyph martyrdom hooks.on_death[0].radius`. Regenerating the complete frozen-literal fixture adds exactly that row and removes none. This is a correction to reported content, not a gameplay or content value change.

Composition lint now reads fallback attributes from `AxisBindings`; its duplicate fallback table is removed. The handler-derived fallback test checks those bindings directly. A written damage effectiveness names the shadowed `damage.effectiveness` attribute specifically for added damage and explains that it also scales the written amount. The unwritten case remains the existing implicit added-damage axis, without a duplicate reported value.

The [lint inventory](2026-10-18-axes-lint-inventory.md) names all 57 added findings relative to main `6e7e241` with a reason for each. Exact output is 50 form-read findings and 7 string-switch findings, rather than the steward's earlier 49/8 count. Nothing is suppressed. The remaining vocabulary and compiler-provenance duplication are named debt, not new runtime policy. The weave contract now identifies the shared binding registry.

Verification in the review worktree:

- Release solution build: 0 warnings and 0 errors.
- Full solution tests: 664 core, 114 client and 43 server passed, with no failures or skips (821 total).
- Content validation: 0 errors, 0 warnings.
- Frozen census: exactly the one named row added.
- Composition lint: 504 to 561 smells, 197 notes unchanged; 57 new keys after normalizing only the archived source prefix.
- `git diff --check`: clean.

Raw output is retained in the ignored `artifacts/axes-followup-*` files and `axes-main-lint.*`. The previous attribution correction was accepted by Claude; this second follow-up requires its own review. No production cast, movement, client, protocol or hot-path behavior changed, so live and performance gates were not repeated.

Acknowledged by: pending Claude review on PR #47.
