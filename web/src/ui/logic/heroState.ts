import type {
  BoardInfo,
  CastInfo,
  CurrencyInfo,
  EntityState,
  FaceInfo,
  Int64,
  InventoryStateMsg,
  ItemInfo,
  PageStateMsg,
  PassiveStateMsg,
  PoolInfo,
  SelfStateMsg,
  StatusInfo,
  WeaveSeatInfo,
  WeaveSlotsMsg,
} from '../../net/protocol';

/** A seat as the bar and the rack draw it, from the weave slot state or, without it, the self state. */
export interface Seat {
  slot: number;
  key: string | null;
  name: string;
  icon: string | null;
  threads: string[];
  /** Local seconds when the seat is ready again. */
  readyAt: number;
  /** The length of the running cooldown, for the sweep. */
  cooldown: number;
  costs: Record<string, number>;
  dormant: string | null;
  uses: number | null;
  usesMax: number | null;
  converted: FaceInfo | null;
  meta: FaceInfo | null;
  info: WeaveSeatInfo | null;
}

export const seatEmpty = (s: Seat): boolean => s.key === null;
export const seatReadyIn = (s: Seat, now: number): number => Math.max(0, s.readyAt - now);

const sameUid = (a: Int64 | undefined | null, b: Int64 | undefined | null): boolean => a !== undefined && a !== null && b !== undefined && b !== null && String(a) === String(b);

/**
 * The latest of every state the server sent about the hero, as the interface reads it: the self state with the
 * cluster's fields, the weave slots, the inventory, the passives and the pages, plus the hero's own entity state
 * for a server that sends no interface states. Timers the server sends as time left (cooldowns, statuses) count
 * down from when they arrived. Nothing here computes a number the server already sent. The desktop client's
 * UI/HeroState.cs.
 */
export class HeroState {
  self: SelfStateMsg | null = null;
  slots: WeaveSlotsMsg | null = null;
  inventory: InventoryStateMsg | null = null;
  passives: PassiveStateMsg | null = null;
  pages: PageStateMsg | null = null;
  entity: EntityState | null = null;
  /** Whether the server granted the interface feature: the self state then carries the cluster's fields. */
  interface = false;
  /** Local seconds when each state arrived, for timers counted down from receipt. */
  selfAt = 0;
  slotsAt = 0;
  /** The server clock as last estimated (server seconds minus local seconds). */
  serverOffset = 0;
  hasServerClock = false;
  /** Bumped on every change, so a view can rebuild only when something changed. */
  version = 0;
  inventoryVersion = 0;
  slotsVersion = 0;
  passivesVersion = 0;
  pagesVersion = 0;
  /** Each slot's running cooldown as the self state reports it: when it ends (local seconds) and its length. */
  private readonly cooldowns = new Map<number, { readyAt: number; span: number }>();

  applySelf(self: SelfStateMsg, now: number): void {
    this.self = self;
    this.selfAt = now;
    for (const w of self.weaves) {
      this.trackCooldown(w.slot, w.ready_in, now);
    }
    this.version++;
  }

  applySlots(slots: WeaveSlotsMsg, now: number): void {
    this.slots = slots;
    this.slotsAt = now;
    this.slotsVersion++;
    this.version++;
  }

  applyInventory(inventory: InventoryStateMsg): void {
    this.inventory = inventory;
    this.inventoryVersion++;
    this.version++;
  }

  applyPassives(passives: PassiveStateMsg): void {
    this.passives = passives;
    this.passivesVersion++;
    this.version++;
  }

  applyPages(pages: PageStateMsg): void {
    this.pages = pages;
    this.pagesVersion++;
    this.version++;
  }

  /**
   * Follows a slot's time left from one self state to the next: a later end than the running one starts a new
   * cooldown whose length is the time left first seen; the same cooldown keeps its length and takes the fresher end.
   */
  private trackCooldown(slot: number, readyIn: number, now: number): void {
    if (readyIn <= 0.01) {
      this.cooldowns.delete(slot);
      return;
    }
    const readyAt = now + readyIn;
    const c = this.cooldowns.get(slot);
    this.cooldowns.set(slot, c !== undefined && readyAt <= c.readyAt + 0.1 ? { readyAt, span: Math.max(c.span, readyIn) } : { readyAt, span: readyIn });
  }

  /** Learns the server clock from a time the server stamped a message with, smoothing jitter. */
  observeServerTime(serverTime: number, now: number): void {
    const offset = serverTime - now;
    if (!this.hasServerClock || offset > this.serverOffset + 1 || offset < this.serverOffset - 1) {
      this.serverOffset = offset;
      this.hasServerClock = true;
      return;
    }
    // A late message reads an older time: keep the largest offset seen, eased toward new readings.
    this.serverOffset = offset > this.serverOffset ? offset : this.serverOffset + (offset - this.serverOffset) * 0.02;
  }

  serverNow(now: number): number {
    return now + this.serverOffset;
  }

  clear(): void {
    this.self = null;
    this.slots = null;
    this.inventory = null;
    this.passives = null;
    this.pages = null;
    this.entity = null;
    this.hasServerClock = false;
    this.cooldowns.clear();
    this.version++;
  }

  // ------------------------------------------------------------ the cluster

  resource(id: string): { current: number; max: number } {
    const s = this.self;
    if (s !== null && s.resources[id] !== undefined) {
      return { current: s.resources[id], max: s.resource_max[id] ?? 0 };
    }
    const e = this.entity;
    if (e !== null && e.resources[id] !== undefined) {
      return { current: e.resources[id], max: e.resource_max[id] ?? 0 };
    }
    return { current: 0, max: 0 };
  }

  /** Whether the hero has a pool at all: it is in the self state with a maximum above zero. */
  hasPool(id: string): boolean {
    return this.resource(id).max > 0;
  }

