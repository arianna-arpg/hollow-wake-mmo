using Godot;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.World.Appearance;

/// <summary>Which representation the player asked for (the bodies option): geometry, cards, or auto by camera.</summary>
public enum BodiesMode
{
    Auto,
    Geometry,
    Cards,
}

/// <summary>
/// The appearance pipeline on the desktop client (docs/design/appearance.md): resolves an entity's look to a
/// body plan with its gear, builds the geometry or hands out a baked card atlas for it, and holds the
/// settings every body reads (the bodies option, reduced motion, shadows). Plans are built per entity, since
/// their seeded variation is the entity's own; atlases are shared per look and gear.
/// </summary>
public sealed class AppearanceService
{
    private readonly Dictionary<string, CardAtlas> _atlases = new(StringComparer.Ordinal);

    public AppearanceCatalog Catalog { get; }
    public MotionLook Motion { get; }
    public CardBaker Baker { get; }
    public BodiesMode Mode { get; set; } = BodiesMode.Auto;
    public bool ReducedMotion { get; set; }
    public bool Shadows { get; set; } = true;

    /// <summary>Above this orthographic size the auto mode draws cards in the isometric camera.</summary>
    public float CardZoom { get; set; } = 36f;

    /// <summary>What the camera is doing, read each frame by the views choosing their representation.</summary>
    public bool Isometric { get; set; } = true;
    public float OrthoSize { get; set; } = 28f;
    public float CameraYaw { get; set; }

    /// <summary>The self's worn equipment, by slot, so its body shows what it wears.</summary>
    public Func<IReadOnlyDictionary<string, ItemInfo>>? SelfEquipment { get; set; }

    public AppearanceService(ContentRegistry content, CardBaker baker)
    {
        Catalog = new AppearanceCatalog(content);
        Motion = Catalog.Timings;
        Baker = baker;
    }

    /// <summary>Whether a body with this card policy should be a card now, with hysteresis around the zoom threshold.</summary>
    public bool WantsCard(string policy, bool isCardNow, float height)
    {
        if (policy == "never" || height > 4f)
        {
            return false;
        }
        switch (Mode)
        {
            case BodiesMode.Geometry:
                return false;
            case BodiesMode.Cards:
                return true;
            default:
                if (!Isometric)
                {
                    return false;
                }
                if (policy == "prefer")
                {
                    return true;
                }
                var threshold = isCardNow ? CardZoom - 4f : CardZoom + 4f;
                return OrthoSize >= threshold;
        }
    }

    /// <summary>The look for an entity: its id's entry, else its pattern's tags, else nothing (the view keeps a block).</summary>
    public AppearanceDef? LookFor(EntityState s) => Catalog.Resolve(s.Look ?? s.Pattern, s.Pattern);

    /// <summary>A built plan for an entity with the gear it wears: the self's own equipment, a monster's grants, a Calling's kit.</summary>
    public BodyPlan PlanFor(EntityState s, AppearanceDef look, bool isSelf, out string gearKey)
    {
        var plan = BodyFamilies.Build(look, s.Key, s.Height, s.Radius);
        var key = new System.Text.StringBuilder(look.Id);
        if (look.Body == "drop" && s.Look is { } dropLook && dropLook.StartsWith("drop:", StringComparison.Ordinal))
        {
            var gear = Catalog.GearForItem(dropLook[5..]);
            if (gear is not null)
            {
                GearFamilies.Lay(plan, gear, s.Key);
                key.Append('+').Append(gear.Id);
            }
        }
        else if (plan.Sockets.Count > 0)
        {
            var worn = new Dictionary<string, (string? Class, IEnumerable<string> Tags)>(StringComparer.Ordinal);
            foreach (var (slot, item) in Catalog.DefaultGear(look, s.Pattern))
            {
                worn[slot] = item;
            }
            if (isSelf && SelfEquipment?.Invoke() is { } equipment)
            {
                worn.Clear();
                foreach (var (slot, item) in equipment)
                {
                    var fromBase = Catalog.ItemLook(item.Base);
                    worn[slot] = (item.Class ?? fromBase?.Class, fromBase?.Tags ?? Array.Empty<string>());
                }
            }
            foreach (var slot in worn.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var (itemClass, tags) = worn[slot];
                var gear = Catalog.GearFor(slot, itemClass, tags);
                if (gear is null)
                {
                    continue;
                }
                GearFamilies.Wear(plan, gear, slot, s.Key);
                key.Append('+').Append(slot).Append('=').Append(gear.Id);
            }
        }
        gearKey = key.ToString();
        return plan;
    }

    /// <summary>The card atlas for a look and gear, baked on first request over the next frames; null until ready.</summary>
    public CardAtlas AtlasFor(string key, Func<BodyPlan> unjitteredPlan)
    {
        if (!_atlases.TryGetValue(key, out var atlas))
        {
            atlas = new CardAtlas(key);
            _atlases[key] = atlas;
            Baker.Enqueue(atlas, unjitteredPlan());
        }
        return atlas;
    }

    /// <summary>Bytes the baked atlases hold on the GPU, counted from their dimensions (RGBA8 with mipmaps).</summary>
    public long AtlasBytes => _atlases.Values.Where(a => a.Ready).Sum(a => a.Bytes);

    public int AtlasCount => _atlases.Values.Count(a => a.Ready);

    /// <summary>Forgets every atlas (the representation was switched off): their textures go with the views that held them.</summary>
    public void DropAtlases()
    {
        foreach (var atlas in _atlases.Values)
        {
            atlas.Dispose();
        }
        _atlases.Clear();
    }
}
