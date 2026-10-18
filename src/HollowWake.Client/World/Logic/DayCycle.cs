using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;

namespace HollowWake.Client.World.Logic;

/// <summary>The light and air at one moment: the sky, sun and ambient colours and energies, the fog, and the sun's place.</summary>
public readonly record struct SkyState(Rgba Sky, Rgba Horizon, Rgba Sun, float SunEnergy, Rgba Ambient, float AmbientEnergy, Rgba Fog, float FogDensity, float SunPitch, float SunYaw, float Daylight);

/// <summary>The world's day as the server reports it: how long it lasts, and the fraction it stood at when a server time was read.</summary>
public readonly record struct ServerDay(double LengthSeconds, double Fraction, double AtServerTime);

/// <summary>
/// The day as the clients draw it (docs/design/appearance.md): the world's day as the server reports it in
/// enter_world and on the self state's cadence, advanced between messages by the server clock, or for a
/// server that never says, the zone clock folded by the content's day length; the sky keyframes blended at
/// that fraction, and the sun's bearing and height around noon. Nothing here is a gameplay clock: the
/// simulation's own day is the server's, and this only follows it.
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

    /// <summary>The content's day length, the fallback for a server that never says how long its day is.</summary>
    public float LengthSeconds => (float)Math.Max(1, _def.Day.LengthSeconds);

    /// <summary>The server's day, once a message carried it; null before that, and for a server that predates the field.</summary>
    public ServerDay? Server { get; private set; }

    /// <summary>How long the day lasts: the server's length once known, else the content's.</summary>
    public float DayLengthSeconds => (float)(Server?.LengthSeconds ?? LengthSeconds);

    /// <summary>
    /// The day as a message reported it: the fraction, and the length when the message carries one (enter_world
    /// does, the self state does not, and a fraction alone keeps the length last reported, else the content's).
    /// The server time is the client's estimate of the server clock as the message arrived; the fraction advances
    /// from it. A message without a fraction changes nothing.
    /// </summary>
    public void ObserveServer(double? lengthSeconds, double? fraction, double atServerTime)
    {
        if (fraction is not { } f || double.IsNaN(f))
        {
            return;
        }
        var length = lengthSeconds ?? Server?.LengthSeconds ?? LengthSeconds;
        Server = new ServerDay(Math.Max(1, length), Wrap(f), atServerTime);
    }

    /// <summary>Forgets the server's day, as on entering a world: the next message that carries one sets it again, and until then the clock folds.</summary>
    public void ForgetServer() => Server = null;

    /// <summary>
    /// The fraction of the day at a server time, from zero (midnight) through half (noon): the server's fraction
    /// advanced by the server clock when the server said, else the clock folded by the content's day from its
    /// start, which agrees between two zones only while their clocks do.
    /// </summary>
    public float TimeOfDay(double serverTime)
    {
        if (Server is { } s)
        {
            return (float)Wrap(s.Fraction + (serverTime - s.AtServerTime) / s.LengthSeconds);
        }
        return (float)Wrap(serverTime / LengthSeconds + _def.Day.Start);
    }

    private static double Wrap(double t)
    {
        t %= 1.0;
        return t < 0 ? t + 1 : t;
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