  reserved(id: string): number {
    return this.self?.reserved?.[id] ?? 0;
  }

  pool(id: string): PoolInfo | undefined {
    return this.self?.pools?.find((p) => p.id === id);
  }

  /** The hero's statuses with time left counted down from receipt; ids alone when the server sends no timers. */
  statuses(now: number): StatusInfo[] {
    if (this.interface) {
      const list = this.self?.statuses;
      if (list === undefined) {
        return [];
      }
      const elapsed = now - this.selfAt;
      return list.map((s) => (s.remaining < 0 ? s : { ...s, remaining: Math.max(0, s.remaining - elapsed) }));
    }
    return this.entity?.statuses.map((id) => ({ id, stacks: 1, remaining: -1, duration: -1 })) ?? [];
  }

  /** The cast in progress: the self state's cast, else (without the interface feature) the hero's entity state's. */
  get cast(): CastInfo | null {
    if (this.interface) {
      return this.self?.cast ?? null;
    }
    const e = this.entity;
    if (e !== null && e.casting) {
      return { slot: -1, mode: 'cast', progress: e.cast_progress, stage: 0, stages: 0, name: e.casting };
    }
    return null;
  }

  // ------------------------------------------------------------ the weave bar and the rack

  seats(count: number): Seat[] {
    const seats: Seat[] = [];
    if (this.slots !== null) {
      for (const s of [...this.slots.seats].sort((a, b) => a.slot - b.slot)) {
        const t = this.timer(s.slot, this.slotsAt + s.ready_in, s.cooldown);
        seats.push({
          slot: s.slot,
          key: s.key ?? null,
          name: s.name,
          icon: s.icon ?? null,
          threads: s.threads ?? [],
          readyAt: t.readyAt,
          cooldown: t.span,
          costs: s.costs ?? {},
          dormant: s.dormant ?? null,
          uses: s.uses ?? null,
          usesMax: s.uses_max ?? null,
          converted: s.converted ?? null,
          meta: s.meta ?? null,
          info: s,
        });
      }
    } else if (this.self !== null) {
      for (const w of this.self.weaves) {
        const t = this.timer(w.slot, this.selfAt + w.ready_in, 0);
        seats.push({
          slot: w.slot,
          key: w.key,
          name: w.name,
          icon: w.look ?? null,
          threads: [],
          readyAt: t.readyAt,
          cooldown: t.span,
          costs: w.costs,
          dormant: null,
          uses: null,
          usesMax: null,
          converted: null,
          meta: null,
          info: null,
        });
      }
    }
    for (let i = seats.length; i < count; i++) {
      seats.push({ slot: i, key: null, name: '', icon: null, threads: [], readyAt: 0, cooldown: 0, costs: {}, dormant: null, uses: null, usesMax: null, converted: null, meta: null, info: null });
    }
    return seats;
  }

  /**
   * A seat's cooldown: the self state's time left (sent every tick) when it reports the slot, else the weave slot
   * state's; its length is the seat's cooldown when that covers the time left, else the length followed.
   */
  private timer(slot: number, fallbackAt: number, cooldown: number): { readyAt: number; span: number } {
    if (this.self === null || !this.self.weaves.some((w) => w.slot === slot)) {
      return { readyAt: fallbackAt, span: cooldown > 0 ? cooldown : Math.max(0, fallbackAt - this.slotsAt) };
    }
    const c = this.cooldowns.get(slot);
    if (c === undefined) {
      return { readyAt: this.selfAt, span: cooldown };
    }
    return { readyAt: c.readyAt, span: cooldown > 0 && cooldown >= c.readyAt - this.selfAt - 0.05 ? cooldown : c.span };
  }

  // ------------------------------------------------------------ items

  /** The bag as a board: the inventory state's, or the self state's list laid out one cell each. */
  bag(columns: number): BoardInfo | null {
    if (this.inventory !== null) {
      return this.inventory.bag;
    }
    const self = this.self;
    if (self === null) {
      return null;
    }
    const cols = Math.max(1, columns);
    const items = self.bag.map((item, i) => ({ ...item, x: i % cols, y: Math.floor(i / cols), w: 1, h: 1 }));
    return { width: cols, height: Math.max(1, Math.ceil(Math.max(self.bag_capacity, self.bag.length) / cols)), items };
  }

  get equipment(): Record<string, ItemInfo> {
    return this.inventory?.equipment ?? this.self?.equipment ?? {};
  }

  findItem(uid: Int64): ItemInfo | null {
    const bag = this.inventory?.bag.items ?? this.self?.bag ?? [];
    const inBag = bag.find((i) => sameUid(i.uid, uid));
    if (inBag !== undefined) {
      return inBag;
    }
    const worn = Object.values(this.equipment).find((i) => sameUid(i.uid, uid));
    if (worn !== undefined) {
      return worn;
    }
    for (const c of this.inventory?.containers ?? []) {
      const found = c.board.items.find((i) => sameUid(i.uid, uid));
      if (found !== undefined) {
        return found;
      }
    }
    return null;
  }

  /** The wallet: the inventory state's, or the self state's currency map with names from content. */
  wallet(nameOf: (id: string) => string): CurrencyInfo[] {
    if (this.inventory !== null) {
      return this.inventory.wallet;
    }
    return Object.entries(this.self?.currency ?? {}).map(([id, amount]) => ({ id, name: nameOf(id), amount }));
  }

  // ------------------------------------------------------------ passives and pages

  get allocated(): string[] {
    return this.passives?.allocated ?? this.self?.passives ?? [];
  }

  points(currency: string): number {
    return this.passives?.points[currency] ?? (currency === 'passive' ? this.self?.passive_points ?? 0 : 0);
  }
}
