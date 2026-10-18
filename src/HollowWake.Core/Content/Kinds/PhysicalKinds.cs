using System.Text.Json;
using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;

namespace HollowWake.Core.Content.Kinds;

internal static class PhysicalKinds
{
    public static void Register()
    {
        ContentKinds.Register<BodyMaterialDef>("body_materials", (d, c) =>
        {
            if (!double.IsFinite(d.Density) || d.Density <= 0) c.Error("density must be finite and positive.");
        });
        ContentKinds.AddValidator<PatternDef>((d, c) =>
        {
            // Resolve only a physical chain; ordinary patterns keep their existing validation path.
            var ancestor = d;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (!ancestor.Components.ContainsKey("physical"))
            {
                if (!seen.Add(ancestor.Id) || ancestor.Extends is null) return;
                var parent = c.Registry.Get<PatternDef>(ancestor.Extends);
                if (parent is null) return;
                ancestor = parent;
            }
            var resolved = new PatternFactory(c.Registry).Resolve(d.Id);
            var raw = resolved.Components["physical"];
            var p = raw.Deserialize<PhysicalBodySpec>(ContentJson.Options)!;
            if (new[] { p.Width, p.Depth, p.Height, p.Fill, p.Heft }.Any(v => !double.IsFinite(v) || v <= 0))
                c.Error("physical dimensions, fill and heft must be finite and positive.");
            if (p.Fill > 1) c.Error("physical fill must not exceed one.");
            if (Math.Abs(p.Height - d.Height) > 0.0001 || Math.Abs(Math.Max(p.Width, p.Depth) / 2 - d.Radius) > 0.0001)
                c.Error("physical envelope must agree with pattern height and enclosing radius.");
            if (p.Materials.Count == 0 || Math.Abs(p.Materials.Sum(s => s.Fraction) - 1) > 0.0001)
                c.Error("physical material fractions must sum to one.");
            foreach (var share in p.Materials)
            {
                c.Ref<BodyMaterialDef>(share.Material, "physical.materials");
                if (!double.IsFinite(share.Fraction) || share.Fraction <= 0) c.Error("material fractions must be finite and positive.");
            }
        });
    }
}
