// Hollow Wake MMO wire protocol, generated from src/HollowWake.Protocol/Messages.cs. Do not edit by hand:
// after changing a message, regenerate with `dotnet run --project src/HollowWake.Tools -- protocol-ts`.
// HollowWake.Server.Tests fails while this file and the C# messages disagree.

/** The protocol version the server speaks (ProtocolInfo.Version), sent in hello. */
export const PROTOCOL_VERSION = 1;
/** ProtocolInfo.EntityDeltaFeature */
export const ENTITY_DELTA_FEATURE = "entity_delta";
/** ProtocolInfo.InterfaceFeature */
export const INTERFACE_FEATURE = "interface";

/**
 * A 64-bit integer from C# (entity keys, item uids, nonces, the world seed): a number when it is a safe
 * JavaScript integer, otherwise its exact decimal string. The server reads either form.
 */
export type Int64 = number | string;

// ---------------------------------------------------------------- messages

export interface HelloMsg {
  t: "hello";
  client_version: string;
  protocol: number;
  features?: string[];
}

export interface LoginMsg {
  t: "login";
  account: string;
  token: string;
}

export interface CreateCharacterMsg {
  t: "create_character";
  name: string;
  calling: string;
}

export interface SelectCharacterMsg {
  t: "select_character";
  character_id: string;
}

export interface LeaveWorldMsg {
  t: "leave_world";
}

export interface MoveDirMsg {
  t: "move_dir";
  x: number;
  y: number;
}

export interface MoveToMsg {
  t: "move_to";
  x: number;
  y: number;
  z: number;
}

export interface StopMsg {
  t: "stop";
}

export interface JumpMsg {
  t: "jump";
}

export interface UseWeaveMsg {
  t: "use_weave";
  slot: number;
  target: Int64;
  x: number;
  y: number;
  z: number;
  dir_x: number;
  dir_y: number;
}

export interface CancelCastMsg {
  t: "cancel_cast";
}

export interface InteractMsg {
  t: "interact";
  target: Int64;
}

export interface PickUpMsg {
  t: "pick_up";
  drop: Int64;
}

export interface EquipMsg {
  t: "equip";
  item_uid: Int64;
  slot?: string;
}

export interface UnequipMsg {
  t: "unequip";
  slot: string;
}

export interface DropItemMsg {
  t: "drop_item";
  item_uid: Int64;
}

export interface AllocatePassiveMsg {
  t: "allocate_passive";
  node: string;
  option?: string;
}

export interface SetWeaveMsg {
  t: "set_weave";
  slot: number;
  /** opaque here: see WeaveSpec in C# */
  spec: unknown;
}

export interface AcceptQuestMsg {
  t: "accept_quest";
  quest: string;
  giver: Int64;
}

export interface TurnInQuestMsg {
  t: "turn_in_quest";
  quest: string;
  giver: Int64;
}

export interface CraftStartMsg {
  t: "craft_start";
  recipe: string;
  station: Int64;
}

export interface CraftSubmitMsg {
  t: "craft_submit";
  recipe: string;
  station: Int64;
  times: number[];
  choices: number[];
  values: Record<string, number>;
}

export interface BindFountMsg {
  t: "bind_fount";
  fount: Int64;
}

export interface EnactDecreeMsg {
  t: "enact_decree";
  holding: string;
  decree: string;
}

export interface BuildStructureMsg {
  t: "build_structure";
  holding: string;
  structure: string;
  x: number;
  y: number;
  z: number;
}

export interface ChatMsg {
  t: "chat";
  channel: string;
  text: string;
}

export interface RespawnMsg {
  t: "respawn";
}

export interface PingMsg {
  t: "ping";
  nonce: Int64;
}

export interface RequestChunkMsg {
  t: "request_chunk";
  x: number;
  y: number;
}

export interface ReleaseWeaveMsg {
  t: "release_weave";
  slot: number;
}

export interface PressWeaveMsg {
  t: "press_weave";
  slot: number;
}

export interface AimMsg {
  t: "aim";
  x: number;
  y: number;
  z: number;
  dir_x: number;
  dir_y: number;
}

export interface UseMetaMsg {
  t: "use_meta";
  slot: number;
}

export interface SwapWeaveSlotsMsg {
  t: "swap_weave_slots";
  a: number;
  b: number;
}

export interface CommandMinionsMsg {
  t: "command_minions";
  command: string;
  target: Int64;
  x: number;
  y: number;
  z: number;
  group?: string;
}

