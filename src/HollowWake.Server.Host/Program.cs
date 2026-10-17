using Microsoft.Extensions.Logging;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;

// hollowwake-server: the pure .NET host. TCP for the desktop client and, unless websocket_enabled is
// false, WebSocket plus the built web client for browsers. No native dependencies.
//   hollowwake-server [--config config/server.yaml] [--root <repo or install dir>] [--port n]
//                     [--ws-port n] [--web-root dir] [--no-websocket] [--log level]

var configPath = ArgValue(args, "--config") ?? "config/server.yaml";
var root = ArgValue(args, "--root") ?? FindRoot();
var config = ServerConfig.Load(Path.IsPathRooted(configPath) ? configPath : Path.Combine(root, configPath)).Rooted(root);
if (ArgValue(args, "--port") is { } portText && int.TryParse(portText, out var port))
{
    config = config with { Port = port };
}
if (ArgValue(args, "--ws-port") is { } wsPortText && int.TryParse(wsPortText, out var wsPort))
{
    config = config with { WebsocketPort = wsPort };
}
if (ArgValue(args, "--web-root") is { } webRoot)
{
    config = config with { WebRoot = Path.GetFullPath(webRoot) };
}
if (ArgValue(args, "--bind") is { } bind)
{
    config = config with { Bind = bind };
}
if (args.Contains("--no-websocket"))
{
    config = config with { WebsocketEnabled = false };
}
if (args.Contains("--websocket"))
{
    config = config with { WebsocketEnabled = true };
}

using var logs = LoggerFactory.Create(b => b
    .SetMinimumLevel(Enum.TryParse<LogLevel>(ArgValue(args, "--log") ?? "Information", true, out var level) ? level : LogLevel.Information)
    .AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    }));

using var cts = new CancellationTokenSource();
void RequestStop()
{
    // ProcessExit may already be in flight while normal shutdown unhooks the handler.
    try { if (!cts.IsCancellationRequested) cts.Cancel(); }
    catch (ObjectDisposedException) { }
}
ConsoleCancelEventHandler interrupt = (_, e) =>
{
    e.Cancel = true;
    RequestStop();
};
EventHandler exiting = (_, _) => RequestStop();
Console.CancelKeyPress += interrupt;
AppDomain.CurrentDomain.ProcessExit += exiting;
Task leaseMonitor = Task.CompletedTask;

// A local playtest owns a unique lease file. Removing it requests the usual save and shutdown;
// no PID killing, console window or game-protocol control endpoint is needed.
if (ArgValue(args, "--lifetime-file") is { } lifetimeFile)
{
    leaseMonitor = Task.Run(async () =>
    {
        try
        {
            while (!cts.IsCancellationRequested && File.Exists(lifetimeFile))
            {
                try { File.SetLastWriteTimeUtc(lifetimeFile, DateTime.UtcNow); }
                catch (IOException) { break; }
                await Task.Delay(250, cts.Token);
            }
            RequestStop();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    });
}

try
{
    var tcp = new TcpTransport(logs.CreateLogger<TcpTransport>());
    ITransport transport = config.WebsocketEnabled
        ? new CompositeTransport(tcp, new WebSocketTransport(logs.CreateLogger<WebSocketTransport>()))
        : tcp;
    await using var server = new GameServer(config, logs, transport);
    await server.RunAsync(cts.Token);
}
finally
{
    Console.CancelKeyPress -= interrupt;
    AppDomain.CurrentDomain.ProcessExit -= exiting;
    await cts.CancelAsync();
    await leaseMonitor;
}
return 0;

static string? ArgValue(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string FindRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8 && dir is not null; i++)
    {
        if (Directory.Exists(Path.Combine(dir, "content", "core")))
        {
            return dir;
        }
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}
