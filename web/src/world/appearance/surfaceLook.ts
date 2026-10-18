import type { Obj } from '../../ui/logic/content';
import { num, str } from '../../ui/logic/content';
import { CellFlag, type CellFlagName } from '../chunks';
import { darken, lerp, parseHex, type Rgb } from '../palette';
import { hasTag } from './looks';

/**
 * The colour of a ground cell from the surfaces content, the twin of the desktop's World/Logic/SurfaceLook.cs:
 * structure cells take the first matching structure rule (with a side colour for their skirts and a roof
 * colour that raises a pitched cap); other cells take the first matching ground rule, then every matching
 * tint in order, then a seeded value jitter in patches. Mixing happens in sRGB, as the desktop does.
 */

interface Rule {
  tags: string[];
  flags: number;
  color: Rgb | null;
  mix: number;
  darken: number;
  side: Rgb | null;
  roof: Rgb | null;
}

const isObj = (v: unknown): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);
const strings = (v: unknown): string[] => (Array.isArray(v) ? v.map((x) => str(x as never)).filter((s) => s.length > 0) : typeof v === 'string' ? v.split(' ').filter((s) => s.length > 0) : []);
const colorOrNull = (v: unknown): Rgb | null => (typeof v === 'string' && /^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?([0-9a-fA-F]{2})?$/.test(v) ? parseHex(v) : null);

function compile(o: unknown): Rule {
  const r = isObj(o) ? o : {};
  let flags = 0;
  for (const name of strings(r.flags)) {
    const bit = CellFlag[name.toLowerCase() as CellFlagName];
    if (bit !== undefined) {
      flags |= bit;
    }
  }
  return { tags: strings(r.tags), flags, color: colorOrNull(r.color), mix: num(r.mix, 1), darken: num(r.darken, 0), side: colorOrNull(r.side), roof: colorOrNull(r.roof) };
}

const matches = (rule: Rule, tags: ReadonlySet<string>, flags: number): boolean => (flags & rule.flags) === rule.flags && rule.tags.every((t) => hasTag(tags, t));

/** A hash of two integers to a fraction from zero to one, the desktop's SurfaceLook.Hash. */
export function hash2(x: number, y: number): number {
  let h = Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h & 0xffffff) / 0x1000000;
}

export class SurfaceLook {
  private readonly ground: Rule[];
  private readonly structureRules: Rule[];
  private readonly tints: Rule[];
  private readonly layers: Rule[];
  private readonly fallback: Rgb;
  private readonly structureFallback: Rgb;
  private readonly layerFallback: Rgb;
  readonly skirtDarken: number;
  readonly variationValue: number;
  readonly variationScale: number;
  readonly water: Rgb;
  readonly waterDeep: Rgb;
  readonly waterLight: Rgb;
  readonly waterOpacity: number;
  readonly waterRipple: number;
  readonly waterSpeed: number;

  constructor(def: Obj | undefined) {
    const d = def ?? {};
    this.ground = (Array.isArray(d.ground) ? d.ground : []).map(compile);
    this.structureRules = (Array.isArray(d.structure) ? d.structure : []).map(compile);
    this.tints = (Array.isArray(d.tints) ? d.tints : []).map(compile);
    this.layers = (Array.isArray(d.layers) ? d.layers : []).map(compile);
    this.fallback = colorOrNull(d.fallback) ?? [0.45, 0.55, 0.35];
    this.structureFallback = colorOrNull(d.structure_fallback) ?? [0.6, 0.58, 0.54];
    this.layerFallback = colorOrNull(d.layer_fallback) ?? [0.6, 0.58, 0.55];
    this.skirtDarken = num(d.skirt_darken, 0.3);
    const variation = isObj(d.variation) ? d.variation : {};
    this.variationValue = num(variation.value, 0.05);
    this.variationScale = Math.max(0.5, num(variation.scale, 3));
    const water = isObj(d.water) ? d.water : {};
    this.water = colorOrNull(water.color) ?? [0.2, 0.45, 0.7];
    this.waterDeep = colorOrNull(water.deep) ?? [0.12, 0.25, 0.36];
    this.waterLight = colorOrNull(water.light) ?? [0.6, 0.84, 0.85];
    this.waterOpacity = num(water.opacity, 0.6);
    this.waterRipple = num(water.ripple, 0.5);
    this.waterSpeed = num(water.speed, 0.4);
  }

  /** A structure cell's top, side and roof colours, or null for a cell that is not a structure. */
  structure(tags: ReadonlySet<string>, flags: number): { top: Rgb; side: Rgb; roof: Rgb | null } | null {
    if ((flags & CellFlag.structure) === 0 && !tags.has('structure')) {
      return null;
    }
    const rule = this.structureRules.find((r) => matches(r, tags, flags));
    const top = rule?.color ?? this.structureFallback;
    return { top, side: rule?.side ?? darken(top, this.skirtDarken), roof: rule?.roof ?? null };
  }

  /** The colour of a cell before its jitter. */
  cell(tags: ReadonlySet<string>, flags: number): Rgb {
    const s = this.structure(tags, flags);
    if (s !== null) {
      return s.top;
    }
    let color = this.ground.find((r) => r.color !== null && matches(r, tags, flags))?.color ?? this.fallback;
    for (const tint of this.tints) {
      if (!matches(tint, tags, flags)) {
        continue;
      }
      if (tint.color !== null) {
        color = lerp(color, tint.color, tint.mix);
      }
      if (tint.darken > 0) {
        color = darken(color, tint.darken);
      }
    }
    return color;
  }

  layer(tags: ReadonlySet<string>): Rgb {
    return this.layers.find((r) => matches(r, tags, 0))?.color ?? this.layerFallback;
  }

  skirt(color: Rgb): Rgb {
    return darken(color, this.skirtDarken);
  }

  /** The seeded value jitter of a cell: smooth patches a few cells wide from the cell's world position; structures and roads stay even. */
  jitter(cellX: number, cellY: number, flags: number): number {
    if (this.variationValue <= 0 || (flags & (CellFlag.structure | CellFlag.road)) !== 0) {
      return 1;
    }
    const fx = cellX / this.variationScale;
    const fy = cellY / this.variationScale;
    const x0 = Math.floor(fx);
    const y0 = Math.floor(fy);
    const smooth = (t: number) => t * t * (3 - 2 * t);
    const sx = smooth(fx - x0);
    const sy = smooth(fy - y0);
    const a = hash2(x0, y0);
    const b = hash2(x0 + 1, y0);
    const c = hash2(x0, y0 + 1);
    const d = hash2(x0 + 1, y0 + 1);
    const v = a + (b - a) * sx + (c + (d - c) * sx - (a + (b - a) * sx)) * sy;
    const fine = hash2(cellX * 7 + 3, cellY * 11 + 5) * 0.3;
    return 1 + ((v - 0.5) * 2 * 0.7 + (fine - 0.15) * 2) * this.variationValue;
  }
}
