using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The core content plus a layer of test forms, glyphs and presets for the cast modes, of charge kinds and of the
/// rules they lean on (ids start with <c>t_</c>), loaded once. Numbers here are the tests' own, so tuning the shipped
/// content cannot move them.
/// </summary>
internal static class CastingTestContent
{
    private const string Layer = """
kind: forms
entries:
  - { id: t_walk, targeting: self, cast_mode: cast, cast_time: 2, cast_move: 0.5, min_threads: 0, effects: [ { type: event, name: t_walk } ] }
  - { id: t_rooted, targeting: self, cast_mode: cast, cast_time: 2, min_threads: 0, effects: [ { type: event, name: t_rooted } ] }
  - { id: t_press_cd, targeting: self, cast_mode: cast, cast_time: 1, cooldown: 5, cooldown_at: press, min_threads: 0, effects: [ { type: event, name: t_press_cd } ] }
  - { id: t_done_cd, targeting: self, cast_mode: cast, cast_time: 1, cooldown: 5, min_threads: 0, effects: [ { type: event, name: t_done_cd } ] }
  - { id: t_free, targeting: self, cast_mode: instant, min_threads: 0, effects: [ { type: event, name: t_free } ] }
  - { id: t_marker, targeting: self, cast_mode: instant, min_threads: 0, effects: [ { type: event, name: t_marker, data: { level: weave.level, radius: weave.area.radius } } ] }
  - { id: t_shot, targeting: enemy, cast_mode: cast, cast_time: 0.5, costs: { mana: 3 }, attributes: { range: 30 }, min_threads: 0, effects: [ { type: event, name: t_shot } ] }
  - { id: t_slow_shot, targeting: enemy, cast_mode: cast, cast_time: 2, costs: { mana: 3 }, attributes: { range: 30 }, min_threads: 0, effects: [ { type: event, name: t_slow_shot } ] }
  - { id: t_hit, targeting: enemy, cast_mode: instant, attributes: { range: 30 }, min_threads: 0, effects: [ { type: damage, amount: 10, threads: [mind], can_crit: false } ] }
  - id: t_burst
    targeting: self
    cast_mode: instant
    min_threads: 0
    attributes: { range: 30, area.radius: 2 }
    effects: [ { type: event, name: t_burst, data: { radius: weave.area.radius } } ]
  - id: t_spender
    targeting: self
    cast_mode: instant
    min_threads: 0
    attributes: { range: 30, area.radius: 2 }
    effects: [ { type: event, name: t_spender, data: { radius: weave.area.radius, spent: charges_spent } } ]
  - id: t_channel
    targeting: direction
    cast_mode: channel
    costs: { mana: 2 }
    cooldown: 3
    min_threads: 0
    attributes: { area.radius: 2 }
    channel:
      interval: 0.5
      windup: 0.25
      move: 0.3
      max_hold: 2
      cooldown_on_end: true
      ramps:
        - { attribute: area.radius, form: increased, per: 0.5, max: 0.75 }
        - { attribute: move.speed, form: more, per: -0.2, min: -0.6, on: caster }
      release:
        min_hold: 0.5
        effects: [ { type: event, name: t_release, data: { held: cast.held, radius: weave.area.radius } } ]
    effects: [ { type: event, name: t_pulse, data: { held: cast.held, radius: weave.area.radius } } ]
  - id: t_channel_bolt
    targeting: direction
    cast_mode: channel
    min_threads: 0
    channel: { interval: 1, windup: 0.1, max_hold: 3 }
    effects:
      - type: projectile
        speed: 2
        range: 30
        on_hit: [ { type: damage, amount: 10, threads: [mind], can_crit: false } ]
  - id: t_charge
    targeting: self
    cast_mode: charge
    min_threads: 0
    attributes: { area.radius: 2 }
    charge:
      max_time: 1
      full_hold: 0.5
      scales: [ { attribute: area.radius, form: increased, at_zero: 0, at_full: 1 } ]
    effects: [ { type: event, name: t_charge, data: { fill: cast.fill, radius: weave.area.radius } } ]
  - id: t_over
    targeting: self
    cast_mode: overcharge
    min_threads: 0
    attributes: { area.radius: 2 }
    overcharge:
      stages: 3
      time: 0.5
      full_hold: 0.6
      cost_per_stage: { mana: 5 }
      per_stage: [ { attribute: area.radius, form: increased, value: 0.5 } ]
      spark: { window: 0.2, bonus: 1 }
    effects: [ { type: event, name: t_over, data: { stage: cast.stage, radius: weave.area.radius } } ]
  - { id: t_bank, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, recharge: 1 }, effects: [ { type: event, name: t_bank } ] }
  - { id: t_mag, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, magazine: { refill: 1.5 } }, effects: [ { type: event, name: t_mag } ] }
  - { id: t_drip, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, magazine: { refill: 2, drip: true } }, effects: [ { type: event, name: t_drip } ] }
  - { id: t_still, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, recharge: 1, still: { bleed: 1 } }, effects: [ { type: event, name: t_still } ] }
  - { id: t_vent, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 3, recharge: 100, vent_all: true, steps_from_bank: true }, effects: [ { type: event, name: t_vent, data: { spent: uses_spent } } ] }
  - { id: t_reloading, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 1, recharge: 100 }, convert: { when: "slot.uses < 1", weave: quiver_restring }, effects: [ { type: event, name: t_reloading } ] }
  - { id: t_manual, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, manual: true }, convert: { when: "slot.uses < 1", weave: quiver_restring }, effects: [ { type: event, name: t_manual } ] }
  - { id: t_manual_mag, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, manual: true, magazine: { refill: 1 } }, convert: { when: "slot.uses < 1", weave: quiver_restring }, effects: [ { type: event, name: t_manual } ] }
  - { id: t_manual_drip, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, manual: true, magazine: { refill: 1, drip: true } }, convert: { when: "slot.uses < 1", weave: quiver_restring }, effects: [ { type: event, name: t_manual } ] }
  - { id: t_manual_still, targeting: self, cast_mode: instant, min_threads: 0, uses: { max: 2, manual: true, recharge: 1, still: { bleed: 1 } }, convert: { when: "slot.uses < 1", weave: quiver_restring }, effects: [ { type: event, name: t_manual } ] }
  - { id: t_gated, targeting: self, cast_mode: instant, min_threads: 0, gates: [ 'life_pct > 0.5' ], effects: [ { type: event, name: t_gated } ] }
  - id: t_guard
    targeting: self
    cast_mode: guard
    min_threads: 0
    guard:
      arc: 120
      pool: 30
      parry: 0.2
      max_duration: 3
      bash: { threshold: 0.3, arm_time: 0.5, effects: [ { type: event, name: t_bash, data: { absorbed: guard.absorbed } } ] }
    effects: [ { type: event, name: t_raise } ]
  - id: t_timed
    targeting: enemy
    cast_mode: cast
    cast_time: 1
    attributes: { range: 30 }
    min_threads: 0
    timing: { kind: perfect, window: 0.25, bonus: 1 }
    effects: [ { type: damage, amount: 10, threads: [mind], can_crit: false } ]
  - id: t_toggle
    targeting: self
    cast_mode: instant
    min_threads: 0
    toggle: { on_deactivate: [ { type: event, name: t_off } ] }
    reserve: { mana: 20 }
    upkeep: { per_second: { mana: 10 } }
    effects: [ { type: event, name: t_on } ]
  - { id: t_meta_host, targeting: self, cast_mode: instant, min_threads: 0, meta_action: { weave: t_meta_a, label: A }, effects: [ { type: event, name: t_meta_host } ] }
  - { id: t_reflex, targeting: self, cast_mode: instant, reflex: true, min_threads: 0, effects: [ { type: event, name: t_reflex } ] }
  - { id: t_recover, targeting: self, cast_mode: instant, recovery: 1, min_threads: 0, effects: [ { type: event, name: t_recover } ] }
  - { id: t_shave, targeting: self, cast_mode: instant, min_threads: 0, effects: [ { type: cooldown, seconds: 2, fraction: 0.5 } ] }
  - { id: t_fused, targeting: enemy, cast_mode: instant, attributes: { range: 30 }, min_threads: 0, effects: [ { type: fuse, delay: 0.5, power: 1, effects: [ { type: damage, amount: 10, threads: [mind], can_crit: false } ] } ] }
  - { id: t_corpse_only, targeting: enemy, cast_mode: instant, costs: { mana: 7 }, attributes: { range: 30 }, min_threads: 0, target: { kind: t_marked }, effects: [ { type: event, name: t_corpse_only } ] }
  - { id: t_far_hint, targeting: enemy, cast_mode: instant, attributes: { range: 30 }, min_threads: 0, ai: { weight: 100, min_range: 3, range_attribute: range }, effects: [ { type: event, name: t_far_hint } ] }
  - { id: t_soft_hint, targeting: enemy, cast_mode: instant, attributes: { range: 30 }, min_threads: 0, ai: { weight: 100, min_range: 3, near_weight: 0.01 }, effects: [ { type: event, name: t_soft_hint } ] }
  - { id: t_near_hint, targeting: enemy, cast_mode: instant, attributes: { range: 30 }, min_threads: 0, ai: { weight: 1 }, effects: [ { type: event, name: t_near_hint } ] }
  - { id: t_brace, targeting: self, cast_mode: instant, reflex: true, min_threads: 0, gates: [ 'has("guarding")' ], effects: [ { type: guard, amount: 25, self: true } ] }
  - id: t_tree
    targeting: self
    cast_mode: instant
    min_threads: 0
    attributes: { area.radius: 2 }
    tree:
      opens_at: 2
      nodes:
        - { id: wide, ranks: 2, modifiers: [ { attribute: area.radius, form: increased, value: 0.5 } ] }
        - { id: far, links: [ wide ], patches: [ { type: event, set: { name: t_tree_far } } ] }
        - { id: near, links: [ wide ], excludes: [ far ], modifiers: [ { attribute: area.radius, form: flat, value: 1 } ] }
        - { id: grafted, links: [ far, near ], glyph: t_repeat }
    effects: [ { type: event, name: t_tree, data: { radius: weave.area.radius } } ]
  - id: t_echo_bolt
    targeting: enemy
    cast_mode: instant
    costs: { mana: 3 }
    cooldown: 2
    attributes: { range: 30 }
    min_threads: 0
    effects:
      - type: projectile
        speed: 6
        range: 30
        on_hit:
          - { type: damage, amount: 10, threads: [mind], can_crit: false }
          - { type: cast, weave: host, at: target, damage_scale: 0.5 }
  - id: t_recast
    targeting: enemy
    cast_mode: instant
    attributes: { range: 30 }
    min_threads: 0
    effects:
      - { type: damage, amount: 10, threads: [mind], can_crit: false }
      - { type: cast, weave: host, at: target }
  - id: t_relayer
    targeting: self
    cast_mode: instant
    min_threads: 0
    effects:
      - { type: event, name: t_relayer }
      - { type: cast, weave: host }
      - { type: cast, weave: t_relay }

---
kind: glyphs
entries:
  - { id: t_meta_b, meta_action: { weave: t_meta_b_preset, label: B } }
  - { id: t_on_hit, trigger: { on: hit, chance: 1 } }
  - { id: t_on_hit_icd, trigger: { on: hit, chance: 1, icd: 1 } }
  - { id: t_on_hurt, trigger: { on: hurt, life_fraction: 0.2 } }
  - { id: t_levy, modifiers: [ { attribute: cooldown.added, form: flat, value: 2 } ] }
  - { id: t_repeat, modifiers: [ { attribute: repeat.count, form: flat, value: 1 } ] }
  - { id: t_half_repeat, modifiers: [ { attribute: repeat.count, form: flat, value: 0.5 } ] }
  - { id: t_scaled_repeat, modifiers: [ { attribute: repeat.count, form: flat, value: 2 }, { attribute: repeat.scale, form: flat, value: 0.5 }, { attribute: repeat.interval, form: override, value: 0.2 } ] }
  - { id: t_retarget, modifiers: [ { attribute: repeat.count, form: flat, value: 1 }, { attribute: repeat.retarget, form: flat, value: 1 } ] }
  - { id: t_follow, follow_up: { weave: t_marker_preset, delay: 0.3 } }
  - { id: t_cycle, cycle: { count: 2, effects: [ { type: event, name: t_cycle } ] } }
  - { id: t_stack, self_stack: { max: 3, duration: 2, modifiers: [ { attribute: area.radius, form: increased, value: 0.5 } ] } }
  - { id: t_chain, chain: { steps: [ t_step_a, t_step_b ], window: 1 } }
  - { id: t_worn, equip_modifiers: [ { attribute: armor, form: flat, value: 15 } ] }
  - { id: t_spend, spend: { charge: t_fury, all: true, minimum: 1, scales: [ { attribute: area.radius, form: increased, per_charge: 0.5 } ] } }
  - { id: t_ranged_trigger, requires_mechanisms: [ projectile ], trigger: { on: hit, chance: 1 } }
  - { id: t_no_forward, forwards: false, requires_mechanisms: [ projectile ], modifiers: [ { attribute: cost, form: increased, value: 0.5 } ] }
  - { id: t_echo, hooks: { on_hit: [ { type: cast, weave: host, at: target, damage_scale: 0.5 } ] } }

---
kind: weaves
entries:
  - { id: t_meta_a, form: t_marker }
  - { id: t_meta_b_preset, form: t_marker }
  - { id: t_marker_preset, form: t_marker }
  - { id: t_step_a, form: t_marker }
  - { id: t_step_b, form: t_marker }
  - { id: t_spark_a, form: t_shot, glyphs: [ t_on_hit ] }
  - { id: t_spark_b, form: t_shot, glyphs: [ t_on_hit ] }
  - { id: t_relay, form: t_marker, effects: [ { type: event, name: t_relay }, { type: cast, weave: host } ] }

---
kind: rules
entries:
  - { id: effects, patch: true, values: { host_depth: 1 } }

---
kind: patterns
entries:
  - { id: t_blank, name: Blank, extends: monster, level: 1, components: { weaves: { list: [] } } }

---
kind: charges
entries:
  - id: t_quicken
    duration: 4
    decay: 1
    modifiers:
      - { attribute: attack.speed, form: increased, value: 0.1 }
      - { attribute: damage, form: increased, value: 0.05, scope: [attack] }
  - id: t_thirst
    modifiers:
      - { attribute: attack.speed, form: increased, value: 0.1, condition: "charge.t_thirst.drain > 0" }
""";

    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        // A target kind for the form that needs one: bodies carrying the tag t_marked.
        TargetKinds.Register("t_marked", static (sim, _, e) => sim.World.Get<Stats>(e)?.Tags.Has("t_marked") ?? false, null, "test bodies tagged t_marked");
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-casting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "casting-tests.yaml"), Layer.Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.False(report.HasErrors, report.ToString());
            registry
                .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
                .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
            return registry;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    });

    public static ContentRegistry Registry => Loaded.Value;

    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;
}