export interface MoveItemMsg {
  t: "move_item";
  item: Int64;
  container: string;
  x: number;
  y: number;
  slot?: string;
}

export interface SocketStoneMsg {
  t: "socket_stone";
  stone: Int64;
  slot: number;
  socket: number;
  host?: Int64;
}

export interface UnsocketStoneMsg {
  t: "unsocket_stone";
  slot: number;
  socket: number;
  x?: number;
  y?: number;
  host?: Int64;
}

export interface LearnFormMsg {
  t: "learn_form";
  item: Int64;
  slot: number;
}

export interface UnlearnFormMsg {
  t: "unlearn_form";
  slot: number;
  x?: number;
  y?: number;
}

export interface RefundPassiveMsg {
  t: "refund_passive";
  node: string;
}

export interface ChooseDialogueMsg {
  t: "choose_dialogue";
  conversation: string;
  choice: string;
  node?: string;
}

export interface CloseDialogueMsg {
  t: "close_dialogue";
  conversation: string;
}

export interface ClosePaneMsg {
  t: "close_pane";
  pane: string;
  source: Int64;
}

export interface BuyMsg {
  t: "buy";
  vendor: Int64;
  stock: string;
  count: number;
}

export interface SellMsg {
  t: "sell";
  vendor: Int64;
  item: Int64;
  count?: number;
}

export interface BuyBackMsg {
  t: "buy_back";
  vendor: Int64;
  item: Int64;
}

export interface BuyPageMsg {
  t: "buy_page";
  container: string;
}

export interface StationServiceMsg {
  t: "station_service";
  station: Int64;
  service: string;
  item?: Int64;
  slot?: number;
  node?: string;
}

export interface BindGraftMsg {
  t: "bind_graft";
  source: string;
  slot: number;
}

export interface WelcomeMsg {
  t: "welcome";
  server_version: string;
  protocol: number;
  server_name: string;
  motd: string;
  features?: string[];
}

export interface LoginResultMsg {
  t: "login_result";
  ok: boolean;
  account_id?: string;
  error?: string;
}

export interface CharacterListMsg {
  t: "character_list";
  characters: CharacterSummary[];
  callings: string[];
}

export interface ErrorMsg {
  t: "error";
  code: string;
  text: string;
}

export interface EnterWorldMsg {
  t: "enter_world";
  character_id: string;
  entity_key: Int64;
  zone: string;
  x: number;
  y: number;
  z: number;
  world: WorldInfo;
  server_time: number;
  tick_rate: number;
}

export interface ZoneChangedMsg {
  t: "zone_changed";
  zone: string;
  zone_name: string;
  level: number;
  safe: boolean;
}

export interface EntitySpawnMsg {
  t: "entity_spawn";
  state: EntityState;
}

export interface EntityUpdateMsg {
  t: "entity_update";
  states: EntityState[];
  server_time: number;
}

export interface EntityDeltaMsg {
  t: "entity_delta";
  deltas: EntityDelta[];
  server_time: number;
}

export interface EntityDespawnMsg {
  t: "entity_despawn";
  key: Int64;
}

export interface SelfStateMsg {
  t: "self_state";
  key: Int64;
  level: number;
  xp: number;
  xp_to_next: number;
  passive_points: number;
  passives: string[];
  attributes: Record<string, number>;
  resources: Record<string, number>;
  resource_max: Record<string, number>;
  weaves: WeaveSlotInfo[];
  bag: ItemInfo[];
  bag_capacity: number;
  equipment: Record<string, ItemInfo>;
  currency: Record<string, number>;
  quests: QuestInfo[];
  reputation: Record<string, number>;
  holdings: string[];
  reserved?: Record<string, number>;
  pools?: PoolInfo[];
  statuses?: StatusInfo[];
  cast?: CastInfo;
  charges?: ChargeInfo[];
  stamps?: Record<string, number>;
}

export interface CombatEventMsg {
  t: "combat_event";
  kind: string;
  source: Int64;
  target: Int64;
  amount: number;
  crit: boolean;
  tags: string[];
  status?: string;
}

export interface ChatBroadcastMsg {
  t: "chat_broadcast";
  from: string;
  channel: string;
  text: string;
}

export interface NoticeMsg {
  t: "notice";
  text: string;
  level: string;
}

export interface QuestOffersMsg {
  t: "quest_offers";
  giver: Int64;
  offers: QuestInfo[];
  turn_ins: QuestInfo[];
}

export interface CraftChallengeMsg {
  t: "craft_challenge";
  recipe: string;
  type: string;
  data: string;
  time_limit: number;
}

