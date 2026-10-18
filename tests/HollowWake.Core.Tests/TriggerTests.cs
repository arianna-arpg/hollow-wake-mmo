using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Triggered weaves, repeats, metas, converts and the other slot behaviors.</summary>
public class TriggerTests
{
    // ------------------------------------------------------------ triggers

    [Fact]
    public void OneEventCastsOneTriggeredWeaveRoundRobinDownTheBar()
    {
        var arena = CastingArena.Create();
        var a = arena.Slot("t_spark_a");
        var b = arena.Slot("t_spark_b");
        arena.Sim.Tick();
        for (var i = 0; i < 4; i++)
        {
            arena.Hit(arena.Caster, arena.Enemy, 5);
        }
        var fired = arena.Of<WeaveTriggered>().Select(t => t.Weave).ToList();
        Assert.Equal(new[] { a.Key, b.Key, a.Key, b.Key }, fired);
        Assert.All(arena.Of<WeaveTriggered>(), t => Assert.Equal("hit", t.Kind));
        Assert.All(arena.Of<WeaveTriggered>(), t => Assert.Equal(arena.Enemy, t.Target));
        // Triggered casts resolve at once, one level deeper than the event, and are casts like any other.
        Assert.Equal(4, arena.Custom("t_shot").Count);
        Assert.All(arena.Of<WeaveTriggered>(), t => Assert.Equal(1, t.Depth));
        // Their costs are paid honestly: three mana each.
        Assert.Equal(arena.Stats.ResourceMax("mana") - 12, arena.Stats.Resource("mana"), 6);
    }

    [Fact]
    public void TriggersKeepTheirInternalCooldownAndTheDepthLimit()
    {
        var arena = CastingArena.Create();
        arena.Slot(new WeaveSpec { Form = "t_shot", Glyphs = { "t_on_hit_icd" } });
        arena.Sim.Tick();
        arena.Hit(arena.Caster, arena.Enemy, 5);
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Single(arena.Of<WeaveTriggered>());
        arena.RunSeconds(1.05);
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Equal(2, arena.Of<WeaveTriggered>().Count());
        // Events at the depth limit (rule triggers.max_depth) trigger nothing.
        arena.RunSeconds(1.05);
        arena.Hit(arena.Caster, arena.Enemy, 5, depth: 4);
        Assert.Equal(2, arena.Of<WeaveTriggered>().Count());
        arena.Hit(arena.Caster, arena.Enemy, 5, depth: 3);
        Assert.Equal(3, arena.Of<WeaveTriggered>().Count());
    }

    [Fact]
    public void SlowWeavesNeedAPermitAndUnaffordableOnesStayQuiet()
    {
        var arena = CastingArena.Create();
        var slow = arena.Slot(new WeaveSpec { Form = "t_slow_shot", Glyphs = { "t_on_hit" } });
        arena.Sim.Tick();
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Empty(arena.Of<WeaveTriggered>());
        // The permit lifts the cast-time limit.
        arena.Book.Set(arena.SlotIndex(slow), new WeaveSlot { Spec = new WeaveSpec { Form = "t_slow_shot", Glyphs = { "t_on_hit", "loosened_lattice" } } });
        arena.Sim.Tick();
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Single(arena.Of<WeaveTriggered>());
        // Without the mana to pay, nothing casts.
        arena.Stats.SetResource("mana", 0);
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Single(arena.Of<WeaveTriggered>());
    }

    [Fact]
    public void TheSlotKeyDisarmsAndRearmsATriggeredWeave()
    {
        var arena = CastingArena.Create();
        var spark = arena.Slot("t_spark_a");
        arena.Sim.Tick();
        arena.Begin(spark);
        Assert.False(Assert.Single(arena.Of<TriggerArmed>()).Armed);
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Empty(arena.Of<WeaveTriggered>());
        arena.Begin(spark);
        Assert.True(arena.Of<TriggerArmed>().Last().Armed);
        arena.Hit(arena.Caster, arena.Enemy, 5);
        Assert.Single(arena.Of<WeaveTriggered>());
    }

