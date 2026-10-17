import { forStatus, lookOf, metric, resolveIcon, type ContentNames, type UiContent, type UiIconSet } from './logic/content';

/** A colour as numbers from zero to one, read from theme content (#rgb, #rrggbb, #rrggbbaa). */
export interface Rgba {
  r: number;
  g: number;
  b: number;
  a: number;
}

export function parseColor(text: string | undefined, fallback: Rgba = { r: 1, g: 1, b: 1, a: 1 }): Rgba {
  if (text === undefined || !/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(text)) {
    return fallback;
  }
  const hex = text.slice(1);
  if (hex.length === 3) {
    return { r: parseInt(hex[0], 16) / 15, g: parseInt(hex[1], 16) / 15, b: parseInt(hex[2], 16) / 15, a: 1 };
  }
  const v = (i: number) => parseInt(hex.slice(i, i + 2), 16) / 255;
  return { r: v(0), g: v(2), b: v(4), a: hex.length === 8 ? v(6) : 1 };
}

export const css = (c: Rgba, alpha = c.a): string => `rgba(${Math.round(c.r * 255)}, ${Math.round(c.g * 255)}, ${Math.round(c.b * 255)}, ${Math.round(alpha * 1000) / 1000})`;
export const lerp = (a: Rgba, b: Rgba, t: number): Rgba => ({ r: a.r + (b.r - a.r) * t, g: a.g + (b.g - a.g) * t, b: a.b + (b.b - a.b) * t, a: a.a + (b.a - a.a) * t });
export const darken = (c: Rgba, by: number): Rgba => ({ ...c, r: c.r * (1 - by), g: c.g * (1 - by), b: c.b * (1 - by) });
export const lighten = (c: Rgba, by: number): Rgba => ({ ...c, r: c.r + (1 - c.r) * by, g: c.g + (1 - c.g) * by, b: c.b + (1 - c.b) * by });
export const luminance = (c: Rgba): number => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

const SVG = 'http://www.w3.org/2000/svg';

/**
 * The theme content turned into what the page draws with: CSS custom properties on the interface root,
 * fonts, a frame class per frame style, surface textures as SVG noise, and glyphs from the icon set. Every
 * view reads its look from here, so a theme change is one new skin. The desktop client's UI/Skin.cs.
 */
export class Skin {
  readonly brass: boolean;

  constructor(
    readonly ui: UiContent,
    readonly names: ContentNames,
  ) {
    this.brass = ui.theme.frame === 'brass';
  }

  get theme() {
    return this.ui.theme;
  }

  get icons(): UiIconSet {
    return this.ui.icons;
  }

  surface(name: string, fallback = '#ffffff'): Rgba {
    return parseColor(this.theme.surface[name] ?? fallback);
  }

  s(name: string, alpha?: number): string {
    const c = this.surface(name);
    return css(c, alpha ?? c.a);
  }

  mat(name: string, fallback: number): number {
    return this.theme.material[name] ?? fallback;
  }

  metric(name: string, fallback: number): number {
    return metric(this.ui.layout, name, fallback);
  }

  fill(resource: string): Rgba {
    return parseColor((this.theme.resources[resource] ?? this.theme.resourceFallback).fill);
  }

  deep(resource: string): Rgba {
    return parseColor((this.theme.resources[resource] ?? this.theme.resourceFallback).deep);
  }

  thread(id: string): Rgba {
    return parseColor(this.theme.threads[id], this.surface('ink'));
  }

  rarity(rarity: string | undefined | null): Rgba {
    return parseColor(this.theme.rarities[rarity ?? 'normal'] ?? this.theme.rarities.normal, this.surface('ink'));
  }

  notice(level: string): Rgba {
    return parseColor(this.theme.notices[level] ?? this.theme.notices.info, this.surface('ink'));
  }

  state(name: string, fallback: string): Rgba {
    return parseColor(this.theme.states[name] ?? fallback);
  }

  font(role: string): string {
    const f = this.theme.fonts[role] ?? this.theme.fonts.body;
    return (f?.families ?? ['serif']).map((family) => (/^[a-z-]+$/.test(family) ? family : `"${family}"`)).join(', ');
  }

  fontSize(role: string): number {
    return (this.theme.fonts[role] ?? this.theme.fonts.body)?.size ?? 14;
  }

  fontWeight(role: string): number {
    return (this.theme.fonts[role] ?? this.theme.fonts.body)?.weight ?? 400;
  }

  /** A canvas font string for a role at a size. */
  canvasFont(role: string, size?: number): string {
    return `${this.fontWeight(role)} ${size ?? this.fontSize(role)}px ${this.font(role)}`;
  }

  /** Ink that reads on a colour. */
  inkOn(background: Rgba): string {
    return luminance(background) > 0.55 ? css(darken(this.surface('ink'), luminance(this.surface('ink')) < 0.5 ? 0 : 0.85)) : this.s('label');
  }

