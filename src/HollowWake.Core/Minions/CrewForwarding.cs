using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Minions;

/// <summary>
/// Crew forwarding: the glyphs socketed in a summoning weave board the weaves of the bodies it raises,
/// wherever they fit. Minions board once the summoning weave has the mechanism rule
/// <c>minions.forward_requires</c> names (a resonant key's); constructs board without it, and their copies
/// keep <c>minions.construct_forward_scale</c> of the glyphs' modifier values. Riders join each weave in the
/// host's socket order after the weave's own glyphs, up to its form's glyph limit; a rider that does not fit
/// a weave stays dormant there, and one the weave already carries is not added twice.
/// </summary>
internal static class CrewForwarding
{
    public static void Board(Simulation sim, Entity body, CompiledWeave host, bool construct)
    {
        if (!sim.World.TryGet<WeaveBook>(body, out var book) || book.Slots.Count == 0)
        {
            return;
        }
        var rules = sim.Content.Rules;
        var key = rules.Text("minions.forward_requires", "resonance");
        if (!construct && key.Length > 0 && !host.HasMechanism(key))
        {
            return;
        }
        var riders = Riders(sim.Content, host, key);
        if (riders.Count == 0)
        {
            return;
        }
        var compiler = sim.Service<WeaveCompiler>();
        var scale = construct ? rules.Number("minions.construct_forward_scale", 0.7) : 1;
        for (var i = 0; i < book.Slots.Count; i++)
        {
            var spec = book.Slots[i].Spec;
            if (!compiler.TryCompile(spec, out var own, out _, GlyphFit.Dormant))
            {
                continue;
            }
            var carried = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in own!.GlyphRefs)
            {
                carried.Add(g.Id);
            }
            foreach (var d in own.Dormant)
            {
                carried.Add(d.Ref.Id);
            }
            var room = own.Form.MaxGlyphs - carried.Count;
            var boarding = new List<GlyphRef>();
            foreach (var rider in riders)
            {
                if (room <= 0)
                {
                    break;
                }
                if (carried.Add(rider.Id))
                {
                    boarding.Add(rider);
                    room--;
                }
            }
            if (boarding.Count == 0)
            {
                continue;
            }
            var boarded = spec with { Glyphs = spec.Glyphs.Concat(boarding).ToList() };
            if (!compiler.TryCompile(boarded, out var weave, out _, GlyphFit.Dormant))
            {
                continue;
            }
            book.Slots[i] = new WeaveSlot { Spec = boarded, Compiled = scale == 1 ? weave : Priced(weave!, boarding, scale) };
        }
    }

    /// <summary>The host's socketed glyphs that board, in socket order (the preset's first), active or dormant on the host.</summary>
    private static List<GlyphRef> Riders(ContentRegistry content, CompiledWeave host, string key)
    {
        var socketed = new List<GlyphRef>();
        if (host.Spec.Preset is { } preset && content.Get<WeaveDef>(preset) is { } def)
        {
            socketed.AddRange(def.Glyphs);
        }
        socketed.AddRange(host.Spec.Glyphs);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var riders = new List<GlyphRef>();
        foreach (var r in socketed)
        {
            if (!seen.Add(r.Id) || content.Get<GlyphDef>(r.Id) is not { } glyph)
            {
                continue;
            }
            if (!(glyph.Forwards ?? glyph.Trigger is null) || (key.Length > 0 && glyph.AddsMechanisms.Has(key)))
            {
                continue;
            }
            riders.Add(r);
        }
        return riders;
    }

    /// <summary>The weave with the boarded glyphs' modifier values scaled: a construct's priced copy.</summary>
    private static CompiledWeave Priced(CompiledWeave weave, List<GlyphRef> boarded, double scale)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in boarded)
        {
            sources.Add("glyph:" + g.Id);
        }
        var modifiers = new List<Attributes.Modifier>(weave.Modifiers.Count);
        foreach (var m in weave.Modifiers)
        {
            modifiers.Add(sources.Contains(m.Source) ? m.Scaled(scale) : m);
        }
        return new CompiledWeave
        {
            Spec = weave.Spec,
            Form = weave.Form,
            Threads = weave.Threads,
            Glyphs = weave.Glyphs,
            Tags = weave.Tags,
            Mechanisms = weave.Mechanisms,
            Bases = weave.Bases,
            Modifiers = modifiers,
            Effects = weave.Effects,
            Costs = weave.Costs,
            Level = weave.Level,
            Name = weave.Name,
            Look = weave.Look,
            Ai = weave.Ai,
            GlyphRefs = weave.GlyphRefs,
            Dormant = weave.Dormant,
            DeclaredMechanisms = weave.DeclaredMechanisms,
            CastScope = weave.CastScope,
            LevelBonus = weave.LevelBonus,
            Specs = weave.Specs,
            Probe = weave.Probe,
        };
    }
}
