using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>A browser-shaped client: one message per binary frame, decoded as it arrives.</summary>
internal sealed class WsTestClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly Channel<Message> _inbox = Channel.CreateUnbounded<Message>();
    private Task? _reader;

    public List<Message> Seen { get; } = new();
    public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;
    public WebSocketState State => _socket.State;

    public async Task ConnectAsync(Uri uri)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _socket.ConnectAsync(uri, timeout.Token);
        _reader = Task.Run(ReadLoop);
    }

    public Task SendAsync(Message message) => SendRawAsync(MessageSerializer.Encode(message), WebSocketMessageType.Binary, true);

    public async Task SendRawAsync(byte[] payload, WebSocketMessageType type, bool endOfMessage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _socket.SendAsync(payload, type, endOfMessage, timeout.Token);
    }

    /// <summary>Reads until a message of type T (matching the predicate) arrives; everything read is kept in <see cref="Seen"/>.</summary>
    public async Task<T> WaitFor<T>(Func<T, bool>? match = null, double seconds = 15) where T : Message
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        try
        {
            while (true)
            {
                var message = await _inbox.Reader.ReadAsync(timeout.Token);
                Seen.Add(message);
                if (message is T typed && (match is null || match(typed)))
                {
                    return typed;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"no {typeof(T).Name} within {seconds}s; saw {string.Join(", ", Seen.Select(m => m.GetType().Name).Distinct())}");
        }
        catch (ChannelClosedException)
        {
            throw new InvalidOperationException($"socket closed ({CloseStatus}) before a {typeof(T).Name} arrived");
        }
    }

    /// <summary>Waits for the server to close the socket and returns the close status it gave.</summary>
    public async Task<WebSocketCloseStatus?> WaitForClose(double seconds = 10)
    {
        if (_reader is not null)
        {
            await _reader.WaitAsync(TimeSpan.FromSeconds(seconds));
        }
        return _socket.CloseStatus;
    }

    private async Task ReadLoop()
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    if (MessageSerializer.Decode(message.ToArray()) is { } decoded)
                    {
                        _inbox.Writer.TryWrite(decoded);
                    }
                    message.SetLength(0);
                }
            }
        }
        catch (WebSocketException)
        {
            // the server went away
        }
        finally
        {
            _inbox.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
            }
        }
        catch
        {
            // closing anyway
        }
        _socket.Dispose();
    }
}

