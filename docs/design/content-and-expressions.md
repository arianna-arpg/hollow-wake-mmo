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

## Composition lint

`hollowwake lint` (`CompositionLint`) names what breaks the combat grammar ([combat-grammar.md](combat-grammar.md)), in which every mechanic is a value on an axis or a node in a graph rather than a bespoke piece. It reports and never fails validation; each finding is a `smell` (the grammar says it should change) or a `note` (worth a look). It walks every effect node of every kind the way the weave compiler does and, given a source root, reads the core's C#.

`form.one_use` names a form exactly one preset composes on. A shell (`drop_weight: 0`) exists to serve every preset of its kind, so there it is a smell; for a form codices teach, which players compose on too, it is a note.

`form.unused` names a form no preset composes on: a smell for a shell, which nothing can then cast, a note for a form codices teach.

`parameter.single_use` names an effect parameter exactly one definition writes or, when one definition alone uses a type, the type once. A patch's `set`, a patch's wrapper and a glyph's hook lists count as writes: a hook list writes every parameter a signature maps to that hook, since the compiler appends it there.

`parameter.unused` names a parameter a handler's signature declares that nothing loaded writes, or a whole type no content has a node of. Where an attribute carries the parameter, the note says leaving it unwritten is the intended state.

`axis.frozen` names a literal number, or an expression that reads nothing, written for a parameter whose handler would otherwise read an attribute: a weave attribute in a form, preset or glyph graph (`radius` on `area` reads `area.radius`), or the holder's attribute in any graph (a charge gain's `max` reads `charge.{id}.max`, which the literal shadows). The table of those fallbacks is read from the handlers and kept in the lint; a multiplier such as `area.angle` still scales a literal, so it is not one.

`expression.copied` names each definition writing a formula, the same text but for whitespace, that at least the copy threshold of definitions write (`CompositionLint.DefaultCopiedThreshold`, `--copies` for another), so each new copy is a new finding; a named basis in `rules.yaml` or an attribute should carry it.

`code.string_switch` reads the C# under the source root (`src/HollowWake.Core` by default, `--code` for another, `--no-code` to skip) a line at a time with comments dropped and strings masked, not as a compiler. Every switch over string words is reported: a smell inside the systems (`CompositionLint.SystemDirectories`) or wherever it re-lists effect types, a note elsewhere and where a handler switches on its own words (parsed in that line, or declared in that file with `ChoiceOf(` or `OneOf(`). Inside the systems, a line is also a smell when it tests a flag, tag or mechanism by a literal name, compares with an id code must never name (a status, resource, thread, charge, proc or attribute), or compares one operand with two or more literals in the file. Tags, flags and mechanisms are open words, so content adding one cannot grow these findings. The scan sees literals in switches and comparisons only: a word array (`string[] Kinds = { ... }`), a name behind a constant and a switch whose cases are constants are invisible to it, and a line that registers words (`Register(`) is never a finding.

`code.form_read` reads the same lines in the cast runtime (`CompositionLint.FormReadDirectories`) for reads through a weave's form: a member (`weave.Form.Channel`, a compiled weave's own `Form.Targeting`) or the whole form taken into a local or handed to a helper. The runtime should read the compiled weave, into which a preset or glyph can merge any block, so these reads are the baseline later work shrinks; a line that also reads the preset's or the glyphs' block is left out. It cannot tell a weave's form from another object's `Form`, and the reads made later through a form held in a local are invisible to it.

Every finding has a key with no line number: the check, kind, id and node path for content; the check, the file and the masked line for code, numbered when the same text recurs in the file. Paths are relative to the repository, with forward slashes, whatever the working directory. `--json file` writes the report grouped by check; with it, `--baseline file` also writes the keys, and a later run given `--baseline file` alone fails naming each finding whose key is not listed, so the baseline may shrink but not grow. Content that does not load fails the run.
