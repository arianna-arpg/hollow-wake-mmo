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
    public HashSet<long> Known { get; } = new();
    public double LastSnapshotAt { get; set; } = double.NegativeInfinity;
    public double LastSelfAt { get; set; } = double.NegativeInfinity;
    public double LastSaveAt { get; set; }
    public string ClientVersion { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;
    public int MessagesThisTick { get; set; }

    public Session(IConnection connection)
    {
        Connection = connection;
    }

    public void Send(Message message)
    {
        if (State != SessionState.Closed && Connection.IsOpen)
        {
            Connection.Send(MessageSerializer.Encode(message), reliable: message is not EntityUpdateMsg);
        }
    }

    public void Error(string code, string text) => Send(new ErrorMsg(code, text));

    public override string ToString() => $"session {Id} ({Connection.RemoteAddress}) {State} {Character?.Name}";
}
