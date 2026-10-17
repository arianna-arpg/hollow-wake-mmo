using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HollowWake.Core.Maths;

/// <summary>
/// Seeded 2D gradient (Perlin-style) noise with fractal summation. Used by zone generators for
/// heightfields, moisture and Thread density maps. Output of <see cref="Sample"/> is in [-1, 1].
/// </summary>
/// <remarks>
/// World generation sums tens of thousands of octaves per chunk and the result must not move by a
/// single bit, so the fractal paths keep the reference arithmetic and change only how it is scheduled:
/// <list type="bullet">
/// <item>The lattice index is the low byte of the floored coordinate. For floors inside the int range it
/// is read from the mantissa after adding 1.5 * 2^52, which is exact, instead of the saturating
/// conversion; any octave outside that range falls back to <see cref="Sample"/>.</item>
/// <item>Gradients come from a table, <c>gx * x + gy * y</c>, instead of a jump table, indexed by a
/// second table holding each permutation entry's gradient slot. That equals the reference except for
/// the sign of an exact zero, and a fractal sum starts at +0 and can never become -0, so adding such a
/// zero leaves the sum unchanged: every fractal result is identical. <see cref="Sample"/> itself keeps
/// the reference gradients.</item>
/// <item>Independent octaves, fields and points are evaluated side by side so the processor overlaps
/// their dependency chains; each sum still adds its terms in octave order.</item>
/// </list>
/// The tables are read without bounds checks: every index is masked into range.
/// </remarks>
public sealed class Noise2D
{
    private const double LowBitsMagic = 6755399441055744.0;
    private const double IntRange = 2147483648.0;

    // Gradient table, (gx, gy) per hash & 7, matching the reference cases; constant data, no static constructor.
    private static ReadOnlySpan<double> Gradients => new double[] { 1, 1, -1, 1, 1, -1, -1, -1, 1, 0, -1, 0, 0, 1, 0, -1 };

    // Bytes rather than ints: values are 0-255, and a sample touches many tables, so small ones stay in cache.
    private readonly byte[] _perm = new byte[512];

