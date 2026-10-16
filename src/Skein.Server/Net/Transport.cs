using System.Collections.Concurrent;
using Skein.Server.Config;

namespace Skein.Server.Net;

/// <summary>One connected client, whatever the transport.</summary>
public interface IConnection
{
    long Id { get; }
    string RemoteAddress { get; }
    bool IsOpen { get; }

    /// <summary>Queues one whole message payload. Unreliable sends may be dropped by UDP transports.</summary>
    void Send(byte[] payload, bool reliable = true);

    void Close(string reason);
}

/// <summary>
/// A way for clients to reach the server. The server never touches sockets directly, so the pure
/// TCP transport, the loopback transport used by tests and the BRUTAL RakNet transport are
/// interchangeable from config.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    string Name { get; }

    /// <summary>Raised from any thread. The server marshals onto its world thread through <see cref="TransportInbox"/>.</summary>
    event Action<IConnection>? Connected;
    event Action<IConnection, byte[]>? Received;
    event Action<IConnection, string>? Disconnected;

    Task StartAsync(ServerConfig config, CancellationToken cancellation);
    Task StopAsync();

    /// <summary>Called once per server tick on the world thread, for transports that need pumping.</summary>
    void Poll();
}

public enum TransportEventKind
{
    Connected,
    Received,
    Disconnected,
}

public readonly record struct TransportEvent(TransportEventKind Kind, IConnection Connection, byte[]? Payload, string? Reason);

/// <summary>Thread-safe queue that moves transport events onto the world thread.</summary>
public sealed class TransportInbox
{
    private readonly ConcurrentQueue<TransportEvent> _queue = new();

    public void Attach(ITransport transport)
    {
        transport.Connected += c => _queue.Enqueue(new TransportEvent(TransportEventKind.Connected, c, null, null));
        transport.Received += (c, payload) => _queue.Enqueue(new TransportEvent(TransportEventKind.Received, c, payload, null));
        transport.Disconnected += (c, reason) => _queue.Enqueue(new TransportEvent(TransportEventKind.Disconnected, c, null, reason));
    }

    public bool TryDequeue(out TransportEvent evt) => _queue.TryDequeue(out evt);

    public int Count => _queue.Count;
}
