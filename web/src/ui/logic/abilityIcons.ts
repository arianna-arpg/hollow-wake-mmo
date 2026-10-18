import type { FaceInfo, ItemInfo } from '../../net/protocol';
import { num, str, type Obj } from './content';

export interface AbilityFace { art: string; color: string; badge: string | null; tone?: string | null }
interface Layer { path: string; fill?: string; stroke?: string; width?: number; transform?: number[] }
interface Artwork { id: string; art?: string; color?: string; badge?: string; layers?: Layer[] }
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
    for (const kind of new Set([...Object.values(object(this.style.roles)), ...Object.keys(object(this.style.parents)), ...Object.values(object(this.style.parents)).map(p => object(p).kind), ...Object.keys(object(this.style.fallbacks)), ...((this.style.rules ?? []) as Obj[]).flatMap(r => strings(r.kinds))].map(v => str(v))))
      for (const entry of kinds.get(kind) ?? []) this.definitions.set(`${kind}.${str(entry.id)}`, entry);
  }

  resolve(kind: string, id: string | null | undefined, threads?: readonly string[] | null): AbilityFace {
    const definition = this.definitions.get(kind + '.' + id) ?? {};
    const chain: Array<{ kind: string; id: string | null | undefined; definition: Obj }> = [];
    const parents = new Set<string>();
    let currentKind = kind, currentId = id;
    while (!parents.has(currentKind + '.' + currentId)) {
      parents.add(currentKind + '.' + currentId);
      const current = this.definitions.get(currentKind + '.' + currentId) ?? {};
      chain.push({ kind: currentKind, id: currentId, definition: current });
      const parent = object(object(this.style.parents)[currentKind]);
      const parentId = str(current[str(parent.field)]);
      if (!parentId) break;
      currentKind = str(parent.kind); currentId = parentId;
    }
    const tags = new Set(chain.flatMap(c => strings(c.definition.tags)));
    let threadColor = '';
    for (const thread of threads ?? strings(definition.threads)) {
      tags.add(thread);
      const t = this.definitions.get(this.role('thread') + '.' + thread) ?? {};
      for (const tag of strings(t.tags)) tags.add(tag);
      threadColor ||= str(t.color);
    }
    const bound = (k: string, i: string | null | undefined, d: Obj): string => {
      const explicit = str(object(d.meta).ability_icon), ownIcon = str(d.icon);
      return this.art.has(explicit) ? explicit : this.art.has(k + '.' + i) ? k + '.' + i : this.art.has(ownIcon) ? ownIcon : '';
    };
    const rules = (this.style.rules ?? []) as Obj[];
    let key = bound(kind, id, definition)
      || str(rules.find(r => strings(r.kinds).includes(kind) && strings(r.tags).some(t => [...tags].some(tag => tag === t || tag.startsWith(t + '.'))))?.art)
      || chain.slice(1).map(c => bound(c.kind, c.id, c.definition)).find(Boolean)
      || str(object(this.style.fallbacks)[kind], str(this.style.default_art));
    let tint = str(object(definition.meta).icon_color, str(definition.color, threadColor));
    let badge = str(object(this.style.badges)[kind]) || null;
    const seen = new Set<string>();
    while (!seen.has(key) && this.art.has(key)) {
      seen.add(key);
      const entry = this.art.get(key)!;
      tint ||= entry.color ?? '';
      badge = entry.badge ?? badge;
      if (!entry.art) break;
      key = entry.art;
    }
    return { art: key, color: color(tint) ? tint : str(this.style.default_color, '#b8b8b8'), badge };
  }

  private role(role: string): string { return str(object(this.style.roles)[role]); }

  glyph(id: string): AbilityFace { return this.resolve(this.role('glyph'), id); }

  item(item: ItemInfo): AbilityFace | null {
    return item.glyph ? this.glyph(item.glyph.id) : item.form
      ? this.resolve(this.role(item.form.weave ? 'weave' : 'form'), item.form.weave ?? item.form.id) : null;
  }

  seat(key: string | null, form: string | null | undefined, preset: string | null | undefined, converted: FaceInfo | null, threads?: readonly string[]): AbilityFace {
    if (converted) {
      if (converted.weave) return this.resolve(this.role('weave'), converted.weave);
      if (converted.form) return this.resolve(this.role('form'), converted.form, threads);
      return { art: converted.icon && this.art.has(converted.icon) ? converted.icon : str(this.style.default_art), color: str(this.style.default_color, '#b8b8b8'), badge: null };
    }
    return preset || (key && this.definitions.has(this.role('weave') + '.' + key)) ? this.resolve(this.role('weave'), preset ?? key, threads) : this.resolve(this.role('form'), form, threads);
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
      const transform = layer.transform?.length === 6 ? ` transform="matrix(${layer.transform.map(v => n(num(v))).join(' ')})"` : '';
      const path = e(layer.path), width = num(layer.width, num(s.line_width, 1.35));
      return `<g${transform}><path d="${path}" fill="${paint(layer.fill)}"/>` + (layer.stroke
        ? `<path d="${path}" fill="none" stroke="${e(outline)}" stroke-width="${n(width + num(s.rim_width, .8))}"/><path d="${path}" fill="none" stroke="${paint(layer.stroke)}" stroke-width="${n(width)}"/>` : '') + '</g>';
      }).join('');
    };
    let svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" stroke-linecap="round" stroke-linejoin="round"><rect width="24" height="24" fill="${e(background)}"/><rect width="24" height="24" fill="${e(face.tone ?? face.color)}" opacity="${n(num(s.tint_alpha, .16))}"/>` + layers(face.art, face.tone ?? face.color, face.tone ?? ink, rim);
    if (face.badge && this.art.has(face.badge)) {
      const badgeBackground = str(s.badge_background, '#21182e'), badgeColor = str(s.badge_color, '#ffe3a0');
      svg += `<g transform="scale(.34)"><rect width="24" height="24" rx="4" fill="${e(badgeBackground)}"/>` + layers(face.badge, face.tone ?? badgeColor, face.tone ?? badgeColor, badgeBackground) + '</g>';
    }
    return svg + '</svg>';
  }
}