    [Fact]
    public void DamageTakenTriggersOnceEnoughLifeIsLost()
    {
        var arena = CastingArena.Create();
        arena.Slot(new WeaveSpec { Form = "t_shot", Glyphs = { "t_on_hurt" } });
        arena.Sim.Tick();
        var max = arena.Stats.ResourceMax("life");
        // A fifth of maximum life must accumulate; the triggered weave answers the attacker.
        arena.Hit(arena.Enemy, arena.Caster, max * 0.12);
        Assert.Empty(arena.Of<WeaveTriggered>());
        arena.Hit(arena.Enemy, arena.Caster, max * 0.12);
        var fired = Assert.Single(arena.Of<WeaveTriggered>());
        Assert.Equal("hurt", fired.Kind);
        Assert.Equal(arena.Enemy, fired.Target);
    }

    [Fact]
    public void ChannelBeatsTriggerWeavesOfTheirCaster()
    {
        var registry = CastingTestContent.Registry;
        var arena = CastingArena.Create(registry: registry);
        arena.Slot("undertow_bolt");
        var channel = arena.Slot("t_channel");
        arena.Sim.Tick();
        arena.Begin(channel);
        arena.RunSeconds(1.5);
        // Undertow's internal cooldown lets it answer every other beat.
        Assert.Equal(3, arena.Of<ChannelPulsed>().Count());
        Assert.Equal(2, arena.Of<WeaveTriggered>().Count(t => t.Kind == "channel_beat"));
    }

    // ------------------------------------------------------------ repeats

    [Fact]
    public void RepeatsRunAgainLargerAndCarryFractions()
    {
        var arena = CastingArena.Create();
        var scaled = arena.Slot(new WeaveSpec { Form = "t_marker", Glyphs = { "t_scaled_repeat" } });
        arena.Begin(scaled);
        arena.RunSeconds(0.5);
        var repeats = arena.Of<WeaveRepeated>().ToList();
        Assert.Equal(new[] { 1, 2 }, repeats.Select(r => r.Index));
        // Each repeat is larger by its scale: the radius grows half again per repeat.
        var radii = arena.Custom("t_marker").Select(m => m.Data["radius"]).ToList();
        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, radii);

