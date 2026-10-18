namespace HollowWake.Protocol;

// The messages of the interface (docs/design/ui.md): the intents its surfaces send and the states they draw.
// Every number a surface shows is computed by the server and drawn as received; no client folds one.
// Fields are only ever added. Optional fields are omitted from the wire when empty.

// ---------------------------------------------------------------- client -> server: cast modes and minions

/// <summary>Lets go of a held weave: ends a channel's hold or releases a charge-up.</summary>
public sealed record ReleaseWeaveMsg(int Slot) : Message;

/// <summary>A second press of a weave's key while it is casting: strike timing, salvo beats.</summary>
public sealed record PressWeaveMsg(int Slot) : Message;

/// <summary>The live aim while a held or tracking cast runs: a point on the ground and a facing.</summary>
public sealed record AimMsg(float X, float Y, float Z, float DirX, float DirY) : Message;

/// <summary>The second ability on a weave slot (its meta face).</summary>
public sealed record UseMetaMsg(int Slot) : Message;

/// <summary>Swaps two weave slots; the rack order is the weave bar order.</summary>
public sealed record SwapWeaveSlotsMsg(int A, int B) : Message;

/// <summary>Orders the hero's minions: a command name, a target entity (0 for none) and a point.</summary>
public sealed record CommandMinionsMsg(string Command, long Target, float X, float Y, float Z) : Message
{
    /// <summary>Only the minions of this group; omitted for all of them.</summary>
    public string? Group { get; init; }
}

// ---------------------------------------------------------------- client -> server: items and the rack

/// <summary>
/// Moves an item to a container: <c>bag</c> or a container's id at a cell (x, y) of its board,
/// <c>equipment</c> with <see cref="Slot"/> naming the equipment slot, or <c>world</c> to drop it. An item
/// already on the target cell is swapped into the moved item's place when it fits there.
/// </summary>
public sealed record MoveItemMsg(long Item, string Container, int X, int Y) : Message
{
    /// <summary>The equipment slot when the container is <c>equipment</c>.</summary>
    public string? Slot { get; init; }
}

/// <summary>
/// Sockets a stone from the bag into a socket: of a weave slot, or with <see cref="Host"/> of a gear item (worn or
/// in the bag). A stone already there goes back to the bag.
/// </summary>
public sealed record SocketStoneMsg(long Stone, int Slot, int Socket) : Message
{
    /// <summary>The gear item whose socket takes the stone; omitted for a weave slot's socket.</summary>
    public long? Host { get; init; }
}

/// <summary>Takes the stone out of a weave slot's socket (or a gear item's, with <see cref="Host"/>) into the bag, at a cell when one is given.</summary>
public sealed record UnsocketStoneMsg(int Slot, int Socket) : Message
{
    public int? X { get; init; }
    public int? Y { get; init; }
    public long? Host { get; init; }
}

/// <summary>Buys the next page of a stash at the server's price.</summary>
public sealed record BuyPageMsg(string Container) : Message;

/// <summary>
/// Asks a station for a service by name (raising a stone's or codex's level, merging codices, resetting passives),
/// naming the item, weave slot or node it applies to where the service needs one.
/// </summary>
public sealed record StationServiceMsg(long Station, string Service) : Message
{
    public long? Item { get; init; }
    public int? Slot { get; init; }
    public string? Node { get; init; }
}

/// <summary>Learns a form item from the bag into a weave slot; a copy already there swaps back to the bag.</summary>
public sealed record LearnFormMsg(long Item, int Slot) : Message;

/// <summary>Takes a learned form and its stones out of a weave slot into the bag.</summary>
public sealed record UnlearnFormMsg(int Slot) : Message
{
    public int? X { get; init; }
    public int? Y { get; init; }
}

// ---------------------------------------------------------------- client -> server: passives, conversations, trade

/// <summary>Refunds one allocated passive node at a font within reach; a choice node refunds its picks with it.</summary>
public sealed record RefundPassiveMsg(string Node) : Message;

