using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Seeded construct scenarios, the independent review's probes made tests: an echo replaying its owner's casts
/// and a totem firing on its own clock, measured and traced event by event.
/// </summary>
public class ConstructScenarioTests
{
    /// <summary>A foe that takes no damage, so a scenario runs its whole length.</summary>
    private static Entity Dummy(MinionArena arena, float east)
    {
        var foe = arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(east, 0, 0));
        arena.Sim.World.Get<Stats>(foe)!.Attributes.Add(new Modifier { Attribute = "damage.taken", Form = ModifierForm.More, Value = -1, Source = "scenario" });
        return foe;
    }

    /// <summary>The scenario's casts and hits from <paramref name="from"/> on, one line each, with their tick.</summary>
    private static void Trace(MinionArena arena, StringBuilder sb, ref int from, Func<Entity, string?> role)
    {
        for (; from < arena.Events.Count; from++)
        {
            switch (arena.Events[from])
            {
                case WeaveCast w when role(w.Caster) is { } who:
                    sb.Append(CultureInfo.InvariantCulture, $"{w.Tick} cast {who} {w.Weave}\n");
                    break;
                case DamageDealt d when role(d.Source) is { } who:
                    sb.Append(CultureInfo.InvariantCulture, $"{d.Tick} hit {who} {d.Total.ToString("R", CultureInfo.InvariantCulture)}{(d.Crit ? " crit" : "")}\n");
                    break;
            }
        }
    }

    /// <summary>The owner casts its spit whenever it is ready for ten seconds, with an echo beside it.</summary>
    private static (int OwnerCasts, int EchoCasts, double FirstEchoAfter, string Trace) Echo(double ownerCastSpeed, float foeAt)
    {
        var arena = MinionArena.Create(9);
        if (ownerCastSpeed != 1)
        {
            arena.OwnerStats.Attributes.Add(new Modifier { Attribute = "cast.speed", Form = ModifierForm.More, Value = ownerCastSpeed - 1, Source = "scenario" });
        }
        var foe = Dummy(arena, foeAt);
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = arena.Minions("echoes").Single();
        arena.Sim.RunSeconds(0.5);
        var book = arena.Sim.World.Get<WeaveBook>(arena.Owner)!;
        var spit = book.Find("cinder_spit")!;
        var runtime = arena.Sim.Service<WeaveRuntime>();
        arena.Events.Clear();
        double? firstOwner = null, firstEcho = null;
        var sb = new StringBuilder();
        var seen = 0;
        string? Role(Entity e) => e == arena.Owner ? "owner" : e == echo ? "echo" : null;
        for (var i = 0; i < 10 * arena.Sim.Clock.TicksPerSecond; i++)
        {
            if (book.Casting is null && book.IsReady(spit, arena.Sim.Clock.Time))
            {
                arena.OwnerStats.FillResources();
                runtime.TryBegin(arena.Sim, arena.Owner, spit, foe, arena.At(foe), Vector2.Zero, out _);
            }
            arena.Sim.Tick();
            foreach (var w in arena.Events.Skip(seen).OfType<WeaveCast>())
            {
                if (w.Caster == arena.Owner) firstOwner ??= arena.Sim.Clock.Time;
                if (w.Caster == echo) firstEcho ??= arena.Sim.Clock.Time;
            }
            Trace(arena, sb, ref seen, Role);
        }
        var ownerCasts = arena.Events.OfType<WeaveCast>().Count(w => w.Caster == arena.Owner && w.Weave == spit.Key);
        var echoCasts = arena.Events.OfType<WeaveCast>().Count(w => w.Caster == echo);
        return (ownerCasts, echoCasts, (firstEcho ?? double.NaN) - (firstOwner ?? double.NaN), sb.ToString());
    }

    /// <summary>A totem planted a unit from its owner, with a foe in reach, for ten seconds.</summary>
    private static (int Casts, double FirstAfter, string Trace) Totem(string preset, string group, float foeAt)
    {
        var arena = MinionArena.Create(9);
        Dummy(arena, foeAt);
        arena.Cast(arena.Learn(preset), point: arena.OwnerAt.Position + new Vector3(1, 0, 0));
        var totem = arena.Minions(group).Single();
        arena.Events.Clear();
        var start = arena.Sim.Clock.Time;
        var sb = new StringBuilder();
        var seen = 0;
        string? Role(Entity e) => e == totem ? "totem" : e == arena.Owner ? "owner" : null;
        for (var i = 0; i < 10 * arena.Sim.Clock.TicksPerSecond; i++)
        {
            arena.Sim.Tick();
            Trace(arena, sb, ref seen, Role);
        }
        var casts = arena.Events.OfType<WeaveCast>().Where(w => w.Caster == totem).ToList();
        var first = casts.Count > 0 ? casts[0].Tick / (double)arena.Sim.Clock.TicksPerSecond - start : double.NaN;
        return (casts.Count, first, sb.ToString());
    }

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void AnEchoReplaysEveryCastItsOwnerMakes()
    {
        foreach (var (speed, at) in new[] { (1.0, 9f), (2.0, 9f), (1.0, 14f) })
        {
            var (owner, echo, _, _) = Echo(speed, at);
            Assert.True(owner > 0);
            Assert.Equal(owner, echo);
        }
    }

    [Fact]
    public void TheEchoScenariosRunAsTheyDidAndTheTotemsOnTheirOwnClock()
    {
        var echo = Echo(1, 9);
        var faster = Echo(2, 9);
        var far = Echo(1, 14);
        Assert.Equal((15, 15), (echo.OwnerCasts, echo.EchoCasts));
        Assert.Equal((29, 29), (faster.OwnerCasts, faster.EchoCasts));
        Assert.Equal((15, 15), (far.OwnerCasts, far.EchoCasts));
        Assert.Equal(0.10, echo.FirstEchoAfter, 2);
        // Event for event what the legacy construct path made: a replay is a resolution of the owner's use.
        Assert.Equal("c28c136d0d052256fed7213509becff09f0c008fd135fce1035412920f522977", Sha256(echo.Trace + faster.Trace + far.Trace));

        // A totem's bolt is a real use with its cast time, so the first flies after a bar and a pace, and ten
        // seconds hold nine (the legacy path fired the first after 0.35 seconds and ten in all; the owner decides).
        var totem = Totem("plant_ember_totem", "ember_totems", 8);
        var plain = Totem("t_totem_plain", "t_plain_totems", 8);
        Assert.Equal(9, totem.Casts);
        Assert.Equal(1.00, totem.FirstAfter, 2);
        Assert.Equal(9, plain.Casts);
        Assert.Equal(1.00, plain.FirstAfter, 2);
        Assert.Equal("837978b2776911e855985caebfab7939c8a13259a48feba1ab5e52fbb148dce8", Sha256(totem.Trace + plain.Trace));
    }

    [Fact]
    public void ATotemsCastPaysItsCostAndWaitsItsCastTime()
    {
        var arena = MinionArena.Create(9);
        Dummy(arena, 8);
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 0, 0));
        var totem = arena.Minions("ember_totems").Single();
        var stats = arena.Sim.World.Get<Stats>(totem)!;
        var book = arena.Sim.World.Get<WeaveBook>(totem)!;
        var runtime = arena.Sim.Service<WeaveRuntime>();
        var weave = runtime.Resolve(arena.Sim, book.Slots[0], out _)!;
        // The shipped devices cast free: their pattern folds every cost to nothing, through the same costs.
        Assert.Equal(0, runtime.Costs(weave, stats)["mana"]);
        // Without that waiver the totem pays as anyone would.
        stats.Attributes.RemoveSource("pattern");
        var cost = runtime.Costs(weave, stats)["mana"];
        Assert.True(cost > 0);
        double? manaAtStart = null, paid = null, startedAt = null, castAt = null, duration = null;
        arena.Sim.Events.Subscribe<WeaveStarted>(w =>
        {
            if (w.Caster == totem && startedAt is null)
            {
                startedAt = arena.Sim.Clock.Time;
                duration = w.CastTime;
                paid = manaAtStart - stats.Resource("mana");
            }
        });
        arena.Sim.Events.Subscribe<WeaveCast>(w =>
        {
            if (w.Caster == totem && castAt is null)
            {
                castAt = arena.Sim.Clock.Time;
            }
        });
        for (var i = 0; i < 3 * arena.Sim.Clock.TicksPerSecond && castAt is null; i++)
        {
            manaAtStart = stats.Resource("mana");
            arena.Sim.Tick();
        }
        Assert.NotNull(startedAt);
        Assert.NotNull(castAt);
        Assert.Equal(cost, paid!.Value, 6);
        Assert.Equal(runtime.CastDuration(weave, stats), duration!.Value, 6);
        Assert.True(duration > 0);
        Assert.True(castAt - startedAt >= duration - 1e-9, $"the bolt flew {castAt - startedAt}s into a {duration}s cast");
    }

    [Fact]
    public void WithNoHandlersATotemCastsNothing()
    {
        var arena = MinionArena.Create(9);
        Dummy(arena, 6);
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 2, 0));
        var totem = arena.Minions("ember_totems").Single();
        arena.Sim.AddService(new Systems.IntentHandlers());
        arena.Events.Clear();
        arena.Sim.RunSeconds(3);
        Assert.DoesNotContain(arena.Events.OfType<WeaveStarted>(), w => w.Caster == totem);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), w => w.Caster == totem);
    }
}
