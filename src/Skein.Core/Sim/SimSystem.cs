namespace Skein.Core.Sim;

/// <summary>
/// The fixed order of work inside one tick. Systems declare a phase and an order within it; mods
/// can slot systems anywhere. Intent comes first, presentation-facing emission last.
/// </summary>
public enum SimPhase
{
    /// <summary>Consume player intents and network inputs.</summary>
    Input = 0,
    /// <summary>AI decisions, perception, scheduled work.</summary>
    Think = 1,
    /// <summary>Casting, channeling, interactions begin or resolve.</summary>
    Act = 2,
    /// <summary>Movement, gravity, projectiles, collision.</summary>
    Move = 3,
    /// <summary>Hits land, damage resolves, deaths are decided.</summary>
    Resolve = 4,
    /// <summary>Statuses tick, resources regenerate, timers expire.</summary>
    Status = 5,
    /// <summary>Loot, quests, territory, progression react to this tick's events.</summary>
    React = 6,
    /// <summary>Lifetimes, despawns, deferred destruction.</summary>
    Cleanup = 7,
}

public interface ISimSystem
{
    string Name { get; }
    SimPhase Phase { get; }
    int Order => 0;

    void Update(Simulation sim);
}

/// <summary>Convenience base for systems with a fixed name and phase.</summary>
public abstract class SimSystem : ISimSystem
{
    public abstract string Name { get; }
    public abstract SimPhase Phase { get; }
    public virtual int Order => 0;
    public abstract void Update(Simulation sim);
}

/// <summary>Sink for simulation diagnostics. The server plugs its logger in; tests capture.</summary>
public interface ISimLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class NullSimLog : ISimLog
{
    public static readonly NullSimLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? exception = null) { }
}

public sealed class ListSimLog : ISimLog
{
    public List<string> Lines { get; } = new();
    public void Info(string message) => Lines.Add("info: " + message);
    public void Warn(string message) => Lines.Add("warn: " + message);
    public void Error(string message, Exception? exception = null) => Lines.Add("error: " + message + (exception is null ? "" : " " + exception.Message));
}