    // Per permutation entry, the index of its gradient pair in Gradients: (perm & 7) * 2.
    private readonly byte[] _slot = new byte[512];

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
            _perm[i] = (byte)p[i & 255];
            _slot[i] = (byte)((p[i & 255] & 7) << 1);
        }
    }

    // Not inlined: the fractal paths call it only for out-of-range octaves and should stay compact.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public double Sample(double x, double y)
    {
        var fx = Math.Floor(x);
        var fy = Math.Floor(y);
        var xi = (int)fx & 255;
        var yi = (int)fy & 255;
        var xf = x - fx;
        var yf = y - fy;
        var u = Fade(xf);
        var v = Fade(yf);

        // xi and yi are at most 255 and the table holds values up to 255, so every index is below 512.
        ref var perm = ref MemoryMarshal.GetArrayDataReference(_perm);
        var a = Unsafe.Add(ref perm, xi) + yi;
        var b = Unsafe.Add(ref perm, xi + 1) + yi;
        var aa = Unsafe.Add(ref perm, a);
        var ab = Unsafe.Add(ref perm, a + 1);
        var ba = Unsafe.Add(ref perm, b);
        var bb = Unsafe.Add(ref perm, b + 1);

        var x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        var x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    /// <summary>Fractal Brownian motion: layered octaves, result normalised to roughly [-1, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public double Fbm(double x, double y, int octaves = 4, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        var n = Math.Max(1, octaves);
        var t = new Tables(this);
        var i = 0;
        for (; i + 1 < n; i += 2)
        {
            var freqB = freq * lacunarity;
            var ampB = amp * gain;
            var xa = x * freq;
            var ya = y * freq;
            var xb = x * freqB;
            var yb = y * freqB;
            var ok = Axis(xa, out var ixa, out var fxa, out var ua)
                & Axis(ya, out var iya, out var fya, out var va)
                & Axis(xb, out var ixb, out var fxb, out var ub)
                & Axis(yb, out var iyb, out var fyb, out var vb);
            double sa, sb;
            if (ok)
            {
                Corners2(t, ixa, iya, fxa, fya, ua, va, t, ixb, iyb, fxb, fyb, ub, vb, out sa, out sb);
            }
            else
            {
                sa = Sample(xa, ya);
                sb = Sample(xb, yb);
            }
            sum += sa * amp;
            norm += amp;
            sum += sb * ampB;
            norm += ampB;
            amp = ampB * gain;
            freq = freqB * lacunarity;
        }
        if (i < n)
        {
            var xa = x * freq;
            var ya = y * freq;
            var s = Axis(xa, out var ix, out var fx, out var u) & Axis(ya, out var iy, out var fy, out var v)
                ? Corners(t, ix, iy, fx, fy, u, v)
                : Sample(xa, ya);
            sum += s * amp;
            norm += amp;
        }
        return norm > 0 ? sum / norm : 0;
    }

    /// <summary>
    /// <see cref="Fbm"/> at five points of a plus-shaped stencil at once: the center (xc, yc), west
    /// (xw, yc), east (xe, yc), south (xc, ys) and north (xc, yn). Each result is exactly what
    /// <see cref="Fbm"/> returns for that point; the points share their floors, fractions and fades.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void FbmCross(
        double xw, double xc, double xe, double ys, double yc, double yn, int octaves, double lacunarity, double gain,
        out double c, out double w, out double e, out double s, out double n)
    {
        double sc = 0, sw = 0, se = 0, ss = 0, sn = 0, amp = 1, freq = 1, norm = 0;
        var count = Math.Max(1, octaves);
        var t = new Tables(this);
        for (var i = 0; i < count; i++)
        {
            var tw = xw * freq;
            var tc = xc * freq;
            var te = xe * freq;
            var ts = ys * freq;
            var tm = yc * freq;
            var tn = yn * freq;
            var ok = Axis(tw, out var iw, out var fw, out var uw)
                & Axis(tc, out var ic, out var fc, out var uc)
                & Axis(te, out var ie, out var fe, out var ue)
                & Axis(ts, out var js, out var gs, out var vs)
                & Axis(tm, out var jm, out var gm, out var vm)
                & Axis(tn, out var jn, out var gn, out var vn);
            double vC, vW, vE, vS, vN;
            if (ok)
            {
                Corners2(t, ic, jm, fc, gm, uc, vm, t, iw, jm, fw, gm, uw, vm, out vC, out vW);
                Corners2(t, ie, jm, fe, gm, ue, vm, t, ic, js, fc, gs, uc, vs, out vE, out vS);
                vN = Corners(t, ic, jn, fc, gn, uc, vn);
            }
            else
            {
                vC = Sample(tc, tm);
                vW = Sample(tw, tm);
                vE = Sample(te, tm);
                vS = Sample(tc, ts);
                vN = Sample(tc, tn);
            }
            sc += vC * amp;
            sw += vW * amp;
            se += vE * amp;
            ss += vS * amp;
            sn += vN * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        c = norm > 0 ? sc / norm : 0;
        w = norm > 0 ? sw / norm : 0;
        e = norm > 0 ? se / norm : 0;
        s = norm > 0 ? ss / norm : 0;
        n = norm > 0 ? sn / norm : 0;
    }

    /// <summary>
    /// <see cref="Fbm"/> of two fields, each at its own point, octave by octave side by side (they share
    /// octave count, lacunarity and gain). Each result is exactly what <see cref="Fbm"/> returns.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void FbmPair(
        Noise2D first, double x1, double y1, Noise2D second, double x2, double y2, int octaves, double lacunarity, double gain,
        out double r1, out double r2)
    {
        double s1 = 0, s2 = 0, amp = 1, freq = 1, norm = 0;
        var n = Math.Max(1, octaves);
        var t1 = new Tables(first);
        var t2 = new Tables(second);
        for (var i = 0; i < n; i++)
        {
            var xa = x1 * freq;
            var ya = y1 * freq;
            var xb = x2 * freq;
            var yb = y2 * freq;
            var ok = Axis(xa, out var ixa, out var fxa, out var ua)
                & Axis(ya, out var iya, out var fya, out var va)
                & Axis(xb, out var ixb, out var fxb, out var ub)
                & Axis(yb, out var iyb, out var fyb, out var vb);
            double sa, sb;
            if (ok)
            {
                Corners2(t1, ixa, iya, fxa, fya, ua, va, t2, ixb, iyb, fxb, fyb, ub, vb, out sa, out sb);
            }
            else
            {
                sa = first.Sample(xa, ya);
                sb = second.Sample(xb, yb);
            }
            s1 += sa * amp;
            s2 += sb * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        r1 = norm > 0 ? s1 / norm : 0;
        r2 = norm > 0 ? s2 / norm : 0;
    }

    /// <summary>References to one noise's tables and the gradient pairs, for the fractal inner loops.</summary>
    private readonly ref struct Tables
    {
        public readonly ref byte Perm;
        public readonly ref byte Slot;
        public readonly ref double Grad;

        public Tables(Noise2D noise)
        {
            Perm = ref MemoryMarshal.GetArrayDataReference(noise._perm);
            Slot = ref MemoryMarshal.GetArrayDataReference(noise._slot);
            Grad = ref MemoryMarshal.GetReference(Gradients);
        }
    }

    /// <summary>
    /// One axis of a lattice lookup: the low byte of the floor, the fraction and its fade. False when
    /// the floor is outside the int range (or not a number), where the caller uses <see cref="Sample"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Axis(double t, out nint index, out double fraction, out double fade)
    {
        var f = Math.Floor(t);
        index = (nint)(BitConverter.DoubleToInt64Bits(f + LowBitsMagic) & 255);
        fraction = t - f;
        fade = Fade(fraction);
        return Math.Abs(f) < IntRange;
    }

    /// <summary>The four lattice corners around a point, blended: the reference sample with table gradients.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Corners(in Tables t, nint xi, nint yi, double xf, double yf, double u, double v)
    {
        var a = (nint)Unsafe.Add(ref t.Perm, xi) + yi;
        var b = (nint)Unsafe.Add(ref t.Perm, xi + 1) + yi;
        var xf1 = xf - 1;
        var yf1 = yf - 1;
        var g00 = Grad(t, Unsafe.Add(ref t.Slot, a), xf, yf);
        var g10 = Grad(t, Unsafe.Add(ref t.Slot, b), xf1, yf);
        var g01 = Grad(t, Unsafe.Add(ref t.Slot, a + 1), xf, yf1);
        var g11 = Grad(t, Unsafe.Add(ref t.Slot, b + 1), xf1, yf1);
        var x1 = Lerp(g00, g10, u);
        var x2 = Lerp(g01, g11, u);
        return Lerp(x1, x2, v);
    }

    /// <summary>
    /// <see cref="Corners"/> for two points (possibly of two noises), written stage by stage across both
    /// so the processor overlaps their load and arithmetic chains.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Corners2(
        in Tables ta, nint xia, nint yia, double xfa, double yfa, double ua, double va,
        in Tables tb, nint xib, nint yib, double xfb, double yfb, double ub, double vb,
        out double ra, out double rb)
    {
        var aA = (nint)Unsafe.Add(ref ta.Perm, xia) + yia;
        var aB = (nint)Unsafe.Add(ref tb.Perm, xib) + yib;
        var bA = (nint)Unsafe.Add(ref ta.Perm, xia + 1) + yia;
        var bB = (nint)Unsafe.Add(ref tb.Perm, xib + 1) + yib;
        var xfa1 = xfa - 1;
        var xfb1 = xfb - 1;
        var yfa1 = yfa - 1;
        var yfb1 = yfb - 1;
        var g00A = Grad(ta, Unsafe.Add(ref ta.Slot, aA), xfa, yfa);
        var g00B = Grad(tb, Unsafe.Add(ref tb.Slot, aB), xfb, yfb);
        var g10A = Grad(ta, Unsafe.Add(ref ta.Slot, bA), xfa1, yfa);
        var g10B = Grad(tb, Unsafe.Add(ref tb.Slot, bB), xfb1, yfb);
        var x1A = Lerp(g00A, g10A, ua);
        var x1B = Lerp(g00B, g10B, ub);
        var g01A = Grad(ta, Unsafe.Add(ref ta.Slot, aA + 1), xfa, yfa1);
        var g01B = Grad(tb, Unsafe.Add(ref tb.Slot, aB + 1), xfb, yfb1);
        var g11A = Grad(ta, Unsafe.Add(ref ta.Slot, bA + 1), xfa1, yfa1);
        var g11B = Grad(tb, Unsafe.Add(ref tb.Slot, bB + 1), xfb1, yfb1);
        var x2A = Lerp(g01A, g11A, ua);
        var x2B = Lerp(g01B, g11B, ub);
        ra = Lerp(x1A, x2A, va);
        rb = Lerp(x1B, x2B, vb);
    }

    /// <summary>A corner's gradient dotted with the offset to it, from the gradient pair at a slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Grad(in Tables t, nint slot, double x, double y) =>
        Unsafe.Add(ref t.Grad, slot) * x + Unsafe.Add(ref t.Grad, slot + 1) * y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
