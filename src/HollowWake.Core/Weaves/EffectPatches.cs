using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;

namespace HollowWake.Core.Weaves;

/// <summary>
/// The effect graph a weave compiles from, held as JSON the compiler can edit: the root list in order,
/// each entry an effect node with its <c>type</c>. Entries that no patch or hook touched compile from
/// their original specs; touched entries compile from the edited JSON. Probes read the graph through
/// <see cref="Nodes()"/>, which walks every effect node depth first.
/// </summary>
public sealed class WeaveGraph
{
    private sealed class Root
    {
        public TypedSpec? Original;
        public JsonObject Node = null!;
        public bool Dirty;
        public bool Patchable;
    }

    private readonly EffectRegistry _effects;
    private readonly List<Root> _roots = new();
    // The nodes that were root entries when added: they stay "root" for patches even once wrapped.
    private readonly HashSet<JsonObject> _rootNodes = new(ReferenceEqualityComparer.Instance);

    public WeaveGraph(EffectRegistry effects)
    {
        _effects = effects;
    }

    public EffectRegistry Effects => _effects;

    public int Count => _roots.Count;

    /// <summary>Appends a root entry. Patchable entries (the form's and the preset's) are the ones patches edit.</summary>
    public void Add(TypedSpec spec, bool patchable)
    {
        var node = ToNode(spec);
        _roots.Add(new Root { Original = spec, Node = node, Patchable = patchable });
        _rootNodes.Add(node);
    }

    /// <summary>Whether a node was a root entry as added, before any wrap moved it under a wrapper.</summary>
    internal bool IsRootNode(JsonObject node) => _rootNodes.Contains(node);

    /// <summary>A spec as an effect node: its params with its <c>type</c>, deep-copied.</summary>
    public static JsonObject ToNode(TypedSpec spec)
    {
        var node = new JsonObject { ["type"] = spec.Type };
        foreach (var kv in spec.Params)
        {
            node[kv.Key] = kv.Value?.DeepClone();
        }
        return node;
    }

    /// <summary>The root list's nodes in order.</summary>
    public IEnumerable<JsonObject> RootNodes
    {
        get
        {
            foreach (var r in _roots)
            {
                yield return r.Node;
            }
        }
    }

    /// <summary>Every effect node in the graph, depth first, parents before their nested lists.</summary>
    public IEnumerable<JsonObject> Nodes()
    {
        var found = new List<NodeRef>();
        for (var i = 0; i < _roots.Count; i++)
        {
            Collect(i, onlyPatchable: false, found);
        }
        foreach (var n in found)
        {
            yield return n.Node;
        }
    }

    /// <summary>Every effect node of one type.</summary>
    public IEnumerable<JsonObject> Nodes(string type)
    {
        foreach (var n in Nodes())
        {
            if (TypeOf(n) == type)
            {
                yield return n;
            }
        }
    }

    public bool Has(string type)
    {
        foreach (var _ in Nodes(type))
        {
            return true;
        }
        return false;
    }

    public static string? TypeOf(JsonObject node) => JsonNum.StringOf(node["type"]);

    /// <summary>The root list as specs to compile: originals where nothing changed, the edited JSON elsewhere.</summary>
    public List<TypedSpec> ToSpecs()
    {
        var specs = new List<TypedSpec>(_roots.Count);
        foreach (var r in _roots)
        {
            if (!r.Dirty && r.Original is not null)
            {
                specs.Add(r.Original);
                continue;
            }
            specs.Add(r.Node.Deserialize<TypedSpec>(ContentJson.Options) ?? throw new InvalidOperationException("A patched effect lost its type."));
        }
        return specs;
    }

    // ------------------------------------------------------------ walking

    /// <summary>Where a node sits, so a wrap can replace it: an index in a list, under the root it belongs to.</summary>
    internal readonly record struct NodeRef(JsonObject Node, JsonArray? List, int Index, int Root, bool AtRoot);

    /// <summary>The effect nodes under one root entry, the entry itself first.</summary>
    internal void Collect(int root, bool onlyPatchable, List<NodeRef> into)
    {
        var r = _roots[root];
        if (onlyPatchable && !r.Patchable)
        {
            return;
        }
        into.Add(new NodeRef(r.Node, null, root, root, true));
        WalkParams(r.Node, root, into);
    }

    internal int RootCount => _roots.Count;

