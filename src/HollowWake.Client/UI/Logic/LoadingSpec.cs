using System.Text.Json;
using System.Text.Json.Nodes;

namespace HollowWake.Client.UI.Logic;

public sealed record LoadingSpec
{
    public string Activity { get; init; } = "";
    public uint Seed { get; init; }
    public uint SeedStep { get; init; }
    public double TimeoutSeconds { get; init; }
    public int ReadyRadius { get; init; }
    public SpiritRules Rules { get; init; } = new();
    public List<GateProfile> GateProfiles { get; init; } = new();
    public Dictionary<string, SpiritStyle> Pickups { get; init; } = new();
    public List<string[]> Choices { get; init; } = new();
    public Dictionary<string, string> Labels { get; init; } = new();
    public Dictionary<string, string> Palette { get; init; } = new();
    public string Runes { get; init; } = "";
    public List<string> Layers { get; init; } = new();
    public WispLook Wisp { get; init; } = new();
    public LoadingControls Controls { get; init; } = new();
    public string Text(string id) => Labels[id];
    public static LoadingSpec FromDocument(string json)
    {
        var entries = JsonNode.Parse(json)!["entries"]!.AsArray();
        return FromMeta(entries.Single(e => e!["default"]!.GetValue<bool>())!["meta"]!);
    }
    public static LoadingSpec FromMeta(JsonNode meta) =>
        meta.Deserialize<LoadingSpec>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}

public sealed record SpiritRules
{
    public double Length { get; init; }
    public double HalfWidth { get; init; }
    public double PlayerU { get; init; }
    public double Radius { get; init; }
    public double BaseSpeed { get; init; }
    public double SpeedPerGate { get; init; }
    public double MaxSpeed { get; init; }
    public double SteerSpeed { get; init; }
    public double GateGapMin { get; init; }
    public double GateGapMax { get; init; }
    public double GateRim { get; init; }
    public double GateThickness { get; init; }
    public double GateSpacing { get; init; }
    public double ChoiceStagger { get; init; }
    public double PickupRadius { get; init; }
    public double PickupFade { get; init; }
    public double CurrentHalfLength { get; init; }
    public double CurrentHalfWidth { get; init; }
    public double CurrentExtra { get; init; }
    public double CurrentSeconds { get; init; }
    public double CurrentEase { get; init; }
    public double MaxBoostSpeed { get; init; }
    public double BurstSeconds { get; init; }
    public int MaxBursts { get; init; }
    public double SurgeSeconds { get; init; }
    public double HinderSeconds { get; init; }
    public double HinderSpeed { get; init; }
    public double EntrySeconds { get; init; }
    public double SpawnAhead { get; init; }
    public double DespawnBehind { get; init; }
    public double OpeningCurrentChance { get; init; }
    public double BetweenCurrentThreshold { get; init; }
    public double SecondCurrentChance { get; init; }
    public double RouteMargin { get; init; }
    public double ChoiceBendMin { get; init; }
    public double ChoiceBendRange { get; init; }
    public double HinderedSteer { get; init; }
    public double BurstDrift { get; init; }
    public double MaxFrame { get; init; }
    public double Step { get; init; }
}
public sealed record GateProfile(double Until, double Drift, double[] Lanes, double Maximum);
public sealed record SpiritStyle(string Name, int BoostGates, double Size, double Radiance, double Flicker);
public sealed record WispLook
{
    public float Radius { get; init; }
    public Dictionary<string,string> Palette { get; init; } = new();
    public List<WispPart> Parts { get; init; } = new();
}
public sealed record WispPart
{
    public string Kind { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public float Scale { get; init; } = 1;
    public Dictionary<string,double> Params { get; init; } = new();
    public double Param(string key, double fallback) => Params.GetValueOrDefault(key, fallback);
}
public sealed record LoadingControls
{
    public string[] NegativeX { get; init; } = Array.Empty<string>();
    public string[] PositiveX { get; init; } = Array.Empty<string>();
    public string[] NegativeY { get; init; } = Array.Empty<string>();
    public string[] PositiveY { get; init; } = Array.Empty<string>();
    public double Deadzone { get; init; }
}

/// <summary>Mulberry32, matching the original toy. Its stream never touches world RNG.</summary>
public sealed class LoadingRng
{
    private uint _state;
    public LoadingRng(uint seed) => _state = seed == 0 ? 0x9e3779b9 : seed;
    public double Next()
    {
        unchecked
        {
            _state += 0x6d2b79f5;
            var t = (_state ^ (_state >> 15)) * (_state | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
