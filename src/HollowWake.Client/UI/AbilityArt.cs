using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI;

/// <summary>Bakes the shared SVG at its drawn size, with a bounded per-skin raster cache.</summary>
public sealed class AbilityArt
{
    private readonly AbilityIcons _catalog;
    private readonly Dictionary<(AbilityFace Face, int Size), LinkedListNode<((AbilityFace Face, int Size) Key, Texture2D Texture)>> _cache = new();
    private readonly LinkedList<((AbilityFace Face, int Size) Key, Texture2D Texture)> _lru = new();

    public AbilityArt(AbilityIcons catalog) => _catalog = catalog;

    public void Draw(CanvasItem canvas, Rect2 rect, AbilityFace face, float alpha = 1, bool dormant = false)
    {
        // Canvas draw commands outlive a cache entry. Keep native resource references on the owner
        // until its next redraw, so LRU eviction cannot erase a still-visible inventory tile.
        var frame = (long)Engine.GetProcessFrames();
        if (!canvas.HasMeta("ability_art_frame") || canvas.GetMeta("ability_art_frame").AsInt64() != frame)
        {
            canvas.SetMeta("ability_art_frame", frame);
            canvas.SetMeta("ability_art_textures", new Godot.Collections.Array<Texture2D>());
        }
        var transform = canvas.GetGlobalTransformWithCanvas();
        var scale = Math.Max(transform.X.Length(), transform.Y.Length());
        var pixels = Math.Clamp((int)Math.Ceiling(Math.Max(rect.Size.X, rect.Size.Y) * scale), 12, _catalog.Style.MaxBakeSize);
        var key = (face, pixels);
        if (!_cache.TryGetValue(key, out var node))
        {
            using var image = new Image();
            var svg = _catalog.Svg(face).Replace("viewBox=", "width=\"24\" height=\"24\" viewBox=", StringComparison.Ordinal);
            if (image.LoadSvgFromString(svg, pixels / 24f) != Error.Ok) return;
            node = _lru.AddLast((key, ImageTexture.CreateFromImage(image)));
            _cache[key] = node;
            if (_cache.Count > _catalog.Style.CacheLimit && _lru.First is { } oldest)
            {
                _lru.RemoveFirst();
                _cache.Remove(oldest.Value.Key);
                oldest.Value.Texture.Dispose();
            }
        }
        else
        {
            _lru.Remove(node);
            _lru.AddLast(node);
        }
        var references = canvas.GetMeta("ability_art_textures").AsGodotArray<Texture2D>();
        if (!references.Contains(node.Value.Texture)) references.Add(node.Value.Texture);
        canvas.DrawTextureRect(node.Value.Texture, rect, false, new Color(dormant ? .5f : 1, dormant ? .5f : 1, dormant ? .5f : 1, alpha));
    }
}
