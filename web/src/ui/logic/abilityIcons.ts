import type { FaceInfo, ItemInfo } from '../../net/protocol';
import { num, str, type Obj } from './content';

export interface AbilityFace { art: string; color: string; badge: string | null }
interface Layer { path: string; fill?: string; stroke?: string; width?: number; transform?: number[] }
interface Artwork { id: string; art?: string; color?: string; badge?: string; layers?: Layer[] }
interface Rule { kinds?: string[]; tags: string[]; art: string }
const object = (v: unknown): Obj => typeof v === 'object' && v !== null && !Array.isArray(v) ? v as Obj : {};
const strings = (v: unknown): string[] => Array.isArray(v) ? v.filter((s): s is string => typeof s === 'string') : typeof v === 'string' ? v.split(' ').filter(Boolean) : [];
const color = (v: string | undefined): boolean => /^#([\da-f]{3}|[\da-f]{6}|[\da-f]{8})$/i.test(v ?? '');

/** The browser twin of AbilityIcons.cs: stable content identity, never name or ID keyword guesses. */
export class AbilityIcons {
  readonly art = new Map<string, Artwork>();
  readonly style: Obj;
  private readonly definitions = new Map<string, Obj>();

  constructor(kinds: Map<string, Obj[]> = new Map()) {
    for (const entry of kinds.get('ui_ability_icons') ?? []) this.art.set(str(entry.id), entry as unknown as Artwork);
    this.style = (kinds.get('ui_ability_styles') ?? []).find(s => s.id === 'default') ?? {};
    for (const kind of ['weaves', 'forms', 'glyphs'])
      for (const entry of kinds.get(kind) ?? []) this.definitions.set(`${kind}.${str(entry.id)}`, entry);
  }

  resolve(kind: string, id: string | null | undefined): AbilityFace {
    const definition = this.definitions.get(`${kind}.${id}`) ?? {};
    const meta = object(definition.meta);
    const explicit = str(meta.ability_icon);
    const alias = str(object(this.style.aliases)[kind]);
    let key = this.art.has(explicit) ? explicit : this.art.has(`${kind}.${id}`) ? `${kind}.${id}`
      : alias && this.art.has(`${alias}.${id}`) ? `${alias}.${id}` : '';
    if (!key) {
      if (kind === 'weaves' && definition.form && !this.art.has(str(definition.icon))) {
        const formFace = this.resolve('forms', str(definition.form));
        const ownColor = str(meta.icon_color, str(definition.color));
        return { ...formFace, color: color(ownColor) ? ownColor : formFace.color };
      }
      const ownIcon = str(definition.icon);
      const tags = new Set(strings(definition.tags));
      if (kind === 'weaves') for (const tag of strings(this.definitions.get(`forms.${str(definition.form)}`)?.tags)) tags.add(tag);
      const rules = (this.style.rules ?? []) as unknown as Rule[];
      key = this.art.has(ownIcon) ? ownIcon : rules.find(r => (r.kinds ?? ['weaves', 'forms']).includes(kind) && r.tags.some(t => tags.has(t)))?.art
        ?? str(object(this.style.fallbacks)[kind], 'arcane');
    }
    let tint = str(meta.icon_color, str(definition.color));
    let badge = str(object(this.style.badges)[kind]) || null;
    const seen = new Set<string>();
    let first = true;
    while (!seen.has(key) && this.art.has(key)) {
      seen.add(key);
      const entry = this.art.get(key)!;
      if (first) tint = str(meta.icon_color, entry.color ?? tint);
      badge = entry.badge ?? badge;
      first = false;
      if (!entry.art) break;
      key = entry.art;
    }
    return { art: key, color: color(tint) ? tint : str(this.style.default_color, '#b8b8b8'), badge };
  }

  item(item: ItemInfo): AbilityFace | null {
    return item.glyph ? this.resolve('glyphs', item.glyph.id) : item.form
      ? this.resolve(item.form.weave ? 'weaves' : 'forms', item.form.weave ?? item.form.id) : null;
  }

  seat(key: string | null, form: string | null | undefined, preset: string | null | undefined, converted: FaceInfo | null): AbilityFace {
    if (converted) {
      if (converted.weave) return this.resolve('weaves', converted.weave);
      if (converted.form) return this.resolve('forms', converted.form);
      return { art: converted.icon && this.art.has(converted.icon) ? converted.icon : str(object(this.style.fallbacks).weaves, 'arcane'), color: str(this.style.default_color, '#b8b8b8'), badge: null };
    }
    return preset || (key && this.definitions.has(`weaves.${key}`)) ? this.resolve('weaves', preset ?? key) : this.resolve('forms', form);
  }

  svg(face: AbilityFace): string {
    const e = (s: string) => s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&apos;');
    const n = (v: number) => String(Number(v.toFixed(15)));
    const s = this.style;
    const background = str(s.background, '#111820'), ink = str(s.ink, '#f4ebd7'), rim = str(s.rim, '#070c12');
    const layers = (id: string, tint: string, foreground: string, outline: string): string => {
      const seen = new Set<string>();
      while (!seen.has(id) && this.art.get(id)?.art) { seen.add(id); id = this.art.get(id)!.art!; }
      return (this.art.get(id)?.layers ?? []).map(layer => {
      const paint = (token?: string) => e(token === 'tint' ? tint : token === 'ink' ? foreground : token === 'shade' ? outline : 'none');
      const transform = layer.transform?.length === 6 ? ` transform="matrix(${layer.transform.map(n).join(' ')})"` : '';
      const path = e(layer.path), width = layer.width ?? num(s.line_width, 1.35);
      return `<g${transform}><path d="${path}" fill="${paint(layer.fill)}"/>` + (layer.stroke
        ? `<path d="${path}" fill="none" stroke="${e(outline)}" stroke-width="${n(width + num(s.rim_width, .8))}"/><path d="${path}" fill="none" stroke="${paint(layer.stroke)}" stroke-width="${n(width)}"/>` : '') + '</g>';
      }).join('');
    };
    let svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" stroke-linecap="round" stroke-linejoin="round"><rect width="24" height="24" fill="${e(background)}"/><rect width="24" height="24" fill="${e(face.color)}" opacity="${n(num(s.tint_alpha, .16))}"/>` + layers(face.art, face.color, ink, rim);
    if (face.badge && this.art.has(face.badge)) {
      const badgeBackground = str(s.badge_background, '#21182e'), badgeColor = str(s.badge_color, '#ffe3a0');
      svg += `<g transform="scale(.34)"><rect width="24" height="24" rx="4" fill="${e(badgeBackground)}"/>` + layers(face.badge, badgeColor, badgeColor, badgeBackground) + '</g>';
    }
    return svg + '</svg>';
  }
}
