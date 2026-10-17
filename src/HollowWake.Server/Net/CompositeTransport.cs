using System.Collections.Concurrent;
using HollowWake.Server.Config;

namespace HollowWake.Server.Net;

/// <summary>
/// Several transports behind one <see cref="ITransport"/>, so a host can accept desktop clients over TCP and
/// browser clients over WebSocket into the same world. Each transport numbers its own connections, so
/// connections are re-identified here to keep ids unique across all of them (sessions are keyed by id).
/// </summary>
public sealed class CompositeTransport : ITransport
{
    private readonly ITransport[] _transports;
    private readonly ConcurrentDictionary<IConnection, Connection> _connections = new(ReferenceEqualityComparer.Instance);
    private long _nextId;

    public string Name { get; }

    public IReadOnlyList<ITransport> Transports => _transports;

    public event Action<IConnection>? Connected;
    public event Action<IConnection, byte[]>? Received;
    public event Action<IConnection, string>? Disconnected;

    public CompositeTransport(params ITransport[] transports)
    {
        if (transports.Length == 0)
        {
            throw new ArgumentException("A composite needs at least one transport.", nameof(transports));
        }
        _transports = transports;
        Name = string.Join("+", transports.Select(t => t.Name));
        foreach (var transport in transports)
        {
            var name = transport.Name;
            transport.Connected += inner =>
            {
                var outer = new Connection(Interlocked.Increment(ref _nextId), name, inner);
                _connections[inner] = outer;
                Connected?.Invoke(outer);
            };
            transport.Received += (inner, payload) =>
            {
                if (_connections.TryGetValue(inner, out var outer))
                {
                    Received?.Invoke(outer, payload);
                }
            };
            transport.Disconnected += (inner, reason) =>
            {
                if (_connections.TryRemove(inner, out var outer))
                {
                    Disconnected?.Invoke(outer, reason);
                }
            };
        }
    }

    public async Task StartAsync(ServerConfig config, CancellationToken cancellation)
    {
        var started = new List<ITransport>();
        try
        {
            foreach (var transport in _transports)
            {
                await transport.StartAsync(config, cancellation);
                started.Add(transport);
            }
        }
        catch
        {
            // Leave nothing half-open: a host that fails to start should release the ports it took.
            foreach (var transport in Enumerable.Reverse(started))
            {
                await StopQuietly(transport);
            }
            throw;
        }
    }

    public async Task StopAsync()
    {
        foreach (var transport in Enumerable.Reverse(_transports))
        {
            await StopQuietly(transport);
        }
    }

    private static async Task StopQuietly(ITransport transport)
    {
        try
        {
            await transport.StopAsync();
        }
        catch
        {
            // keep stopping the others
        }
    }

    public void Poll()
    {
        foreach (var transport in _transports)
        {
            transport.Poll();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in Enumerable.Reverse(_transports))
        {
            await transport.DisposeAsync();
        }
    }

    private sealed class Connection : IConnection
    {
        private readonly IConnection _inner;

        public long Id { get; }
        public string RemoteAddress { get; }
        public bool IsOpen => _inner.IsOpen;

        public Connection(long id, string transport, IConnection inner)
        {
            Id = id;
            _inner = inner;
            RemoteAddress = $"{transport} {inner.RemoteAddress}";
        }

        public void Send(byte[] payload, bool reliable = true) => _inner.Send(payload, reliable);

        public void Close(string reason) => _inner.Close(reason);
    }
}