        // Half a repeat per use: every second use repeats.
        var half = arena.Slot(new WeaveSpec { Form = "t_free", Glyphs = { "t_half_repeat" } });
        for (var i = 0; i < 4; i++)
        {
            arena.Begin(half);
            arena.RunSeconds(0.3);
        }
        Assert.Equal(2, arena.Of<WeaveRepeated>().Count(r => r.Weave == half.Key));
    }

    [Fact]
    public void RetargetedRepeatsTurnToTheNearestFoe()
    {
        var arena = CastingArena.Create(distance: 8);
        var near = arena.Spawn("hob", new Vector3(12, 10, 0));
        var slot = arena.Slot(new WeaveSpec { Form = "t_hit", Glyphs = { "t_retarget" } });
        arena.Begin(slot);
        arena.RunSeconds(0.5);
        var hits = arena.Of<DamageDealt>().Where(d => d.Source == arena.Caster).Select(d => d.Target).ToList();
        Assert.Equal(new[] { arena.Enemy, near }, hits);
        Assert.Equal(near, Assert.Single(arena.Of<WeaveRepeated>()).Target);
    }

    // ------------------------------------------------------------ metas, converts, chains

    [Fact]
    public void MetasRunInSlotOrderOneBeatApart()
    {
        var arena = CastingArena.Create();
        var host = arena.Slot(new WeaveSpec { Form = "t_meta_host", Glyphs = { "t_meta_b" } });
        arena.Sim.Tick();
        Assert.True(arena.Runtime.UseMeta(arena.Sim, arena.Caster, arena.SlotIndex(host), out var reason), reason);
        Assert.False(arena.Runtime.UseMeta(arena.Sim, arena.Caster, arena.SlotIndex(host), out var busy));
        Assert.Equal("busy", busy);
        arena.RunSeconds(0.5);
        var metas = arena.Of<MetaUsed>().ToList();
        Assert.Equal(new[] { "t_meta_a", "t_meta_b_preset" }, metas.Select(m => m.Meta));
        // The rule's beat between them.
        Assert.Equal(arena.Sim.Clock.TicksFromSeconds(arena.Sim.Content.Rules.Number("casting.meta_interval", 0)), metas[1].Tick - metas[0].Tick);
        // Metas cast at the host's level and cost nothing.
        Assert.All(arena.Custom("t_marker"), m => Assert.Equal(1, m.Data["level"]));
        Assert.Empty(arena.Custom("t_meta_host"));
        Assert.False(arena.Runtime.UseMeta(arena.Sim, arena.Caster, arena.SlotIndex(arena.Slot("t_free")), out var none));
        Assert.Equal("no_meta", none);
    }

    [Fact]
    public void ConvertsAndCombosPutAnotherFaceOnTheSlot()
    {
        var arena = CastingArena.Create();
        var ward = arena.Slot(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "last_word" } });
        Assert.Equal(ward.Key, arena.Runtime.FaceOf(arena.Sim, arena.Caster, ward)!.Key);
        arena.Stats.SetResource("life", arena.Stats.ResourceMax("life") * 0.2);
        Assert.Equal("lumen_ward", arena.Runtime.FaceOf(arena.Sim, arena.Caster, ward)!.Key);
        arena.Begin(ward);
        Assert.Contains(arena.Of<WeaveCast>(), c => c.Weave == "lumen_ward");

        // A combo chain walks its steps on presses inside its window, then starts over.
        var chain = arena.Slot(new WeaveSpec { Form = "t_free", Glyphs = { "t_chain" } });
        var faces = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            faces.Add(arena.Runtime.FaceOf(arena.Sim, arena.Caster, chain)!.Key);
            arena.Begin(chain);
            arena.RunSeconds(0.2);
        }
        Assert.Equal(new[] { chain.Key, "t_step_a", "t_step_b", chain.Key }, faces);
        arena.RunSeconds(1.2);
        Assert.Equal(chain.Key, arena.Runtime.FaceOf(arena.Sim, arena.Caster, chain)!.Key);
    }

    // ------------------------------------------------------------ follow-ups, cycles, stacks, worn modifiers

    [Fact]
    public void FollowUpsCyclesAndStacksFollowTheirUses()
    {
        var arena = CastingArena.Create();
        var follow = arena.Slot(new WeaveSpec { Form = "t_free", Glyphs = { "t_follow" } });
        arena.Begin(follow);
        arena.RunSeconds(0.2);
        Assert.Empty(arena.Custom("t_marker"));
        arena.RunSeconds(0.2);
        Assert.Single(arena.Custom("t_marker"));

        var cycle = arena.Slot(new WeaveSpec { Form = "t_free", Glyphs = { "t_cycle" } });
        for (var i = 0; i < 4; i++)
        {
            arena.Begin(cycle);
        }
        Assert.Equal(2, arena.Custom("t_cycle").Count);

        // Each use grows the pile, which empowers the next use; a pause drops it.
        var stack = arena.Slot(new WeaveSpec { Form = "t_burst", Glyphs = { "t_stack" } });
        for (var i = 0; i < 4; i++)
        {
            arena.Begin(stack);
        }
        var radii = arena.Custom("t_burst").Select(b => b.Data["radius"]).ToList();
        Assert.Equal(new[] { 2.0, 3.0, 4.0, 5.0 }, radii);
        Assert.Equal(3, arena.Runtime.Stacks(arena.Sim, stack));
        arena.RunSeconds(2.5);
        Assert.Equal(0, arena.Runtime.Stacks(arena.Sim, stack));
    }

    [Fact]
    public void SlottedWeavesGrantWornModifiersAndSeatsTakeGrafts()
    {
        var arena = CastingArena.Create();
        var armor = arena.Stats.Get("armor");
        var worn = arena.Slot(new WeaveSpec { Form = "t_free", Glyphs = { "t_worn" } });
        arena.Sim.Tick();
        Assert.Equal(armor + 15, arena.Stats.Get("armor"), 6);
        var index = arena.SlotIndex(worn);
        arena.Book.Set(index, new WeaveSlot { Spec = new WeaveSpec { Form = "t_free" } });
        arena.Sim.Tick();
        Assert.Equal(armor, arena.Stats.Get("armor"), 6);

        // A graft puts a glyph on whatever sits in a seat, at the graft's level.
        var seat = index + 1;
        arena.Stats.Attributes.Add(new Modifier { Attribute = $"glyph.slot{seat}.t_repeat", Form = ModifierForm.Flat, Value = 2, Source = "test" });
        arena.Book.Changed();
        arena.Sim.Tick();
        var weave = arena.Book.Slots[seat - 1].Compiled!;
        var graft = Assert.Single(weave.Grafts);
        Assert.Equal("t_repeat", graft.Id);
        Assert.Equal(2, graft.Level);
        Assert.Contains(weave.Glyphs, g => g.Id == "t_repeat");
    }

    [Fact]
    public void GraftsFromOutsideTheBookReachTheirSeatOnTheNextTick()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot("t_free");
        arena.Sim.Tick();
        var seat = arena.SlotIndex(slot) + 1;

        // A modifier from anywhere (here, as a ring would grant it) grafts the glyph; nothing tells the book.
        arena.Stats.Attributes.Add(new Modifier { Attribute = $"glyph.slot{seat}.t_repeat", Form = ModifierForm.Flat, Value = 1, Source = "ring" });
        arena.Sim.Tick();
        var weave = arena.Runtime.Resolve(arena.Sim, arena.Book.Slots[seat - 1], out _)!;
        Assert.Equal("t_repeat", Assert.Single(weave.Grafts).Id);

        // Losing it is noticed too.
        arena.Stats.Attributes.RemoveSource("ring");
        arena.Sim.Tick();
        Assert.Empty(arena.Runtime.Resolve(arena.Sim, arena.Book.Slots[seat - 1], out _)!.Grafts);
    }

    [Fact]
    public void ForwardingGlyphsRideAResonantSummonAndBillItTheirCosts()
    {
        var compiler = new WeaveCompiler(CastingTestContent.Registry, Effects.EffectRegistry.Shared);
        var keyed = compiler.Compile(new WeaveSpec { Form = "summon", Threads = { "umbra" }, Glyphs = { "resonant_key", "loosened_lattice", "t_ranged_trigger" } }, GlyphFit.Dormant);

        // The lattice fits no summon, but it forwards to the crew, so it rides: not dormant, nothing but its cost.
        Assert.Equal("loosened_lattice", Assert.Single(keyed.Riding).Id);
        Assert.DoesNotContain(keyed.Glyphs, g => g.Id == "loosened_lattice");
        Assert.Contains(keyed.Modifiers, m => m.Source == "glyph:loosened_lattice" && m.Attribute == "cost");
        Assert.False(keyed.HasMechanism("trigger_permit"));
        // A glyph with a trigger keeps to its owner's hands, so its misfit stays dormant; so does one that says it does not forward.
        Assert.Equal("t_ranged_trigger", Assert.Single(keyed.Dormant).Ref.Id);
        var unforwarded = compiler.Compile(new WeaveSpec { Form = "summon", Threads = { "umbra" }, Glyphs = { "resonant_key", "t_no_forward" } }, GlyphFit.Dormant);
        Assert.Equal("t_no_forward", Assert.Single(unforwarded.Dormant).Ref.Id);
        Assert.DoesNotContain(unforwarded.Modifiers, m => m.Source == "glyph:t_no_forward");

        // Strict compiles accept a rider; without the key the same glyph is a dormant misfit.
        Assert.True(compiler.TryCompile(new WeaveSpec { Form = "summon", Threads = { "umbra" }, Glyphs = { "resonant_key", "loosened_lattice" } }, out _, out var error), error);
        var plain = compiler.Compile(new WeaveSpec { Form = "summon", Threads = { "umbra" }, Glyphs = { "loosened_lattice" } }, GlyphFit.Dormant);
        Assert.Empty(plain.Riding);
        Assert.Equal("loosened_lattice", Assert.Single(plain.Dormant).Ref.Id);
        Assert.DoesNotContain(plain.Modifiers, m => m.Source == "glyph:loosened_lattice");
    }

    // ------------------------------------------------------------ effects of the casting family

    [Fact]
    public void FusesLandLaterAndHarder()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot("t_fused"));
        Assert.Empty(arena.Of<DamageDealt>());
        arena.RunSeconds(0.6);
        var hit = Assert.Single(arena.Of<DamageDealt>());
        var plain = CastingArena.Create();
        plain.Begin(plain.Slot("t_hit"));
        Assert.Equal(Assert.Single(plain.Of<DamageDealt>()).Total * 2, hit.Total, 3);
        Assert.Equal(1, hit.Depth - Assert.Single(plain.Of<DamageDealt>()).Depth);
    }

    [Fact]
    public void CooldownEffectsShaveOtherWeavesAndRestoresRefillBanks()
    {
        var arena = CastingArena.Create();
        var long_ = arena.Slot("t_press_cd");
        arena.Begin(long_);
        arena.Runtime.Interrupt(arena.Sim, arena.Caster);
        var ready = arena.Book.ReadyAt[long_.Key];
        arena.Begin(arena.Slot("t_shave"));
        // Two seconds off, then half of what remains: (5 - 2) / 2 left.
        Assert.Equal(arena.Now + 1.5, arena.Book.ReadyAt[long_.Key], 6);
        Assert.True(arena.Book.ReadyAt[long_.Key] < ready);

        var bank = arena.Slot("t_bank");
        arena.Begin(bank);
        arena.Begin(bank);
        Assert.Equal(0, arena.Runtime.Uses(arena.Sim, arena.Caster, bank, out _));
        var restore = arena.Sim.Service<EffectRegistry>().Compile(CastingTestContent.Spec("{\"type\": \"restore_uses\", \"scope\": \"all\", \"amount\": 1}"),
            new EffectCompileContext { Content = arena.Sim.Content, Registry = arena.Sim.Service<EffectRegistry>(), Owner = "test" });
        restore.Execute(new EffectContext { Sim = arena.Sim, Source = arena.Caster, Rng = new Rng(3) });
        Assert.Equal(1, arena.Runtime.Uses(arena.Sim, arena.Caster, bank, out _));
    }

    // ------------------------------------------------------------ the acceptance duel

    private static List<string> Duel(ulong seed)
    {
        var registry = CastingTestContent.Registry;
        var sim = ZoneBuilder.CreateArena(registry, seed);
        var factory = sim.Service<PatternFactory>();
        var channeler = factory.Spawn(sim, "tide_channeler", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var duelist = factory.Spawn(sim, "spark_duelist", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
        // Hold the target for this proc-order acceptance case. A freely kiting channeler now
        // leaves contact range during each melee windup, so its old guaranteed blows are misses.
        sim.World.Get<Stats>(channeler)!.Attributes.Add(new Modifier { Attribute = "move.speed", Form = ModifierForm.Override, Value = 0 });
        var trace = new List<string>();
        sim.Events.Subscribe<ChannelPulsed>(p => trace.Add($"{p.Tick} beat {p.Pulse}"));
        sim.Events.Subscribe<WeaveReleased>(r => trace.Add($"{r.Tick} release {r.Weave} {r.Reason} {r.Fired}"));
        sim.Events.Subscribe<WeaveTriggered>(t => trace.Add($"{t.Tick} trigger {t.Weave} {t.Kind}"));
        sim.Events.Subscribe<DamageDealt>(d => trace.Add($"{d.Tick} damage {d.Total:0.###}"));
        sim.RunSeconds(15);
        _ = channeler;
        _ = duelist;
        return trace;
    }

    [Fact]
    public void AChannelerAndATriggerCasterDuelTheSameWayTwice()
    {
        var first = Duel(41);
        Assert.Equal(first, Duel(41));
        Assert.Contains(first, l => l.Contains(" beat "));
        Assert.Contains(first, l => l.Contains(" release tide_torrent ") && l.EndsWith("True"));
        // The duelist's two spurred sparks take turns, one per landed melee blow.
        var sparks = first.Where(l => l.Contains(" trigger ")).Select(l => l.Split(' ')[2]).ToList();
        Assert.True(sparks.Count >= 2, string.Join("\n", first));
        for (var i = 0; i < sparks.Count; i++)
        {
            Assert.Equal(i % 2 == 0 ? "kindling_spark" : "static_spark", sparks[i]);
        }
    }
}