public class WebSocketTests
{
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static string ContentDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "content", "core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("content/core");
    }

    /// <summary>A real server loop on its own task with a WebSocket transport (optionally beside others) on a free port.</summary>
    private sealed class Running : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public GameServer Server { get; }
        public int Port { get; }
        public Uri Socket => new($"ws://localhost:{Port}{WebSocketTransport.SocketPath}");
        public Uri Http => new($"http://localhost:{Port}/");

        private Running(GameServer server, int port)
        {
            Server = server;
            Port = port;
            _loop = Task.Run(() => server.RunAsync(_cts.Token));
        }

        public static async Task<Running> StartAsync(string? webRoot = null, params ITransport[] others)
        {
            var port = FreePort();
            var config = new ServerConfig
            {
                ContentLayers = new List<string> { ContentDir() },
                WorldEvents = false,
                AutosaveSeconds = 1e9,
                Bind = "localhost",
                WebsocketPort = port,
                WebRoot = webRoot ?? "",
            };
            var ws = new WebSocketTransport(NullLogger<WebSocketTransport>.Instance);
            ITransport transport = others.Length == 0 ? ws : new CompositeTransport(others.Append(ws).ToArray());
            var stores = new MemoryStores();
            var server = new GameServer(config, NullLoggerFactory.Instance, transport, stores, stores, stores);
            var running = new Running(server, port);
            // The listener is up once the start zone has loaded and the server reports itself running.
            using var http = new HttpClient();
            for (var i = 0; i < 200; i++)
            {
                try
                {
                    using var response = await http.GetAsync(running.Http);
                    return running;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }
            throw new TimeoutException("websocket transport did not start");
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _loop.WaitAsync(TimeSpan.FromSeconds(15));
            await Server.DisposeAsync();
        }
    }

    [Fact]
    public async Task BrowserShapedClientLogsInEntersMovesAndSeesUpdates()
    {
        await using var running = await Running.StartAsync();
        await using var client = new WsTestClient();
        await client.ConnectAsync(running.Socket);

        await client.SendAsync(new HelloMsg("ws-test", ProtocolInfo.Version));
        var welcome = await client.WaitFor<WelcomeMsg>();
        Assert.Equal(ProtocolInfo.Version, welcome.Protocol);

        await client.SendAsync(new LoginMsg("webtester", ""));
        var login = await client.WaitFor<LoginResultMsg>();
        Assert.True(login.Ok, login.Error);
        var list = await client.WaitFor<CharacterListMsg>();
        Assert.Empty(list.Characters);
        Assert.Contains("kindler", list.Callings);

        await client.SendAsync(new CreateCharacterMsg("Wren", "kindler"));
        list = await client.WaitFor<CharacterListMsg>(l => l.Characters.Count == 1);
        Assert.Equal("Wren", list.Characters[0].Name);

        await client.SendAsync(new SelectCharacterMsg(list.Characters[0].Id));
        var enter = await client.WaitFor<EnterWorldMsg>();
        await client.WaitFor<EntitySpawnMsg>(s => s.State.Key == enter.EntityKey && s.State.IsPlayer);
        await client.WaitFor<SelfStateMsg>(s => s.Weaves.Count > 0);

        // The browser builds terrain from the server's chunks, so the chunk under the player must come back whole.
        var cx = (int)Math.Floor(enter.X / enter.World.ChunkSize);
        var cy = (int)Math.Floor(enter.Y / enter.World.ChunkSize);
        await client.SendAsync(new RequestChunkMsg(cx, cy));
        var chunk = await client.WaitFor<ChunkDataMsg>(c => c.X == cx && c.Y == cy);
        Assert.Equal(chunk.Size * chunk.Size, chunk.Height.Length);
        Assert.Equal(chunk.Size * chunk.Size, chunk.SurfaceTag.Length);
        Assert.Equal(chunk.Size * chunk.Size, chunk.Flags.Length);
        Assert.NotNull(chunk.WaterLevel);
        Assert.All(chunk.SurfaceTag, i => Assert.InRange(i, 0, chunk.Tags.Count - 1));

        await client.SendAsync(new RequestChunkMsg(cx + 1000, cy));
        var far = await client.WaitFor<ErrorMsg>();
        Assert.Equal("chunk_out_of_range", far.Code);

        await client.SendAsync(new MoveDirMsg(1, 0));
        var moved = await client.WaitFor<EntityUpdateMsg>(u => u.States.Any(s => s.Key == enter.EntityKey && s.X > enter.X + 1));
        Assert.True(moved.ServerTime > enter.ServerTime);

        await client.SendAsync(new JumpMsg());
        await client.WaitFor<EntityUpdateMsg>(u => u.States.Any(s => s.Key == enter.EntityKey && !s.Grounded));

        await client.SendAsync(new PingMsg(42));
        Assert.Equal(42L, (await client.WaitFor<PongMsg>()).Nonce);

        Assert.Single(running.Server.Sessions.Sessions);
        await client.DisposeAsync();
        for (var i = 0; i < 50 && running.Server.Sessions.Sessions.Count > 0; i++)
        {
            await Task.Delay(100);
        }
        Assert.Empty(running.Server.Sessions.Sessions);
    }

    [Fact]
    public async Task TextFramesAndOversizeMessagesCloseTheConnection()
    {
        await using var running = await Running.StartAsync();

        await using var text = new WsTestClient();
        await text.ConnectAsync(running.Socket);
        await text.SendRawAsync(Encoding.UTF8.GetBytes("{\"t\":\"ping\",\"nonce\":1}"), WebSocketMessageType.Text, true);
        Assert.Equal(WebSocketCloseStatus.InvalidMessageType, await text.WaitForClose());

        await using var big = new WsTestClient();
        await big.ConnectAsync(running.Socket);
        var piece = new byte[256 * 1024];
        try
        {
            for (var sent = 0; sent <= MessageSerializer.MaxMessageBytes; sent += piece.Length)
            {
                await big.SendRawAsync(piece, WebSocketMessageType.Binary, endOfMessage: false);
            }
        }
        catch (WebSocketException)
        {
            // the server may close while the rest is still in flight
        }
        catch (OperationCanceledException)
        {
        }
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, await big.WaitForClose());
    }

    [Fact]
    public async Task ServesTheWebClientBesideTheSocket()
    {
        var root = Directory.CreateTempSubdirectory("hollowwake-web-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "index.html"), "<!doctype html><title>Hollow Wake</title>");
            Directory.CreateDirectory(Path.Combine(root.FullName, "assets"));
            File.WriteAllText(Path.Combine(root.FullName, "assets", "app-1a2b.js"), "console.log('weft');");
            await using var running = await Running.StartAsync(root.FullName);
            using var http = new HttpClient { BaseAddress = running.Http };

            using var index = await http.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Equal("text/html", index.Content.Headers.ContentType?.MediaType);
            Assert.Contains("Hollow Wake", await index.Content.ReadAsStringAsync());

            using var script = await http.GetAsync("/assets/app-1a2b.js");
            Assert.Equal(HttpStatusCode.OK, script.StatusCode);
            Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
            Assert.Contains("immutable", script.Headers.CacheControl?.ToString());

            using var route = await http.GetAsync("/characters/new");
            Assert.Equal(HttpStatusCode.OK, route.StatusCode);
            Assert.Contains("Hollow Wake", await route.Content.ReadAsStringAsync());

            using var missing = await http.GetAsync("/assets/missing.js");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            // A plain GET on the socket path describes the server, which is how a page finds the game on its own origin.
            using var described = await http.GetAsync(WebSocketTransport.SocketPath);
            Assert.Equal(HttpStatusCode.OK, described.StatusCode);
            var about = JsonNode.Parse(await described.Content.ReadAsStringAsync())!;
            Assert.Equal(WebSocketTransport.Service, (string?)about["service"]);
            Assert.Equal(ProtocolInfo.Version, (int)about["protocol"]!);
            Assert.Equal(WebSocketTransport.SocketPath, (string?)about["socket"]);

            using var post = await http.PostAsync("/", new StringContent("x"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WebSocketAndLoopbackPlayersShareOneWorld()
    {
        var loopback = new LoopbackTransport();
        await using var running = await Running.StartAsync(null, loopback);

        var desk = loopback.Connect();
        desk.Send(new HelloMsg("loopback", ProtocolInfo.Version));
        desk.Send(new LoginMsg("deskplayer", ""));
        desk.Send(new CreateCharacterMsg("Tamsin", "holdfast"));
        CharacterSummary? deskCharacter = null;
        for (var i = 0; i < 100 && deskCharacter is null; i++)
        {
            await Task.Delay(50);
            deskCharacter = desk.Drain().OfType<CharacterListMsg>().SelectMany(l => l.Characters).FirstOrDefault();
        }
        Assert.NotNull(deskCharacter);
        desk.Send(new SelectCharacterMsg(deskCharacter!.Id));
        EnterWorldMsg? deskEnter = null;
        for (var i = 0; i < 100 && deskEnter is null; i++)
        {
            await Task.Delay(50);
            deskEnter = desk.Drain().OfType<EnterWorldMsg>().FirstOrDefault();
        }
        Assert.NotNull(deskEnter);

        await using var browser = new WsTestClient();
        await browser.ConnectAsync(running.Socket);
        await browser.SendAsync(new HelloMsg("ws-test", ProtocolInfo.Version));
        await browser.SendAsync(new LoginMsg("browserplayer", ""));
        await browser.SendAsync(new CreateCharacterMsg("Wren", "kindler"));
        var list = await browser.WaitFor<CharacterListMsg>(l => l.Characters.Count == 1);
        await browser.SendAsync(new SelectCharacterMsg(list.Characters[0].Id));
        var enter = await browser.WaitFor<EnterWorldMsg>();

        // Both players stand at the start point, so the browser must see the loopback player arrive.
        await browser.WaitFor<EntitySpawnMsg>(s => s.State.Key == deskEnter!.EntityKey && s.State.IsPlayer);
        Assert.NotEqual(deskEnter!.EntityKey, enter.EntityKey);
        var ids = running.Server.Sessions.Sessions.Select(s => s.Id).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}

public class CompositeTransportTests
{
    [Fact]
    public async Task ConnectionIdsStayUniqueAcrossTransports()
    {
        var a = new LoopbackTransport();
        var b = new LoopbackTransport();
        var composite = new CompositeTransport(a, b);
        Assert.Equal("loopback+loopback", composite.Name);
        var connected = new List<IConnection>();
        var received = new List<(IConnection, byte[])>();
        var closed = new List<IConnection>();
        composite.Connected += connected.Add;
        composite.Received += (c, p) => received.Add((c, p));
        composite.Disconnected += (c, _) => closed.Add(c);
        await composite.StartAsync(new ServerConfig(), CancellationToken.None);

        // Each loopback transport numbers from the same start, so without the composite these would collide.
        var first = a.Connect();
        var second = b.Connect();
        Assert.Equal(2, connected.Count);
        Assert.NotEqual(connected[0].Id, connected[1].Id);
        Assert.StartsWith("loopback", connected[0].RemoteAddress);

        second.Send(new PingMsg(7));
        var (from, payload) = Assert.Single(received);
        Assert.Same(connected[1], from);
        Assert.Equal(7L, Assert.IsType<PingMsg>(MessageSerializer.Decode(payload)).Nonce);

        connected[0].Send(MessageSerializer.Encode(new PongMsg(7, 1)));
        Assert.IsType<PongMsg>(Assert.Single(first.Drain()));

        first.Disconnect();
        Assert.Same(connected[0], Assert.Single(closed));
        await composite.StopAsync();
    }
}

public class WebRootTests
{
    [Fact]
    public void ResolvesFilesRoutesAndNothingOutside()
    {
        var parent = Directory.CreateTempSubdirectory("hollowwake-root-");
        try
        {
            var root = parent.CreateSubdirectory("site");
            File.WriteAllText(Path.Combine(root.FullName, "index.html"), "app");
            File.WriteAllText(Path.Combine(root.FullName, "config.json"), "{}");
            Directory.CreateDirectory(Path.Combine(root.FullName, "assets"));
            File.WriteAllText(Path.Combine(root.FullName, "assets", "a.js"), "");
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            File.WriteAllText(Path.Combine(root.FullName, ".git", "config"), "secret");
            File.WriteAllText(Path.Combine(parent.FullName, "outside.txt"), "secret");
            var site = new WebRoot(root.FullName);
            var index = Path.Combine(site.Directory, "index.html");

            Assert.Equal(index, site.Resolve("/"));
            Assert.Equal(Path.Combine(site.Directory, "config.json"), site.Resolve("/config.json"));
            Assert.Equal(Path.Combine(site.Directory, "assets", "a.js"), site.Resolve("/assets/a.js"));
            Assert.Equal(index, site.Resolve("/play/again"));
            Assert.Null(site.Resolve("/assets/b.js"));
            Assert.Null(site.Resolve("/../outside.txt"));
            Assert.Null(site.Resolve("/%2e%2e/outside.txt"));
            Assert.Null(site.Resolve("/assets/..%2f..%2foutside.txt"));
            Assert.Null(site.Resolve("/.git/config"));
            Assert.Equal("public, max-age=31536000, immutable", site.CacheControl(Path.Combine(site.Directory, "assets", "a.js")));
            Assert.Equal("no-cache", site.CacheControl(index));
            Assert.Equal("text/javascript; charset=utf-8", WebRoot.ContentType("x.js"));
        }
        finally
        {
            parent.Delete(recursive: true);
        }
    }
}
