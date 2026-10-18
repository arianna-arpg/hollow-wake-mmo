using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Logic;

public static class EntryChoices
{
    public static List<VesselChoice> Build(CharacterListMsg list, ContentRegistry content, EntrySceneSpec spec)
    {
        var choices = new List<VesselChoice>();
        foreach (var c in list.Characters)
        {
            var calling = content.Get<CallingDef>(c.Calling);
            choices.Add(new("character:" + c.Id, c.Calling, c.Name, $"{calling?.Name ?? c.Calling} · Level {c.Level} · {c.Zone}", calling?.Look ?? c.Calling, c.Id, true, 0));
        }
        foreach (var c in list.Entry?.Callings ?? list.Callings.Select(id => new CallingAvailability(id, true)).ToList())
        {
            var calling = content.Get<CallingDef>(c.Id);
            var available = c.Available && (list.Entry?.CanCreate ?? true);
            choices.Add(new("calling:" + c.Id, c.Id, calling?.Name ?? c.Id,
                !c.Available ? spec.Labels["unavailable"] : list.Entry?.CanCreate == false ? spec.Labels["full"] : calling?.Description ?? "",
                calling?.Look ?? c.Id, null, available, 1));
        }
        return choices;
    }
}