    private void WalkParams(JsonObject node, int root, List<NodeRef> into)
    {
        foreach (var kv in node)
        {
            if (kv.Key == "type")
            {
                continue;
            }
            WalkValue(kv.Value, root, into);
        }
    }

    private void WalkValue(JsonNode? value, int root, List<NodeRef> into)
    {
        switch (value)
        {
            case JsonArray list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is JsonObject element)
                    {
                        if (IsEffect(element))
                        {
                            into.Add(new NodeRef(element, list, i, root, false));
                            WalkParams(element, root, into);
                        }
                        else
                        {
                            WalkData(element, root, into);
                        }
                    }
                }
                break;
            case JsonObject obj:
                // A data object (a random choice, an on_tick block) may hold effect lists; an effect node
                // outside a list is not a graph position the compiler edits.
                if (!IsEffect(obj))
                {
                    WalkData(obj, root, into);
                }
                break;
        }
    }

    private void WalkData(JsonObject obj, int root, List<NodeRef> into)
    {
        foreach (var kv in obj)
        {
            WalkValue(kv.Value, root, into);
        }
    }

    private bool IsEffect(JsonObject obj) => TypeOf(obj) is { } type && _effects.Has(type);

    // ------------------------------------------------------------ editing

    internal void MarkDirty(int root) => _roots[root].Dirty = true;

    /// <summary>Replaces a node with another at the same position.</summary>
    internal void Replace(NodeRef at, JsonObject replacement)
    {
        if (at.AtRoot)
        {
            var r = _roots[at.Root];
            r.Node = replacement;
            r.Dirty = true;
            return;
        }
        at.List![at.Index] = replacement;
        _roots[at.Root].Dirty = true;
    }
}

/// <summary>
/// Applies glyph patches and hooks to a weave's graph. Patches run in glyph order and edit only the
/// form's and preset's nodes: <c>first</c> sets a parameter unless an earlier <c>first</c> patch set it
/// (beating the form's own value), <c>default</c> sets it only where the node has none, <c>append</c>
/// concatenates lists, and a <c>wrap</c> puts the wrapper directly around the matched node (once per
/// wrapper type in <c>first</c> and <c>default</c> modes; nesting in <c>wrap</c> and <c>append</c> modes,
/// where the first glyph's wrapper ends outermost). A node added at the root stays a root node for
/// <c>where: root</c> after a wrap. Hooks append each glyph's lists into every node exposing the hook,
/// including the root entries glyphs add.
/// </summary>
public sealed class EffectPatcher
{
    private readonly WeaveGraph _graph;
    private readonly EffectRegistry _effects;
    // Parameters a first-mode patch set on a node, so later glyphs cannot change them.
    private readonly Dictionary<JsonObject, HashSet<string>> _locked = new(ReferenceEqualityComparer.Instance);
    // Wrapper types already placed around a node, by the node's identity.
    private readonly Dictionary<JsonObject, HashSet<string>> _wrappedBy = new(ReferenceEqualityComparer.Instance);

    public EffectPatcher(WeaveGraph graph)
    {
        _graph = graph;
        _effects = graph.Effects;
    }

    /// <summary>Applies one glyph's patch. Throws <see cref="InvalidOperationException"/> when the patch is malformed.</summary>
    public void Apply(EffectPatch patch, string glyph)
    {
        var matches = Matches(patch);
        var mode = patch.EffectiveMode;
        foreach (var at in matches)
        {
            var node = at.Node;
            if (patch.Set is { } set)
            {
                foreach (var kv in set)
                {
                    SetParam(at, kv.Key, kv.Value, mode, glyph, patch.Type);
                }
            }
            if (patch.Wrap is { } wrap)
            {
                WrapNode(at, node, wrap, mode, glyph);
            }
        }
    }

    private List<WeaveGraph.NodeRef> Matches(EffectPatch patch)
    {
        var all = new List<WeaveGraph.NodeRef>();
        for (var i = 0; i < _graph.RootCount; i++)
        {
            _graph.Collect(i, onlyPatchable: true, all);
        }
        var matches = new List<WeaveGraph.NodeRef>();
        foreach (var n in all)
        {
            if (WeaveGraph.TypeOf(n.Node) != patch.Type)
            {
                continue;
            }
            if (patch.Where == PatchWhere.Root && !_graph.IsRootNode(n.Node))
            {
                continue;
            }
            matches.Add(n);
            if (patch.Where == PatchWhere.First)
            {
                break;
            }
        }
        return matches;
    }

