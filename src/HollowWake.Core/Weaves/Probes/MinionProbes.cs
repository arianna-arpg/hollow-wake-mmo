namespace HollowWake.Core.Weaves.Probes;

/// <summary>
/// Mechanism probes about minions and constructs: pools, contracts, corpses and orders. Registered
/// through <see cref="MechanismRegistry.Register"/> when the registry starts. The built-in probes already
/// answer <c>summon</c>, <c>minion</c>, <c>construct</c> and <c>engagement</c>.
/// </summary>
internal static class MinionProbes
{
    public static void Register()
    {
        MechanismRegistry.Register("contract", static (w, _) => Summons(w).Any(n => n.ContainsKey("contract")), description: "a summon held by a contract: a reservation, and bodies that return");
        MechanismRegistry.Register("pool", static (w, _) => Summons(w).Concat(w.Nodes("deploy")).Any(n => n.ContainsKey("pool")), description: "bodies drawn from a weighted pool");
        MechanismRegistry.Register("corpse", static (w, _) => Corpse(w), description: "the weave raises, lays down or gathers corpses");
        MechanismRegistry.Register("spends_bodies", static (w, _) => w.Has("consume_corpse") || w.Has("consume_minion")
            || Summons(w).Any(n => n.ContainsKey("from_corpse")), description: "the weave consumes a corpse or minion");
        MechanismRegistry.Register("orders", static (w, _) => w.Has("command_minions") || w.Has("minion_cast") || w.Has("recall_minions"), description: "the weave orders minions: commands, casts or recalls");
        MechanismRegistry.Register("unlife", static (w, _) => Summons(w).Concat(w.Nodes("deploy")).Any(n => n.ContainsKey("decay")), description: "bodies whose life drains away");
    }

    private static IEnumerable<System.Text.Json.Nodes.JsonObject> Summons(WeaveProbe w) => w.Nodes("summon");

    private static bool Corpse(WeaveProbe w) =>
        Summons(w).Any(n => n.ContainsKey("from_corpse"))
        || w.Nodes("spawn").Any(n => WeaveProbe.Flag(n, "as_corpse"))
        || w.Has("gather_corpses")
        || w.Has("consume_corpse");
}
