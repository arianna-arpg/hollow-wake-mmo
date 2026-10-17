using System.Collections.Concurrent;
using Brutal.RakNetApi;
using Microsoft.Extensions.Logging;
using HollowWake.Server.Config;
using HollowWake.Server.Net;

namespace HollowWake.Server.Host.Brutal;

/// <summary>
/// UDP transport on BRUTAL's RakNet binding. Reliable-ordered for everything that must arrive,
/// unreliable-sequenced for entity updates that are superseded by the next one. RakNet runs its own
/// network thread; <see cref="Poll"/> drains its packet queue on the world thread each tick, which is
/// exactly how RakNet expects to be used.
/// </summary>
public sealed unsafe class RakNetTransport : ITransport
{
    private readonly ILogger _log;
    private RakPeerInstance _peer;
    private readonly ConcurrentDictionary<ulong, RakConnection> _connections = new();
    private readonly ConcurrentQueue<(RakConnection Connection, byte[] Payload, bool Reliable)> _outbox = new();
    private long _nextId = 1;
    private bool _started;

    public string Name => "raknet";

    public event Action<IConnection>? Connected;
    public event Action<IConnection, byte[]>? Received;
    public event Action<IConnection, string>? Disconnected;

    public RakNetTransport(ILogger<RakNetTransport> log)
    {
        _log = log;
    }

    public Task StartAsync(ServerConfig config, CancellationToken cancellation)
    {
        _peer = RakNetLibrary.CreateInstance();
        if (_peer.IsNull())
        {
            throw new InvalidOperationException("RakNet could not create a peer instance.");
        }
        Span<SocketDescriptor> sockets = stackalloc SocketDescriptor[1];
        sockets[0] = config.Bind is "0.0.0.0" or "" ? new SocketDescriptor(config.Port) : new SocketDescriptor(config.Bind, config.Port);
        var result = _peer.Startup((uint)Math.Max(1, config.MaxPlayers), sockets);
        if (result != StartupResult.RaknetStarted)
        {
            throw new InvalidOperationException($"RakNet startup failed: {result}");
        }
        _peer.SetMaximumIncomingConnections((ushort)Math.Clamp(config.MaxPlayers, 1, ushort.MaxValue));
        _peer.SetTimeoutTime(15000, SystemAddress.UNASSIGNED_SYSTEM_ADDRESS);
        _started = true;
        _log.LogInformation("raknet transport listening on udp {Bind}:{Port} (guid {Guid})", config.Bind, config.Port, _peer.GetMyGUID());
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_started)
        {
            _started = false;
            _peer.Shutdown(300);
            _peer.Dispose();
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    /// <summary>Drains incoming packets and flushes queued sends. Called once per server tick.</summary>
    public void Poll()
    {
        if (!_started)
        {
            return;
        }
        while (_outbox.TryDequeue(out var item))
        {
            if (!item.Connection.IsOpen)
            {
                continue;
            }
            var payload = item.Payload;
            var framed = new byte[payload.Length + 1];
            framed[0] = (byte)DefaultMessageIDTypes.UserPacketEnum;
            payload.CopyTo(framed, 1);
            _peer.Send(framed,
                item.Reliable ? PacketPriority.HighPriority : PacketPriority.MediumPriority,
                item.Reliable ? PacketReliability.ReliableOrdered : PacketReliability.UnreliableSequenced,
                (byte)(item.Reliable ? 0 : 1),
                AddressOrGUID.From(item.Connection.Guid),
                false);
        }
        var budget = 10000;
        for (var packet = _peer.Receive(); packet is not null && budget-- > 0; packet = _peer.Receive())
        {
            try
            {
                HandlePacket(packet);
            }
            finally
            {
                _peer.DeallocatePacket(packet);
            }
        }
    }

    private void HandlePacket(Packet* packet)
    {
        if (packet->Length == 0)
        {
            return;
        }
        var id = (DefaultMessageIDTypes)packet->Data[0];
        var guid = packet->Guid;
        switch (id)
        {
            case DefaultMessageIDTypes.NewIncomingConnection:
            {
                var conn = new RakConnection(Interlocked.Increment(ref _nextId), guid, packet->SystemAddress.ToString(true), this);
                _connections[guid.G] = conn;
                Connected?.Invoke(conn);
                break;
            }
            case DefaultMessageIDTypes.DisconnectionNotification:
            case DefaultMessageIDTypes.ConnectionLost:
            {
                if (_connections.TryRemove(guid.G, out var conn))
                {
                    conn.MarkClosed();
                    Disconnected?.Invoke(conn, id == DefaultMessageIDTypes.ConnectionLost ? "connection lost" : "closed by peer");
                }
                break;
            }
            case DefaultMessageIDTypes.UserPacketEnum:
            {
                if (_connections.TryGetValue(guid.G, out var conn))
                {
                    var payload = new byte[packet->Length - 1];
                    new ReadOnlySpan<byte>(packet->Data + 1, (int)packet->Length - 1).CopyTo(payload);
                    Received?.Invoke(conn, payload);
                }
                break;
            }
            default:
                break;
        }
    }

    internal void Enqueue(RakConnection conn, byte[] payload, bool reliable) => _outbox.Enqueue((conn, payload, reliable));

    internal void Close(RakConnection conn, string reason)
    {
        if (_connections.TryRemove(conn.Guid.G, out _))
        {
            _peer.CloseConnection(AddressOrGUID.From(conn.Guid), true);
            Disconnected?.Invoke(conn, reason);
        }
    }

    internal sealed class RakConnection : IConnection
    {
        private readonly RakNetTransport _owner;
        private volatile bool _open = true;

        public long Id { get; }
        public RakNetGUID Guid { get; }
        public string RemoteAddress { get; }
        public bool IsOpen => _open;

        public RakConnection(long id, RakNetGUID guid, string address, RakNetTransport owner)
        {
            Id = id;
            Guid = guid;
            RemoteAddress = address;
            _owner = owner;
        }

        public void Send(byte[] payload, bool reliable = true)
        {
            if (_open)
            {
                _owner.Enqueue(this, payload, reliable);
            }
        }

        public void Close(string reason)
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            _owner.Close(this, reason);
        }

        internal void MarkClosed() => _open = false;
    }
}
