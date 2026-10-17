using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Characters;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The cast modes and the slot behaviors of the weave runtime.</summary>
public class CastModeTests
{
    private static (Simulation Sim, Entity Hero, Player Player) Hero(ContentRegistry? registry = null)
    {
        var sim = ZoneBuilder.CreateArena(registry ?? CoreContent.Registry, 5);
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "c-cast", "a-cast", "Caster", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(5, 5, 0);
        var hero = characters.Materialize(sim, sheet);
        return (sim, hero, sim.World.Get<Player>(hero)!);
    }

    [Fact]
    public void PlayerSlotsKeepMisfitGlyphsDormantAcrossRecompiles()
    {
        var (sim, hero, player) = Hero();
        var rejected = new List<IntentRejected>();
        sim.Events.Subscribe<IntentRejected>(rejected.Add);
        // A strike cannot carry fork (it needs a projectile): the seat takes it anyway, dormant with its reason.
        player.Intents.Enqueue(new SetWeaveIntent(0, new WeaveSpec { Form = "strike", Glyphs = { "fork", "whetted" } }));
        sim.Tick();
        Assert.Empty(rejected);
        var book = sim.World.Get<WeaveBook>(hero)!;
        var slot = book.Slots[0];
        var dormant = Assert.Single(slot.Dormant);
        Assert.Equal("fork", dormant.Ref.Id);
        Assert.Contains("fork", dormant.Reason);

        // A gear change drops the compiled weave; the next resolve compiles it dormant again, so the seat still casts.
        book.InvalidateCompiled();
        var weave = sim.Service<WeaveRuntime>().Resolve(sim, slot, out var error);
        Assert.Null(error);
        Assert.NotNull(weave);
        Assert.Equal(new[] { "whetted" }, weave!.Glyphs.Select(g => g.Id));
        Assert.Equal("fork", Assert.Single(slot.Dormant).Ref.Id);
    }

    private sealed class Refusing : IWeaveOwnership
    {
        public bool Allows(Simulation sim, Entity hero, int slot, WeaveSpec spec, out string reason)
        {
            reason = "not_owned";
            return false;
        }
    }

    [Fact]
    public void OwnershipIsAskedOnlyWhileTheRuleHolds()
    {
        foreach (var required in new[] { false, true })
        {
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            registry.With(new RulesDef { Id = "casting", Values = new JsonObject { ["require_ownership"] = required } });
            var (sim, hero, player) = Hero(registry);
            sim.AddService<IWeaveOwnership>(new Refusing());
            var rejected = new List<IntentRejected>();
            sim.Events.Subscribe<IntentRejected>(rejected.Add);
            player.Intents.Enqueue(new SetWeaveIntent(1, new WeaveSpec { Preset = "ember_bolt" }));
            sim.Tick();
            var book = sim.World.Get<WeaveBook>(hero)!;
            if (required)
            {
                Assert.Equal("not_owned", Assert.Single(rejected).Reason);
                Assert.NotEqual("ember_bolt", book.Slots[1].Spec.Preset);
            }
            else
            {
                Assert.Empty(rejected);
                Assert.Equal("ember_bolt", book.Slots[1].Spec.Preset);
            }
        }
    }

    // ------------------------------------------------------------ moving while casting, cooldowns

    [Fact]
    public void CastsKeepTheirFormsShareOfSpeedPlusMobility()
    {
        var arena = CastingArena.Create();
        var walk = arena.Slot("t_walk");
        arena.Begin(walk);
        Assert.Equal(0.5f, arena.Book.Casting!.MoveFactor, 4);
        arena.Runtime.Interrupt(arena.Sim, arena.Caster);

        // Attribute cast.mobility adds to the form's own factor; the factor stays within zero and one.
        arena.Stats.Attributes.Add(new Modifier { Attribute = "cast.mobility", Form = ModifierForm.Flat, Value = 0.25, Source = "test" });
        arena.Sim.Run(3);
        arena.Begin(walk);
        Assert.Equal(0.75f, arena.Book.Casting!.MoveFactor, 4);
        arena.Runtime.Interrupt(arena.Sim, arena.Caster);
        arena.Sim.Run(3);
        arena.Begin(arena.Slot("t_rooted"));
        Assert.Equal(0.25f, arena.Book.Casting!.MoveFactor, 4);
        arena.Runtime.Interrupt(arena.Sim, arena.Caster);

        // A channel keeps its own move factor.
        arena.Stats.Attributes.RemoveSource("test");
        arena.Sim.Run(3);
        arena.Begin(arena.Slot("t_channel"));
        Assert.Equal(0.3f, arena.Book.Casting!.MoveFactor, 4);
    }

