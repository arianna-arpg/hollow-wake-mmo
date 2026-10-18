import type { ItemInfo } from '../../net/protocol';
import type { Obj } from '../../ui/logic/content';
import { str, num } from '../../ui/logic/content';
import { parseHex, type Rgb } from '../palette';

/**
 * The appearance content as the browser reads it (docs/design/appearance.md), the twin of the desktop's
 * World/Logic/Looks.cs: looks by id and by tags, gear by slot and item class, the default surfaces, world
 * and motion entries, pattern tags through their extends chain, and the relation of each faction to the
 * players. Read from the bundled content, never from the wire.
 */

export interface AppearanceVariation {
  hue: number;
  value: number;
  scale: number;
}

export interface AppearanceDef {
  id: string;
  match: string[];
  priority: number;
  body: string;
  slot: string | null;
  shape: Record<string, number>;
  palette: Record<string, string>;
  features: string[];
  gear: Record<string, string>;
  variation: AppearanceVariation;
  card: string;
  scale: number;
  glow: string | null;
}

export interface ItemLook {
  itemClass: string | null;
  tags: string[];
}

const isObj = (v: unknown): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);
const strings = (v: unknown): string[] => (Array.isArray(v) ? v.map((x) => str(x as never)).filter((s) => s.length > 0) : typeof v === 'string' ? v.split(' ').filter((s) => s.length > 0) : []);
const numMap = (v: unknown): Record<string, number> => (isObj(v) ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, num(x)])) : {});
const strMap = (v: unknown): Record<string, string> => (isObj(v) ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, str(x)])) : {});

/** Whether a hierarchical tag set holds a tag: a set with weapon.sword satisfies weapon, as the core's TagSet does. */
export function hasTag(tags: ReadonlySet<string>, tag: string): boolean {
  if (tags.has(tag)) {
    return true;
  }
  const prefix = tag + '.';
  for (const t of tags) {
    if (t.startsWith(prefix)) {
      return true;
    }
  }
  return false;
}

function readAppearance(o: Obj): AppearanceDef {
  const variation = isObj(o.variation) ? o.variation : {};
  return {
    id: str(o.id),
    match: strings(o.match),
    priority: num(o.priority),
    body: str(o.body),
    slot: typeof o.slot === 'string' ? o.slot : null,
    shape: numMap(o.shape),
    palette: strMap(o.palette),
    features: strings(o.features),
    gear: strMap(o.gear),
    variation: { hue: num(variation.hue, 0.02), value: num(variation.value, 0.06), scale: num(variation.scale, 0.05) },
    card: str(o.card, 'allow'),
    scale: num(o.scale, 1),
    glow: typeof o.glow === 'string' ? o.glow : null,
  };
}

function byPriority(a: AppearanceDef, b: AppearanceDef): number {
  return b.priority - a.priority || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
}

/** A stable hash of a key and a salt to a fraction from zero to one, the desktop's Variation.Unit on 64-bit arithmetic. */
export function variationUnit(key: number | string, salt: number): number {
  let h = BigInt.asUintN(64, BigInt(typeof key === 'number' ? Math.trunc(key) : key) * 0x9e3779b97f4a7c15n + BigInt(salt) * 0xbf58476d1ce4e5b9n);
  h ^= h >> 31n;
  h = BigInt.asUintN(64, h * 0x94d049bb133111ebn);
  h ^= h >> 29n;
  return Number(h & 0xffffffn) / 0x1000000;
}

export const variationSigned = (key: number | string, salt: number): number => variationUnit(key, salt) * 2 - 1;

export function toHsv(c: Rgb): [number, number, number] {
  const max = Math.max(c[0], c[1], c[2]);
  const min = Math.min(c[0], c[1], c[2]);
  const d = max - min;
  let h = 0;
  if (d > 1e-6) {
    if (max === c[0]) {
      h = ((c[1] - c[2]) / d + 6) % 6;
    } else if (max === c[1]) {
      h = (c[2] - c[0]) / d + 2;
    } else {
      h = (c[0] - c[1]) / d + 4;
    }
    h /= 6;
  }
  return [h, max <= 1e-6 ? 0 : d / max, max];
}

