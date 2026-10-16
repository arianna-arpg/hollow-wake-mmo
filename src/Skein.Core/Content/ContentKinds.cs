using System.Text.Json;
using System.Text.Json.Nodes;
using Skein.Core.Definitions;

namespace Skein.Core.Content;

/// <summary>A registered definition kind: its content name, its C# type, and an optional validator.</summary>
public sealed class ContentKind
{
    public required string Name { get; init; }
    public required Type DefinitionType { get; init; }
    public required Func<JsonObject, IDefinition> Bind { get; init; }
    public Action<IDefinition, ValidationContext>? Validate { get; init; }
    public bool Ordered { get; init; }
}

/// <summary>
/// Registry of definition kinds. Built-in kinds are registered at startup; code mods register their
/// own with <see cref="Register{T}"/> and from then on the loader binds, validates and exposes them
/// like any other kind.
/// </summary>
public static class ContentKinds
{
    private static readonly Dictionary<string, ContentKind> Kinds = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, ContentKind> ByType = new();
    private static readonly object Gate = new();

    static ContentKinds()
    {
        BuiltinKinds.RegisterAll();
    }

    public static void Register<T>(string name, Action<T, ValidationContext>? validate = null) where T : class, IDefinition
    {
        var kind = new ContentKind
        {
            Name = name,
            DefinitionType = typeof(T),
            Bind = obj => ContentJson.Bind<T>(obj),
            Validate = validate is null ? null : (d, ctx) => validate((T)d, ctx),
        };
        lock (Gate)
        {
            Kinds[name] = kind;
            ByType[typeof(T)] = kind;
        }
    }

    public static bool TryGet(string name, out ContentKind? kind)
    {
        lock (Gate)
        {
            return Kinds.TryGetValue(name, out kind);
        }
    }

    public static bool TryGet(Type type, out ContentKind? kind)
    {
        lock (Gate)
        {
            return ByType.TryGetValue(type, out kind);
        }
    }

    public static string NameOf<T>() where T : IDefinition
    {
        if (TryGet(typeof(T), out var k) && k is not null)
        {
            return k.Name;
        }
        throw new InvalidOperationException($"{typeof(T).Name} is not a registered content kind.");
    }

    public static IReadOnlyList<ContentKind> All
    {
        get
        {
            lock (Gate)
            {
                return Kinds.Values.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
            }
        }
    }
}