    [Fact]
    public void CooldownsStartWhereTheFormSaysAndTakeLevies()
    {
        var arena = CastingArena.Create();
        var pressed = arena.Slot("t_press_cd");
        var done = arena.Slot("t_done_cd");
        var start = arena.Now;
        arena.Begin(pressed);
        Assert.Equal(start + 5, arena.Book.ReadyAt[pressed.Key], 6);
        arena.RunSeconds(1.2);
        Assert.Single(arena.Custom("t_press_cd"));
        Assert.Equal(start + 5, arena.Book.ReadyAt[pressed.Key], 6);

        var second = arena.Now;
        arena.Begin(done);
        Assert.False(arena.Book.ReadyAt.ContainsKey(done.Key));
        arena.RunSeconds(1.2);
        Assert.Single(arena.Custom("t_done_cd"));
        // The complete clock starts when the bar finishes, one second (and a tick) after the press.
        Assert.InRange(arena.Book.ReadyAt[done.Key] - second, 6 - 1e-6, 6.06);

        // A glyph levies a cooldown on a weave that has none, and recovery shortens it.
        var levied = arena.Slot("t_free", "t_levy");
        var at = arena.Now;
        arena.Begin(levied);
        Assert.Equal(at + 2, arena.Book.ReadyAt[levied.Key], 6);
        Assert.True(levied.Compiled!.HasMechanism("cooldown"));
        arena.Stats.Attributes.Add(new Modifier { Attribute = "cooldown.recovery", Form = ModifierForm.Increased, Value = 1, Source = "test" });
        arena.RunSeconds(2.1);
        at = arena.Now;
        arena.Begin(levied);
        Assert.Equal(at + 1, arena.Book.ReadyAt[levied.Key], 6);
    }

    // ------------------------------------------------------------ channels

    [Fact]
    public void ChannelsWindUpRampAndReleaseAtTheirLimit()
    {
        var arena = CastingArena.Create();
        var channel = arena.Slot("t_channel");
        var baseSpeed = arena.Stats.Get("move.speed");
        var start = arena.Now;
        arena.Begin(channel);
        Assert.True(arena.Stats.HasFlag("channeling"));
        arena.RunSeconds(0.2);
        Assert.Empty(arena.Custom("t_pulse"));
        arena.RunSeconds(2.2);

        var pulses = arena.Custom("t_pulse");
        Assert.Equal(4, pulses.Count);
        // The first beat waits for the windup, then one every half second.
        var beats = arena.Of<ChannelPulsed>().Select(p => Math.Round(p.Held, 2)).ToList();
        Assert.Equal(new[] { 0.25, 0.75, 1.25, 1.75 }, beats);
        // The ramp grows the radius with held time up to its cap: 2 * (1 + min(0.75, 0.5 * held)).
        var radii = pulses.Select(p => Math.Round(p.Data["radius"], 3)).ToList();
        Assert.Equal(new[] { 2.25, 2.75, 3.25, 3.5 }, radii);

        // At its limit the channel releases: the payload runs with the held time and the full ramp.
        var release = Assert.Single(arena.Custom("t_release"));
        Assert.Equal(2, release.Data["held"], 2);
        Assert.Equal(3.5, release.Data["radius"], 3);
        var ended = Assert.Single(arena.Of<WeaveReleased>());
        Assert.True(ended.Fired);
        Assert.Equal("max_hold", ended.Reason);
        Assert.Null(arena.Book.Casting);
        Assert.False(arena.Stats.HasFlag("channeling"));
        // The caster's ramp came off with the channel, and the cooldown started when it ended.
        Assert.Equal(baseSpeed, arena.Stats.Get("move.speed"), 6);
        Assert.InRange(arena.Book.ReadyAt[channel.Key] - start, 5 - 1e-6, 5.06);
        // Four beats and the release are casts; the press paid the first beat and each later beat paid its own.
        Assert.Equal(5, arena.Of<WeaveCast>().Count(c => c.Weave == channel.Key));
        Assert.Equal(arena.Stats.ResourceMax("mana") - 8, arena.Stats.Resource("mana"), 6);
    }

