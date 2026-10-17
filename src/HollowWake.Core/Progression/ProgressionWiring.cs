using HollowWake.Core.Crafting;
using HollowWake.Core.Dialogue;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

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
        var rack = new RackService();
        sim.AddService(rack);
        inventory.Register(rack);
        sim.AddService<IWeaveOwnership>(new WeaveOwnership());
        sim.AddService(new DialogueService());
        sim.AddService(new VendorService());
        sim.AddService(new StationService());
        sim.AddSystem(new PassiveSystem());
        sim.AddSystem(new PaneSystem());
    }
}
