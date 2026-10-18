using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;

namespace HollowWake.Client.World.Logic;

/// <summary>The light and air at one moment: the sky, sun and ambient colours and energies, the fog, and the sun's place.</summary>
public readonly record struct SkyState(Rgba Sky, Rgba Horizon, Rgba Sun, float SunEnergy, Rgba Ambient, float AmbientEnergy, Rgba Fog, float FogDensity, float SunPitch, float SunYaw, float Daylight);

/// <summary>
/// The day as the clients draw it (docs/design/appearance.md): the server clock folded by the content's day
/// length into a fraction of the day, the sky keyframes blended at that fraction, and the sun's bearing and
/// height around noon. Nothing here is a gameplay clock; the simulation's own day, should a rule read one,
/// is the server's and is asked for in the review record.
/// </summary>
public sealed class DayCycle
{
    private readonly WorldLookDef _def;
    private readonly List<(float At, SkyKey Key)> _keys = new();

    public DayCycle(WorldLookDef def)
    {
        _def = def;
        foreach (var k in def.Sky.OrderBy(k => k.At))
        {
            _keys.Add(((float)k.At, k));
        }
        if (_keys.Count == 0)
        {
            _keys.Add((0, new SkyKey()));
        }
    }

    public float LengthSeconds => (float)Math.Max(1, _def.Day.LengthSeconds);

    /// <summary>The fraction of the day at a server time, from zero (midnight) through half (noon).</summary>
    public float TimeOfDay(double serverTime)
    {
        var day = LengthSeconds;
        var t = (serverTime / day + _def.Day.Start) % 1.0;
        return (float)(t < 0 ? t + 1 : t);
    }

    /// <summary>The sky at a fraction of the day, blended between the keyframes around it, wrapping at midnight.</summary>
    public SkyState At(float fraction)
    {
        fraction = ((fraction % 1) + 1) % 1;
        var (a, b, t) = Bracket(fraction);
        Rgba C(string x, string y) => Rgba.Parse(x, Rgba.White).Lerp(Rgba.Parse(y, Rgba.White), t);
        float F(double x, double y) => (float)(x + (y - x) * t);
        // the sun climbs over the daylight hours centred on noon, below the horizon outside them
        var daylight = (float)_def.Sun.Daylight;
        var fromNoon = (fraction - 0.5f) / (daylight / 2);
        var up = MathF.Max(0, 1 - fromNoon * fromNoon);
        var pitch = (float)_def.Sun.NoonPitch * MathF.Sqrt(up);
        var dayness = Math.Clamp(1 - MathF.Abs(fromNoon), 0, 1);
        var yaw = (float)_def.Sun.NoonYaw + fromNoon * 70f;
        var night = 1 - MathF.Min(1, up * 1.5f);
        var fog = (float)_def.Haze.Density * (1 + ((float)_def.Haze.Night - 1) * night);
        return new SkyState(C(a.Sky, b.Sky), C(a.Horizon, b.Horizon), C(a.Sun, b.Sun), F(a.SunEnergy, b.SunEnergy), C(a.Ambient, b.Ambient), F(a.AmbientEnergy, b.AmbientEnergy), C(a.Fog, b.Fog), fog, pitch, yaw, dayness);
    }

    private (SkyKey A, SkyKey B, float T) Bracket(float fraction)
    {
        if (_keys.Count == 1)
        {
            return (_keys[0].Key, _keys[0].Key, 0);
        }
        for (var i = 0; i < _keys.Count - 1; i++)
        {
            var (at0, k0) = _keys[i];
            var (at1, k1) = _keys[i + 1];
            if (fraction >= at0 && fraction <= at1)
            {
                var span = at1 - at0;
                return (k0, k1, span <= 1e-6f ? 0 : (fraction - at0) / span);
            }
        }
        // past the last key: wrap to the first
        var (lastAt, last) = _keys[^1];
        var (firstAt, first) = _keys[0];
        var wrap = 1 - lastAt + firstAt;
        var tt = wrap <= 1e-6f ? 0 : fraction >= lastAt ? (fraction - lastAt) / wrap : (fraction + 1 - lastAt) / wrap;
        return (last, first, Math.Clamp(tt, 0, 1));
    }
}
