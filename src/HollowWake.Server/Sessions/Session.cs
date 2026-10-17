using HollowWake.Core.Characters;
using HollowWake.Core.Ecs;
using HollowWake.Protocol;
using HollowWake.Server.Net;
using HollowWake.Server.World;

namespace HollowWake.Server.Sessions;

public enum SessionState
{
    Connected,
    Authenticated,
    InWorld,
    Closed,
}

/// <summary>One client's life on the server: connection, account, character, zone, and what it can see.</summary>
public sealed class Session
{
    public long Id => Connection.Id;
    public IConnection Connection { get; }
    public SessionState State { get; set; } = SessionState.Connected;
    public string? AccountId { get; set; }
    public CharacterSheet? Character { get; set; }
    public Entity Entity { get; set; } = Entity.None;
    public ZoneInstance? Zone { get; set; }

    /// <summary>What this client has been told about its zone: known entities, fingerprints, refresh cadences.</summary>
    public SnapshotView View { get; } = new();

    /// <summary>Keys of the entities this client has been spawned and not yet despawned.</summary>
    public HashSet<long> Known => View.Known;

    public double LastSnapshotAt { get; set; } = double.NegativeInfinity;
    public double LastSelfAt { get; set; } = double.NegativeInfinity;
    public double LastSaveAt { get; set; }
    public string ClientVersion { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;
    public int MessagesThisTick { get; set; }

    /// <summary>Payload bytes and messages sent to this client since it connected (framing excluded).</summary>
    public long BytesSent { get; private set; }
    public long MessagesSent { get; private set; }

    /// <summary>Payload bytes of snapshot traffic: entity spawns, updates, deltas and despawns, and self states.</summary>
    public long SnapshotBytes { get; private set; }

    /// <summary>The part of <see cref="SnapshotBytes"/> that is entity changes: updates and deltas.</summary>
    public long ChangeBytes { get; private set; }

    /// <summary>The part of <see cref="SnapshotBytes"/> that is interest: spawns and despawns.</summary>
    public long InterestBytes { get; private set; }

    /// <summary>The part of <see cref="SnapshotBytes"/> that is self states.</summary>
    public long SelfBytes { get; private set; }

    /// <summary>Entity snapshots built for this client; with <see cref="SnapshotBytes"/>, the bytes it costs per snapshot.</summary>
    public long SnapshotsBuilt { get; set; }

    public double SnapshotBytesPerSnapshot => SnapshotsBuilt == 0 ? 0 : SnapshotBytes / (double)SnapshotsBuilt;

    public Session(IConnection connection)
    {
        Connection = connection;
    }

    public void Send(Message message)
    {
        if (State != SessionState.Closed && Connection.IsOpen)
        {
            var payload = MessageSerializer.Encode(message);
            BytesSent += payload.Length;
            MessagesSent++;
            switch (message)
            {
                case EntityUpdateMsg or EntityDeltaMsg:
                    ChangeBytes += payload.Length;
                    SnapshotBytes += payload.Length;
                    break;
                case EntitySpawnMsg or EntityDespawnMsg:
                    InterestBytes += payload.Length;
                    SnapshotBytes += payload.Length;
                    break;
                case SelfStateMsg:
                    SelfBytes += payload.Length;
                    SnapshotBytes += payload.Length;
                    break;
            }
            // Updates and deltas may be dropped by unreliable transports; the snapshot refresh repairs them.
            Connection.Send(payload, reliable: message is not (EntityUpdateMsg or EntityDeltaMsg));
        }
    }

    public void Error(string code, string text) => Send(new ErrorMsg(code, text));

    public override string ToString() => $"session {Id} ({Connection.RemoteAddress}) {State} {Character?.Name}";
}