/// <summary>A caster and a sturdy foe four cells east, behaviors removed, every event kept.</summary>
internal sealed class CastingArena
{
    public required Simulation Sim;
    public required Entity Caster;
    public required Entity Enemy;
    public readonly List<GameEvent> Events = new();

    public static CastingArena Create(ulong seed = 11, string caster = "loosed_kindler", string enemy = "hob", float distance = 4, ContentRegistry? registry = null)
    {
        var sim = ZoneBuilder.CreateArena(registry ?? CastingTestContent.Registry, seed);
        sim.EmitCastPresentation = true;
        var factory = sim.Service<PatternFactory>();
        var c = factory.Spawn(sim, caster, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var e = factory.Spawn(sim, enemy, new Vector3(10 + distance, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(c);
        sim.World.Remove<Behavior>(e);
        Sturdy(sim, c);
        Sturdy(sim, e);
        sim.World.Get<Transform>(c)!.Yaw = 0;
        var arena = new CastingArena { Sim = sim, Caster = c, Enemy = e };
        sim.Events.SubscribeAll(arena.Events.Add);
        return arena;
    }

    /// <summary>
    /// Plenty of life and mana and no regeneration, so a test is never cut short by a death or an empty pool
    /// and what a weave pays shows exactly.
    /// </summary>
    public static void Sturdy(Simulation sim, Entity e)
    {
        var stats = sim.World.Get<Stats>(e)!;
        stats.Attributes.Add(new Attributes.Modifier { Attribute = "life.max", Form = Attributes.ModifierForm.Flat, Value = 5000, Source = "sturdy" });
        stats.Attributes.Add(new Attributes.Modifier { Attribute = "mana.max", Form = Attributes.ModifierForm.Flat, Value = 500, Source = "sturdy" });
        foreach (var regen in new[] { "life.regen", "mana.regen", "stamina.regen", "ward.regen" })
        {
            stats.Attributes.Add(new Attributes.Modifier { Attribute = regen, Form = Attributes.ModifierForm.Override, Value = 0, Source = "sturdy" });
        }
        stats.FillResources();
    }

    /// <summary>Another body for the arena, standing still.</summary>
    public Entity Spawn(string pattern, Vector3 at, string faction = "blue")
    {
        var e = Sim.Service<PatternFactory>().Spawn(Sim, pattern, at, new SpawnOptions { Faction = faction });
        Sim.World.Remove<Behavior>(e);
        Sturdy(Sim, e);
        return e;
    }

    public WeaveRuntime Runtime => Sim.Service<WeaveRuntime>();
    public WeaveBook Book => Sim.World.Get<WeaveBook>(Caster)!;
    public Stats Stats => Sim.World.Get<Stats>(Caster)!;
    public Stats EnemyStats => Sim.World.Get<Stats>(Enemy)!;
    public double Now => Sim.Clock.Time;

    public WeaveSlot Slot(WeaveSpec spec) => Book.Add(spec);

    public WeaveSlot Slot(string formOrPreset, params GlyphRef[] glyphs) =>
        Slot(CastingTestContent.Registry.Has<WeaveDef>(formOrPreset) && glyphs.Length == 0
            ? new WeaveSpec { Preset = formOrPreset }
            : new WeaveSpec { Form = formOrPreset, Glyphs = glyphs.ToList() });

    public bool Begin(WeaveSlot slot, out string reason)
    {
        var at = Sim.World.Get<Transform>(Enemy)!.Position;
        return Runtime.TryBegin(Sim, Caster, slot, Enemy, at, Vector2.UnitX, out reason);
    }

    public void Begin(WeaveSlot slot)
    {
        Assert.True(Begin(slot, out var reason), reason);
    }

    public int SlotIndex(WeaveSlot slot) => Book.Slots.IndexOf(slot);

    public void RunSeconds(double seconds) => Sim.Run((int)Math.Round(seconds * Sim.Clock.TicksPerSecond));

    public IEnumerable<T> Of<T>() where T : GameEvent => Events.OfType<T>();

    public List<CustomEvent> Custom(string name) => Events.OfType<CustomEvent>().Where(e => e.Name == name).ToList();

    /// <summary>A damage effect from one body to another, as a delivery would land it.</summary>
    public void Hit(Entity source, Entity target, double amount, int depth = 0, string tags = "")
    {
        var effects = Sim.Service<EffectRegistry>();
        var spec = CastingTestContent.Spec($"{{\"type\": \"damage\", \"amount\": {amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"threads\": [\"mind\"], \"can_crit\": false, \"tags\": [{string.Join(", ", tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => "\"" + t + "\""))}]}}");
        var effect = effects.Compile(spec, new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test" });
        effect.Execute(new EffectContext
        {
            Sim = Sim,
            Source = source,
            Target = target,
            Point = Sim.World.Get<Transform>(target)!.Position,
            Depth = depth,
            Rng = new Rng(5),
        });
    }
}