    [Fact]
    public void ChannelsSlowTheirCasterWhileHeldAndReleaseOnlyAfterTheirMinimum()
    {
        var arena = CastingArena.Create();
        var channel = arena.Slot("t_channel");
        var baseSpeed = arena.Stats.Get("move.speed");
        arena.Begin(channel);
        arena.RunSeconds(1.3);
        // The caster ramp: move.speed more by -0.2 per held second, so slower than at rest.
        Assert.True(arena.Stats.Get("move.speed") < baseSpeed - 1e-6);
        Assert.True(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(channel)));
        var release = Assert.Single(arena.Custom("t_release"));
        Assert.InRange(release.Data["held"], 1.29, 1.31);
        Assert.Equal(baseSpeed, arena.Stats.Get("move.speed"), 6);

        // Released before its minimum hold, nothing fires; a stun interrupts without a release.
        arena.Book.ReadyAt.Clear();
        arena.Begin(channel);
        arena.RunSeconds(0.3);
        Assert.True(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(channel)));
        Assert.False(arena.Of<WeaveReleased>().Last().Fired);
        arena.Book.ReadyAt.Clear();
        arena.Begin(channel);
        arena.RunSeconds(0.6);
        arena.Stats.Flags.Add("stunned");
        arena.Sim.Tick();
        Assert.Null(arena.Book.Casting);
        Assert.Single(arena.Of<WeaveInterrupted>());
        Assert.Single(arena.Custom("t_release"));
        // Releasing a slot that holds nothing is ignored.
        Assert.False(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(channel)));
    }

    [Fact]
    public void ForbiddenTagsInterruptARunningWeave()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot("t_channel"));
        arena.RunSeconds(0.5);
        arena.Stats.Flags.Add("forbid:weave");
        arena.Sim.Tick();
        Assert.Null(arena.Book.Casting);
        Assert.Single(arena.Of<WeaveInterrupted>());
    }

    // ------------------------------------------------------------ charge-ups and overcharges

    [Fact]
    public void ChargeUpsScaleByTheirFillAndReleaseWhenHeldFull()
    {
        var arena = CastingArena.Create();
        var charge = arena.Slot("t_charge");
        arena.Begin(charge);
        Assert.Equal(1, arena.Book.Casting!.Duration, 6);
        arena.RunSeconds(0.5);
        Assert.True(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(charge)));
        var half = Assert.Single(arena.Custom("t_charge"));
        Assert.Equal(0.5, half.Data["fill"], 6);
        // The radius scales from its value at no charge to double at a full one.
        Assert.Equal(3, half.Data["radius"], 6);

        // Held past full, it releases by itself after its full hold.
        arena.Begin(charge);
        arena.RunSeconds(1.6);
        var full = arena.Custom("t_charge")[1];
        Assert.Equal(1, full.Data["fill"], 6);
        Assert.Equal(4, full.Data["radius"], 6);
        Assert.Equal("full", arena.Of<WeaveReleased>().Last().Reason);
        Assert.True(arena.Of<WeaveReleased>().Last().Held >= 1.5 - 1e-6);
    }

    [Fact]
    public void OverchargesClimbPayingEachStageAndSparkOnTheBeat()
    {
        var arena = CastingArena.Create();
        var over = arena.Slot("t_over");
        var mana = arena.Stats.Resource("mana");
        arena.Begin(over);
        Assert.Equal(3, arena.Book.Casting!.Stages);
        arena.RunSeconds(1.05);
        var stages = arena.Of<OverchargeStaged>().ToList();
        Assert.Equal(new[] { 1, 2 }, stages.Select(s => s.Stage));
        // Five mana a stage, on top of what the press paid.
        Assert.Equal(10, mana - arena.Stats.Resource("mana"), 6);
        // Released a beat after the second stage: two stages banked and the spark's bonus.
        Assert.True(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(over)));
        var fired = Assert.Single(arena.Custom("t_over"));
        Assert.Equal(2, fired.Data["stage"]);
        Assert.Equal(4, fired.Data["radius"], 6);

        // A glyph raises the stages; held to the last, it releases by itself after its full hold.
        arena.Book.ReadyAt.Clear();
        var lensed = arena.Slot(new WeaveSpec { Form = "t_over", Glyphs = { "third_lens" } });
        arena.Begin(lensed);
        Assert.Equal(4, arena.Book.Casting!.Stages);
        arena.RunSeconds(3);
        Assert.Equal(4, arena.Custom("t_over")[1].Data["stage"]);
        Assert.Equal("full", arena.Of<WeaveReleased>().Last().Reason);
    }

    // ------------------------------------------------------------ strike timing and aim

    [Fact]
    public void APressInTheGoldenTailLandsFlawless()
    {
        double Dealt(double pressAt)
        {
            var arena = CastingArena.Create();
            var timed = arena.Slot("t_timed");
            var flawless = new List<TriggerEvent>();
            arena.Sim.Service<TriggerHub>().Subscribe("flawless", arena.Caster, (in TriggerEvent e) => flawless.Add(e));
            arena.Begin(timed);
            arena.RunSeconds(pressAt);
            Assert.True(arena.Runtime.Press(arena.Sim, arena.Caster, arena.SlotIndex(timed)));
            arena.RunSeconds(1.2);
            var pressed = Assert.Single(arena.Of<WeavePressed>());
            Assert.Equal(pressed.Flawless, flawless.Count == 1);
            return arena.Of<DamageDealt>().Single(d => d.Source == arena.Caster).Total;
        }
        var early = Dealt(0.3);
        var late = Dealt(0.85);
        // A flawless press adds its bonus as more damage: twice the blow.
        Assert.Equal(early * 2, late, 3);
    }

    [Fact]
    public void AimIsRateLimitedClampedAndMirroredOnTheCast()
    {
        var arena = CastingArena.Create();
        Assert.True(arena.Runtime.Aim(arena.Sim, arena.Caster, new Vector3(12, 10, 0), new Vector2(0, 2)));
        Assert.Equal(Vector2.UnitY, arena.Book.AimDirection);
        // Faster than the rule's rate is dropped.
        Assert.False(arena.Runtime.Aim(arena.Sim, arena.Caster, new Vector3(13, 10, 0), Vector2.UnitX));
        arena.RunSeconds(0.2);
        // A point beyond the rule's range is pulled within it.
        Assert.True(arena.Runtime.Aim(arena.Sim, arena.Caster, new Vector3(1010, 10, 0), Vector2.UnitX));
        Assert.Equal(50, arena.Book.AimPoint!.Value.X, 3);
        arena.RunSeconds(0.2);
        arena.Begin(arena.Slot("t_channel"));
        Assert.True(arena.Runtime.Aim(arena.Sim, arena.Caster, new Vector3(10, 14, 0), Vector2.UnitY));
        Assert.Equal(new Vector3(10, 14, 0), arena.Book.Casting!.AimPoint);
        Assert.Equal(Vector2.UnitY, arena.Book.Casting.AimDirection);
    }

    // ------------------------------------------------------------ use banks

    [Fact]
    public void BanksTrickleBackAndMagazinesRefill()
    {
        var arena = CastingArena.Create();
        var bank = arena.Slot("t_bank");
        arena.Begin(bank);
        arena.Begin(bank);
        Assert.False(arena.Begin(bank, out var reason));
        Assert.Equal("uses", reason);
        Assert.Equal(0, arena.Runtime.Uses(arena.Sim, arena.Caster, bank, out var max));
        Assert.Equal(2, max);
        arena.RunSeconds(1.05);
        Assert.Equal(1, arena.Runtime.Uses(arena.Sim, arena.Caster, bank, out _));
        arena.Begin(bank);

        // A magazine refills all at once when emptied; a dripping one fills a use at a time.
        var mag = arena.Slot("t_mag");
        arena.Begin(mag);
        arena.Begin(mag);
        arena.RunSeconds(1);
        Assert.False(arena.Begin(mag, out _));
        arena.RunSeconds(0.6);
        Assert.Equal(2, arena.Runtime.Uses(arena.Sim, arena.Caster, mag, out _));
        var drip = arena.Slot("t_drip");
        arena.Begin(drip);
        arena.Begin(drip);
        arena.RunSeconds(1.05);
        Assert.Equal(1, arena.Runtime.Uses(arena.Sim, arena.Caster, drip, out _));
        Assert.All(arena.Of<UsesChanged>(), u => Assert.True(u.Uses >= 0));
    }

    [Fact]
    public void StillBanksRecoverOnlyStandingStillAndVentsStepFromTheBank()
    {
        var arena = CastingArena.Create();
        var still = arena.Slot("t_still");
        arena.Begin(still);
        arena.Begin(still);
        arena.Sim.World.Get<Motion>(arena.Caster)!.MoveDir = Vector2.UnitY;
        arena.RunSeconds(2);
        Assert.Equal(0, arena.Runtime.Uses(arena.Sim, arena.Caster, still, out _));
        arena.Sim.World.Get<Motion>(arena.Caster)!.MoveDir = Vector2.Zero;
        arena.RunSeconds(2);
        Assert.True(arena.Runtime.Uses(arena.Sim, arena.Caster, still, out _) >= 1);

        // Venting spends the whole bank and casts once more for each extra use.
        var vent = arena.Slot("t_vent");
        arena.Begin(vent);
        Assert.Equal(3, Assert.Single(arena.Custom("t_vent")).Data["spent"]);
        arena.RunSeconds(1);
        Assert.Equal(2, arena.Of<WeaveRepeated>().Count(r => r.Weave == vent.Key));
    }

    [Fact]
    public void AnEmptyBankConvertsTheSlotToItsRestring()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot("t_reloading");
        Assert.Same(slot.Compiled ?? arena.Runtime.Resolve(arena.Sim, slot, out _), arena.Runtime.FaceOf(arena.Sim, arena.Caster, slot));
        arena.Begin(slot);
        Assert.Equal("quiver_restring", arena.Runtime.FaceOf(arena.Sim, arena.Caster, slot)!.Key);
        // The press casts the restring (a bar), which puts the host slot's bank back.
        arena.Begin(slot);
        Assert.Equal("quiver_restring", arena.Book.Casting!.Weave!.Key);
        arena.RunSeconds(1);
        Assert.Equal(1, arena.Runtime.Uses(arena.Sim, arena.Caster, slot, out _));
        Assert.Contains(arena.Of<WeaveCast>(), c => c.Weave == "quiver_restring");
        Assert.Equal(slot.Key, arena.Runtime.FaceOf(arena.Sim, arena.Caster, slot)!.Key);
    }

    // ------------------------------------------------------------ gates, costs, spends

    [Fact]
    public void GatesRefuseWithTheirReasonAndAddedCostsArePaid()
    {
        var arena = CastingArena.Create();
        var gated = arena.Slot("t_gated");
        arena.Begin(gated);
        arena.Stats.SetResource("life", arena.Stats.ResourceMax("life") * 0.3);
        Assert.False(arena.Begin(gated, out var reason));
        Assert.Equal("gate:life_pct > 0.5", reason);

        // A glyph's gate joins the form's; its added cost charges a resource the weave did not cost.
        arena.Stats.FillResources();
        var levy = arena.Slot(new WeaveSpec { Form = "t_hit", Glyphs = { "t_levy" } });
        var life = arena.Stats.Resource("life");
        arena.Stats.Attributes.Add(new Modifier { Attribute = "cost.added.life", Form = ModifierForm.Flat, Value = 6, Source = "test" });
        arena.Begin(levy);
        Assert.Equal(life - 6, arena.Stats.Resource("life"), 6);
        Assert.Equal(6, arena.Runtime.Costs(levy.Compiled!, arena.Stats)["life"], 6);

        // Converting a cost moves part of it to another resource.
        arena.Stats.Attributes.Add(new Modifier { Attribute = "cost.convert.life.mana", Form = ModifierForm.Flat, Value = 0.5, Source = "test" });
        var costs = arena.Runtime.Costs(levy.Compiled!, arena.Stats);
        Assert.Equal(3, costs["life"], 6);
        Assert.Equal(3, costs["mana"], 6);
    }

    [Fact]
    public void ChargeSpendsScaleAUseAndRefuseWithoutTheirMinimum()
    {
        var arena = CastingArena.Create();
        var spender = arena.Slot(new WeaveSpec { Form = "t_spender", Glyphs = { "t_spend" } });
        Assert.False(arena.Begin(spender, out var reason));
        Assert.Equal("charges:t_fury", reason);
        arena.Sim.Service<ChargeService>().Gain(arena.Sim, arena.Caster, "t_fury", 3);
        arena.Begin(spender);
        var burst = Assert.Single(arena.Custom("t_spender"));
        Assert.Equal(3, burst.Data["spent"]);
        // Each charge spent adds half the radius: 2 * (1 + 1.5).
        Assert.Equal(5, burst.Data["radius"], 6);
        Assert.Equal(0, arena.Sim.Service<ChargeService>().Count(arena.Sim, arena.Caster, "t_fury"));
    }

    [Fact]
    public void TargetKindsRefuseBeforeCostsAreSpent()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot("t_corpse_only");
        var mana = arena.Stats.Resource("mana");
        Assert.False(arena.Begin(slot, out var reason));
        Assert.Equal("no_target", reason);
        Assert.Equal(mana, arena.Stats.Resource("mana"), 6);
        // A fitting body near the aim is found by the kind's search.
        var marked = arena.Spawn("hob", new Vector3(15, 11, 0));
        var stats = arena.Sim.World.Get<Stats>(marked)!;
        stats.Tags = stats.Tags.With("t_marked");
        arena.Begin(slot);
        Assert.Contains(arena.Of<WeaveCast>(), c => c.Weave == slot.Key && c.Target == marked);
    }

    // ------------------------------------------------------------ guards

    [Fact]
    public void GuardsParryFreshAbsorbInTheirArcAndBash()
    {
        var arena = CastingArena.Create();
        var guard = arena.Slot("t_guard");
        arena.Begin(guard);
        Assert.True(arena.Stats.HasFlag("guarding"));
        Assert.Single(arena.Custom("t_raise"));
        var life = arena.Stats.Resource("life");

        // In the parry window a hit from the front is parried whole.
        arena.Hit(arena.Enemy, arena.Caster, 50);
        Assert.Single(arena.Of<GuardParried>());
        Assert.Equal(life, arena.Stats.Resource("life"), 6);
        Assert.True(arena.Of<DamageDealt>().Last().Blocked);

        // Afterwards the pool takes what it can: a hit inside it is caught, a larger one partly seeps.
        arena.RunSeconds(0.3);
        arena.Hit(arena.Enemy, arena.Caster, 10);
        Assert.Equal(life, arena.Stats.Resource("life"), 6);
        var behind = arena.Spawn("hob", new Vector3(6, 10, 0));
        arena.Hit(behind, arena.Caster, 10);
        Assert.True(arena.Stats.Resource("life") < life - 5);
        var guardState = arena.Sim.World.Get<Guard>(arena.Caster)!;
        Assert.Equal(10, guardState.Absorbed, 6);

        // Released armed (held past its arm time with a third of the pool absorbed), it bashes.
        arena.RunSeconds(0.3);
        Assert.True(arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(guard)));
        var bash = Assert.Single(arena.Custom("t_bash"));
        Assert.Equal(10, bash.Data["absorbed"], 6);
        Assert.False(arena.Sim.World.Has<Guard>(arena.Caster));
        Assert.False(arena.Stats.HasFlag("guarding"));

        // A pool that runs dry breaks the guard, which ends without a bash.
        arena.Book.ReadyAt.Clear();
        arena.Begin(guard);
        arena.RunSeconds(0.3);
        arena.Hit(arena.Enemy, arena.Caster, 100);
        arena.Sim.Tick();
        Assert.Single(arena.Of<GuardBroken>());
        Assert.Equal("break", arena.Of<WeaveReleased>().Last().Reason);
        Assert.Single(arena.Custom("t_bash"));
    }

    // ------------------------------------------------------------ toggles

    [Fact]
    public void TogglesReserveAndPayUpkeepUntilTheyStarve()
    {
        var arena = CastingArena.Create();
        var toggle = arena.Slot("t_toggle");
        arena.Begin(toggle);
        Assert.Single(arena.Custom("t_on"));
        Assert.True(WeaveRuntime.IsToggledOn(toggle));
        Assert.Equal(20, arena.Stats.Get("mana.reserved"), 6);
        Assert.True(arena.Stats.HasFlag("toggle.t_toggle"));
        var mana = arena.Stats.Resource("mana");
        arena.RunSeconds(1);
        Assert.InRange(mana - arena.Stats.Resource("mana"), 4, 10.5);

        // Pressing again turns it off and runs its deactivation; the reservation comes off.
        arena.Begin(toggle);
        Assert.False(WeaveRuntime.IsToggledOn(toggle));
        Assert.Single(arena.Custom("t_off"));
        Assert.Equal(0, arena.Stats.Get("mana.reserved"), 6);

        // Without mana to pay the upkeep it starves.
        arena.Book.ReadyAt.Clear();
        arena.Begin(toggle);
        arena.Stats.SetResource("mana", 1);
        arena.Stats.Attributes.Add(new Modifier { Attribute = "mana.regen", Form = ModifierForm.Override, Value = 0, Source = "test" });
        arena.RunSeconds(0.5);
        Assert.False(WeaveRuntime.IsToggledOn(toggle));
        Assert.Equal("starved", arena.Of<WeaveToggled>().Last().Reason);
    }

    // ------------------------------------------------------------ reflexes, recovery, fizzles

    [Fact]
    public void ReflexesPierceACastRecoveryHoldsAndFizzlesStun()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot("t_walk"));
        Assert.False(arena.Begin(arena.Slot("t_free"), out var busy));
        Assert.Equal("busy", busy);
        arena.Begin(arena.Slot("t_reflex"));
        Assert.Single(arena.Custom("t_reflex"));
        Assert.NotNull(arena.Book.Casting);
        arena.Runtime.Interrupt(arena.Sim, arena.Caster);

        var recover = arena.Slot("t_recover");
        arena.Begin(recover);
        Assert.False(arena.Begin(arena.Slot("t_free"), out var held));
        Assert.Equal("cooldown", held);
        arena.RunSeconds(1.05);

        arena.Stats.Attributes.Add(new Modifier { Attribute = "cast.fizzle_chance", Form = ModifierForm.Flat, Value = 1, Source = "test" });
        Assert.False(arena.Begin(arena.Slot("t_free"), out var fizzled));
        Assert.Equal("fizzled", fizzled);
        Assert.True(arena.Stats.HasFlag("stunned"));
    }

    // ------------------------------------------------------------ the recent-cast ring

    [Fact]
    public void TheBookRemembersRecentCastsForVariedAndRepeatedCasting()
    {
        var arena = CastingArena.Create();
        var a = arena.Slot("t_free");
        var b = arena.Slot("t_marker");
        arena.Begin(a);
        arena.Begin(a);
        Assert.True(arena.Book.RecentlyRepeated(2));
        arena.Begin(b);
        Assert.True(arena.Book.RecentlyVaried(2));
        Assert.False(arena.Book.RecentlyRepeated(2));
        Assert.Equal(b.Key, arena.Book.Recent(0));
        Assert.Equal(a.Key, arena.Book.Recent(1));
    }
}
