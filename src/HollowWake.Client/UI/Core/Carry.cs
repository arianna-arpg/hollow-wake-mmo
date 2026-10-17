using Godot;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>Where a carried item came from: a board (the bag or a container) at a cell, or an equipment slot.</summary>
public sealed record CarrySource(string Container, int X, int Y, string? Slot = null);

/// <summary>
/// An item picked up from a board or the doll: carried while the button is held (drag) or after a click
/// (click-lift), until it is dropped on a target or cancelled. Dropping sends one move intent; the server
/// answers with the new state.
/// </summary>
public sealed class Carry
{
    public required ItemInfo Item { get; init; }
    public required CarrySource From { get; init; }
    public int W => Math.Max(1, Item.W ?? 1);
    public int H => Math.Max(1, Item.H ?? 1);

    /// <summary>Lifted by a click rather than held: the next click drops it.</summary>
    public bool Lifted { get; set; }

    /// <summary>The button is still down from the press that picked it up.</summary>
    public bool Pressed { get; set; } = true;

    public Vector2 PressAt { get; init; }
}

/// <summary>A surface an item can be dropped on: a board, the doll, a weave socket, a vendor's counter.</summary>
public interface IDropTarget
{
    /// <summary>Whether the pointer (in viewport pixels) is over this target and the target is showing.</summary>
    bool Covers(Vector2 point);

    /// <summary>Tints what the carried item would do here.</summary>
    void Hover(Carry carry, Vector2 point);

    /// <summary>Clears any tint.</summary>
    void Leave();

    /// <summary>Sends the intent for dropping here; false when the drop means nothing here.</summary>
    bool Drop(Carry carry, Vector2 point);
}

/// <summary>The carried item, following the pointer.</summary>
public partial class CarryGhost : Control
{
    private readonly Skin _skin;
    private Carry? _carry;
    private float _cell;

    public CarryGhost(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 60;
        Visible = false;
    }

    public void Follow(Carry? carry, Vector2 pointer, float cell)
    {
        _carry = carry;
        _cell = cell;
        Visible = carry is not null;
        if (carry is null)
        {
            return;
        }
        Size = new Vector2(carry.W * cell, carry.H * cell);
        Position = pointer - Size / 2;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_carry is null)
        {
            return;
        }
        ItemArt.Tile(this, _skin, new Rect2(Vector2.Zero, Size), _carry.Item, 0.85f, true);
    }
}
