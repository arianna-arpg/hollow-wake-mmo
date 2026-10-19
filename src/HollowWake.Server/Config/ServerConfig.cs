using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;

namespace HollowWake.Server.Config;

/// <summary>Server settings, from config/server.yaml. Every field has a sensible development default.</summary>
public sealed record ServerConfig
{
    public string Name { get; init; } = "Hollow Wake MMO Dev";
    public string Motd { get; init; } = "A quiet corner of the Weftmarches.";
    public int Port { get; init; } = 7777;
    public string Bind { get; init; } = "0.0.0.0";
    public string Transport { get; init; } = "tcp";
    /// <summary>Accept browser clients over WebSocket beside the main transport (pure host), and serve the web client.</summary>
    public bool WebsocketEnabled { get; init; } = true;
    public int WebsocketPort { get; init; } = 7778;
    /// <summary>The built web client served over HTTP on the WebSocket port. Empty serves only the socket.</summary>
    public string WebRoot { get; init; } = "web/dist";
    /// <summary>How far from the player's chunk, in chunks, a client may request terrain.</summary>
    public int ChunkRequestRadius { get; init; } = 8;
    public int TickRate { get; init; } = 20;
    public int SnapshotRate { get; init; } = 10;
    public double InterestRadius { get; init; } = 48;
    /// <summary>Snapshot precision and redundancy, given to every session's view; see <see cref="SnapshotSettings"/>.</summary>
    public SnapshotSettings Snapshots { get; init; } = SnapshotSettings.Default;
    public CastForecastSettings CastForecasts { get; init; } = new();
    public List<string> ContentLayers { get; init; } = new() { "content/core" };
    public bool StrictContent { get; init; } = true;
    public string DataDir { get; init; } = "data";
    public string? Seed { get; init; }
    /// <summary>dev: any account name logs in and is created on first use. token: a shared secret in Token.</summary>
    public string Auth { get; init; } = "dev";
    public string? Token { get; init; }
    public int MaxPlayers { get; init; } = 64;
    public double AutosaveSeconds { get; init; } = 60;
    public double ZoneUnloadSeconds { get; init; } = 300;
    public bool WorldEvents { get; init; } = true;
    public double WorldEventIntervalSeconds { get; init; } = 900;
    public int MaxActiveWorldEvents { get; init; } = 3;
    public bool LogIntents { get; init; }

    public static ServerConfig Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return new ServerConfig();
        }
        var text = File.ReadAllText(path);
        var docs = YamlToJson.ParseDocuments(text);
        if (docs.Count == 0 || docs[0] is not JsonObject obj)
        {
            return new ServerConfig();
        }
        return obj.Deserialize<ServerConfig>(ContentJson.Options) ?? new ServerConfig();
    }

    /// <summary>Resolves relative content, data and web paths against a root directory (the repository or install root).</summary>
    public ServerConfig Rooted(string root) => this with
    {
        ContentLayers = ContentLayers.Select(l => Path.IsPathRooted(l) ? l : Path.GetFullPath(Path.Combine(root, l))).ToList(),
        DataDir = Path.IsPathRooted(DataDir) ? DataDir : Path.GetFullPath(Path.Combine(root, DataDir)),
        WebRoot = string.IsNullOrWhiteSpace(WebRoot) || Path.IsPathRooted(WebRoot) ? WebRoot : Path.GetFullPath(Path.Combine(root, WebRoot)),
    };
}