/// <summary>Binds a graft a passive grants (its source: a node, or node:option) to a weave slot; a slot below zero unbinds it.</summary>
public sealed record BindGraftMsg(string Source, int Slot) : Message;

/// <summary>Buys a node of a weave slot's own tree; refused with the compiler's reason when the node would not count.</summary>
public sealed record BuyTreeNodeMsg(int Slot, string Node) : Message;

/// <summary>Picks one of the open choices of the conversation's current line.</summary>
public sealed record ChooseDialogueMsg(string Conversation, string Choice) : Message
{
    /// <summary>The line the choice was read from; a choice from a line the conversation has left is refused.</summary>
    public string? Node { get; init; }
}

/// <summary>Ends a conversation from the hero's side.</summary>
public sealed record CloseDialogueMsg(string Conversation) : Message;

/// <summary>Closes a world pane (<c>vendor</c>, <c>station</c>, <c>stash</c>) opened by an entity.</summary>
public sealed record ClosePaneMsg(string Pane, long Source) : Message;

/// <summary>Buys units of a vendor's stock entry at the server's price. The vendor is the entity that opened the vendor pane.</summary>
public sealed record BuyMsg(long Vendor, string Stock, int Count) : Message;

/// <summary>Sells a bag item to a vendor at the price the vendor state quoted.</summary>
public sealed record SellMsg(long Vendor, long Item) : Message
{
    /// <summary>Units of a stack to sell; omitted sells the whole stack.</summary>
    public int? Count { get; init; }
}

/// <summary>Buys back an item sold to the vendor.</summary>
public sealed record BuyBackMsg(long Vendor, long Item) : Message;

// ---------------------------------------------------------------- server -> client: the hero's cluster

/// <summary>A pool before life and its state: <c>ready</c>, <c>recharging</c>, <c>broken</c> or <c>rearming</c>.</summary>
public sealed record PoolInfo(string Id, string State)
{
    /// <summary>Server time the state began.</summary>
    public double? Since { get; init; }
    /// <summary>Server time the state ends, when it has an end.</summary>
    public double? Until { get; init; }
}

/// <summary>One of the hero's statuses: its stacks, seconds remaining and full duration in seconds.</summary>
public sealed record StatusInfo(string Id, int Stacks, double Remaining, double Duration);

/// <summary>
/// The cast in progress: the slot it came from, its mode (<c>cast</c>, <c>channel</c>, <c>charge</c>, <c>overcharge</c>
/// or <c>guard</c>), progress from zero to one (how full a charge-up or overcharge is; how far a bar has run), and the
/// overcharge stage reached out of the stage count.
/// </summary>
public sealed record CastInfo(int Slot, string Mode, double Progress, int Stage, int Stages)
{
    /// <summary>The weave's name.</summary>
    public string? Name { get; init; }
    /// <summary>Where the brim begins on the bar, from zero to one, for modes that have one.</summary>
    public double? Brim { get; init; }
    /// <summary>Seconds the cast has been held, for channels and charge-ups.</summary>
    public double? Held { get; init; }
    /// <summary>The full cast or charge time in seconds, when the cast has one.</summary>
    public double? Duration { get; init; }
    /// <summary>For a raised guard: the fraction of its pool left, from zero to one; null in every other mode.</summary>
    public double? Pool { get; init; }
}

/// <summary>A kind of banked charge: how many, and the cap.</summary>
public sealed record ChargeInfo(string Id, double Count, double Max);

// ---------------------------------------------------------------- server -> client: the weave slots

/// <summary>Every weave slot, empty ones included, as the rack and the weave bar draw them. Sent when any changes.</summary>
public sealed record WeaveSlotsMsg(List<WeaveSeatInfo> Seats) : Message;

