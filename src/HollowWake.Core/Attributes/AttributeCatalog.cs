using HollowWake.Core.Definitions;

namespace HollowWake.Core.Attributes;

/// <summary>
/// The set of attribute definitions known to a simulation. Attributes not in the catalog still work
/// (base 0, no clamping) so content can introduce channels such as <c>projectile.count</c> freely,
/// but declaring them gives names, defaults, formulas, bounds and rounding.
/// </summary>
public sealed class AttributeCatalog
{
    private readonly Dictionary<string, AttributeDef> _defs = new(StringComparer.Ordinal);

    public static readonly AttributeCatalog Empty = new();

    public AttributeCatalog()
    {
    }

    public AttributeCatalog(IEnumerable<AttributeDef> defs)
    {
        foreach (var d in defs)
        {
            _defs[d.Id] = d;
        }
    }

    public int Count => _defs.Count;

    public IEnumerable<AttributeDef> All => _defs.Values;

    public bool TryGet(string id, out AttributeDef? def) => _defs.TryGetValue(id, out def);

    public bool Contains(string id) => _defs.ContainsKey(id);

    public AttributeDef? Get(string id) => _defs.TryGetValue(id, out var d) ? d : null;
}
