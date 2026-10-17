using Microsoft.Extensions.Logging;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Host.Brutal;

// hollowwake-server-brutal: the BRUTAL host. RakNet UDP transport; the same GameServer underneath.
//   hollowwake-server-brutal [--config config/server.yaml] [--root <repo or install dir>]
// BRUTAL is Copyright (c) 2024 Ahwoo Limited, used under its licence (see docs/adr/0003-brutal-on-the-server.md).

var configPath = ArgValue(args, "--config") ?? "config/server.yaml";
var root = ArgValue(args, "--root") ?? FindRoot();
var config = ServerConfig.Load(Path.IsPathRooted(configPath) ? configPath : Path.Combine(root, configPath)).Rooted(root) with { Transport = "raknet" };
if (ArgValue(args, "--port") is { } portText && int.TryParse(portText, out var port))
{
    config = config with { Port = port };
}

using var logs = LoggerFactory.Create(b => b
    .SetMinimumLevel(Enum.TryParse<LogLevel>(ArgValue(args, "--log") ?? "Information", true, out var level) ? level : LogLevel.Information)
    .AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    }));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

var transport = new RakNetTransport(logs.CreateLogger<RakNetTransport>());
await using var server = new GameServer(config, logs, transport);
await server.RunAsync(cts.Token);
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
