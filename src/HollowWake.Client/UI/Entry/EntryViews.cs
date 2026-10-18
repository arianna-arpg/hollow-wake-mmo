using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Appearance;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Entry;

/// <summary>Account stages share lifecycle, not mechanics. A tutorial registers its own view and server stage.</summary>
public abstract partial class AccountEntryView : Control
{
    public abstract string? Pending { get; }
    public Func<bool> Suspended { get; set; } = () => false;
    public event Action<string, string>? CreateRequested;
    public event Action<string>? EnterRequested;
    public event Action? LeaveRequested;
    public event Action? OptionsRequested;
    protected void RequestCreate(string name, string calling) => CreateRequested?.Invoke(name, calling);
    protected void RequestEnter(string id) => EnterRequested?.Invoke(id);
    protected void RequestLeave() => LeaveRequested?.Invoke();
    protected void RequestOptions() => OptionsRequested?.Invoke();
    public abstract void ShowCharacters(CharacterListMsg list);
    public abstract void Error(string text);
    public virtual void FocusCharacter(string id) { }
}

public static class EntryViews
{
    public delegate AccountEntryView Factory(UiEntrySceneDef definition, ContentRegistry content, AppearanceService appearance);
    private static readonly Dictionary<(string Stage, string Activity), Factory> Views = new() {
        [("vessels", "spatial_vessels")] = (definition, content, appearance) => new EntryScene(EntrySceneSpec.Read(definition), content, appearance),
    };
    public static void Register(string stage, string activity, Factory factory) => Views.Add((stage, activity), factory);
    public static AccountEntryView? Create(string stage, UiEntrySceneDef definition, ContentRegistry content, AppearanceService appearance) =>
        Views.TryGetValue((stage, definition.Meta?["activity"]?.ToString() ?? ""), out var factory) ? factory(definition, content, appearance) : null;
}
