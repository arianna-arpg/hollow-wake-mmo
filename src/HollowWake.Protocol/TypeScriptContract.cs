using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HollowWake.Protocol;

/// <summary>
/// The wire contract as a TypeScript module for the web client: an interface per message and per nested
/// record, with field names exactly as <see cref="MessageSerializer"/> writes them, and a union of every
/// message keyed by its "t" discriminator. Generated from the message types so the browser client cannot
/// drift from Messages.cs; a server test fails while the committed module is stale.
/// </summary>
public static class TypeScriptContract
{
    /// <summary>Where the web client keeps the module, relative to the repository root.</summary>
    public const string ModulePath = "web/src/net/protocol.ts";

    public const string RegenerateCommand = "dotnet run --project src/HollowWake.Tools -- protocol-ts";

    public static string Generate()
    {
        var options = MessageSerializer.Options;
        var naming = options.PropertyNamingPolicy ?? JsonNamingPolicy.SnakeCaseLower;
        var nullability = new NullabilityInfoContext();
        var messages = typeof(Message).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => (Type: a.DerivedType, Tag: a.TypeDiscriminator as string ?? throw new InvalidOperationException($"{a.DerivedType.Name} needs a string discriminator.")))
            .ToList();
        var records = new List<Type>();
        var known = new HashSet<Type>(messages.Select(m => m.Type));

        string Map(Type t)
        {
            if (Nullable.GetUnderlyingType(t) is { } underlying)
            {
                return Map(underlying);
            }
            if (t == typeof(string))
            {
                return "string";
            }
            if (t == typeof(bool))
            {
                return "boolean";
            }
            if (t == typeof(long) || t == typeof(ulong))
            {
                return "Int64";
            }
            if (t == typeof(byte[]))
            {
                return "string";
            }
            if (t == typeof(int) || t == typeof(uint) || t == typeof(short) || t == typeof(ushort) || t == typeof(byte) || t == typeof(sbyte)
                || t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            {
                return "number";
            }
            if (t.IsArray)
            {
                return ArrayOf(Map(t.GetElementType()!));
            }
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                var args = t.GetGenericArguments();
                if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IReadOnlyList<>) || def == typeof(IEnumerable<>) || def == typeof(HashSet<>))
                {
                    return ArrayOf(Map(args[0]));
                }
                if ((def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>)) && args[0] == typeof(string))
                {
                    return $"Record<string, {Map(args[1])}>";
                }
            }
            if (options.Converters.Any(c => c.CanConvert(t)))
            {
                // Shapes owned by a content converter (weave specs, tag sets) stay opaque to the web client.
                return "unknown";
            }
            if (t.IsClass && t.Namespace == typeof(Message).Namespace)
            {
                if (known.Add(t))
                {
                    records.Add(t);
                }
                return t.Name;
            }
            throw new NotSupportedException($"No TypeScript mapping for {t.FullName}; extend {nameof(TypeScriptContract)}.");
        }

        void Interface(StringBuilder sb, Type type, string? tag)
        {
            sb.Append("export interface ").Append(type.Name).Append(" {\n");
            if (tag is not null)
            {
                sb.Append("  t: \"").Append(tag).Append("\";\n");
            }
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetMethod is not null && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .OrderBy(p => p.MetadataToken);
            foreach (var p in properties)
            {
                var name = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? naming.ConvertName(p.Name);
                var optional = Nullable.GetUnderlyingType(p.PropertyType) is not null
                    || (!p.PropertyType.IsValueType && nullability.Create(p).ReadState == NullabilityState.Nullable);
                var ts = Map(p.PropertyType);
                if (p.PropertyType == typeof(byte[]))
                {
                    sb.Append("  /** base64 */\n");
                }
                else if (ts == "unknown")
                {
                    sb.Append("  /** opaque here: see ").Append(p.PropertyType.Name).Append(" in C# */\n");
                }
                sb.Append("  ").Append(name).Append(optional ? "?: " : ": ").Append(ts).Append(";\n");
            }
            sb.Append("}\n");
        }

        var messageText = new StringBuilder();
        foreach (var (type, tag) in messages)
        {
            messageText.Append('\n');
            Interface(messageText, type, tag);
        }
        var recordText = new StringBuilder();
        for (var i = 0; i < records.Count; i++)
        {
            recordText.Append('\n');
            Interface(recordText, records[i], null);
        }

        var sb = new StringBuilder();
        sb.Append("// Hollow Wake MMO wire protocol, generated from src/HollowWake.Protocol/Messages.cs. Do not edit by hand:\n");
        sb.Append("// after changing a message, regenerate with `").Append(RegenerateCommand).Append("`.\n");
        sb.Append("// HollowWake.Server.Tests fails while this file and the C# messages disagree.\n");
        sb.Append('\n');
        sb.Append("/** The protocol version the server speaks (ProtocolInfo.Version), sent in hello. */\n");
        sb.Append("export const PROTOCOL_VERSION = ").Append(ProtocolInfo.Version).Append(";\n");
        sb.Append('\n');
        sb.Append("/**\n");
        sb.Append(" * A 64-bit integer from C# (entity keys, item uids, nonces, the world seed): a number when it is a safe\n");
        sb.Append(" * JavaScript integer, otherwise its exact decimal string. The server reads either form.\n");
        sb.Append(" */\n");
        sb.Append("export type Int64 = number | string;\n");
        sb.Append("\n// ---------------------------------------------------------------- messages\n");
        sb.Append(messageText);
        sb.Append("\n// ---------------------------------------------------------------- records inside messages\n");
        sb.Append(recordText);
        sb.Append('\n');
        sb.Append("/** Every message, discriminated by t. */\n");
        sb.Append("export type Message =\n");
        for (var i = 0; i < messages.Count; i++)
        {
            sb.Append("  | ").Append(messages[i].Type.Name).Append(i == messages.Count - 1 ? ";\n" : "\n");
        }
        sb.Append('\n');
        sb.Append("export type MessageType = Message[\"t\"];\n");
        sb.Append('\n');
        sb.Append("/** The message interface for a discriminator, for example MessageOf<\"chunk_data\">. */\n");
        sb.Append("export type MessageOf<T extends MessageType> = Extract<Message, { t: T }>;\n");
        sb.Append('\n');
        sb.Append("/** Every discriminator, in the order Messages.cs declares them. */\n");
        sb.Append("export const MESSAGE_TYPES: readonly MessageType[] = [\n");
        foreach (var (_, tag) in messages)
        {
            sb.Append("  \"").Append(tag).Append("\",\n");
        }
        sb.Append("];\n");
        return sb.ToString();
    }

    private static string ArrayOf(string element) => element.Contains(' ') || element.Contains('|') ? $"({element})[]" : element + "[]";
}