export interface CraftResultMsg {
  t: "craft_result";
  recipe: string;
  success: boolean;
  score: number;
  quality: number;
  reason: string;
  item?: ItemInfo;
}

export interface ChunkDataMsg {
  t: "chunk_data";
  x: number;
  y: number;
  size: number;
  height: number[];
  /** base64 */
  flags: string;
  tags: string[];
  surface_tag: number[];
  layers: LayerInfo[];
  gen_version: number;
  water_level?: number;
}

export interface IntentRejectedMsg {
  t: "intent_rejected";
  intent: string;
  reason: string;
}

export interface PongMsg {
  t: "pong";
  nonce: Int64;
  server_time: number;
}

export interface DisconnectMsg {
  t: "disconnect";
  reason: string;
}

export interface InventoryStateMsg {
  t: "inventory_state";
  bag: BoardInfo;
  slots: EquipSlotInfo[];
  equipment: Record<string, ItemInfo>;
  wallet: CurrencyInfo[];
  containers: ContainerInfo[];
}

export interface WeaveSlotsMsg {
  t: "weave_slots";
  seats: WeaveSeatInfo[];
}

export interface PassiveStateMsg {
  t: "passive_state";
  allocated: string[];
  picks: Record<string, string[]>;
  points: Record<string, number>;
  realms: string[];
  refund: boolean;
  refund_cost?: string;
  font?: Int64;
  grafts?: GraftInfo[];
}

export interface PageStateMsg {
  t: "page_state";
  pages: PageInfo[];
}

export interface DialogueStateMsg {
  t: "dialogue_state";
  source: Int64;
  conversation: string;
  speaker: string;
  line: string;
  choices: DialogueChoiceInfo[];
  ended: boolean;
  node?: string;
  reach?: number;
}

export interface VendorStateMsg {
  t: "vendor_state";
  source: Int64;
  vendor: string;
  name: string;
  stock: VendorStockInfo[];
  buy_back: VendorBuyBackInfo[];
  sell_prices: VendorQuoteInfo[];
  closed: boolean;
  reach?: number;
}

export interface StationStateMsg {
  t: "station_state";
  source: Int64;
  name: string;
  recipes: RecipeInfo[];
  closed: boolean;
  tags?: string[];
  reach?: number;
  services?: StationServiceInfo[];
}

// ---------------------------------------------------------------- records inside messages

export interface CharacterSummary {
  id: string;
  name: string;
  calling: string;
  level: number;
  zone: string;
}

export interface WorldInfo {
  seed: Int64;
  chunk_size: number;
  zone_chunks: number;
  region_zones: number;
  overlays: OverlayInfo[];
  content_hash: string;
}

export interface EntityState {
  key: Int64;
  name: string;
  pattern?: string;
  look?: string;
  x: number;
  y: number;
  z: number;
  yaw: number;
  radius: number;
  height: number;
  faction?: string;
  rarity?: string;
  is_player: boolean;
  level: number;
  resources: Record<string, number>;
  resource_max: Record<string, number>;
  flags: string[];
  statuses: string[];
  interactable?: string;
  dead: boolean;
  casting?: string;
  cast_progress: number;
  grounded: boolean;
  vx: number;
  vy: number;
  vz: number;
  static?: boolean;
  shape?: string;
  shape_size?: ShapeSizeInfo;
  heading?: number;
  spin?: number;
  until?: number;
}

export interface EntityDelta {
  key: Int64;
  name?: string;
  pattern?: string;
  look?: string;
  x?: number;
  y?: number;
  z?: number;
  yaw?: number;
  radius?: number;
  height?: number;
  faction?: string;
  rarity?: string;
  level?: number;
  resources?: Record<string, number>;
  resource_max?: Record<string, number>;
  flags?: string[];
  statuses?: string[];
  interactable?: string;
  dead?: boolean;
  casting?: string;
  cast_progress?: number;
  grounded?: boolean;
  vx?: number;
  vy?: number;
  vz?: number;
  shape?: string;
  shape_size?: ShapeSizeInfo;
  heading?: number;
  spin?: number;
}

export interface WeaveSlotInfo {
  slot: number;
  key: string;
  name: string;
  look?: string;
  ready_in: number;
  costs: Record<string, number>;
  targeting: string;
  range: number;
}

