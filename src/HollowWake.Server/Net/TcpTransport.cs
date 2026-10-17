using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using HollowWake.Protocol;
using HollowWake.Server.Config;

namespace HollowWake.Server.Net;

/// <summary>
/// Plain TCP with length-prefixed frames. Reliable and ordered by nature, zero native dependencies,
/// fine for a handful of players and for every development machine. UDP transports exist for scale.
/// </summary>
public sealed class TcpTransport : ITransport
{
    private readonly ILogger _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private long _nextId = 1;

    public string Name => "tcp";

    public event Action<IConnection>? Connected;
    public event Action<IConnection, byte[]>? Received;
    public event Action<IConnection, string>? Disconnected;

    public TcpTransport(ILogger<TcpTransport> log)
    {
        _log = log;
    }

    public Task StartAsync(ServerConfig config, CancellationToken cancellation)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _listener = new TcpListener(IPAddress.Parse(config.Bind == "0.0.0.0" ? "0.0.0.0" : config.Bind), config.Port);
        _listener.Start();
        _log.LogInformation("tcp transport listening on {Bind}:{Port}", config.Bind, config.Port);
        _acceptLoop = Task.Run(() => AcceptLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "accept failed");
                continue;
            }
            client.NoDelay = true;
            var conn = new TcpConnection(Interlocked.Increment(ref _nextId), client, this, _log);
            Connected?.Invoke(conn);
            conn.Start(ct);
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch
            {
                // shutting down
            }
        }
    }

    public void Poll()
    {
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    internal void RaiseReceived(IConnection c, byte[] payload) => Received?.Invoke(c, payload);
    internal void RaiseDisconnected(IConnection c, string reason) => Disconnected?.Invoke(c, reason);

    private sealed class TcpConnection : IConnection
    {
        private readonly TcpClient _client;
        private readonly TcpTransport _owner;
        private readonly ILogger _log;
        private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        private int _closed;

        public long Id { get; }
        public string RemoteAddress { get; }
        public bool IsOpen => _closed == 0 && _client.Connected;

        public TcpConnection(long id, TcpClient client, TcpTransport owner, ILogger log)
        {
            Id = id;
            _client = client;
            _owner = owner;
            _log = log;
            RemoteAddress = client.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        public void Start(CancellationToken ct)
        {
            _ = Task.Run(() => ReadLoop(ct), ct);
            _ = Task.Run(() => WriteLoop(ct), ct);
        }

        private async Task ReadLoop(CancellationToken ct)
        {
            var reader = new MessageSerializer.FrameReader();
            var buffer = new byte[16 * 1024];
            try
            {
                var stream = _client.GetStream();
                while (!ct.IsCancellationRequested)
                {
                    var n = await stream.ReadAsync(buffer, ct);
                    if (n <= 0)
                    {
                        Close("closed by peer");
                        return;
                    }
                    reader.Append(buffer.AsSpan(0, n));
                    while (reader.TryRead(out var payload))
                    {
                        _owner.RaiseReceived(this, payload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Close("server stopping");
            }
            catch (Exception ex)
            {
                Close("read failed: " + ex.GetType().Name);
            }
        }

        private async Task WriteLoop(CancellationToken ct)
        {
            try
            {
                var stream = _client.GetStream();
                await foreach (var payload in _outbox.Reader.ReadAllAsync(ct))
                {
                    await stream.WriteAsync(MessageSerializer.Frame(payload), ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Close("write failed: " + ex.GetType().Name);
            }
        }

        public void Send(byte[] payload, bool reliable = true)
        {
            if (_closed == 0)
            {
                _outbox.Writer.TryWrite(payload);
            }
        }

        public void Close(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }
            _outbox.Writer.TryComplete();
            try
            {
                _client.Close();
            }
            catch
            {
                // already closed
            }
            _owner.RaiseDisconnected(this, reason);
        }
    }
}
