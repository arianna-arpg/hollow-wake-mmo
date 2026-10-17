namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Placements that put a delivery at many points or join points: scattered strikes, cascades, tethers and
/// detonations (<c>scatter</c>, <c>cascade</c>, <c>tether</c>, <c>detonate</c>). Wrappers among them
/// declare the list they wrap so glyph patches can wrap a form's node in them.
/// </summary>
public static class PlacementHandlers
{
    public static void Register(EffectRegistry r)
    {
        _ = r;
    }
}