/// <summary>
/// One weave slot. An empty slot has no key and no form. Cooldowns count down from <see cref="ReadyIn"/> as
/// of the message; <see cref="Cooldown"/> is the full duration the sweep is drawn against.
/// </summary>
public sealed record WeaveSeatInfo(int Slot, string? Key, string? Form, string Name, int Level, int Sockets, List<SeatGlyphInfo> Glyphs, double ReadyIn, double Cooldown, List<PreviewLineInfo> Preview)
{
    /// <summary>The authored preset, distinct from a freely composed form, for client presentation.</summary>
    public string? Preset { get; init; }
    public string? Icon { get; init; }
    /// <summary>Why the weave as a whole cannot be used, or null when it can.</summary>
    public string? Dormant { get; init; }
    /// <summary>The learned form item in the slot.</summary>
    public long? Item { get; init; }
    public List<string>? Threads { get; init; }
    /// <summary><c>instant</c>, <c>cast</c>, <c>channel</c>, <c>charge</c>, <c>overcharge</c> or <c>guard</c>.</summary>
    public string? CastMode { get; init; }
    public string? Targeting { get; init; }
    /// <summary>Costs per resource after the hero's multipliers.</summary>
    public Dictionary<string, double>? Costs { get; init; }
    /// <summary>Use charges held and their cap, for weaves that bank uses.</summary>
    public int? Uses { get; init; }
    public int? UsesMax { get; init; }
    /// <summary>Self stacks the slot holds, for weaves that pile them on use.</summary>
    public int? Stacks { get; init; }
    /// <summary>The face the slot shows while a convert rule holds.</summary>
    public FaceInfo? Converted { get; init; }
    /// <summary>The slot's second ability.</summary>
    public FaceInfo? Meta { get; init; }
    /// <summary>True while the slot's toggle or contract runs (a sustained summon, an aura), so its face shows on.</summary>
    public bool? Active { get; init; }
    /// <summary>The nodes of the weave's own tree bought for the slot, in the order they were bought (a node once per rank).</summary>
    public List<string>? Tree { get; init; }
    /// <summary>Bought nodes that do not count, each with the compiler's reason.</summary>
    public List<DormantNodeInfo>? DormantNodes { get; init; }
    /// <summary>For a weave whose form has a tree: the points spent on nodes that count, and the points its level grants.</summary>
    public int? TreeSpent { get; init; }
    public int? TreePoints { get; init; }
}

/// <summary>A bought tree node that does not count, and why (unknown, unlinked, excluded, past its ranks or the points).</summary>
public sealed record DormantNodeInfo(string Id, string Reason);

/// <summary>A glyph in a weave slot's socket: its level, and why it is dormant, or null when it is active.</summary>
public sealed record SeatGlyphInfo(int Socket, string Id, string Name, int Level)
{
    public string? Dormant { get; init; }
    /// <summary>The stone item, for stones a player socketed.</summary>
    public long? Item { get; init; }
    /// <summary>A glyph that does not fit the weave but rides to its crew: it gives the weave nothing but its costs.</summary>
    public bool? Riding { get; init; }
}

/// <summary>A face a slot can show: a label and an icon.</summary>
public sealed record FaceInfo(string Label)
{
    public string? Weave { get; init; }
    public string? Form { get; init; }
    public string? Icon { get; init; }
}

/// <summary>One line of a weave's preview, formatted by the server: a label and its text.</summary>
public sealed record PreviewLineInfo(string Label, string Text);

// ---------------------------------------------------------------- server -> client: inventory and containers

/// <summary>
/// The hero's items: the bag board, the equipment doll with every slot from content, the wallet, and every
/// container the hero owns with whether it can be reached from here. Sent when any of it changes.
/// </summary>
public sealed record InventoryStateMsg(BoardInfo Bag, List<EquipSlotInfo> Slots, Dictionary<string, ItemInfo> Equipment, List<CurrencyInfo> Wallet, List<ContainerInfo> Containers) : Message;

