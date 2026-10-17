using HollowWake.Core.Items;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>
/// The services and systems of progression, items, conversations and trade, registered into a simulation in
/// one place. <see cref="ZoneBuilder.Wire"/> calls it, so zones, arenas, tools and tests all have them.
/// </summary>
public static class ProgressionWiring
{
    public static void Wire(Simulation sim)
    {
        var inventory = new InventoryService();
        sim.AddService(inventory);
    }
}
