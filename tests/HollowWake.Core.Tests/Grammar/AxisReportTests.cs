using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// The axis report places a compiled weave on the nine dimensions of the combat grammar from what the compiled
/// weave states: its folds, its graph at every depth, its cast layer, its probes; and names who stated each
/// value, so the frozen literals of a graph stand apart from the axes a modifier reaches.
/// </summary>
public class AxisReportTests
{
    /// <summary>The core content plus a flight with written numbers and a preset with a channel of its own (ids start with <c>t_axes_</c>).</summary>
    private static readonly Lazy<ContentRegistry> Layered = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-axes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "axes-tests.yaml"), """
kind: forms
entries:
  - id: t_axes_flight
    targeting: enemy
    cast_mode: instant
    min_threads: 0
    attributes: { range: 12, projectile.speed: 18, area.radius: 2, projectile.spread: 1 }
    effects:
      - type: projectile
        radius: 0.3
        speed: 20
        on_hit:
          - { type: damage, amount: 5, threads: [ember] }
          - type: area
            shape: cone
            angle: 90
            radius: "weave.area.radius"
            effects: [ { type: damage, amount: 2, threads: [ember] } ]
          - { type: apply_status, status: burning, duration: 3 }
  - id: t_axes_reads
    targeting: direction
    cast_mode: instant
    min_threads: 0
    tags: [melee]
    attributes: { range: 10, duration: 3, area.width: 2, melee.reach: 1, damage: 0, damage.effectiveness: 1.5 }
    effects:
      - { type: area, shape: bar, radius: 3 }
      - { type: area, shape: band, radius: 3 }
      - { type: area, shape: line, radius: 3 }
      - { type: area, shape: circle, radius: 3, width: 1, angle: 45 }
      - type: projectile
        duration: 2
        on_hit:
          - { type: damage, amounts: { ember: 4 } }
          - { type: damage, amount: 3, effectiveness: 0.5, threads: [ember] }
          - { type: damage, threads: [ember] }
      - { type: aura, toggle: true }
---
kind: weaves
entries:
  - id: t_axes_brisk
    form: torrent
    threads: [tide]
    channel: { interval: 0.25, max_hold: 1.5 }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.False(report.HasErrors, report.ToString());
            return registry;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    });

    private static IReadOnlyList<AxisValue> Report(ContentRegistry content, WeaveSpec spec) =>
        AxisReport.Of(new WeaveCompiler(content, EffectRegistry.Shared).Compile(spec, GlyphFit.Strict), content);

    [Fact]
    public void EmberBoltIsPlacedOnTheDimensionsOfACast()
    {
        var values = Report(CoreContent.Registry, new WeaveSpec { Preset = "ember_bolt" });

        // Geometry: the chain leaps a written six units, a literal no modifier reaches; the flights fold the bolt's base.
        var leap = Assert.Single(values, v => v.Name == "geometry.chain_range");
        Assert.Equal((6.0, "units", AxisSource.Literal, "chain"), (leap.Number, leap.Unit, leap.Source, leap.Effect));
        Assert.True(leap.Frozen);
        var flights = Assert.Single(values, v => v.Name == "geometry.flights");
        Assert.Equal((1.0, AxisSource.Attribute, "projectile.count"), (flights.Number, flights.Source, flights.Attribute));

        // Kinematics: the bolt's base speed, folded, where the flight writes none.
        var speed = Assert.Single(values, v => v.Name == "kinematics.speed");
        Assert.Equal((16.0, "units/s", AxisSource.Attribute, "projectile.speed", "effects[0].speed"), (speed.Number, speed.Unit, speed.Source, speed.Attribute, speed.Path));
        Assert.False(speed.Frozen);

        // Payload: a written chance is a literal; the damage is a formula the caster completes, so it stays a formula.
        var chance = Assert.Single(values, v => v.Name == "payload.chance");
        Assert.Equal((0.25, "fraction", AxisSource.Literal), (chance.Number, chance.Unit, chance.Source));
        var amount = values.First(v => v.Name == "payload.amount");
        Assert.Equal(AxisSource.Expression, amount.Source);
        Assert.Null(amount.Number);
        Assert.Contains("weave.base_damage", amount.Word);
        Assert.Contains(values, v => v.Name == "payload.base_damage" && v.Number == 14 && v.Source == AxisSource.Attribute);

        // Economy and timing: the form's cast layer.
        var mana = Assert.Single(values, v => v.Name == "economy.cost.mana");
        Assert.Equal((8.0, "resource", AxisSource.Form, "form.costs.mana"), (mana.Number, mana.Unit, mana.Source, mana.Path));
        var mode = Assert.Single(values, v => v.Name == "timing.cast_mode");
        Assert.Equal(("cast", AxisSource.Form), (mode.Word, mode.Source));
        var bar = Assert.Single(values, v => v.Name == "timing.cast_time");
        Assert.Equal((0.6, "s", AxisSource.Form), (bar.Number, bar.Unit, bar.Source));

        // Targeting: the form's mode, and the reach the weave's range folds to.
        Assert.Contains(values, v => v.Name == "targeting.mode" && v.Word == "enemy" && v.Source == AxisSource.Form);
        var range = Assert.Single(values, v => v.Name == "targeting.range");
        Assert.Equal((14.0, AxisSource.Attribute, "range"), (range.Number, range.Source, range.Attribute));

        // Coupling: the Ember Thread's own status, written on the struck body.
        var burning = Assert.Single(values, v => v.Name == "coupling.writes.status.burning");
        Assert.Equal(("target", AxisSource.Literal), (burning.Word, burning.Source));

        // The frozen axes are exactly the literals no attribute multiplies.
        Assert.Equal(values.Where(v => v.Source == AxisSource.Literal && v.Number is not null && !v.Multiplied), AxisReport.Literals(values));
    }

    [Fact]
    public void ALiteralFlightRadiusIsAFrozenAxisAndAMultipliedAngleIsNot()
    {
        var content = Layered.Value;
        var values = Report(content, new WeaveSpec { Form = "t_axes_flight" });
        var literals = AxisReport.Literals(values);

        // A flight's radius has no attribute behind it today: the engine owes one.
        var radius = Assert.Single(literals, v => v.Name == "geometry.radius" && v.Effect == "projectile");
        Assert.Equal((0.3, "units", "effects[0].radius"), (radius.Number, radius.Unit, radius.Path));
        Assert.Null(radius.Attribute);

        // A written speed freezes an axis content could leave to the weave: the literal names the attribute it shadows.
        var speed = Assert.Single(literals, v => v.Name == "kinematics.speed");
        Assert.Equal((20.0, "projectile.speed"), (speed.Number, speed.Attribute));

        // A written amount passes through the attacker's damage channel, so a modifier reaches it: it is not frozen.
        var amount = Assert.Single(values, v => v.Name == "payload.amount" && v.Path == "effects[0].on_hit[0].amount");
        Assert.Equal((5.0, "damage", true), (amount.Number, amount.Attribute, amount.Multiplied));
        Assert.DoesNotContain(literals, v => v.Name == "payload.amount");

        // A written status duration is scaled by the applier's effect.duration, read on the caster's own stack.
        var duration = Assert.Single(values, v => v.Name == "payload.duration");
        Assert.Equal((3.0, "effect.duration", true), (duration.Number, duration.Attribute, duration.Multiplied));
        Assert.Contains("caster's own stack", duration.Detail);
        Assert.DoesNotContain(literals, v => v.Name == "payload.duration");

        // An aimed fan's spreads are read only where the flight has one, though the weave states projectile.spread.
        Assert.DoesNotContain(values, v => v.Parameter is { } p && p.StartsWith("spread_by_aim", StringComparison.Ordinal));

        // The area's angle is written too, but the weave's area.angle multiplies it, so a modifier still reaches it.
        var angle = Assert.Single(values, v => v.Name == "geometry.angle");
        Assert.True(angle.Multiplied);
        Assert.Equal((90.0, "area.angle"), (angle.Number, angle.Attribute));
        Assert.DoesNotContain(literals, v => v.Name == "geometry.angle");

        // A reference to the weave's attribute is the attribute, not a literal.
        var area = Assert.Single(values, v => v.Name == "geometry.radius" && v.Effect == "area");
        Assert.Equal((AxisSource.Attribute, 2.0, "area.radius"), (area.Source, area.Number, area.Attribute));
        Assert.DoesNotContain(literals, v => v.Effect == "area" && v.Name == "geometry.radius");
    }

    [Fact]
    public void AValueIsPlacedOnlyWhereTheHandlerReadsIt()
    {
        var values = Report(Layered.Value, new WeaveSpec { Form = "t_axes_reads" });
        double? At(string path) => values.SingleOrDefault(v => v.Path == path)?.Number;

        // A width shapes only the shapes with one; unwritten, it is the shape's share of the weave's area width:
        // a quarter for a bar, all of it for a line, none for a band, whose width is its radius.
        Assert.Equal(0.5, At("effects[0].width"));
        Assert.Null(At("effects[1].width"));
        Assert.Equal(2.0, At("effects[2].width"));
        // A circle's written width and angle shape nothing, so neither is placed.
        Assert.Null(At("effects[3].width"));
        Assert.Null(At("effects[3].angle"));

        // A melee weave's reach lengthens the shapes that extend from the caster (a band, a line), not a bar or a circle.
        Assert.Equal(new[] { "effects[1].melee_reach", "effects[2].melee_reach" },
            values.Where(v => v.Name == "geometry.melee_reach").Select(v => v.Path).OrderBy(p => p, StringComparer.Ordinal));

        // A flight with a duration and no range is bounded by its duration: the weave's range is not its range.
        Assert.Null(At("effects[4].range"));
        Assert.Contains(values, v => v.Path == "effects[4].duration" && v.Number == 2);

        // Per-Thread amounts alone mean no split amount; a node that writes neither takes ten through the damage channel.
        Assert.Null(At("effects[4].on_hit[0].amount"));
        Assert.Equal(4.0, At("effects[4].on_hit[0].amounts.ember"));
        var unwritten = Assert.Single(values, v => v.Path == "effects[4].on_hit[2].amount");
        Assert.Equal((10.0, AxisSource.Attribute, "damage"), (unwritten.Number, unwritten.Source, unwritten.Attribute));

        // The weave's damage effectiveness scales only the added damage, and only where the node writes no effectiveness.
        Assert.Null(At("effects[4].on_hit[1].added_effectiveness"));
        Assert.Equal(0.5, At("effects[4].on_hit[1].effectiveness"));
        Assert.Equal(1.5, At("effects[4].on_hit[2].added_effectiveness"));
        Assert.DoesNotContain(values, v => v.Name == "payload.effectiveness" && v.Source == AxisSource.Attribute);

        // A toggled aura with no written duration lasts until it is turned off: the weave's duration is not its duration.
        Assert.Null(At("effects[5].duration"));
    }

    [Fact]
    public void AGlyphPatchThatSetsAShapeIsReportedAsThePatchs()
    {
        var content = CoreContent.Registry;
        var plain = Assert.Single(Report(content, new WeaveSpec { Form = "nova", Threads = { "ember" } }), v => v.Name == "geometry.shape");
        Assert.Equal(("circle", AxisSource.Literal), (plain.Word, plain.Source));

        var values = Report(content, new WeaveSpec { Form = "nova", Threads = { "ember" }, Glyphs = { "crescent_sigil" } });
        var shape = Assert.Single(values, v => v.Name == "geometry.shape");
        Assert.Equal(("crescent", AxisSource.Patch, "effects[0].shape"), (shape.Word, shape.Source, shape.Path));
        Assert.Contains("crescent_sigil", shape.Detail);
        // What the patch did not touch stays the form's.
        Assert.Contains(values, v => v.Name == "geometry.center" && v.Source == AxisSource.Literal);

        // A wrap patch's wrapper is the patch's, and the node it wrapped keeps its own provenance under it.
        var wrapped = Report(content, new WeaveSpec { Form = "blast", Threads = { "stone" }, Glyphs = { "cascading" } });
        var placements = Assert.Single(wrapped, v => v.Name == "geometry.placements");
        Assert.Equal((AxisSource.Patch, "effects[0].count", 2.0), (placements.Source, placements.Path, placements.Number));
        Assert.Contains(wrapped, v => v.Name == "geometry.shape" && v.Path == "effects[0].effects[0].shape" && v.Source == AxisSource.Literal);

        // A weave tree's node patches as a glyph does, and is named as the patch's author.
        var tree = Report(content, new WeaveSpec { Form = "torrent", Threads = { "tide" }, Level = 3, Tree = { "swelling", "broad_stream" } });
        var angle = Assert.Single(tree, v => v.Name == "geometry.angle" && v.Path == "effects[0].angle");
        Assert.Equal(AxisSource.Patch, angle.Source);
        Assert.Contains("tree broad_stream", angle.Detail);
    }

    [Fact]
    public void RidersAreTheirGlyphsHooks()
    {
        var values = Report(CoreContent.Registry, new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "hexing", "shedding" } });
        var riders = values.Where(v => v.Path.StartsWith("effects[0].on_tick[0]", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(riders);
        Assert.All(riders, v => Assert.Equal(AxisSource.Hook, v.Source));
        Assert.Contains(riders, v => v.Name == "geometry.radius" && v.Number == 0.25);
        var hook = Assert.Single(values, v => v.Name == "causality.hook.on_tick");
        Assert.Equal(("shedding", AxisSource.Hook), (hook.Word, hook.Source));

        // A hook lands on every moment of its name the graph had when hooks were placed: the flight's hit and the
        // chain's, after their own effects, and never on another rider's.
        var hexes = values.Where(v => v.Name == "coupling.writes.status.shocked").Select(v => v.Path).ToList();
        Assert.Equal(new[] { "effects[0].on_hit[2].effects[1].status", "effects[0].on_hit[3].status" }, hexes.OrderBy(p => p, StringComparer.Ordinal));
        Assert.All(values.Where(v => v.Name == "coupling.writes.status.shocked"), v => Assert.Equal(AxisSource.Hook, v.Source));
        Assert.Contains(values, v => v.Name == "coupling.writes.status.burning" && v.Source == AxisSource.Literal);

        // The bolt's own flight is not a rider, and a rider's literals are its glyph's, not the bolt's frozen axes.
        Assert.Contains(values, v => v.Name == "kinematics.speed" && v.Path == "effects[0].speed" && v.Source == AxisSource.Attribute);
        Assert.DoesNotContain(AxisReport.Literals(values), v => v.Source == AxisSource.Hook);
    }

    [Fact]
    public void APresetsChannelIsReadFromTheCompiledWeaveNotTheForm()
    {
        var content = Layered.Value;
        var brisk = Report(content, new WeaveSpec { Preset = "t_axes_brisk" });
        var interval = Assert.Single(brisk, v => v.Name == "timing.channel.interval");
        Assert.Equal((0.25, AxisSource.Preset, "preset.channel.interval"), (interval.Number, interval.Source, interval.Path));
        Assert.Contains(brisk, v => v.Name == "timing.channel.max_hold" && v.Number == 1.5 && v.Source == AxisSource.Preset);
        // The preset's block replaces the form's whole: none of the form's windup, ramps or release reach it.
        Assert.DoesNotContain(brisk, v => v.Name == "timing.channel.windup");
        Assert.DoesNotContain(brisk, v => v.Name.StartsWith("timing.channel.ramp.", StringComparison.Ordinal));
        Assert.DoesNotContain(brisk, v => v.Path.StartsWith("release[", StringComparison.Ordinal));
        Assert.DoesNotContain(brisk, v => v.Path.StartsWith("form.channel", StringComparison.Ordinal));

        // The form compiled bare holds its own channel, its release section included.
        var form = Report(content, new WeaveSpec { Form = "torrent", Threads = { "tide" } });
        Assert.Contains(form, v => v.Name == "timing.channel.interval" && v.Number == 0.5 && v.Source == AxisSource.Form);
        Assert.Contains(form, v => v.Name == "timing.channel.windup" && v.Number == 0.3);
        Assert.Contains(form, v => v.Path.StartsWith("release[0]", StringComparison.Ordinal) && v.Name == "geometry.radius");
    }

    [Fact]
    public void EveryCoreFormAndPresetIsPlacedOnTheNineDimensions()
    {
        var all = AxisReport.OfAll(CoreContent.Registry);
        Assert.NotEmpty(all);
        Assert.All(all, weave => Assert.Null(weave.Error));
        foreach (var weave in all)
        {
            foreach (var v in weave.Values)
            {
                Assert.True(Enum.IsDefined(v.Dimension), $"{weave.Id}: {v.Name} has no dimension");
                Assert.False(string.IsNullOrEmpty(v.Unit), $"{weave.Id}: {v.Name} has no unit");
                Assert.True(v.Number is not null || !string.IsNullOrEmpty(v.Word), $"{weave.Id}: {v.Name} states neither a number nor a word");
                Assert.StartsWith(AxisValue.DimensionName(v.Dimension) + ".", v.Name);
                Assert.False(string.IsNullOrEmpty(v.Path), $"{weave.Id}: {v.Name} has no path");
            }
            // Every weave has a cast mode and is aimed somehow.
            Assert.Contains(weave.Values, v => v.Name == "timing.cast_mode");
            Assert.Contains(weave.Values, v => v.Name == "targeting.mode");
        }
        // The shipped content carries frozen literals: the baseline pass one works down.
        Assert.Contains(all, weave => weave.Values.Any(v => v.Frozen));
    }

    [Fact]
    public void EveryCoreGlyphOnAFormItFitsIsPlaced()
    {
        // Each glyph on the first form (by id) it fits: its patches, hooks, added effects and cast-layer grants read
        // without a throw, and what it adds is the glyph's.
        var content = CoreContent.Registry;
        var compiler = new WeaveCompiler(content, EffectRegistry.Shared);
        var threads = content.Each<ThreadDef>().Select(t => t.Id).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var forms = content.Each<FormDef>().OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        var placed = 0;
        foreach (var glyph in content.Each<GlyphDef>().OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            foreach (var form in forms.Where(f => f.MaxGlyphs > 0))
            {
                var spec = new WeaveSpec { Form = form.Id, Threads = threads.Take(Math.Max(1, form.MinThreads)).Take(form.MaxThreads).ToList(), Glyphs = { glyph.Id } };
                if (!compiler.TryCompile(spec, out var weave, out _, GlyphFit.Dormant) || weave!.Dormant.Count > 0 || weave.Riding.Count > 0)
                {
                    continue;
                }
                var values = AxisReport.Of(weave, content);
                Assert.All(values, v => Assert.True(v.Number is not null || !string.IsNullOrEmpty(v.Word), $"{glyph.Id} on {form.Id}: {v.Name}"));
                Assert.DoesNotContain(values, v => v.Detail is { } d && d.StartsWith("glyph ", StringComparison.Ordinal) && !d.StartsWith("glyph " + glyph.Id, StringComparison.Ordinal));
                placed++;
                break;
            }
        }
        Assert.True(placed > 0);
    }

    [Fact]
    public void EverySignatureParameterIsDeclaredAndEveryBindingNamesWhatExists()
    {
        var effects = EffectRegistry.Shared;
        var content = CoreContent.Registry;
        foreach (var type in effects.Types)
        {
            Assert.True(effects.TryGetSignature(type, out var signature));
            foreach (var parameter in signature.Params)
            {
                Assert.True(AxisBindings.Declares(type, parameter), $"{type}.{parameter} is declared nowhere in AxisBindings");
            }
        }
        foreach (var binding in AxisBindings.All)
        {
            Assert.True(effects.Has(binding.Type), $"AxisBindings declares unknown effect type '{binding.Type}'");
            if (!binding.Implicit)
            {
                Assert.True(effects.TryGetSignature(binding.Type, out var signature) && signature.Accepts(binding.Top), $"{binding.Type} has no parameter '{binding.Top}'");
            }
            if (binding.Attribute is { } attribute)
            {
                Assert.True(content.Attributes.Contains(attribute), $"{binding.Type}.{binding.Parameter} names undeclared attribute '{attribute}'");
            }
            Assert.True(binding.Kind != ParameterKind.Axis || binding.Dimension is not null, $"{binding.Type}.{binding.Parameter} is an axis on no dimension");
            if (binding.Kind == ParameterKind.Choice && binding.Registry is { } registry && AxisBindings.WordsOf(binding) is { } words)
            {
                Assert.NotEmpty(words);
            }
        }
        foreach (var attribute in AxisBindings.WeaveAttributes)
        {
            Assert.True(content.Attributes.Contains(attribute.Attribute), $"weave-level attribute '{attribute.Attribute}' is undeclared");
        }
    }
}
