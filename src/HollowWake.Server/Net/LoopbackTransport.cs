using System.Collections.Concurrent;
using HollowWake.Protocol;
using HollowWake.Server.Config;

namespace HollowWake.Server.Net;

/// <summary>
/// An in-process transport: tests and tools connect a <see cref="LoopbackClient"/> and talk to the
/// server through the same code path real sockets use, with no ports and no threads.
/// </summary>
public sealed class LoopbackTransport : ITransport
{
    private long _nextId = 1;

    public string Name => "loopback";

    public event Action<IConnection>? Connected;
    public event Action<IConnection, byte[]>? Received;
    public event Action<IConnection, string>? Disconnected;

    public Task StartAsync(ServerConfig config, CancellationToken cancellation) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void Poll() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public LoopbackClient Connect(string address = "loopback")
    {
        var conn = new LoopbackConnection(Interlocked.Increment(ref _nextId), address, this);
        Connected?.Invoke(conn);
        return new LoopbackClient(conn);
    }

    internal void RaiseReceived(IConnection c, byte[] payload) => Received?.Invoke(c, payload);
    internal void RaiseDisconnected(IConnection c, string reason) => Disconnected?.Invoke(c, reason);

    internal sealed class LoopbackConnection : IConnection
    {
        private readonly LoopbackTransport _owner;
        public readonly ConcurrentQueue<byte[]> ToClient = new();

        public long Id { get; }
        public string RemoteAddress { get; }
        public bool IsOpen { get; private set; } = true;

        public LoopbackConnection(long id, string address, LoopbackTransport owner)
        {
            Id = id;
            RemoteAddress = address;
            _owner = owner;
        }

        public void Send(byte[] payload, bool reliable = true) => ToClient.Enqueue(payload);

        public void Close(string reason)
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            _owner.RaiseDisconnected(this, reason);
        }

        internal void Deliver(byte[] payload)
        {
            if (IsOpen)
            {
                _owner.RaiseReceived(this, payload);
            }
        }
    }
}

/// <summary>The client end of a loopback connection: send messages in, read messages out.</summary>
public sealed class LoopbackClient
{
    private readonly LoopbackTransport.LoopbackConnection _conn;

    internal LoopbackClient(LoopbackTransport.LoopbackConnection conn)
    {
        _conn = conn;
    }

    public bool IsOpen => _conn.IsOpen;

    public void Send(Message message) => _conn.Deliver(MessageSerializer.Encode(message));

    public List<Message> Drain()
    {
        var list = new List<Message>();
        while (_conn.ToClient.TryDequeue(out var payload))
        {
            var m = MessageSerializer.Decode(payload);
            if (m is not null)
            {
                list.Add(m);
            }
        }
        return list;
    }

    public void Disconnect() => _conn.Close("client left");
}