/// <summary>A grid of cells holding items with their positions and footprints. Mask rows use X for a usable cell and . for none.</summary>
public sealed record BoardInfo(int Width, int Height, List<ItemInfo> Items)
{
    public List<string>? Mask { get; init; }
    /// <summary>The unlock rungs whose cells make up the board.</summary>
    public List<RungInfo>? Rungs { get; init; }
}

/// <summary>An unlock rung of a board: the cells it adds, whether the hero has it, and what it requires as text.</summary>
public sealed record RungInfo(int Index, bool Unlocked, List<string> Cells)
{
    public string? Requirement { get; init; }
}

/// <summary>An equipment slot of the doll, from content.</summary>
public sealed record EquipSlotInfo(string Id, string Name, int Order)
{
    /// <summary>The slot whose item also fills this one (a two-handed weapon's off hand).</summary>
    public string? OccupiedBy { get; init; }
}

/// <summary>A currency in the wallet.</summary>
public sealed record CurrencyInfo(string Id, string Name, double Amount);

/// <summary>
/// A container the hero owns: a stash page, the reliquary, a station's board. Each board has its own id, which
/// is the container a move names. A stash's pages share <see cref="Of"/> and number from one.
/// </summary>
public sealed record ContainerInfo(string Id, string Name, string Kind, BoardInfo Board, bool Reachable)
{
    /// <summary>Why the container cannot be reached or changed from here.</summary>
    public string? Reason { get; init; }
    /// <summary><c>character</c> or <c>account</c>.</summary>
    public string? Owner { get; init; }
    /// <summary>Items seated here are active on the hero.</summary>
    public bool? Active { get; init; }
    /// <summary>The container definition this board belongs to, for paged containers.</summary>
    public string? Of { get; init; }
    public int? Page { get; init; }
    /// <summary>A page not yet bought, and its price per currency.</summary>
    public bool? Locked { get; init; }
    public Dictionary<string, double>? Price { get; init; }
}

/// <summary>A gear socket and the stone it holds, if any.</summary>
public sealed record SocketInfo(int Index)
{
    public ItemInfo? Stone { get; init; }
}

/// <summary>A requirement as text and whether the hero meets it.</summary>
public sealed record RequirementInfo(string Text, bool Met);

/// <summary>What a glyph stone holds.</summary>
public sealed record ItemGlyphInfo(string Id, string Name, int Level)
{
    /// <summary>The rows the stone rolled, as text.</summary>
    public List<string>? Rows { get; init; }
}

/// <summary>What a form item teaches: the form, its level and the sockets it gives a weave slot.</summary>
public sealed record ItemFormInfo(string Id, string Name, int Level, int Sockets)
{
    /// <summary>A preset memory's identity; null for a plain form memory.</summary>
    public string? Weave { get; init; }
}

/// <summary>
/// How a stone fits a weave slot: <c>fits</c>, <c>misfit</c> (it would sit dormant, with the reason),
/// <c>full</c> (no free socket) or <c>no_sockets</c>.
/// </summary>
public sealed record StoneFitInfo(int Slot, string Fit)
{
    public string? Reason { get; init; }
}

// ---------------------------------------------------------------- server -> client: passives and pages

/// <summary>
/// The hero's passives: allocated nodes, the options picked on choice nodes, points to spend per currency,
/// the realms open to the hero, and whether refunds can be made here (at a font) and at what cost.
/// </summary>
public sealed record PassiveStateMsg(List<string> Allocated, Dictionary<string, List<string>> Picks, Dictionary<string, int> Points, List<string> Realms, bool Refund) : Message
{
    /// <summary>What a refund costs, as text.</summary>
    public string? RefundCost { get; init; }
    /// <summary>The font in reach that allows refunds.</summary>
    public long? Font { get; init; }
    /// <summary>The grafts the hero's passives grant, each with the weave slot it is bound to (absent while unbound).</summary>
    public List<GraftInfo>? Grafts { get; init; }
}