  /** The theme as CSS custom properties, set on the interface's root so every rule reads it. */
  apply(root: HTMLElement): void {
    const set = (name: string, value: string) => root.style.setProperty(name, value);
    for (const [name, value] of Object.entries(this.theme.surface)) {
      set(`--${name.replace(/_/g, '-')}`, css(parseColor(value)));
    }
    for (const [name, value] of Object.entries(this.theme.states)) {
      set(`--state-${name.replace(/_/g, '-')}`, css(parseColor(value)));
    }
    set('--font-body', this.font('body'));
    set('--font-title', this.font('title'));
    set('--font-num', this.font('numerals'));
    set('--size-body', `${this.fontSize('body')}px`);
    set('--size-title', `${this.fontSize('title')}px`);
    set('--size-num', `${this.fontSize('numerals')}px`);
    set('--weight-title', String(this.fontWeight('title')));
    set('--etch-strength', String(this.mat('etch', 0.5)));
    set('--bevel', `${this.mat('bevel', 3)}px`);
    set('--corner', `${this.mat('corner', 3)}px`);
    set('--shadow-strength', String(this.mat('shadow', 0.5)));
    set('--vignette', css(this.surface('edge_dark'), this.mat('vignette', 0.4)));
    set('--edge-bright', css(lighten(this.surface('edge'), 0.38)));
    set('--surface-texture', `url("${this.texture()}")`);
    for (const [name, value] of Object.entries(this.ui.layout.metrics)) {
      set(`--m-${name.replace(/_/g, '-')}`, `${value}px`);
    }
    root.dataset.frame = this.theme.frame;
  }

  /** The surface texture: grain, and seams for stone or fibres for parchment, as an SVG noise overlay. */
  texture(): string {
    const grain = this.mat('grain', 0.12);
    const fibre = this.mat('fibre', 0);
    const veins = this.mat('veins', 0);
    const dark = this.surface('edge_dark');
    const tint = `${dark.r.toFixed(3)} 0 0 0 0 0 ${dark.g.toFixed(3)} 0 0 0 0 0 ${dark.b.toFixed(3)} 0 0`;
    const layers = [
      `<filter id='g'><feTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2' stitchTiles='stitch'/><feColorMatrix values='0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ${(grain * 1.6).toFixed(3)} 0'/></filter><rect width='100%' height='100%' filter='url(#g)'/>`,
      `<filter id='c'><feTurbulence type='fractalNoise' baseFrequency='0.012' numOctaves='3' seed='3' stitchTiles='stitch'/><feColorMatrix values='${tint} 0 0 0 ${(0.35 + fibre * 0.3).toFixed(3)} -0.12'/></filter><rect width='100%' height='100%' filter='url(#c)'/>`,
    ];
    if (fibre > 0) {
      layers.push(`<filter id='f'><feTurbulence type='fractalNoise' baseFrequency='0.004 0.22' numOctaves='2' seed='7' stitchTiles='stitch'/><feColorMatrix values='${tint} 0 0 0 ${(fibre * 0.9).toFixed(3)} -0.38'/></filter><rect width='100%' height='100%' filter='url(#f)'/>`);
    }
    if (veins > 0) {
      layers.push(`<filter id='v'><feTurbulence type='turbulence' baseFrequency='0.006' numOctaves='3' seed='11' stitchTiles='stitch'/><feComponentTransfer><feFuncA type='table' tableValues='0 0 0 0 0 0 0 0 0 ${(veins * 1.4).toFixed(3)} 0 0 0 0'/></feComponentTransfer><feColorMatrix values='${tint} 0 0 0 1 0'/></filter><rect width='100%' height='100%' filter='url(#v)'/>`);
    }
    const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='512' height='512'>${layers.join('')}</svg>`;
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  }

  /**
   * A status's glyph and tint as content presents it: the icon and colour of its meta block (or its own colour),
   * else the icon set's glyph for it and its Thread's colour, else the accent.
   */
  statusFace(status: string): { icon: string; tint: Rgba } {
    const look = lookOf(this.names, status);
    const icon = look.icon !== null && this.icons.icons[look.icon] !== undefined ? look.icon : forStatus(this.icons, status);
    const thread = this.names.threadOfStatus.get(status);
    const tint = look.color !== null ? parseColor(look.color) : thread !== undefined ? this.thread(thread) : this.surface('accent');
    return { icon, tint };
  }

  /** A glyph of the icon set as an inline SVG, drawn in the current text colour. */
  icon(id: string, size: number, className = 'glyph'): SVGSVGElement {
    const set = this.icons;
    const resolved = resolveIcon(set, id);
    const svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('viewBox', `0 0 ${set.box} ${set.box}`);
    svg.setAttribute('width', String(size));
    svg.setAttribute('height', String(size));
    svg.setAttribute('class', className);
    svg.setAttribute('aria-hidden', 'true');
    const path = document.createElementNS(SVG, 'path');
    path.setAttribute('d', set.icons[resolved] ?? '');
    if (set.filled.has(resolved)) {
      path.setAttribute('fill', 'currentColor');
    } else {
      path.setAttribute('fill', 'none');
      path.setAttribute('stroke', 'currentColor');
      path.setAttribute('stroke-width', String(set.stroke));
      path.setAttribute('stroke-linecap', 'round');
      path.setAttribute('stroke-linejoin', 'round');
    }
    svg.append(path);
    return svg;
  }

  /** A glyph as a Path2D in its view box, for canvases. */
  iconPath(id: string): Path2D {
    return new Path2D(this.icons.icons[resolveIcon(this.icons, id)] ?? '');
  }
}
