namespace Skein.Core.Maths;

/// <summary>
/// Seeded 2D gradient (Perlin-style) noise with fractal summation. Used by zone generators for
/// heightfields, moisture and Thread density maps. Output of <see cref="Sample"/> is in [-1, 1].
/// </summary>
public sealed class Noise2D
{
    private readonly int[] _perm = new int[512];

    public Noise2D(ulong seed)
    {
        var rng = new Rng(seed);
        var p = new int[256];
        for (var i = 0; i < 256; i++)
        {
            p[i] = i;
        }
        rng.Shuffle(p);
        for (var i = 0; i < 512; i++)
        {
            _perm[i] = p[i & 255];
        }
    }

    public double Sample(double x, double y)
    {
        var xi = (int)Math.Floor(x) & 255;
        var yi = (int)Math.Floor(y) & 255;
        var xf = x - Math.Floor(x);
        var yf = y - Math.Floor(y);
        var u = Fade(xf);
        var v = Fade(yf);

        var aa = _perm[_perm[xi] + yi];
        var ab = _perm[_perm[xi] + yi + 1];
        var ba = _perm[_perm[xi + 1] + yi];
        var bb = _perm[_perm[xi + 1] + yi + 1];

        var x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        var x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    /// <summary>Fractal Brownian motion: layered octaves, result normalised to roughly [-1, 1].</summary>
    public double Fbm(double x, double y, int octaves = 4, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        for (var i = 0; i < Math.Max(1, octaves); i++)
        {
            sum += Sample(x * freq, y * freq) * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return norm > 0 ? sum / norm : 0;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad(int hash, double x, double y)
    {
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }
}