/// <summary>A graft a passive grants: its source (a node, or node:option), the glyph and its level, and the slot it is bound to.</summary>
public sealed record GraftInfo(string Source, string Glyph, string Name, int Level)
{
    public int? Slot { get; init; }
}

/// <summary>The pages the hero has. A page left out does not exist for the hero.</summary>
public sealed record PageStateMsg(List<PageInfo> Pages) : Message;

/// <summary>A page: sealed with a reason when it cannot be used from here, and pips for what is unspent on it.</summary>
public sealed record PageInfo(string Id, bool Sealed, int Pips)
{
    public string? Reason { get; init; }
    /// <summary>The container a container page shows.</summary>
    public string? Container { get; init; }
}

// ---------------------------------------------------------------- server -> client: world panes

/// <summary>
/// A conversation's current line: who speaks, the line, and the choices with whether each can be taken and why
/// not. <see cref="Ended"/> closes the pane. The source is the entity the hero talks to.
/// </summary>
public sealed record DialogueStateMsg(long Source, string Conversation, string Speaker, string Line, List<DialogueChoiceInfo> Choices, bool Ended) : Message
{
    /// <summary>The line's id, echoed back with a choice.</summary>
    public string? Node { get; init; }
    /// <summary>How far the hero may stand from the source before the conversation ends.</summary>
    public double? Reach { get; init; }
}

public sealed record DialogueChoiceInfo(string Id, string Text, bool Enabled)
{
    public string? Reason { get; init; }
}

/// <summary>
/// A vendor's pane: its stock with prices, what the hero sold and can buy back, and what the vendor pays for
/// each bag item. Sent on opening and after every trade; <see cref="Closed"/> closes the pane.
/// </summary>
public sealed record VendorStateMsg(long Source, string Vendor, string Name, List<VendorStockInfo> Stock, List<VendorBuyBackInfo> BuyBack, List<VendorQuoteInfo> SellPrices, bool Closed) : Message
{
    public double? Reach { get; init; }
}

/// <summary>A stock entry: the item card, the price of one unit per currency, and units left (omitted when endless).</summary>
public sealed record VendorStockInfo(string Id, ItemInfo Item, Dictionary<string, double> Price)
{
    public int? Count { get; init; }
    /// <summary>False when the hero cannot buy it now, with the reason.</summary>
    public bool? Enabled { get; init; }
    public string? Reason { get; init; }
}

public sealed record VendorBuyBackInfo(ItemInfo Item, Dictionary<string, double> Price);

/// <summary>What the vendor pays for one bag item, per currency.</summary>
public sealed record VendorQuoteInfo(long Item, Dictionary<string, double> Price);

/// <summary>A crafting station's pane: its recipes with what they need, what the hero has, and whether each can be made.</summary>
public sealed record StationStateMsg(long Source, string Name, List<RecipeInfo> Recipes, bool Closed) : Message
{
    public List<string>? Tags { get; init; }
    public double? Reach { get; init; }
    /// <summary>The services the station offers, each quoted for what it would apply to.</summary>
    public List<StationServiceInfo>? Services { get; init; }
}

/// <summary>A service a station offers, quoted for one item, weave slot or node: its price and whether it can be done now.</summary>
public sealed record StationServiceInfo(string Id, string Label)
{
    public long? Item { get; init; }
    public int? Slot { get; init; }
    public string? Node { get; init; }
    public Dictionary<string, double>? Price { get; init; }
    public bool? Enabled { get; init; }
    public string? Reason { get; init; }
}

public sealed record RecipeInfo(string Id, string Name, List<RecipeInputInfo> Inputs, ItemInfo Output, bool Craftable)
{
    public string? Reason { get; init; }
    /// <summary>Currency the recipe costs.</summary>
    public Dictionary<string, double>? Cost { get; init; }
}

/// <summary>One input of a recipe as text, with how many the hero has and how many it needs.</summary>
public sealed record RecipeInputInfo(string Text, int Have, int Need);
