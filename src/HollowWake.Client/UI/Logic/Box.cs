namespace HollowWake.Client.UI.Logic;

/// <summary>A screen rectangle in pixels: x right, y down.</summary>
public readonly record struct Box(float X, float Y, float W, float H)
{
    public static readonly Box Empty = new(0, 0, 0, 0);

    public float Right => X + W;
    public float Bottom => Y + H;
    public float CenterX => X + W / 2;
    public float CenterY => Y + H / 2;
    public bool IsEmpty => W <= 0 || H <= 0;

    public bool Contains(float px, float py) => px >= X && px < Right && py >= Y && py < Bottom;

    public Box Union(Box other)
    {
        if (IsEmpty)
        {
            return other;
        }
        if (other.IsEmpty)
        {
            return this;
        }
        var x = MathF.Min(X, other.X);
        var y = MathF.Min(Y, other.Y);
        return new Box(x, y, MathF.Max(Right, other.Right) - x, MathF.Max(Bottom, other.Bottom) - y);
    }

    /// <summary>The overlapping area, or zero.</summary>
    public float OverlapArea(Box other)
    {
        var w = MathF.Min(Right, other.Right) - MathF.Max(X, other.X);
        var h = MathF.Min(Bottom, other.Bottom) - MathF.Max(Y, other.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }

    public bool Overlaps(Box other) => OverlapArea(other) > 0;

    public Box Inflated(float by) => new(X - by, Y - by, W + by * 2, H + by * 2);

    public Box Moved(float dx, float dy) => this with { X = X + dx, Y = Y + dy };

    /// <summary>Moves the box inside the bounds, keeping its size where it fits.</summary>
    public Box ClampedInto(Box bounds)
    {
        var w = MathF.Min(W, bounds.W);
        var h = MathF.Min(H, bounds.H);
        var x = Math.Clamp(X, bounds.X, bounds.Right - w);
        var y = Math.Clamp(Y, bounds.Y, bounds.Bottom - h);
        return new Box(x, y, w, h);
    }
}
