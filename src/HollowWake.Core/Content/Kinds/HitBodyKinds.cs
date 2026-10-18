using HollowWake.Core.Combat;
using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;

namespace HollowWake.Core.Content.Kinds;

internal static class HitBodyKinds
{
    public static void Register()
    {
        ContentKinds.Register<HitBodyDef>("hit_bodies", (d, c) =>
        {
            if (d.Volumes.Count == 0 || d.Aim < 0 || d.Aim >= d.Volumes.Count) c.Error("hit body needs volumes and an aim index within them.");
            foreach (var v in d.Volumes)
            {
                if (!HitVolumeShapes.Has(v.Shape)) c.Error($"unknown hit volume shape '{v.Shape}'.");
                if (!float.IsFinite(v.Forward) || !float.IsFinite(v.Left) || !float.IsFinite(v.Up) ||
                    !float.IsFinite(v.Radius) || !float.IsFinite(v.HalfHeight) || v.Radius <= 0 || v.HalfHeight <= 0 ||
                    v.Up < v.HalfHeight || v.Up + v.HalfHeight > 1.001f)
                    c.Error("hit volumes require finite positive sizes and must stay between feet and authority height.");
            }
        });
        ContentKinds.AddValidator<PatternDef>((d, c) =>
        {
            var resolved = new PatternFactory(c.Registry).Resolve(d.Id);
            if (resolved.Components.TryGetValue("hit_body", out var data))
                c.Ref<HitBodyDef>(data["profile"]?.GetValue<string>() ?? "", "hit_body.profile");
        });
        ContentKinds.AddValidator<RulesDef>((d, c) =>
        {
            if (d.Id == "body" && d.Values?["player_hit_profile"] is { } profile)
                c.Ref<HitBodyDef>(profile.GetValue<string>(), "body.player_hit_profile");
        });
    }
}
