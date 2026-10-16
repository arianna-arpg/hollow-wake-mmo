using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skein.Core.Content;

namespace Skein.Protocol;

/// <summary>
/// Encodes messages to bytes and back, and frames them with a 4-byte little-endian length prefix.
/// The body is JSON using the content conventions (snake_case, tag sets, expressions, weave specs),
/// so a message can be read by eye on the wire during development.
/// </summary>
public static class MessageSerializer
{
    public static readonly JsonSerializerOptions Options = Create();

    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(ContentJson.Options)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return o;
    }

    public static byte[] Encode(Message message) => JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static Message? Decode(ReadOnlySpan<byte> payload) => JsonSerializer.Deserialize<Message>(payload, Options);

    public static string ToJson(Message message) => JsonSerializer.Serialize(message, Options);

    /// <summary>Writes [length][payload] into a buffer.</summary>
    public static byte[] Frame(byte[] payload)
    {
        var framed = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(framed, payload.Length);
        payload.CopyTo(framed, 4);
        return framed;
    }

    /// <summary>
    /// Incremental reader for a stream of frames. Feed bytes as they arrive; complete payloads come
    /// out in order. Rejects oversize frames so a bad client cannot make the server allocate freely.
    /// </summary>
    public sealed class FrameReader
    {
        private byte[] _buffer = new byte[64 * 1024];
        private int _length;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (_length + data.Length > _buffer.Length)
            {
                var grown = new byte[Math.Max(_buffer.Length * 2, _length + data.Length)];
                Array.Copy(_buffer, grown, _length);
                _buffer = grown;
            }
            data.CopyTo(_buffer.AsSpan(_length));
            _length += data.Length;
        }

        public bool TryRead(out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (_length < 4)
            {
                return false;
            }
            var size = BinaryPrimitives.ReadInt32LittleEndian(_buffer);
            if (size < 0 || size > MaxMessageBytes)
            {
                throw new InvalidDataException($"Frame size {size} out of range.");
            }
            if (_length < 4 + size)
            {
                return false;
            }
            payload = new byte[size];
            Array.Copy(_buffer, 4, payload, 0, size);
            var remaining = _length - 4 - size;
            if (remaining > 0)
            {
                Array.Copy(_buffer, 4 + size, _buffer, 0, remaining);
            }
            _length = remaining;
            return true;
        }
    }
}
