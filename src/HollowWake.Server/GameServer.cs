using System.Diagnostics;
using Microsoft.Extensions.Logging;
using HollowWake.Core.Content;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.Persistence;
using HollowWake.Server.Sessions;
using HollowWake.Server.World;

namespace HollowWake.Server;

/// <summary>
/// The composition root and main loop. One world thread ticks every zone, drains the transport
/// inbox and saves on a timer. Hosts (pure .NET or BRUTAL) construct it with their transport and
/// either call <see cref="RunAsync"/> or step it themselves.
/// </summary>
public sealed class GameServer : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly ITransport _transport;
    private readonly TransportInbox _inbox = new();
    private double _sinceSave;

    public ServerConfig Config { get; }
    public ContentRegistry Content { get; }
    public ZoneHost Zones { get; }
    public SessionManager Sessions { get; }
    public ILoggerFactory Logs { get; }
    public long Ticks { get; private set; }
    public double TickSeconds => 1.0 / Config.TickRate;

    public GameServer(ServerConfig config, ILoggerFactory logs, ITransport transport, IAccountStore? accounts = null, ICharacterStore? characters = null, IWorldStore? world = null)
    {
        Config = config;
        Logs = logs;
        _log = logs.CreateLogger<GameServer>();
        _transport = transport;

        Content = ContentRegistry.Load(config.ContentLayers, out var report, strict: false);
        foreach (var issue in report.Issues)
        {
            switch (issue.Severity)
            {
                case Severity.Error:
                    _log.LogError("content: {Issue}", issue);
                    break;
                case Severity.Warning:
                    _log.LogWarning("content: {Issue}", issue);
                    break;
            }
        }
        if (report.HasErrors && config.StrictContent)
        {
            throw new ContentLoadException(report);
        }
        _log.LogInformation("content loaded: {Kinds} kinds from {Layers}", Content.KindNames.Count(), string.Join(", ", config.ContentLayers));

        var stores = accounts is null || characters is null || world is null ? new JsonFileStores(config.DataDir) : null;
        Zones = new ZoneHost(config, Content, logs, world ?? stores!, characters ?? stores!);
        Sessions = new SessionManager(config, logs.CreateLogger<SessionManager>(), Zones, accounts ?? stores!, characters ?? stores!);
        _inbox.Attach(transport);
    }

    public async Task StartAsync(CancellationToken cancellation)
    {
        await _transport.StartAsync(Config, cancellation);
        Zones.GetOrCreate(Zones.Generator.StartZone());
        _log.LogInformation("{Name} is up: transport {Transport}, tick {Tick} Hz, data in {Data}", Config.Name, _transport.Name, Config.TickRate, Config.DataDir);
    }

    /// <summary>One server tick: network in, simulation, network out, autosave.</summary>
    public void Step()
    {
        _transport.Poll();
        var handled = 0;
        while (_inbox.TryDequeue(out var evt) && handled++ < 10000)
        {
            Sessions.Handle(evt);
        }
        Zones.TickAll(TickSeconds);
        Ticks++;
        _sinceSave += TickSeconds;
        if (_sinceSave >= Config.AutosaveSeconds)
        {
            _sinceSave = 0;
            Save();
        }
    }

    public void Save()
    {
        try
        {
            Zones.SaveAll();
            _log.LogDebug("saved");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "save failed");
        }
    }

    /// <summary>Runs the fixed-step loop until cancelled, then saves and shuts the transport.</summary>
    public async Task RunAsync(CancellationToken cancellation)
    {
        await StartAsync(cancellation);
        var stopwatch = Stopwatch.StartNew();
        var next = 0.0;
        var lagWarnings = 0;
        while (!cancellation.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed.TotalSeconds;
            if (now >= next)
            {
                var start = stopwatch.Elapsed.TotalSeconds;
                Step();
                var took = stopwatch.Elapsed.TotalSeconds - start;
                if (took > TickSeconds && ++lagWarnings % 50 == 1)
                {
                    _log.LogWarning("tick took {Took:0.0} ms (budget {Budget:0.0} ms)", took * 1000, TickSeconds * 1000);
                }
                next += TickSeconds;
                if (now - next > 1.0)
                {
                    // Fell far behind (debugger, sleep): do not try to catch up.
                    next = now;
                }
                continue;
            }
            var wait = next - now;
            if (wait > 0.002)
            {
                await Task.Delay(TimeSpan.FromSeconds(wait - 0.001), CancellationToken.None);
            }
        }
        await ShutdownAsync();
    }

    public async Task ShutdownAsync()
    {
        _log.LogInformation("shutting down");
        Sessions.DisconnectAll("server shutting down");
        Save();
        await _transport.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
    }
}
