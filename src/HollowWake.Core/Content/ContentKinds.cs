using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content;

/// <summary>
/// A registered definition kind: its content name, its C# type, and its validators. A kind can have
/// several validators: the one it registers with, then any a package or mod adds for its own fields.
/// </summary>
public sealed class ContentKind
{
    private Action<IDefinition, ValidationContext>[] _validators = Array.Empty<Action<IDefinition, ValidationContext>>();

    public required string Name { get; init; }
    public required Type DefinitionType { get; init; }
    public required Func<JsonObject, IDefinition> Bind { get; init; }
    public bool Ordered { get; init; }

    /// <summary>Validates the collection, including an absent kind, after every definition is bound.</summary>
    public Action<ValidationContext>? ValidateCollection { get; init; }

    /// <summary>Every validator of the kind, in the order they were added.</summary>
    public IReadOnlyList<Action<IDefinition, ValidationContext>> Validators => Volatile.Read(ref _validators);

    /// <summary>All validators as one delegate, or null when the kind has none.</summary>
    public Action<IDefinition, ValidationContext>? Validate
    {
        get
        {
            var validators = Volatile.Read(ref _validators);
            if (validators.Length == 0)
            {
                return null;
            }
            return (d, ctx) =>
            {
                foreach (var v in validators)
                {
                    v(d, ctx);
                }
            };
        }
    }

    /// <summary>Appends a validator. Copy on write, so a load in progress keeps the list it started with.</summary>
    internal void AddValidator(Action<IDefinition, ValidationContext> validator)
    {
        var next = new Action<IDefinition, ValidationContext>[_validators.Length + 1];
        Array.Copy(_validators, next, _validators.Length);
        next[^1] = validator;
        Volatile.Write(ref _validators, next);
    }
}

/// <summary>
/// Registry of definition kinds. Built-in kinds are registered at startup; code mods register their
/// own with <see cref="Register{T}"/> and from then on the loader binds, validates and exposes them
/// like any other kind. Packages that add fields to an existing kind add a validator for them with
/// <see cref="AddValidator{T}"/> rather than editing the kind's own.
/// </summary>
public static class ContentKinds
{
    // Registration is rare; content lookup happens for every resource, hit and snapshot.
    // Publish both indexes together, and never mutate dictionaries visible to readers.
    private sealed record Index(Dictionary<string, ContentKind> Kinds, Dictionary<Type, ContentKind> ByType);
    private static Index _index = new(new(StringComparer.Ordinal), new());
    private static readonly object Gate = new();

    static ContentKinds()
    {
        BuiltinKinds.RegisterAll();
    }

    /// <summary>Registers a kind, replacing any earlier kind of the same name (and its validators).</summary>
    public static void Register<T>(string name, Action<T, ValidationContext>? validate = null, Action<ValidationContext>? validateCollection = null) where T : class, IDefinition
    {
        var kind = new ContentKind
        {
            Name = name,
            DefinitionType = typeof(T),
            Bind = obj => ContentJson.Bind<T>(obj),
            ValidateCollection = validateCollection,
        };
        if (validate is not null)
        {
            kind.AddValidator((d, ctx) => validate((T)d, ctx));
        }
        lock (Gate)
        {
            var previous = _index;
            var kinds = new Dictionary<string, ContentKind>(previous.Kinds, StringComparer.Ordinal) { [name] = kind };
            var types = new Dictionary<Type, ContentKind>(previous.ByType) { [typeof(T)] = kind };
            Volatile.Write(ref _index, new Index(kinds, types));
        }
    }

    /// <summary>Adds a validator to the kind registered for <typeparamref name="T"/>.</summary>
    public static void AddValidator<T>(Action<T, ValidationContext> validate) where T : class, IDefinition
    {
        lock (Gate)
        {
            if (!_index.ByType.TryGetValue(typeof(T), out var kind))
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not a registered content kind.");
            }
            kind.AddValidator((d, ctx) => validate((T)d, ctx));
        }
    }

    public static bool TryGet(string name, out ContentKind? kind)
    {
        return Volatile.Read(ref _index).Kinds.TryGetValue(name, out kind);
    }

    public static bool TryGet(Type type, out ContentKind? kind)
    {
        return Volatile.Read(ref _index).ByType.TryGetValue(type, out kind);
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
            return Volatile.Read(ref _index).Kinds.Values.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
        }
    }
}
