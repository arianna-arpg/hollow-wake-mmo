using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>
/// The hero and their world as content gates read them: conversation lines and choices, realm unlocks, page gates,
/// vendor stock. Beside the hero's own attributes and flags it resolves <c>wallet.{currency}</c>, <c>rep.{faction}</c>
/// and <c>points.{currency}</c> as numbers, and the flags <c>quest.active.{id}</c>, <c>quest.complete.{id}</c>
/// (objectives done, not yet turned in), <c>quest.completed.{id}</c> (turned in), <c>quest.taken.{id}</c>,
/// <c>flag.{name}</c> (story flags), <c>realm.{id}</c>, <c>passive.{node}</c> and <c>calling.{id}</c>.
/// </summary>
public sealed class HeroScope : IExprScope
{
    private readonly Simulation _sim;
    private readonly Entity _hero;
    private readonly Stats? _stats;
    private readonly QuestLog? _log;

    public HeroScope(Simulation sim, Entity hero)
    {
        _sim = sim;
        _hero = hero;
        _stats = sim.World.Get<Stats>(hero);
        _log = sim.World.Get<QuestLog>(hero);
    }

    /// <summary>Scope prefixes this scope adds, for content validation.</summary>
    public static readonly string[] Prefixes = { "wallet.", "rep.", "points." };

    public bool TryResolve(string name, out double value)
    {
        if (name.StartsWith("wallet.", StringComparison.Ordinal))
        {
            value = _sim.World.Get<Inventory>(_hero)?.CurrencyOf(name[7..]) ?? 0;
            return true;
        }
        if (name.StartsWith("rep.", StringComparison.Ordinal))
        {
            value = _log is not null && _log.Reputation.TryGetValue(name[4..], out var standing) ? standing : 0;
            return true;
        }
        if (name.StartsWith("points.", StringComparison.Ordinal))
        {
            value = _sim.TryService<ProgressionService>(out var progression) ? progression.Points(_sim, _hero, name[7..]) : 0;
            return true;
        }
        if (_stats is not null)
        {
            return _stats.TryResolve(name, out value);
        }
        value = 0;
        return false;
    }

    public bool HasFlag(string flag)
    {
        if (flag.StartsWith("quest.", StringComparison.Ordinal) && _log is not null && QuestFlag(flag) is { } quest)
        {
            return quest;
        }
        if (flag.StartsWith("flag.", StringComparison.Ordinal))
        {
            return _log is not null && _log.Flags.Contains(flag[5..]);
        }
        if (flag.StartsWith("realm.", StringComparison.Ordinal))
        {
            var realm = flag[6..];
            return realm == "star" || (_sim.World.Get<PassiveState>(_hero)?.OpenRealms.Contains(realm) ?? false);
        }
        if (flag.StartsWith("passive.", StringComparison.Ordinal))
        {
            return _stats is not null && _stats.Passives.Contains(flag[8..]);
        }
        if (flag.StartsWith("calling.", StringComparison.Ordinal) && _sim.World.TryGet<Identity>(_hero, out var identity))
        {
            return identity.Tags.Has(flag);
        }
        return _stats?.HasFlag(flag) ?? false;
    }

    private bool? QuestFlag(string flag)
    {
        var log = _log!;
        var rest = flag.AsSpan(6);
        var dot = rest.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }
        var state = rest[..dot].ToString();
        var id = rest[(dot + 1)..].ToString();
        return state switch
        {
            "active" => log.Quests.TryGetValue(id, out var a) && a.State == QuestState.Active,
            "complete" => log.Quests.TryGetValue(id, out var c) && c.State == QuestState.Complete,
            "completed" => log.Completed.Contains(id),
            "taken" => log.Quests.ContainsKey(id),
            _ => null,
        };
    }

    public double NextRandom() => _stats?.NextRandom() ?? 0.5;
}