export function fromHsv(h: number, s: number, v: number): Rgb {
  const i = Math.floor(h * 6) % 6;
  const f = h * 6 - Math.floor(h * 6);
  const p = v * (1 - s);
  const q = v * (1 - f * s);
  const t = v * (1 - (1 - f) * s);
  switch (i) {
    case 0:
      return [v, t, p];
    case 1:
      return [q, v, p];
    case 2:
      return [p, v, t];
    case 3:
      return [p, q, v];
    case 4:
      return [t, p, v];
    default:
      return [v, p, q];
  }
}

/** A palette colour shifted by the entry's hue and value jitter for this key. */
export function jitter(color: Rgb, variation: AppearanceVariation, key: number | string): Rgb {
  const hue = variationSigned(key, 1) * variation.hue;
  const value = 1 + variationSigned(key, 2) * variation.value;
  const [h, s, v] = toHsv(color);
  return fromHsv((h + hue + 1) % 1, s, Math.min(1, Math.max(0, v * value)));
}

export const scaleOf = (variation: AppearanceVariation, key: number | string): number => 1 + variationSigned(key, 3) * variation.scale;

/** The motion timings with their defaults, read once. */
export class MotionLook {
  private readonly groups: Record<string, Record<string, number>> = {};
  readonly contact: { opacity: number; hostile: string; friendly: string; neutral: string };

  constructor(entry: Obj | undefined) {
    for (const g of ['walk', 'idle', 'swing', 'cast', 'hit', 'death', 'camera', 'combat_text', 'labels']) {
      this.groups[g] = numMap(entry?.[g]);
    }
    const contact = isObj(entry?.contact) ? entry.contact : {};
    this.contact = { opacity: num(contact.opacity, 0.35), hostile: str(contact.hostile, 'teeth'), friendly: str(contact.friendly, 'ticks'), neutral: str(contact.neutral, 'plain') };
  }

  get(group: string, name: string, fallback: number): number {
    const v = this.groups[group]?.[name];
    return v === undefined ? fallback : v;
  }

  walk = (name: string, fallback: number) => this.get('walk', name, fallback);
  idle = (name: string, fallback: number) => this.get('idle', name, fallback);
  swing = (name: string, fallback: number) => this.get('swing', name, fallback);
  cast = (name: string, fallback: number) => this.get('cast', name, fallback);
  hit = (name: string, fallback: number) => this.get('hit', name, fallback);
  death = (name: string, fallback: number) => this.get('death', name, fallback);
  camera = (name: string, fallback: number) => this.get('camera', name, fallback);
  combatText = (name: string, fallback: number) => this.get('combat_text', name, fallback);
  labels = (name: string, fallback: number) => this.get('labels', name, fallback);
}

export class AppearanceCatalog {
  private readonly byId = new Map<string, AppearanceDef>();
  private readonly bodiesByTags: AppearanceDef[] = [];
  private readonly gear: AppearanceDef[] = [];
  private readonly patterns = new Map<string, Obj>();
  private readonly patternTagCache = new Map<string, Set<string>>();
  private readonly items = new Map<string, Obj>();
  private readonly pois = new Map<string, Obj>();
  private readonly callings: Obj[] = [];
  private readonly relations = new Map<string, string>();
  readonly surfaces: Obj | undefined;
  readonly world: Obj | undefined;
  readonly motion: MotionLook;

  constructor(kinds: Map<string, Obj[]>) {
    const defs = (kinds.get('ui_appearances') ?? []).map(readAppearance).sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
    for (const def of defs) {
      this.byId.set(def.id, def);
      if (def.slot !== null) {
        this.gear.push(def);
      } else if (def.match.length > 0) {
        this.bodiesByTags.push(def);
      }
    }
    this.bodiesByTags.sort(byPriority);
    this.gear.sort(byPriority);
    const first = (kind: string) => {
      const entries = kinds.get(kind) ?? [];
      return entries.find((e) => str(e.id) === 'default') ?? entries[0];
    };
    this.surfaces = first('ui_surfaces');
    this.world = first('ui_world');
    this.motion = new MotionLook(first('ui_motion'));
    for (const p of kinds.get('patterns') ?? []) {
      this.patterns.set(str(p.id), p);
    }
    for (const i of kinds.get('items') ?? []) {
      this.items.set(str(i.id), i);
    }
    for (const p of kinds.get('pois') ?? []) {
      this.pois.set(str(p.id), p);
    }
    this.callings.push(...(kinds.get('callings') ?? []));
    const factions = kinds.get('factions') ?? [];
    const players = factions.find((f) => str(f.id) === 'players');
    for (const f of factions) {
      const id = str(f.id);
      const theirs = isObj(f.relations) ? f.relations : {};
      const ours = isObj(players?.relations) ? players.relations : {};
      this.relations.set(id, str(ours[id] ?? theirs.players ?? f.default, 'neutral'));
    }
  }