export interface ItemInfo {
  uid: Int64;
  base: string;
  name: string;
  rarity: string;
  count: number;
  level: number;
  slot?: string;
  lines: string[];
  local: Record<string, number>;
  x?: number;
  y?: number;
  w?: number;
  h?: number;
  class?: string;
  icon?: string;
  quality?: number;
  flavor?: string;
  locked?: boolean;
  sockets?: SocketInfo[];
  requirements?: RequirementInfo[];
  glyph?: ItemGlyphInfo;
  form?: ItemFormInfo;
  fits?: StoneFitInfo[];
}

export interface QuestInfo {
  id: string;
  name: string;
  state: string;
  objectives: string[];
  counts: number[];
  required: number[];
  giver?: string;
  level: number;
}

export interface PoolInfo {
  id: string;
  state: string;
  since?: number;
  until?: number;
}

export interface StatusInfo {
  id: string;
  stacks: number;
  remaining: number;
  duration: number;
}

export interface CastInfo {
  slot: number;
  mode: string;
  progress: number;
  stage: number;
  stages: number;
  name?: string;
  brim?: number;
  held?: number;
  duration?: number;
  pool?: number;
}

export interface ChargeInfo {
  id: string;
  count: number;
  max: number;
}

export interface LayerInfo {
  index: number;
  height: number;
  walkable: boolean;
  tags: string;
}

export interface BoardInfo {
  width: number;
  height: number;
  items: ItemInfo[];
  mask?: string[];
  rungs?: RungInfo[];
}

export interface EquipSlotInfo {
  id: string;
  name: string;
  order: number;
  occupied_by?: string;
}

export interface CurrencyInfo {
  id: string;
  name: string;
  amount: number;
}

export interface ContainerInfo {
  id: string;
  name: string;
  kind: string;
  board: BoardInfo;
  reachable: boolean;
  reason?: string;
  owner?: string;
  active?: boolean;
  of?: string;
  page?: number;
  locked?: boolean;
  price?: Record<string, number>;
}

export interface WeaveSeatInfo {
  slot: number;
  key?: string;
  form?: string;
  name: string;
  level: number;
  sockets: number;
  glyphs: SeatGlyphInfo[];
  ready_in: number;
  cooldown: number;
  preview: PreviewLineInfo[];
  icon?: string;
  dormant?: string;
  item?: Int64;
  threads?: string[];
  cast_mode?: string;
  targeting?: string;
  costs?: Record<string, number>;
  uses?: number;
  uses_max?: number;
  stacks?: number;
  converted?: FaceInfo;
  meta?: FaceInfo;
  active?: boolean;
}

export interface GraftInfo {
  source: string;
  glyph: string;
  name: string;
  level: number;
  slot?: number;
}

export interface PageInfo {
  id: string;
  sealed: boolean;
  pips: number;
  reason?: string;
  container?: string;
}

export interface DialogueChoiceInfo {
  id: string;
  text: string;
  enabled: boolean;
  reason?: string;
}

export interface VendorStockInfo {
  id: string;
  item: ItemInfo;
  price: Record<string, number>;
  count?: number;
  enabled?: boolean;
  reason?: string;
}

export interface VendorBuyBackInfo {
  item: ItemInfo;
  price: Record<string, number>;
}

export interface VendorQuoteInfo {
  item: Int64;
  price: Record<string, number>;
}

export interface RecipeInfo {
  id: string;
  name: string;
  inputs: RecipeInputInfo[];
  output: ItemInfo;
  craftable: boolean;
  reason?: string;
  cost?: Record<string, number>;
}

export interface StationServiceInfo {
  id: string;
  label: string;
  item?: Int64;
  slot?: number;
  node?: string;
  price?: Record<string, number>;
  enabled?: boolean;
  reason?: string;
}

export interface OverlayInfo {
  id: string;
  global: boolean;
  regions: number[][];
  zones: number[][];
  circle?: number[];
  params: Record<string, number>;
  priority: number;
}

export interface ShapeSizeInfo {
  radius?: number;
  angle?: number;
  width?: number;
  inner?: number;
  curvature?: number;
}

export interface SocketInfo {
  index: number;
  stone?: ItemInfo;
}

export interface RequirementInfo {
  text: string;
  met: boolean;
}

export interface ItemGlyphInfo {
  id: string;
  name: string;
  level: number;
  rows?: string[];
}

export interface ItemFormInfo {
  id: string;
  name: string;
  level: number;
  sockets: number;
}

export interface StoneFitInfo {
  slot: number;
  fit: string;
  reason?: string;
}

export interface RungInfo {
  index: number;
  unlocked: boolean;
  cells: string[];
  requirement?: string;
}

