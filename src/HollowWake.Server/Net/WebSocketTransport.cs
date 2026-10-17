using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using HollowWake.Protocol;
using HollowWake.Server.Config;

namespace HollowWake.Server.Net;

/// <summary>
/// WebSocket transport for browser clients, on <see cref="HttpListener"/>. Upgrades at <see cref="SocketPath"/>
/// carry one whole message per binary frame with no length prefix (the WebSocket frame is the frame);
/// text frames and messages over <see cref="MessageSerializer.MaxMessageBytes"/> close the connection.
/// Every other path serves the built web client from the configured web root, so one port gives a
/// browser both the page and the game. Zero native dependencies, like the TCP transport.
/// </summary>
public sealed class WebSocketTransport : ITransport
{
    public const string SocketPath = "/ws";

    /// <summary>What a plain GET on the socket path names itself, so a page can recognise a game server.</summary>
    public const string Service = "hollow-wake";

    /// <summary>Unreliable sends (entity updates) are dropped once this many messages wait for a slow client.</summary>
    private const int UnreliableBacklog = 128;

    private readonly ILogger _log;
    private readonly ConcurrentDictionary<long, Task> _running = new();
    private HttpListener? _listener;
    private WebRoot? _site;
    private string _describe = "{}";
    private CancellationTokenSource? _acceptCts;
    private CancellationTokenSource? _connectionCts;
    private Task? _acceptLoop;
    private long _nextId;

    public string Name => "websocket";

    /// <summary>The prefix the listener ended up on, for logs and tests.</summary>
    public string? ListeningOn { get; private set; }

    public event Action<IConnection>? Connected;
    public event Action<IConnection, byte[]>? Received;
    public event Action<IConnection, string>? Disconnected;

    public WebSocketTransport(ILogger<WebSocketTransport> log)
    {
        _log = log;
    }

    public Task StartAsync(ServerConfig config, CancellationToken cancellation)
    {
        _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _site = string.IsNullOrWhiteSpace(config.WebRoot) ? null : new WebRoot(config.WebRoot);
        _describe = JsonSerializer.Serialize(new { service = Service, protocol = ProtocolInfo.Version, socket = SocketPath, name = config.Name });
        _listener = Listen(config.Bind, config.WebsocketPort);
        if (_site is null)
        {
            _log.LogInformation("websocket transport listening on {Prefix} (socket at {Path}, no web root)", ListeningOn, SocketPath);
        }
        else if (_site.Exists)
        {
            _log.LogInformation("websocket transport listening on {Prefix} (socket at {Path}, web client from {Root})", ListeningOn, SocketPath, _site.Directory);
        }
        else
        {
            _log.LogWarning("websocket transport listening on {Prefix} (socket at {Path}); web root {Root} does not exist, build the web client with: npm --prefix web ci && npm --prefix web run build", ListeningOn, SocketPath, _site.Directory);
        }
        _acceptLoop = Task.Run(() => AcceptLoop(_acceptCts.Token));
        return Task.CompletedTask;
    }

    private HttpListener Listen(string bind, int port)
    {
        var wildcard = bind is "" or "0.0.0.0" or "::" or "*" or "+";
        var host = wildcard ? "+" : bind.Contains(':') && !bind.StartsWith('[') ? $"[{bind}]" : bind;
        try
        {
            return Start($"http://{host}:{port}/");
        }
        catch (HttpListenerException ex) when (OperatingSystem.IsWindows() && host != "localhost")
        {
            // http.sys reserves the wildcard prefix for administrators unless a URL ACL grants it; loopback
            // prefixes are open to every user, which is all local development needs.
            _log.LogWarning("websocket transport cannot listen on {Host}:{Port} ({Error}); falling back to this machine only. To accept other machines on Windows, run once as administrator: netsh http add urlacl url=http://+:{Port}/ user=Everyone", host, port, ex.Message, port);
            try
            {
                return Start($"http://localhost:{port}/", $"http://127.0.0.1:{port}/");
            }
            catch (HttpListenerException)
            {
                return Start($"http://localhost:{port}/");
            }
        }
    }