  get all(): AppearanceDef[] {
    return [...this.byId.values()];
  }

  byIdOf(id: string): AppearanceDef | undefined {
    return this.byId.get(id);
  }

  /** The relation of a faction toward the players (hostile, friendly, allied, neutral). */
  relationOf(faction: string | null | undefined): string {
    return (faction && this.relations.get(faction)) || 'neutral';
  }

  /** A pattern's tags with those of everything it extends, plus its id as a tag. */
  patternTags(patternId: string | null | undefined): Set<string> {
    if (!patternId) {
      return new Set();
    }
    const cached = this.patternTagCache.get(patternId);
    if (cached) {
      return cached;
    }
    const tags = new Set<string>([patternId]);
    const seen = new Set<string>();
    let cur = this.patterns.get(patternId);
    while (cur && !seen.has(str(cur.id))) {
      seen.add(str(cur.id));
      for (const t of strings(cur.tags)) {
        tags.add(t);
      }
      cur = typeof cur.extends === 'string' ? this.patterns.get(cur.extends) : undefined;
    }
    this.patternTagCache.set(patternId, tags);
    return tags;
  }

  /** The body entry for an entity: by its look id, then by the tags of its pattern (or its place), then the fallbacks. */
  resolve(look: string | null | undefined, patternId: string | null | undefined, extraTags?: Iterable<string>): AppearanceDef | null {
    if (look) {
      const byLook = this.byId.get(look);
      if (byLook && byLook.slot === null) {
        return byLook;
      }
      if (look.startsWith('poi:')) {
        const poi = this.pois.get(look.slice(4));
        return this.byTags(new Set(strings(poi?.tags))) ?? this.byId.get('marker') ?? null;
      }
      if (look.startsWith('drop:')) {
        return this.byId.get('drop') ?? null;
      }
      if (look === 'projectile' || look === 'field' || look === 'fount') {
        return this.byId.get(look) ?? null;
      }
    }
    const tags = new Set(this.patternTags(patternId));
    for (const t of extraTags ?? []) {
      tags.add(t);
    }
    if (patternId) {
      const byPattern = this.byId.get(patternId);
      if (byPattern && byPattern.slot === null) {
        return byPattern;
      }
    }
    return this.byTags(tags);
  }

  private byTags(tags: ReadonlySet<string>): AppearanceDef | null {
    for (const def of this.bodiesByTags) {
      if (def.match.every((t) => hasTag(tags, t))) {
        return def;
      }
    }
    return null;
  }

  /** The gear entry for an item worn in a slot, by its class (hierarchical) and tags. */
  gearFor(slot: string, itemClass: string | null, itemTags: Iterable<string> | null): AppearanceDef | null {
    if (itemClass === null && itemTags === null) {
      return null;
    }
    const tags = new Set<string>(itemTags ?? []);
    if (itemClass) {
      tags.add(itemClass);
    }
    for (const def of this.gear) {
      if (def.slot === slot && def.match.length > 0 && def.match.every((t) => hasTag(tags, t))) {
        return def;
      }
    }
    return null;
  }

  /** A gear entry for a dropped or carried item base, in whichever slot it belongs to; a parcel for anything else. */
  gearForItem(itemBase: string): AppearanceDef | null {
    const item = this.items.get(itemBase);
    if (!item) {
      return null;
    }
    const tags = new Set(strings(item.tags));
    tags.add(str(item.class));
    for (const def of this.gear) {
      if (def.match.length > 0 && def.match.every((t) => hasTag(tags, t))) {
        return def;
      }
    }
    return this.byId.get('gear_sack') ?? null;
  }

  itemLook(itemBase: string): ItemLook | null {
    const item = this.items.get(itemBase);
    return item ? { itemClass: str(item.class) || null, tags: strings(item.tags) } : null;
  }

