using Godot;

namespace HollowWake.Client.World;

/// <summary>
/// Where sound will attach when assets arrive: the buses (music, effects, ambience, interface) under the
/// master, and one call per moment the animator and the world raise (a footstep on a surface, a swing, a hit,
/// a death, the water's edge). A cue plays the stream registered under its name, most specific first
/// (footstep.grass before footstep), at the body's place; with nothing registered, as today, it plays
/// nothing and costs a dictionary lookup. No asset ships yet.
/// </summary>
public partial class AudioCues : Node3D
{
    private readonly Dictionary<string, AudioStream> _streams = new(StringComparer.Ordinal);
    private readonly Queue<AudioStreamPlayer3D> _players = new();
    private int _busEffects;

    /// <summary>Cues raised since the last read, by name, for the smoke to see the hooks fire.</summary>
    public Dictionary<string, int> Raised { get; } = new(StringComparer.Ordinal);

    public override void _Ready()
    {
        Name = "AudioCues";
        foreach (var bus in new[] { "Music", "Effects", "Ambience", "Interface" })
        {
            if (AudioServer.GetBusIndex(bus) < 0)
            {
                AudioServer.AddBus();
                AudioServer.SetBusName(AudioServer.BusCount - 1, bus);
                AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master");
            }
        }
        _busEffects = AudioServer.GetBusIndex("Effects");
        for (var i = 0; i < 8; i++)
        {
            var player = new AudioStreamPlayer3D { Bus = "Effects", MaxDistance = 40f };
            AddChild(player);
            _players.Enqueue(player);
        }
    }

    /// <summary>Registers a stream for a cue name (an asset pack's job); cues with no stream stay silent.</summary>
    public void Register(string cue, AudioStream stream) => _streams[cue] = stream;

    /// <summary>The effects bus's volume, from the master volume option until buses get their own.</summary>
    public void SetEffectsVolume(float linear) => AudioServer.SetBusVolumeDb(_busEffects, Mathf.LinearToDb(Mathf.Max(0.0001f, linear)));

    /// <summary>
    /// Raises a cue at a place: <c>footstep</c> with a surface variant (<c>footstep.grass</c>), <c>swing</c>,
    /// <c>hit</c>, <c>death</c>, <c>splash</c>. Plays the most specific registered stream, if any.
    /// </summary>
    public void Play(string cue, string? variant, Vector3 at)
    {
        var name = variant is null ? cue : $"{cue}.{variant}";
        Raised[name] = Raised.GetValueOrDefault(name) + 1;
        if (!_streams.TryGetValue(name, out var stream) && !_streams.TryGetValue(cue, out stream))
        {
            return;
        }
        var player = _players.Dequeue();
        _players.Enqueue(player);
        player.Stream = stream;
        player.GlobalPosition = at;
        player.Play();
    }
}
