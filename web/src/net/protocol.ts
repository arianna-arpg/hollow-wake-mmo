// Hollow Wake MMO wire protocol, generated from src/HollowWake.Protocol/Messages.cs. Do not edit by hand:
// after changing a message, regenerate with `dotnet run --project src/HollowWake.Tools -- protocol-ts`.
// HollowWake.Server.Tests fails while this file and the C# messages disagree.

/** The protocol version the server speaks (ProtocolInfo.Version), sent in hello. */
export const PROTOCOL_VERSION = 1;
/** ProtocolInfo.EntityDeltaFeature */
export const ENTITY_DELTA_FEATURE = "entity_delta";

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

export interface LayerInfo {
  index: number;
  height: number;
  walkable: boolean;
  tags: string;
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
  | DisconnectMsg;

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
];