  /** The default gear a body wears: the entry's gear map, a monster's grants over it, else a Calling's starting items. */
  defaultGear(body: AppearanceDef, patternId: string | null | undefined): Map<string, ItemLook> {
    const gear = new Map<string, ItemLook>();
    for (const [slot, itemClass] of Object.entries(body.gear)) {
      gear.set(slot, { itemClass, tags: splitClass(itemClass) });
    }
    if (patternId) {
      const seen = new Set<string>();
      let cur = this.patterns.get(patternId);
      while (cur && !seen.has(str(cur.id))) {
        seen.add(str(cur.id));
        const components = isObj(cur.components) ? cur.components : {};
        const equipment = isObj(components.equipment) ? components.equipment : {};
        for (const grant of Array.isArray(equipment.grants) ? equipment.grants : []) {
          const itemId = isObj(grant) ? str(grant.item) : '';
          const item = this.items.get(itemId);
          if (item && typeof item.slot === 'string') {
            gear.set(item.slot, { itemClass: str(item.class) || null, tags: strings(item.tags) });
          }
        }
        cur = typeof cur.extends === 'string' ? this.patterns.get(cur.extends) : undefined;
      }
    }
    if (gear.size === 0) {
      const calling = this.callings.find((c) => str(c.look) === body.id || str(c.id) === body.id);
      for (const grant of Array.isArray(calling?.items) ? calling.items : []) {
        const item = this.items.get(isObj(grant) ? str(grant.item) : '');
        if (item && typeof item.slot === 'string' && !gear.has(item.slot)) {
          gear.set(item.slot, { itemClass: str(item.class) || null, tags: strings(item.tags) });
        }
      }
    }
    return gear;
  }

  /**
   * What a body wears, in order of preference: the owner's own equipment (the self's body, from its self state), the
   * public descriptor the server sends for a player (EntityState.appearance), else the defaults of defaultGear. A
   * descriptor that names nothing falls to the defaults, as does a state without one: the server omits it when nothing
   * is worn, and a server that predates it never sends one.
   */
  wornBy(body: AppearanceDef, patternId: string | null | undefined, appearance: readonly string[] | undefined, selfEquipment: Record<string, ItemInfo> | undefined): Map<string, ItemLook> {
    if (selfEquipment && Object.keys(selfEquipment).length > 0) {
      const own = new Map<string, ItemLook>();
      for (const [slot, item] of Object.entries(selfEquipment)) {
        const fromBase = this.itemLook(item.base);
        own.set(slot, { itemClass: item.class ?? fromBase?.itemClass ?? null, tags: fromBase?.tags ?? [] });
      }
      return own;
    }
    if (appearance && appearance.length > 0) {
      const worn = wornFromDescriptor(appearance);
      if (worn.size > 0) {
        return worn;
      }
    }
    return this.defaultGear(body, patternId);
  }
}

/**
 * The gear a public appearance descriptor says a player wears (docs/design/networking-and-persistence.md): one entry per
 * slot, `slot=class tag tag ...`, the class split as an entry's own gear is so its trailing word reads as a tag. Entries
 * without a slot or a class are skipped.
 */
export function wornFromDescriptor(appearance: readonly string[]): Map<string, ItemLook> {
  const worn = new Map<string, ItemLook>();
  for (const entry of appearance) {
    const eq = entry.indexOf('=');
    if (eq <= 0) {
      continue;
    }
    const words = entry.slice(eq + 1).split(' ').filter((w) => w.length > 0);
    if (words.length === 0) {
      continue;
    }
    worn.set(entry.slice(0, eq), { itemClass: words[0], tags: [...splitClass(words[0]), ...words.slice(1)] });
  }
  return worn;
}

/** An item class as tags: armor.chest.heavy carries armor, armor.chest and the trailing word as its own tag. */
function splitClass(itemClass: string): string[] {
  const parts = itemClass.split('.');
  return parts.length >= 3 ? [itemClass, parts[parts.length - 1], parts.slice(0, 2).join('.')] : [itemClass];
}

/** A colour from a palette entry, falling back when it is missing or malformed. */
export function paletteColor(palette: Record<string, string>, name: string, fallback: string): Rgb {
  const text = palette[name] ?? fallback;
  return /^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?([0-9a-fA-F]{2})?$/.test(text) ? parseHex(text) : parseHex(fallback);
}
