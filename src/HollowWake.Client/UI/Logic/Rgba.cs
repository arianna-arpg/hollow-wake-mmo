using System.Globalization;

namespace HollowWake.Client.UI.Logic;

/// <summary>A colour with channels from zero to one, read from theme content as #rgb, #rrggbb or #rrggbbaa.</summary>
public readonly record struct Rgba(float R, float G, float B, float A = 1f)
{
    public static readonly Rgba White = new(1, 1, 1);
    public static readonly Rgba Black = new(0, 0, 0);
    public static readonly Rgba Clear = new(0, 0, 0, 0);

    public static bool TryParse(string? text, out Rgba color)
    {
        color = default;
        if (text is null || text.Length < 4 || text[0] != '#')
        {
            return false;
        }
        var hex = text.AsSpan(1);
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            return false;
        }
        switch (hex.Length)
        {
            case 3:
                color = new Rgba(((v >> 8) & 0xF) / 15f, ((v >> 4) & 0xF) / 15f, (v & 0xF) / 15f);
                return true;
            case 6:
                color = new Rgba(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
                return true;
            case 8:
                color = new Rgba(((v >> 24) & 0xFF) / 255f, ((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
                return true;
            default:
                return false;
        }
    }

    public static Rgba Parse(string? text, Rgba fallback) => TryParse(text, out var c) ? c : fallback;

    public Rgba WithAlpha(float a) => this with { A = a };

    public Rgba Lerp(Rgba to, float t) => new(R + (to.R - R) * t, G + (to.G - G) * t, B + (to.B - B) * t, A + (to.A - A) * t);

    /// <summary>Toward black by a fraction, keeping alpha.</summary>
    public Rgba Darkened(float amount) => new(R * (1 - amount), G * (1 - amount), B * (1 - amount), A);

    /// <summary>Toward white by a fraction, keeping alpha.</summary>
    public Rgba Lightened(float amount) => new(R + (1 - R) * amount, G + (1 - G) * amount, B + (1 - B) * amount, A);

    /// <summary>Relative luminance, for choosing ink that reads on a colour.</summary>
    public float Luminance => 0.2126f * R + 0.7152f * G + 0.0722f * B;

    /// <summary>The colour as #rrggbb or #rrggbbaa.</summary>
    public string Hex()
    {
        static int B8(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);
        return A >= 0.999f
            ? $"#{B8(R):x2}{B8(G):x2}{B8(B):x2}"
            : $"#{B8(R):x2}{B8(G):x2}{B8(B):x2}{B8(A):x2}";
    }
}