export interface SeatGlyphInfo {
  socket: number;
  id: string;
  name: string;
  level: number;
  dormant?: string;
  item?: Int64;
}

export interface PreviewLineInfo {
  label: string;
  text: string;
}

export interface FaceInfo {
  label: string;
  icon?: string;
}

export interface RecipeInputInfo {
  text: string;
  have: number;
  need: number;
}

/** Every message, discriminated by t. */
export type Message =
  | HelloMsg
  | LoginMsg
  | CreateCharacterMsg
  | SelectCharacterMsg
  | LeaveWorldMsg
  | MoveDirMsg
  | MoveToMsg
  | StopMsg
  | JumpMsg
  | UseWeaveMsg
  | CancelCastMsg
  | InteractMsg
  | PickUpMsg
  | EquipMsg
  | UnequipMsg
  | DropItemMsg
  | AllocatePassiveMsg
  | SetWeaveMsg
  | AcceptQuestMsg
  | TurnInQuestMsg
  | CraftStartMsg
  | CraftSubmitMsg
  | BindFountMsg
  | EnactDecreeMsg
  | BuildStructureMsg
  | ChatMsg
  | RespawnMsg
  | PingMsg
  | RequestChunkMsg
  | ReleaseWeaveMsg
  | PressWeaveMsg
  | AimMsg
  | UseMetaMsg
  | SwapWeaveSlotsMsg
  | CommandMinionsMsg
  | MoveItemMsg
  | SocketStoneMsg
  | UnsocketStoneMsg
  | LearnFormMsg
  | UnlearnFormMsg
  | RefundPassiveMsg
  | ChooseDialogueMsg
  | CloseDialogueMsg
  | ClosePaneMsg
  | BuyMsg
  | SellMsg
  | BuyBackMsg
  | BuyPageMsg
  | StationServiceMsg
  | BindGraftMsg
  | WelcomeMsg
  | LoginResultMsg
  | CharacterListMsg
  | ErrorMsg
  | EnterWorldMsg
  | ZoneChangedMsg
  | EntitySpawnMsg
  | EntityUpdateMsg
  | EntityDeltaMsg
  | EntityDespawnMsg
  | SelfStateMsg
  | CombatEventMsg
  | ChatBroadcastMsg
  | NoticeMsg
  | QuestOffersMsg
  | CraftChallengeMsg
  | CraftResultMsg
  | ChunkDataMsg
  | IntentRejectedMsg
  | PongMsg
  | DisconnectMsg
  | InventoryStateMsg
  | WeaveSlotsMsg
  | PassiveStateMsg
  | PageStateMsg
  | DialogueStateMsg
  | VendorStateMsg
  | StationStateMsg;

export type MessageType = Message["t"];

/** The message interface for a discriminator, for example MessageOf<"chunk_data">. */
export type MessageOf<T extends MessageType> = Extract<Message, { t: T }>;

/** Every discriminator, in the order Messages.cs declares them. */
export const MESSAGE_TYPES: readonly MessageType[] = [
  "hello",
  "login",
  "create_character",
  "select_character",
  "leave_world",
  "move_dir",
  "move_to",
  "stop",
  "jump",
  "use_weave",
  "cancel_cast",
  "interact",
  "pick_up",
  "equip",
  "unequip",
  "drop_item",
  "allocate_passive",
  "set_weave",
  "accept_quest",
  "turn_in_quest",
  "craft_start",
  "craft_submit",
  "bind_fount",
  "enact_decree",
  "build_structure",
  "chat",
  "respawn",
  "ping",
  "request_chunk",
  "release_weave",
  "press_weave",
  "aim",
  "use_meta",
  "swap_weave_slots",
  "command_minions",
  "move_item",
  "socket_stone",
  "unsocket_stone",
  "learn_form",
  "unlearn_form",
  "refund_passive",
  "choose_dialogue",
  "close_dialogue",
  "close_pane",
  "buy",
  "sell",
  "buy_back",
  "buy_page",
  "station_service",
  "bind_graft",
  "welcome",
  "login_result",
  "character_list",
  "error",
  "enter_world",
  "zone_changed",
  "entity_spawn",
  "entity_update",
  "entity_delta",
  "entity_despawn",
  "self_state",
  "combat_event",
  "chat_broadcast",
  "notice",
  "quest_offers",
  "craft_challenge",
  "craft_result",
  "chunk_data",
  "intent_rejected",
  "pong",
  "disconnect",
  "inventory_state",
  "weave_slots",
  "passive_state",
  "page_state",
  "dialogue_state",
  "vendor_state",
  "station_state",
];
