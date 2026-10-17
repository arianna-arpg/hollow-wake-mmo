# Content layers and expressions

See `docs/modding.md` for the authoring guide. This is the contract.

## Loading

`ContentRegistry.Load(layers)` reads every YAML and JSON file in each layer directory in path order, merges entries by kind and id across layers (replace, patch, remove), binds each entry to its kind's record type through the content JSON conventions, expands attribute families into attributes, builds the attribute catalog and the rules, then runs every validator of every kind with a context that can resolve references to any other kind. A kind's own validator comes first; areas that add fields to a kind add their validators with `ContentKinds.AddValidator`. The report carries errors and warnings with origins. Strict loading throws on errors.

## Conventions

- Keys are snake_case; records map them by naming policy or explicit names.
- Tag sets accept arrays or space-separated strings; tags are lowercased; dots make hierarchies (`weapon.sword` satisfies `weapon`).
- Expressions accept numbers or formula strings; percent literals divide by one hundred.
- Typed specs are objects with a `type` key; the rest are parameters bound by the handler.
- Numbers read from JSON nodes go through `JsonNum` because YAML integers, YAML floats and wire JSON produce different node types.
- Unknown keys warn; missing required keys error; wrong types error with the entry's origin.

## Expressions

Grammar: numbers (with optional `%`), identifiers with dots, strings in quotes (only as `has()` arguments), `+ - * / % ^`, comparisons, `&& || !`, `? :`, function calls. Division by zero yields zero. Unknown identifiers throw at evaluation; validation warns about identifiers that are neither attributes nor declared scope names.

Functions: `min max clamp floor ceil round trunc abs sign sqrt pow log exp lerp inverse_lerp step smoothstep pct avg sum diminish`, plus `has("flag")`, `rand()`, `if(c, a, b)`. Registration adds more.

Scopes: `DictScope` for tools and tests, `ScopeOverlay` for layering values and mounting sub-scopes under prefixes, `Stats` for entities, `WeaveScope` for `weave.*`, the effect context which mounts `source`, `target`, `caster`, `weave` and its variables, and the hit scope of a hit's folds where `target.*`, `source.*` and `distance` resolve.

Curves: `Curves` names shaping curves (`linear`, `quadratic`, `quad_out`, `exponential`, `breath` ...) that ramps and envelopes refer to by name; registration adds more.
