namespace HollowWake.Client.UI.Logic;

/// <summary>
/// The pan and zoom of a canvas such as the passive constellation: content units to screen pixels, a zoom
/// clamped to its bounds that keeps the point under the pointer still, and a fit that frames content.
/// </summary>
public sealed class PanZoom
{
    public float Zoom { get; private set; } = 1f;
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public float MinZoom { get; }
    public float MaxZoom { get; }

    /// <summary>Pixels per content unit at zoom one.</summary>
    public float Scale { get; }

    public PanZoom(float scale, float minZoom, float maxZoom)
    {
        Scale = scale;
        MinZoom = minZoom;
        MaxZoom = maxZoom;
    }

    /// <summary>Content y grows north, screen y grows down.</summary>
    public (float X, float Y) ToScreen(double x, double y) => ((float)x * Scale * Zoom + OffsetX, -(float)y * Scale * Zoom + OffsetY);

    public (double X, double Y) ToContent(float sx, float sy) => ((sx - OffsetX) / (Scale * Zoom), -(sy - OffsetY) / (Scale * Zoom));

    public void Pan(float dx, float dy)
    {
        OffsetX += dx;
        OffsetY += dy;
    }

    /// <summary>Zooms by a factor about a screen point, which stays over the same content point.</summary>
    public void ZoomAt(float sx, float sy, float factor)
    {
        var (cx, cy) = ToContent(sx, sy);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var (nx, ny) = ToScreen(cx, cy);
        OffsetX += sx - nx;
        OffsetY += sy - ny;
    }

    /// <summary>Centres content bounds in a screen area at the largest zoom that shows them, within the zoom bounds.</summary>
    public void Fit(double minX, double minY, double maxX, double maxY, float width, float height, float margin = 24)
    {
        var w = Math.Max(1e-3, maxX - minX) * Scale;
        var h = Math.Max(1e-3, maxY - minY) * Scale;
        Zoom = Math.Clamp((float)Math.Min((width - margin * 2) / w, (height - margin * 2) / h), MinZoom, MaxZoom);
        var (cx, cy) = ((minX + maxX) / 2, (minY + maxY) / 2);
        OffsetX = width / 2 - (float)cx * Scale * Zoom;
        OffsetY = height / 2 + (float)cy * Scale * Zoom;
    }
}