    private void SetParam(WeaveGraph.NodeRef at, string param, JsonNode? value, PatchMode mode, string glyph, string type)
    {
        if (param == "type")
        {
            throw new InvalidOperationException($"Glyph '{glyph}' patch on '{type}' cannot set 'type'.");
        }
        if (_effects.TryGetSignature(type, out var signature) && !signature.Accepts(param))
        {
            throw new InvalidOperationException($"Glyph '{glyph}' patches '{type}' parameter '{param}', which '{type}' does not have.");
        }
        var node = at.Node;
        switch (mode)
        {
            case PatchMode.Default:
                if (node.ContainsKey(param))
                {
                    return;
                }
                node[param] = value?.DeepClone();
                break;
            case PatchMode.Append:
            {
                if (value is not JsonArray more)
                {
                    throw new InvalidOperationException($"Glyph '{glyph}' appends to '{type}' parameter '{param}' with a value that is not a list.");
                }
                if (node[param] is not JsonArray existing)
                {
                    existing = new JsonArray();
                    node[param] = existing;
                }
                foreach (var item in more)
                {
                    existing.Add(item?.DeepClone());
                }
                break;
            }
            default:
                // first (and a set riding along with a wrap): the first glyph to set a parameter keeps it.
                if (_locked.TryGetValue(node, out var locked) && locked.Contains(param))
                {
                    return;
                }
                node[param] = value?.DeepClone();
                if (!_locked.TryGetValue(node, out locked))
                {
                    locked = new HashSet<string>(StringComparer.Ordinal);
                    _locked[node] = locked;
                }
                locked.Add(param);
                break;
        }
        _graph.MarkDirty(at.Root);
    }

    private void WrapNode(WeaveGraph.NodeRef at, JsonObject node, TypedSpec wrap, PatchMode mode, string glyph)
    {
        if (!_effects.TryGetSignature(wrap.Type, out var signature))
        {
            throw new InvalidOperationException($"Glyph '{glyph}' wraps in unknown effect type '{wrap.Type}'.");
        }
        if (signature.Wraps is not { } inner)
        {
            throw new InvalidOperationException($"Glyph '{glyph}' wraps in '{wrap.Type}', which is not a wrapper.");
        }
        var once = mode is PatchMode.First or PatchMode.Default;
        if (_wrappedBy.TryGetValue(node, out var wrappers) && wrappers.Contains(wrap.Type) && once)
        {
            return;
        }
        var wrapper = WeaveGraph.ToNode(wrap);
        var list = wrapper[inner] as JsonArray ?? new JsonArray();
        wrapper[inner] = list;
        _graph.Replace(at, wrapper);
        list.Add(node);
        if (!_wrappedBy.TryGetValue(node, out wrappers))
        {
            wrappers = new HashSet<string>(StringComparer.Ordinal);
            _wrappedBy[node] = wrappers;
        }
        wrappers.Add(wrap.Type);
    }

    /// <summary>
    /// Appends hook lists into every node exposing the hook. <paramref name="targets"/> is the graph as it
    /// stood before any hook, so effects a hook adds are never themselves hooked.
    /// </summary>
    public void AppendHooks(IReadOnlyList<(JsonObject Node, int Root)> targets, string hook, IReadOnlyList<TypedSpec> effects)
    {
        if (effects.Count == 0)
        {
            return;
        }
        foreach (var (node, root) in targets)
        {
            var type = WeaveGraph.TypeOf(node);
            if (type is null || !_effects.TryGetSignature(type, out var signature) || !signature.Hooks.TryGetValue(hook, out var param))
            {
                continue;
            }
            if (node[param] is not JsonArray list)
            {
                list = new JsonArray();
                node[param] = list;
            }
            foreach (var spec in effects)
            {
                list.Add(WeaveGraph.ToNode(spec));
            }
            _graph.MarkDirty(root);
        }
    }

    /// <summary>The nodes hooks may target: every node now in the graph, with the root it belongs to.</summary>
    public IReadOnlyList<(JsonObject Node, int Root)> HookTargets()
    {
        var all = new List<WeaveGraph.NodeRef>();
        for (var i = 0; i < _graph.RootCount; i++)
        {
            _graph.Collect(i, onlyPatchable: false, all);
        }
        var targets = new List<(JsonObject, int)>(all.Count);
        foreach (var n in all)
        {
            targets.Add((n.Node, n.Root));
        }
        return targets;
    }
}