    private HttpListener Start(params string[] prefixes)
    {
        var listener = new HttpListener { IgnoreWriteExceptions = true };
        try
        {
            foreach (var prefix in prefixes)
            {
                listener.Prefixes.Add(prefix);
            }
            listener.Start();
        }
        catch
        {
            listener.Close();
            throw;
        }
        ListeningOn = string.Join(" ", prefixes);
        return listener;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true } listener)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception) when (ct.IsCancellationRequested || !listener.IsListening)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "accept failed");
                continue;
            }
            _ = Task.Run(() => HandleAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (string.Equals(path, SocketPath, StringComparison.Ordinal))
            {
                if (!context.Request.IsWebSocketRequest)
                {
                    // A plain request says what is here, so a page this server serves can find the game on its own origin.
                    await RespondAsync(context, 200, _describe, "application/json; charset=utf-8");
                    return;
                }
                await AcceptSocketAsync(context);
                return;
            }
            await ServeFileAsync(context);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "http request failed");
            try
            {
                context.Response.Abort();
            }
            catch
            {
                // already gone
            }
        }
    }

    private async Task AcceptSocketAsync(HttpListenerContext context)
    {
        var ct = _connectionCts?.Token ?? CancellationToken.None;
        if (ct.IsCancellationRequested)
        {
            await RespondAsync(context, 503, "Server stopping.");
            return;
        }
        HttpListenerWebSocketContext accepted;
        try
        {
            accepted = await context.AcceptWebSocketAsync(subProtocol: null, receiveBufferSize: 16 * 1024, keepAliveInterval: TimeSpan.FromSeconds(20));
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "websocket upgrade failed");
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }
        var remote = context.Request.RemoteEndPoint?.ToString() ?? "?";
        var conn = new WebSocketConnection(Interlocked.Increment(ref _nextId), remote, accepted.WebSocket, this);
        var run = new TaskCompletionSource();
        _running[conn.Id] = run.Task;
        try
        {
            Connected?.Invoke(conn);
            await conn.RunAsync(ct);
        }
        finally
        {
            _running.TryRemove(conn.Id, out _);
            run.TrySetResult();
        }
    }

    private async Task ServeFileAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        if (request.HttpMethod is not ("GET" or "HEAD"))
        {
            response.AddHeader("Allow", "GET, HEAD");
            await RespondAsync(context, 405, "Method not allowed.");
            return;
        }
        if (_site is null || !_site.Exists)
        {
            await RespondAsync(context, 404, $"Hollow Wake MMO server. The web client is not built here; build it with `npm --prefix web ci && npm --prefix web run build` or point web_root at a build. Game socket: {SocketPath}");
            return;
        }
        var file = _site.Resolve(request.Url?.AbsolutePath ?? "/");
        if (file is null)
        {
            await RespondAsync(context, 404, "Not found.");
            return;
        }
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, useAsync: true);
        response.StatusCode = 200;
        response.ContentType = WebRoot.ContentType(file);
        response.ContentLength64 = stream.Length;
        response.AddHeader("Cache-Control", _site.CacheControl(file));
        response.AddHeader("X-Content-Type-Options", "nosniff");
        if (request.HttpMethod == "GET")
        {
            await stream.CopyToAsync(response.OutputStream);
        }
        response.Close();
    }

    private static async Task RespondAsync(HttpListenerContext context, int status, string text, string contentType = "text/plain; charset=utf-8")
    {
        var body = Encoding.UTF8.GetBytes(text + "\n");
        var response = context.Response;
        response.StatusCode = status;
        response.ContentType = contentType;
        response.AddHeader("Cache-Control", "no-store");
        response.ContentLength64 = body.Length;
        response.AddHeader("X-Content-Type-Options", "nosniff");
        if (context.Request.HttpMethod != "HEAD")
        {
            await response.OutputStream.WriteAsync(body);
        }
        response.Close();
    }

    public async Task StopAsync()
    {
        _acceptCts?.Cancel();
        // Sessions were told to close before the transport stops; give their goodbyes and close frames a moment.
        await WaitForConnections(TimeSpan.FromSeconds(2));
        _connectionCts?.Cancel();
        await WaitForConnections(TimeSpan.FromSeconds(1));
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
        try
        {
            _listener?.Close();
        }
        catch
        {
            // already closed
        }
        _listener = null;
    }

    private async Task WaitForConnections(TimeSpan limit)
    {
        var running = _running.Values.ToArray();
        if (running.Length > 0)
        {
            await Task.WhenAny(Task.WhenAll(running), Task.Delay(limit));
        }
    }

    public void Poll()
    {
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    internal void RaiseReceived(IConnection c, byte[] payload) => Received?.Invoke(c, payload);
    internal void RaiseDisconnected(IConnection c, string reason) => Disconnected?.Invoke(c, reason);

    private sealed class WebSocketConnection : IConnection
    {
        private readonly WebSocket _socket;
        private readonly WebSocketTransport _owner;
        private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        private readonly CancellationTokenSource _readCts = new();
        private WebSocketCloseStatus _closeStatus = WebSocketCloseStatus.NormalClosure;
        private string _closeDescription = "";
        private int _closed;
        private int _queued;

        public long Id { get; }
        public string RemoteAddress { get; }
        public bool IsOpen => _closed == 0 && _socket.State == WebSocketState.Open;

        public WebSocketConnection(long id, string remoteAddress, WebSocket socket, WebSocketTransport owner)
        {
            Id = id;
            RemoteAddress = remoteAddress;
            _socket = socket;
            _owner = owner;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            using var read = CancellationTokenSource.CreateLinkedTokenSource(ct, _readCts.Token);
            var writer = Task.Run(() => WriteLoop(ct), CancellationToken.None);
            await ReadLoop(read.Token);
            Close("closed by peer", WebSocketCloseStatus.NormalClosure, "");
            // Let the writer flush what is queued and answer with a close frame, but never wait on a dead peer.
            await Task.WhenAny(writer, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None));
            _socket.Abort();
            _socket.Dispose();
            _readCts.Dispose();
        }

        private async Task ReadLoop(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var message = new byte[16 * 1024];
            var length = 0;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(buffer.AsMemory(), ct);
                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            Close("closed by peer", WebSocketCloseStatus.NormalClosure, "");
                            return;
                        case WebSocketMessageType.Text:
                            Close("sent a text frame", WebSocketCloseStatus.InvalidMessageType, "binary frames only");
                            return;
                    }
                    if (length + result.Count > MessageSerializer.MaxMessageBytes)
                    {
                        Close("sent an oversize message", WebSocketCloseStatus.MessageTooBig, "message too big");
                        return;
                    }
                    if (length + result.Count > message.Length)
                    {
                        Array.Resize(ref message, Math.Min(MessageSerializer.MaxMessageBytes, Math.Max(message.Length * 2, length + result.Count)));
                    }
                    buffer.AsSpan(0, result.Count).CopyTo(message.AsSpan(length));
                    length += result.Count;
                    if (result.EndOfMessage)
                    {
                        _owner.RaiseReceived(this, message.AsSpan(0, length).ToArray());
                        length = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Close("server stopping", WebSocketCloseStatus.EndpointUnavailable, "server stopping");
            }
            catch (Exception ex)
            {
                Close("read failed: " + ex.GetType().Name, WebSocketCloseStatus.ProtocolError, "");
            }
        }

        private async Task WriteLoop(CancellationToken ct)
        {
            try
            {
                await foreach (var payload in _outbox.Reader.ReadAllAsync(ct))
                {
                    Interlocked.Decrement(ref _queued);
                    await _socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct);
                }
                // The outbox completes when the connection closes: say goodbye properly, then stop waiting for the peer.
                if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await _socket.CloseOutputAsync(_closeStatus, _closeDescription, timeout.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Close("write failed: " + ex.GetType().Name, WebSocketCloseStatus.ProtocolError, "");
            }
            finally
            {
                try
                {
                    _readCts.CancelAfter(TimeSpan.FromSeconds(2));
                }
                catch (ObjectDisposedException)
                {
                    // the read side already finished
                }
            }
        }

        public void Send(byte[] payload, bool reliable = true)
        {
            if (_closed != 0)
            {
                return;
            }
            // A client that cannot keep up loses stale entity updates first; the next snapshot carries full states.
            if (!reliable && Volatile.Read(ref _queued) > UnreliableBacklog)
            {
                return;
            }
            if (_outbox.Writer.TryWrite(payload))
            {
                Interlocked.Increment(ref _queued);
            }
        }

        public void Close(string reason) => Close(reason, WebSocketCloseStatus.NormalClosure, reason);

        private void Close(string reason, WebSocketCloseStatus status, string description)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }
            _closeStatus = status;
            // A close reason is limited to 123 bytes of UTF-8.
            _closeDescription = description.Length > 100 ? description[..100] : description;
            _outbox.Writer.TryComplete();
            _owner.RaiseDisconnected(this, reason);
        }
    }
}
