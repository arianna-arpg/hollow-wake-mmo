using System.Text.Json;
using System.Text.Json.Nodes;
using Skein.Core.Content;

namespace Skein.Server.Config;

/// <summary>Server settings, from config/server.yaml. Every field has a sensible development default.</summary>
public sealed record ServerConfig
{
    public string Name { get; init; } = "Skein Dev";
    public string Motd { get; init; } = "A quiet corner of the Weftmarches.";
    public int Port { get; init; } = 7777;
    public string Bind { get; init; } = "0.0.0.0";
    public string Transport { get; init; } = "tcp";
    public int TickRate { get; init; } = 20;
    public int SnapshotRate { get; init; } = 10;
    public double InterestRadius { get; init; } = 48;
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

    /// <summary>Resolves relative content and data paths against a root directory (the repository or install root).</summary>
    public ServerConfig Rooted(string root) => this with
    {
        ContentLayers = ContentLayers.Select(l => Path.IsPathRooted(l) ? l : Path.GetFullPath(Path.Combine(root, l))).ToList(),
        DataDir = Path.IsPathRooted(DataDir) ? DataDir : Path.GetFullPath(Path.Combine(root, DataDir)),
    };
}
