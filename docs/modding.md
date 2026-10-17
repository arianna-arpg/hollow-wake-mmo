# Content and modding

Everything the engine runs comes from content layers. A layer is a directory of YAML or JSON files. The server lists its layers in `config/server.yaml` under `content_layers`, in load order; `content/core` is layer zero.

## File shape

```yaml
kind: patterns
entries:
  - id: ember_wolf
    name: Ember Wolf
    extends: wolf
    tags: [beast, wolf, ember]
    components:
      stats: { attributes: { resist.ember: 0.5 } }
      weaves: { list: [bite, cinder_spit] }
```

`kind` names a registered definition kind. `entries` is a list of objects with `id`, or a map from id to object. A file may hold several documents separated by `---`.

## Layering

A later layer may:

- **Replace** an entry by using the same id.
- **Patch** an entry with `patch: true`: objects merge recursively, `null` deletes a key, lists replace.
- **Remove** an entry with `remove: true`.

```yaml
kind: rules
entries:
  - id: combat
    patch: true
    values:
      resist_cap: 0.8
```

`hollowwake validate --content content/core;mods/my-mod` loads layers in that order and reports every problem with its origin.

## The kinds

| Kind | What it defines |
|---|---|
| `attributes`, `resources`, `threads`, `tags`, `slots`, `factions`, `rules` | The vocabulary and the formulas |
| `statuses`, `forms`, `glyphs`, `weaves`, `monster_rarities`, `monster_affixes` | Combat |
| `items`, `affixes`, `uniques`, `item_rarities`, `currencies`, `loot` | Itemization |
| `patterns`, `behaviors`, `passives`, `callings` | Entities and characters |
| `biomes`, `spawn_tables`, `region_archetypes`, `pois`, `landmarks`, `region_overrides`, `zone_overrides`, `world_events` | The world |
| `quests`, `quest_templates`, `recipes`, `procedures`, `decrees`, `structures` | Play |

`hollowwake schema` prints every kind and its fields.

## Expressions

Any field typed as an expression accepts a number or a formula string: `"40 + level * 10 + vigor * 4"`, `"has(\"burning\") ? 1.5 : 1"`, `"lerp(weapon.damage.min, weapon.damage.max, rand())"`. Identifiers are attribute ids and the scope names the evaluating system provides (`level`, `life_pct`, `target.*`, `weave.*`, `amount`, and the AI perception names). Percent literals work: `20%` is `0.2`. `hollowwake expr "..."` evaluates against a sample scope.

## Modifiers

```yaml
modifiers:
  - { attribute: damage, form: increased, value: 0.2, scope: [ember, spell] }
  - { attribute: life.max, form: flat, value: 20 }
  - { attribute: damage, form: more, value: 0.3, condition: 'has("stationary")' }
  - { attribute: damage, form: increased, value: 0.02, gauge: status.poison.stacks }
  - { attribute: thorns, form: link, from: life.regen, value: 0.4 }
```

Forms: `flat`, `increased`, `more`, `override`, `link`. Scope tags must all be present in the query context. Victim-facing scopes use `vs.` tags (`[vs.burning]`, `[vs.rare]`, `[vs.low_life]`).

## Effects

Effects are typed specs. Nest them freely.

```yaml
effects:
  - type: projectile
    on_hit:
      - { type: damage, amount: "weave.base_damage * spell.power", threads: $threads }
      - { type: apply_status, status: $thread_status, chance: 0.3, threads: $threads }
      - type: chain
        count: "weave.chain.count"
        effects:
          - { type: damage, amount: "weave.base_damage * spell.power * 0.7", threads: $threads }
```

`$threads` resolves to the weave's Threads, `$thread_status` to the first Thread's status. `hollowwake schema` does not list effect parameters; the handlers in `HollowWake.Core/Effects/BuiltinEffects.cs` are the reference.

## Code mods

A code mod is a .NET assembly that references `HollowWake.Core` and, before the server loads content, registers what it adds:

- `ContentKinds.Register<MyDef>("my_kind", validator)` for a new definition kind.
- `EffectRegistry.Register(new MyEffectHandler())` for a new effect type.
- `AiActions.Register("my_action", ...)` for a new AI action.
- `QuestService.Register("my_objective", matcher)` for a new objective type.
- `InteractionService.Register("my_kind", handler)` for a new interactable kind.
- `PatternFactory.Register("my_component", binder)` for a new component.
- `ProcedureRegistry.Register(new MyProcedure())` for a new crafting minigame.
- `ExprFunctions.Register("fn", min, max, impl)` for a new expression function.
- `ScopeNames.Declare("name")` so validation knows a new scope variable.

Systems are added to a simulation through `Simulation.AddSystem` with a phase and order. A loader for mod assemblies is on the roadmap; today a host wires them in code.

## Landmarks

Hand-made structures are ASCII rows with a legend. `.` and spaces keep natural terrain. Legend entries set height (half-steps as digits are a convention), walkability, tags, a pattern to spawn, and `layer: true` for a surface above the ground (bridges, floors). Rows read north to south.

## Checks that catch mistakes

- Unknown keys on an entry warn with the file and id.
- Missing references error (a weave naming a form that does not exist).
- Expressions reading undeclared names warn.
- Glyph gates, slot limits and thread counts are enforced when weaves compile; `hollowwake validate` compiles every preset weave through the tests.
