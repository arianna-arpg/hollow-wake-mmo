# Content layers and expressions

See `docs/modding.md` for the authoring guide. This is the contract.

## Loading

`ContentRegistry.Load(layers)` reads every YAML and JSON file in each layer directory in path order, merges entries by kind and id across layers (replace, patch, remove), binds each entry to its kind's record type through the content JSON conventions, expands attribute families into attributes, builds the attribute catalog and the rules, then runs every validator of every kind with a context that can resolve references to any other kind. A kind's own validator comes first; areas that add fields to a kind add their validators with `ContentKinds.AddValidator`. The report carries errors and warnings with origins. Strict loading throws on errors. A layer defines an id once: a second definition of the same kind and id within one layer warns and replaces the first, since only a later layer is meant to replace or patch an earlier one.

## Conventions

- Keys are snake_case; records map them by naming policy or explicit names.
- Tag sets accept arrays or space-separated strings; tags are lowercased; dots make hierarchies (`weapon.sword` satisfies `weapon`).
- Expressions accept numbers or formula strings; percent literals divide by one hundred.
- Typed specs are objects with a `type` key; the rest are parameters bound by the handler.
- Numbers read from JSON nodes go through `JsonNum` because YAML integers, YAML floats and wire JSON produce different node types.
- Unknown keys warn; missing required keys error; wrong types error with the entry's origin.
- The loader validates document `kind`, list-entry `id`, and optional `patch`/`remove` controls before applying an entry. Invalid metadata produces an error with the source file instead of a conversion exception; valid sibling entries and later files still load for diagnostics. A malformed control cannot replace or remove an existing definition. Strict loading rejects the completed report. Absent or null optional controls retain their false default.

## Expressions

Grammar: numbers (with optional `%`), identifiers with dots, strings in quotes (only as `has()` arguments), `+ - * / % ^`, comparisons, `&& || !`, `? :`, function calls. Division by zero yields zero. Unknown identifiers throw at evaluation; validation warns about identifiers that are neither attributes nor declared scope names.

Functions: `min max clamp floor ceil round trunc abs sign sqrt pow log exp lerp inverse_lerp step smoothstep pct avg sum diminish`, plus the forms the parser knows: `has("flag")`, `rand()` (a fraction from the scope's random stream), `randi(lo, hi)` (a whole number from lo to hi inclusive, from the same stream), `curve("name", t)` (a named curve at t; an unknown name fails parsing) and `if(c, a, b)`. Registration adds more functions.

Scopes: `DictScope` for tools and tests, `ScopeOverlay` for layering values and mounting sub-scopes under prefixes, `Stats` for entities, `WeaveScope` for `weave.*`, the effect context which mounts `source`, `target`, `caster`, `weave` and its variables, and the hit scope of a hit's folds where `target.*`, `source.*` and `distance` resolve.

Curves: `Curves` names shaping curves (`linear`, `quadratic`, `quad_out`, `exponential`, `breath` ...) that ramps and envelopes refer to by name; registration adds more.

Read-only observers can inspect `Expr.IsPreviewSafe` without evaluating the tree. Random forms are unsafe; all branches and arguments participate. Built-in numeric functions and curves are pure. Custom `ExprFunctions.Register` and `Curves.Register` calls default to unsafe for previews, while remaining executable. `previewSafe: true` explicitly promises a deterministic function of arguments only, with no external reads, writes or random draws. Registration happens before content runs. Parsed function calls retain their registered implementation and purity promise; curves resolve by name at evaluation, so their safety is checked against the current registration. Scope identifiers and flags still need separate dependency analysis: a tree with no random calls can refer to a random attribute formula. The shared [delivery forecasting](cast-forecasting.md#implemented-core-foundation) preflight performs that analysis before using the ordinary numeric fold.
