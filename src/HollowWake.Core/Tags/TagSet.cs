using System.Collections;

namespace HollowWake.Core.Tags;

/// <summary>
/// Immutable, order-independent set of lowercase tags.
/// Tags are hierarchical with dots: a set containing "weapon.sword" satisfies <c>Has("weapon")</c>.
/// Tags are the universal join of the whole engine: modifiers scope by them, affixes spawn by them,
/// quests count by them, behaviors react to them, zones and lore carry them.
/// </summary>
public sealed class TagSet : IEquatable<TagSet>, IEnumerable<string>
{
    public static readonly TagSet Empty = new(Array.Empty<string>());

    private readonly string[] _tags;
    private readonly int _hash;

    private TagSet(string[] sortedDistinct)
    {
        _tags = sortedDistinct;
        var h = new HashCode();
        foreach (var t in _tags)
        {
            h.Add(t, StringComparer.Ordinal);
        }
        _hash = h.ToHashCode();
    }

    public static TagSet Of(params string[] tags) => From(tags);

    public static TagSet From(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Empty;
        }
        var list = new List<string>();
        foreach (var raw in tags)
        {
            var n = Normalize(raw);
            if (n.Length > 0)
            {
                list.Add(n);
            }
        }
        if (list.Count == 0)
        {
            return Empty;
        }
        var arr = list.Distinct(StringComparer.Ordinal).ToArray();
        Array.Sort(arr, StringComparer.Ordinal);
        return new TagSet(arr);
    }

    /// <summary>Parses "a b c" or "a, b, c" into a set.</summary>
    public static TagSet Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }
        return From(text.Split(new[] { ' ', ',', ';', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Normalize(string tag) => tag.Trim().ToLowerInvariant();

    public int Count => _tags.Length;
    public bool IsEmpty => _tags.Length == 0;

    /// <summary>True when the set contains the tag or any descendant of it ("weapon" matches "weapon.sword").</summary>
    public bool Has(string tag)
    {
        var t = Normalize(tag);
        foreach (var mine in _tags)
        {
            if (mine.Length == t.Length)
            {
                if (string.Equals(mine, t, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (mine.Length > t.Length && mine[t.Length] == '.' && mine.StartsWith(t, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasExact(string tag) => Array.BinarySearch(_tags, Normalize(tag), StringComparer.Ordinal) >= 0;

    public bool HasAll(TagSet other)
    {
        foreach (var t in other._tags)
        {
            if (!Has(t))
            {
                return false;
            }
        }
        return true;
    }

    public bool HasAny(TagSet other)
    {
        foreach (var t in other._tags)
        {
            if (Has(t))
            {
                return true;
            }
        }
        return false;
    }

    public TagSet With(params string[] more) => Union(From(more));

    /// <summary>Adds one tag without rebuilding the set: the common case on the hit path.</summary>
    public TagSet With(string tag)
    {
        var n = Normalize(tag);
        if (n.Length == 0)
        {
            return this;
        }
        var at = Array.BinarySearch(_tags, n, StringComparer.Ordinal);
        if (at >= 0)
        {
            return this;
        }
        at = ~at;
        var arr = new string[_tags.Length + 1];
        Array.Copy(_tags, 0, arr, 0, at);
        arr[at] = n;
        Array.Copy(_tags, at, arr, at + 1, _tags.Length - at);
        return new TagSet(arr);
    }

    /// <summary>Merges two sorted sets in one pass; returns one of the inputs when the other adds nothing.</summary>
    public TagSet Union(TagSet other)
    {
        if (other.IsEmpty || ReferenceEquals(this, other))
        {
            return this;
        }
        if (IsEmpty)
        {
            return other;
        }
        var a = _tags;
        var b = other._tags;
        var merged = new string[a.Length + b.Length];
        var i = 0;
        var j = 0;
        var n = 0;
        while (i < a.Length && j < b.Length)
        {
            var c = string.CompareOrdinal(a[i], b[j]);
            if (c < 0)
            {
                merged[n++] = a[i++];
            }
            else if (c > 0)
            {
                merged[n++] = b[j++];
            }
            else
            {
                merged[n++] = a[i++];
                j++;
            }
        }
        while (i < a.Length)
        {
            merged[n++] = a[i++];
        }
        while (j < b.Length)
        {
            merged[n++] = b[j++];
        }
        if (n == a.Length)
        {
            return this;
        }
        if (n == b.Length)
        {
            return other;
        }
        Array.Resize(ref merged, n);
        return new TagSet(merged);
    }

    public TagSet Without(TagSet other)
    {
        if (other.IsEmpty || IsEmpty)
        {
            return this;
        }
        return From(_tags.Where(t => !other.HasExact(t)));
    }

    public TagSet Without(string tag) => Without(Of(tag));

    public IReadOnlyList<string> Items => _tags;

    public bool Equals(TagSet? other)
    {
        if (other is null || other._tags.Length != _tags.Length)
        {
            return false;
        }
        for (var i = 0; i < _tags.Length; i++)
        {
            if (!string.Equals(_tags[i], other._tags[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is TagSet t && Equals(t);
    public override int GetHashCode() => _hash;
    public override string ToString() => "[" + string.Join(" ", _tags) + "]";

    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_tags).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
