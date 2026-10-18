import './cluster.css';
import { clear, el } from './dom';
import { lookOf, type UiOrb } from './logic/content';
import { seatArt } from './abilityArt';
import { seatEmpty, seatReadyIn, type HeroState, type Seat } from './logic/heroState';
import { clusterLayout, ringRadius, rimRadius, type Box, type ClusterLayout } from './logic/layout';
import { badgeText, trayBadge, traySignature, type TrayRow } from './logic/menu';
import { arc, cracks, noise, roundShadow, runeBand, ticks, track } from './paint';
import { css, darken, lerp, lighten, type Rgba, type Skin } from './skin';
import { badge } from './widgets';

/**
 * The cluster at the bottom centre: the orbs the layout names (life on the left, mana on the right) with the
 * pools on and around them, the weave bar between them, the cast bar and the status row above it, and the menu
 * button with its tray beside the mana orb. It lays itself out every frame for the viewport and publishes the
 * rectangle it drew, which every pane fits above. The desktop client's UI/Cluster.
 */
export class ClusterView {
  readonly root: HTMLElement;
  layout: ClusterLayout | null = null;
  private readonly orbs: OrbView[] = [];
  private readonly bar: WeaveBarView;
  private readonly cast: CastBarView;
  private readonly status: StatusRowView;
  private readonly menu: MenuButtonView;
  readonly tray: MenuTrayView;

  constructor(
    private readonly skin: Skin,
    handlers: { slot: (slot: number) => void; menu: () => void; pick: (pageId: string) => void },
  ) {
    this.root = el('div', { class: 'layer cluster' });
    for (const orb of skin.ui.layout.orbs) {
      const view = new OrbView(skin, orb);
      this.orbs.push(view);
      this.root.append(view.root);
    }
    this.bar = new WeaveBarView(skin, handlers.slot);
    this.cast = new CastBarView(skin);
    this.status = new StatusRowView(skin);
    this.menu = new MenuButtonView(skin, handlers.menu);
    this.tray = new MenuTrayView(skin, handlers.pick);
    this.root.append(this.bar.root, this.cast.root, this.status.root, this.menu.root, this.tray.root);
  }

  /** The rectangle the cluster drew this frame, in interface pixels. */
  get drawn(): Box {
    return this.layout?.rect ?? { x: 0, y: 0, w: 0, h: 0 };
  }

  update(
    viewW: number,
    viewH: number,
    hero: HeroState,
    now: number,
    rows: readonly TrayRow[],
    trayOpen: boolean,
    labels: { menu: string; slot: (index: number) => string },
    pixelScale: number,
    reduced: boolean,
  ): void {
    const layout = clusterLayout(this.skin.ui.layout, viewW, viewH, this.skin.metric('scale', 1));
    this.layout = layout;
    for (const orb of this.orbs) {
      orb.update(layout, orb.side === 'right' ? layout.rightOrb : layout.leftOrb, hero, now, pixelScale, reduced);
    }
    this.bar.update(layout, hero, now, labels.slot);
    this.cast.update(layout, hero, now);
    this.status.update(layout, hero, now);
    this.menu.update(layout, trayBadge(rows), trayOpen, labels.menu);
    this.tray.update(layout, viewW, rows, trayOpen);
  }
}

const place = (node: HTMLElement, b: Box, origin: Box | null = null): void => {
  node.style.left = `${b.x - (origin?.x ?? 0)}px`;
  node.style.top = `${b.y - (origin?.y ?? 0)}px`;
  node.style.width = `${b.w}px`;
  node.style.height = `${b.h}px`;
};

const clamp01 = (v: number): number => Math.min(1, Math.max(0, v));
const smooth = (e0: number, e1: number, x: number): number => {
  const t = clamp01((x - e0) / (e1 - e0));
  return t * t * (3 - 2 * t);
};

// ---------------------------------------------------------------- orbs

/**
 * One orb and the pools drawn on and around it, nearest first: the fill that drains from the top with a moving
 * surface, overlays laid over it from the top (a shield veils the orb and pulses along its edge while it
 * recharges), the reserved cap darkening the top, the etched rim band hugging the orb (ward), and concentric
 * rings around it (poise, insight, stamina). A pool the hero has no maximum in is not drawn. Its tooltip names
 * every pool on the orb with the numbers the server sent.
 */
class OrbView {
  readonly root: HTMLElement;
  private readonly canvas: HTMLCanvasElement;
  private readonly g: CanvasRenderingContext2D | null;
  private readonly current: HTMLElement;
  private readonly maximum: HTMLElement;
  private readonly mirror: boolean;
  private tip = '';
  private pattern: CanvasPattern | null = null;

  constructor(
    private readonly skin: Skin,
    private readonly orb: UiOrb,
  ) {
    this.mirror = orb.side === 'right';
    this.canvas = el('canvas', { class: 'orb-canvas' });
    this.g = this.canvas.getContext('2d');
    this.current = el('span', { class: 'orb-current num' });
    this.maximum = el('span', { class: 'orb-max num' });
    this.root = el('div', { class: 'orb', 'data-orb': orb.fill }, this.canvas, el('div', { class: 'orb-label' }, this.current, this.maximum));
  }

  get side(): string {
    return this.orb.side;
  }

  update(layout: ClusterLayout, box: Box, hero: HeroState, now: number, pixelScale: number, reduced: boolean): void {
    place(this.root, box);
    const ps = Math.max(1, pixelScale);
    const cw = Math.round(box.w * ps);
    const ch = Math.round(box.h * ps);
    if (this.canvas.width !== cw || this.canvas.height !== ch) {
      this.canvas.width = cw;
      this.canvas.height = ch;
    }
    const { current, max } = hero.resource(this.orb.fill);
    const label = max > 0;
    this.current.textContent = label ? String(Math.round(current)) : '';
    this.maximum.textContent = label ? String(Math.round(max)) : '';
    this.root.style.setProperty('--orb-text', `${Math.max(10, this.skin.fontSize('numerals') * layout.scale)}px`);
    this.root.style.setProperty('--orb-label-y', `${layout.orbRadius * 0.52}px`);
    const tip = this.tooltip(hero);
    if (tip !== this.tip) {
      this.tip = tip;
      this.root.title = tip;
    }
    const g = this.g;
    if (g === null) {
      return;
    }
    g.setTransform(ps, 0, 0, ps, 0, 0);
    g.clearRect(0, 0, box.w, box.h);
    this.draw(g, layout, box, hero, now, reduced);
  }

  private tooltip(hero: HeroState): string {
    const lines: string[] = [];
    const lineFor = (pool: string, always: boolean) => {
      const { current, max } = hero.resource(pool);
      if (max <= 0 && !always) {
        return;
      }
      const state = hero.pool(pool)?.state;
      const reserved = hero.reserved(pool);
      let text = `${this.skin.names.resources.get(pool)?.name ?? pool}  ${Math.round(current)} / ${Math.round(max)}`;
      if (reserved > 0) {
        text += `  (${Math.round(reserved)} reserved)`;
      }
      if (state !== undefined && state !== 'ready') {
        text += `  ${state}`;
      }
      lines.push(text);
    };
    lineFor(this.orb.fill, true);
    for (const p of [...this.orb.overlays, ...this.orb.rim, ...this.orb.rings]) {
      lineFor(p, false);
    }
    return lines.join('\n');
  }

  private draw(g: CanvasRenderingContext2D, layout: ClusterLayout, box: Box, hero: HeroState, now: number, reduced: boolean): void {
    const s = this.skin;
    const cx = box.w / 2;
    const cy = box.h / 2;
    const r = layout.orbRadius;
    const dark = s.surface('edge_dark');
    const well = s.surface('well');
    const etchColor = s.surface('etch');
    const etchA = s.mat('etch', 0.55);
    roundShadow(g, cx, cy, r + layout.rimWidth);

    const { current, max } = hero.resource(this.orb.fill);
    const fill = max > 0 ? clamp01(current / max) : 0;
    const reserved = max > 0 ? clamp01(hero.reserved(this.orb.fill) / max) : 0;
    let veil = 0;
    let pulse = 0;
    for (const overlay of this.orb.overlays) {
      const o = hero.resource(overlay);
      if (o.max <= 0) {
        continue;
      }
      veil = clamp01(o.current / o.max);
      if (hero.pool(overlay)?.state === 'recharging') {
        pulse = reduced ? 0.35 : 0.5 + 0.5 * Math.sin(now * 4.2);
      }
      break;
    }
    this.liquid(g, cx, cy, r, fill, reserved, veil, pulse, reduced ? 0 : now * 0.6 + (this.mirror ? 2.3 : 0), reduced);

    // The setting: the orb sits in a bezel the width of the rim band.
    const rimR = rimRadius(layout);
    const rimW = layout.rimWidth;
    this.bezel(g, cx, cy, rimR, rimW);
    for (const pool of this.orb.rim) {
      const p = hero.resource(pool);
      if (p.max <= 0) {
        continue;
      }
      const frac = clamp01(p.current / p.max);
      const color = s.fill(pool);
      arc(g, cx, cy, rimR, rimW - 1.5, frac, css(darken(color, 0.05)), this.mirror);
      arc(g, cx, cy, rimR + rimW * 0.25, 1.2, frac, css(lighten(color, 0.35), 0.8), this.mirror);
      // Runes cut into the band: dark in gold where the ward stands, faint where it is spent.
      runeBand(g, cx, cy, rimR, rimW, 24, frac > 0.02 ? css(s.deep(pool), 0.85) : css(etchColor, etchA * 0.85));
      break;
    }

    // The rings, nearest first.
    for (let i = 0; i < this.orb.rings.length; i++) {
      const pool = this.orb.rings[i];
      const p = hero.resource(pool);
      if (p.max <= 0) {
        continue;
      }
      const radius = ringRadius(layout, i);
      const width = layout.ringWidth;
      const info = hero.pool(pool);
      const broken = info?.state === 'broken' || info?.state === 'rearming';
      track(g, cx, cy, radius, width + 2, css(dark, 0.9));
      track(g, cx, cy, radius, width, css(well, 0.92));
      const frac = clamp01(p.current / p.max);
      const color = broken ? s.state('broken', '#6d6d6d') : s.fill(pool);
      if (info?.state === 'rearming' && info.since !== undefined && info.until !== undefined && info.until > info.since && hero.hasServerClock) {
        // Re-arming: the ring fills back in dimly over the time the server named.
        const t = clamp01((hero.serverNow(now) - info.since) / (info.until - info.since));
        arc(g, cx, cy, radius, width - 1, t, css(s.fill(pool), 0.35), this.mirror);
      }
      arc(g, cx, cy, radius, width - 1, frac, css(color), this.mirror);
      arc(g, cx, cy, radius + width * 0.3, 1, frac, css(lighten(color, 0.4), 0.55), this.mirror);
      ticks(g, cx, cy, radius - width / 2, radius + width / 2, 15, css(etchColor, etchA * 0.45));
      if (broken) {
        cracks(g, cx, cy, radius, width, css(dark));
      }
    }
  }

  /** The pool in its glass: the theme's orb shader as canvas gradients. */
  private liquid(g: CanvasRenderingContext2D, cx: number, cy: number, r: number, fill: number, reserved: number, veil: number, pulse: number, t: number, reduced: boolean): void {
    const s = this.skin;
    const top = cy - r;
    const d = r * 2;
    const fillColor = s.fill(this.orb.fill);
    const deep = s.deep(this.orb.fill);
    const glass = s.surface('glass', '#ffffff');
    const glassStrength = s.mat('glass', 0.22);
    g.save();
    g.beginPath();
    g.arc(cx, cy, r, 0, Math.PI * 2);
    g.clip();
    const empty = darken(s.surface('well'), 0.2);
    const eg = g.createLinearGradient(0, top, 0, top + d);
    eg.addColorStop(0, css(darken(empty, 0.35)));
    eg.addColorStop(1, css(empty));
    g.fillStyle = eg;
    g.fillRect(cx - r, top, d, d);
    this.pattern ??= g.createPattern(noise(), 'repeat');
    const calm = reduced ? 0 : smooth(0, 0.04, fill) * (1 - smooth(0.96, 1, fill));
    if (fill > 0.001) {
      const level = top + d * (1 - fill);
      const surface = (x: number) => {
        const u = (x + r) / d;
        return level - (Math.sin(u * 9 + t * 1.7) * 0.012 + Math.sin(u * 17 - t * 2.3) * 0.006) * d * calm;
      };
      g.beginPath();
      g.moveTo(cx - r, top + d);
      for (let x = -r; x <= r + 0.01; x += 3) {
        g.lineTo(cx + x, surface(x));
      }
      g.lineTo(cx + r, top + d);
      g.closePath();
      const lg = g.createLinearGradient(0, level, 0, top + d);
      lg.addColorStop(0, css(fillColor));
      lg.addColorStop(1, css(lerp(fillColor, deep, 0.8)));
      g.fillStyle = lg;
      g.fill();
      if (this.pattern !== null) {
        g.save();
        g.clip();
        g.globalAlpha = 0.1;
        g.globalCompositeOperation = 'overlay';
        g.translate((t * 9) % 96, (-t * 14) % 96);
        g.fillStyle = this.pattern;
        g.fillRect(cx - r - 96, top - 96, d + 192, d + 192);
        g.restore();
      }
      // The glint along the surface.
      g.beginPath();
      for (let x = -r; x <= r + 0.01; x += 3) {
        if (x === -r) {
          g.moveTo(cx + x, surface(x));
        } else {
          g.lineTo(cx + x, surface(x));
        }
      }
      g.strokeStyle = css(lighten(fillColor, 0.45), 0.15 + 0.5 * calm);
      g.lineWidth = 1.6;
      g.stroke();
    }
    if (reserved > 0) {
      const edge = top + d * reserved;
      g.fillStyle = css(s.state('reserved', '#07060c'), 0.8);
      g.fillRect(cx - r, top, d, edge - top);
      g.fillStyle = 'rgba(255, 255, 255, 0.22)';
      g.fillRect(cx - r, edge - 0.6, d, 1.2);
    }
    if (veil > 0) {
      // The shield: a cool glass over the orb from the top, frosted, lit along its edge and pulsing while it recharges.
      const veilColor = this.orb.overlays.length > 0 ? s.fill(this.orb.overlays[0]) : glass;
      const edge = top + d * veil;
      g.fillStyle = css(veilColor, 0.3);
      g.fillRect(cx - r, top, d, edge - top);
      if (this.pattern !== null) {
        g.save();
        g.beginPath();
        g.rect(cx - r, top, d, edge - top);
        g.clip();
        g.globalAlpha = 0.12;
        g.globalCompositeOperation = 'screen';
        g.translate((t * 4) % 96, 0);
        g.fillStyle = this.pattern;
        g.fillRect(cx - r - 96, top, d + 192, d);
        g.restore();
      }
      g.fillStyle = css(veilColor, 0.45 + 0.55 * pulse);
      g.fillRect(cx - r, edge - (1 + 1.5 * pulse), d, 2 + 3 * pulse);
    }
    const vg = g.createRadialGradient(cx, cy, r * 0.42, cx, cy, r);
    vg.addColorStop(0, 'rgba(0, 0, 0, 0)');
    vg.addColorStop(1, 'rgba(0, 0, 0, 0.58)');
    g.fillStyle = vg;
    g.fillRect(cx - r, top, d, d);
    g.save();
    g.translate(cx - 0.32 * r, cy - 0.44 * r);
    g.scale(1, 1 / 1.75);
    const hg = g.createRadialGradient(0, 0, 0, 0, 0, r * 0.5);
    hg.addColorStop(0, css(glass, glassStrength));
    hg.addColorStop(1, css(glass, 0));
    g.fillStyle = hg;
    g.beginPath();
    g.arc(0, 0, r * 0.5, 0, Math.PI * 2);
    g.fill();
    g.restore();
    g.beginPath();
    g.arc(cx, cy, r * 0.95, 0.12 * Math.PI, 0.88 * Math.PI);
    g.strokeStyle = css(glass, glassStrength * 0.5);
    g.lineWidth = r * 0.06;
    g.stroke();
    g.restore();
  }

  private bezel(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number, width: number): void {
    const s = this.skin;
    const dark = s.surface('edge_dark');
    const edge = s.surface('edge');
    if (s.brass) {
      const bright = lighten(edge, 0.38);
      for (let i = 0; i < 4; i++) {
        const t = i / 3;
        const col: Rgba = t < 0.34 ? lerp(dark, bright, t / 0.34) : lerp(bright, darken(edge, 0.2), (t - 0.34) / 0.66);
        track(g, cx, cy, radius - width / 2 + (width * (i + 0.5)) / 4, width / 4 + 0.6, css(col));
      }
    } else {
      track(g, cx, cy, radius, width, css(darken(edge, 0.35)));
      g.beginPath();
      g.arc(cx, cy, radius + width / 2 - 0.5, Math.PI * 1.05, Math.PI * 1.95);
      g.strokeStyle = css(lighten(edge, 0.1), 0.7);
      g.lineWidth = 1.2;
      g.stroke();
      g.beginPath();
      g.arc(cx, cy, radius - width / 2 + 0.5, Math.PI * 0.05, Math.PI * 0.95);
      g.strokeStyle = css(edge, 0.35);
      g.lineWidth = 1;
      g.stroke();
    }
    track(g, cx, cy, radius + width / 2 + 0.5, 1.2, css(dark));
    track(g, cx, cy, radius - width / 2 - 0.3, 1.2, css(dark));
  }
}

// ---------------------------------------------------------------- the weave bar

interface SlotParts {
  root: HTMLButtonElement;
  sweep: HTMLElement;
  cooldown: HTMLElement;
  signature: string;
}

/**
 * The weave bar between the orbs: the weave slots in rack order, each with its glyph tinted by its Thread, the
 * cooldown sweep drawn against the full cooldown the server sent, the cost in its resource's colour, use pips,
 * self stacks, a converted or meta face when the slot has one, lit while its toggle or contract runs, and a
 * greyed, hatched face with its reason when the weave is dormant. A click casts the slot as its key would.
 */
class WeaveBarView {
  readonly root: HTMLElement;
  private readonly slots: SlotParts[] = [];

  constructor(
    private readonly skin: Skin,
    private readonly press: (slot: number) => void,
  ) {
    this.root = el('div', { class: 'weave-bar', id: 'hud-weaves' });
  }

  update(layout: ClusterLayout, hero: HeroState, now: number, keyLabel: (index: number) => string): void {
    place(this.root, layout.bar);
    while (this.slots.length < layout.slots.length) {
      const index = this.slots.length;
      const sweep = el('span', { class: 'slot-sweep' });
      const cooldown = el('span', { class: 'slot-cooldown num' });
      const root = el('button', { class: 'slot', type: 'button', tabindex: '-1', 'data-slot': String(index) });
      root.addEventListener('pointerdown', (e) => {
        if (e.button === 0) {
          e.stopPropagation();
          this.press(index);
        }
      });
      root.addEventListener('contextmenu', (e) => e.preventDefault());
      this.root.append(root);
      this.slots.push({ root, sweep, cooldown, signature: '' });
    }
    const seats = hero.seats(layout.slots.length);
    const cast = hero.cast;
    for (let i = 0; i < this.slots.length; i++) {
      const parts = this.slots[i];
      const box = layout.slots[i];
      parts.root.hidden = box === undefined;
      if (box === undefined) {
        continue;
      }
      place(parts.root, box, layout.bar);
      const seat = seats[i];
      const casting = cast !== null && cast.slot === i;
      const label = keyLabel(i);
      const signature = this.signature(seat, casting, label, layout.slotSize);
      if (signature !== parts.signature) {
        parts.signature = signature;
        this.build(parts, seat, i, casting, label, layout.slotSize);
      }
      const left = seat === undefined || seatEmpty(seat) ? 0 : seatReadyIn(seat, now);
      if (left > 0.05 && seat.cooldown > 0) {
        parts.sweep.style.setProperty('--sweep', `${(clamp01(left / seat.cooldown) * 360).toFixed(1)}deg`);
        parts.sweep.hidden = false;
        parts.cooldown.textContent = left >= 10 ? String(Math.ceil(left)) : left.toFixed(1);
      } else if (!parts.sweep.hidden) {
        parts.sweep.hidden = true;
        parts.cooldown.textContent = '';
      }
    }
  }

  private signature(seat: Seat | undefined, casting: boolean, label: string, size: number): string {
    if (seat === undefined || seatEmpty(seat)) {
      return `empty|${label}|${size}`;
    }
    const info = seat.info;
    return [seat.key, seat.name, seat.icon, seat.threads.join(','), seat.dormant, Object.entries(seat.costs).map(([k, v]) => `${k}${Math.round(v)}`).join(','),
      `${seat.uses}/${seat.usesMax}`, info?.form, info?.preset, info?.stacks, seat.converted?.icon, seat.converted?.weave, seat.converted?.form, seat.converted?.label, seat.meta?.label, info?.active, casting, label, size, info?.level, info?.preview.map((p) => p.text).join(';')].join('|');
  }

  private build(parts: SlotParts, seat: Seat | undefined, index: number, casting: boolean, label: string, size: number): void {
    const s = this.skin;
    const root = parts.root;
    clear(root);
    root.className = 'slot';
    root.classList.toggle('casting', casting);
    const keySize = Math.max(9, Math.round(size * 0.22));
    root.style.setProperty('--key-size', `${keySize}px`);
    const key = el('span', { class: 'slot-key num', text: label });
    if (seat === undefined || seatEmpty(seat)) {
      root.classList.add('empty');
      root.title = `Seat ${index + 1}: empty`;
      root.append(key);
      return;
    }
    const dormant = seat.dormant !== null;
    const active = seat.info?.active === true;
    root.classList.toggle('dormant', dormant);
    root.classList.toggle('active', active);
    const face = seat.converted;
    const tint = dormant ? s.state('dormant', '#777777') : seat.threads.length > 0 ? s.thread(seat.threads[0]) : s.surface('label');
    root.style.setProperty('--tint', css(tint));
    root.append(el('span', { class: 'slot-halo' }), seatArt(s, seat, Math.round(size * 0.62), 'slot-glyph'));
    if (dormant) {
      root.append(el('span', { class: 'slot-hatch' }));
    }
    root.append(parts.sweep, parts.cooldown, key);
    const costs = el('span', { class: 'slot-costs num' });
    for (const [resource, amount] of Object.entries(seat.costs)) {
      if (amount <= 0) {
        continue;
      }
      const c = el('span', { text: String(Math.round(amount)) });
      c.style.color = css(lighten(s.fill(resource), 0.25));
      costs.append(c);
    }
    root.append(costs);
    if (seat.usesMax !== null && seat.usesMax > 1) {
      const pips = el('span', { class: 'slot-pips' });
      for (let i = 0; i < seat.usesMax; i++) {
        pips.append(el('i', { class: i < (seat.uses ?? 0) ? 'on' : '' }));
      }
      root.append(pips);
    }
    const stacks = seat.info?.stacks ?? 0;
    if (stacks > 0) {
      root.append(el('span', { class: 'slot-stacks num', text: `x${stacks}` }));
    }
    if (face !== null) {
      root.append(el('span', { class: 'slot-face' }));
    }
    if (seat.meta !== null) {
      root.append(el('span', { class: 'slot-meta', text: '+' }));
    }
    root.title = this.tip(seat, index, label);
  }

  private tip(seat: Seat, index: number, label: string): string {
    const lines = [`${seat.name}  (key ${label || index + 1})`];
    const info = seat.info;
    if (info !== null) {
      lines.push(`Level ${info.level}${info.cast_mode ? `  ${info.cast_mode}` : ''}${info.targeting ? `  ${info.targeting}` : ''}${info.active ? '  on' : ''}`);
      for (const p of info.preview) {
        lines.push(`${p.label}: ${p.text}`);
      }
      for (const glyph of info.glyphs) {
        lines.push(`Glyph ${glyph.name} ${glyph.level}${glyph.dormant ? `  (dormant: ${glyph.dormant})` : ''}`);
      }
      if (info.meta) {
        lines.push(`Second ability: ${info.meta.label}`);
      }
    }
    if (seat.dormant !== null) {
      lines.push(`Dormant: ${seat.dormant}`);
    }
    return lines.join('\n');
  }
}

// ---------------------------------------------------------------- the cast bar

/**
 * The bar above the weave bar while a cast runs, drawn by its mode: a cast or a charge-up fills with the
 * progress the server sent (run on with the duration it named until the next state); a channel shows its brim;
 * an overcharge shows its stages with a pip per stage banked; a raised guard shows the pool it has left over the
 * hold that remains. Hidden when nothing is cast.
 */
class CastBarView {
  readonly root: HTMLElement;
  private readonly fill: HTMLElement;
  private readonly brim: HTMLElement;
  private readonly mark: HTMLElement;
  private readonly stages: HTMLElement;
  private readonly hold: HTMLElement;
  private readonly name: HTMLElement;
  private stageSignature = '';

  constructor(private readonly skin: Skin) {
    this.fill = el('div', { class: 'cast-fill' });
    this.brim = el('div', { class: 'cast-brim' });
    this.mark = el('div', { class: 'cast-mark' });
    this.stages = el('div', { class: 'cast-stages' });
    this.hold = el('div', { class: 'cast-hold' });
    this.name = el('div', { class: 'cast-name' });
    this.root = el('div', { class: 'cast-bar', hidden: '' }, this.brim, this.fill, this.hold, this.stages, this.mark, this.name);
  }

  update(layout: ClusterLayout, hero: HeroState, now: number): void {
    const cast = hero.cast;
    this.root.hidden = cast === null;
    if (cast === null) {
      return;
    }
    place(this.root, layout.cast);
    let progress = cast.progress;
    const timed = cast.mode === 'cast' || cast.mode === 'charge' || cast.mode === 'guard';
    if (cast.duration !== undefined && cast.duration > 0 && timed) {
      progress = Math.min(1, progress + (now - hero.selfAt) / cast.duration);
    }
    progress = clamp01(progress);
    const seats = cast.slot >= 0 ? hero.seats(Math.max(8, cast.slot + 1)) : [];
    const thread = seats[cast.slot]?.threads[0];
    const color = thread !== undefined ? this.skin.thread(thread) : this.skin.surface('accent');
    this.root.dataset.mode = cast.mode;
    this.root.style.setProperty('--cast-color', css(color));
    const guard = cast.mode === 'guard';
    if (guard) {
      const guardColor = this.skin.theme.states.guard !== undefined ? this.skin.state('guard', '#ffffff') : this.skin.fill('ward');
      this.root.style.setProperty('--guard-color', css(guardColor));
      this.fill.style.width = `${clamp01(cast.pool ?? 1) * 100}%`;
      this.hold.hidden = false;
      this.hold.style.width = `${(1 - progress) * 100}%`;
    } else {
      this.fill.style.width = `${progress * 100}%`;
      this.hold.hidden = true;
    }
    const brim = cast.mode === 'channel' && cast.brim !== undefined && cast.brim > 0 && cast.brim < 1 ? cast.brim : null;
    this.brim.hidden = brim === null;
    this.mark.hidden = brim === null;
    if (brim !== null) {
      this.brim.style.left = `${brim * 100}%`;
      this.mark.style.left = `${brim * 100}%`;
    }
    const stageSignature = `${cast.stages}:${cast.stage}`;
    if (stageSignature !== this.stageSignature) {
      this.stageSignature = stageSignature;
      clear(this.stages);
      if (cast.stages > 1 && !guard) {
        for (let i = 1; i < cast.stages; i++) {
          const line = el('i', { class: 'cast-stage-line' });
          line.style.left = `${(i / cast.stages) * 100}%`;
          this.stages.append(line);
        }
        for (let i = 0; i < cast.stages; i++) {
          const pip = el('b', { class: `cast-pip${i < cast.stage ? ' on' : ''}` });
          pip.style.left = `${((i + 0.5) / cast.stages) * 100}%`;
          this.stages.append(pip);
        }
      }
    }
    let name = cast.name ?? '';
    if (cast.mode === 'overcharge' && cast.stages > 0) {
      name = name.length > 0 ? `${name}  ${cast.stage}/${cast.stages}` : `${cast.stage}/${cast.stages}`;
    }
    if (this.name.textContent !== name) {
      this.name.textContent = name;
    }
    this.root.style.setProperty('--cast-text', `${Math.max(10, layout.cast.h * 0.85)}px`);
  }
}

// ---------------------------------------------------------------- statuses

/**
 * The hero's statuses in a row above the cast bar: each a glyph tinted by the Thread whose ailment it is, its
 * stacks, and a sweep for the time left against its full duration. The tooltip names each status from content.
 */
class StatusRowView {
  readonly root: HTMLElement;
  private signature = '';
  private readonly sweeps: HTMLElement[] = [];

  constructor(private readonly skin: Skin) {
    this.root = el('div', { class: 'status-row', id: 'hud-statuses' });
  }

  update(layout: ClusterLayout, hero: HeroState, now: number): void {
    // Statuses content marks hidden (the pool states the orbs draw) stay out of the row.
    const statuses = hero.statuses(now).filter((s) => !lookOf(this.skin.names, s.id).hidden);
    const icon = layout.statusIcon;
    const width = statuses.length * (icon + 4);
    place(this.root, { x: layout.status.x + layout.status.w / 2 - width / 2, y: layout.status.y, w: Math.max(1, width), h: layout.status.h });
    const signature = `${icon}|${statuses.map((s) => `${s.id}:${s.stacks}`).join(',')}`;
    if (signature !== this.signature) {
      this.signature = signature;
      clear(this.root);
      this.sweeps.length = 0;
      for (const s of statuses) {
        const look = lookOf(this.skin.names, s.id);
        const face = this.skin.statusFace(s.id);
        const item = el('div', { class: `status kind-${look.kind}${look.cue === 'pulse' ? ' cue-pulse' : ''}`, 'data-status': s.id });
        item.style.width = `${icon}px`;
        item.style.height = `${icon}px`;
        item.style.color = css(face.tint);
        item.append(this.skin.icon(face.icon, Math.round(icon * 0.78)));
        const sweep = el('span', { class: 'status-sweep' });
        item.append(sweep);
        this.sweeps.push(sweep);
        if (s.stacks > 1) {
          item.append(el('span', { class: 'status-stacks num', text: String(s.stacks) }));
        }
        this.root.append(item);
      }
    }
    statuses.forEach((s, i) => {
      const sweep = this.sweeps[i];
      if (sweep === undefined) {
        return;
      }
      if (s.duration > 0 && s.remaining >= 0) {
        sweep.hidden = false;
        sweep.style.setProperty('--sweep', `${((1 - clamp01(s.remaining / s.duration)) * 360).toFixed(1)}deg`);
      } else {
        sweep.hidden = true;
      }
    });
    const tip = statuses.map((s) => `${this.skin.names.statuses.get(s.id)?.name ?? s.id}${s.stacks > 1 ? ` x${s.stacks}` : ''}${s.remaining >= 0 ? `  ${s.remaining.toFixed(1)}s` : ''}`).join('\n');
    if (this.root.title !== tip) {
      this.root.title = tip;
    }
  }
}

// ---------------------------------------------------------------- the menu

/** The menu button beside the mana orb: it wears the sum of the pips while the tray is closed. */
class MenuButtonView {
  readonly root: HTMLButtonElement;
  private readonly badgeHost: HTMLElement;
  private state = '';

  constructor(skin: Skin, press: () => void) {
    this.badgeHost = el('span', { class: 'menu-badge' });
    this.root = el('button', { class: 'menu-button', type: 'button', tabindex: '-1', title: 'Menu', id: 'hud-menu' });
    this.root.append(skin.icon('menu', 26), this.badgeHost);
    this.root.addEventListener('pointerdown', (e) => {
      if (e.button === 0) {
        e.stopPropagation();
        press();
      }
    });
  }

  update(layout: ClusterLayout, pips: number, open: boolean, keyLabel: string): void {
    place(this.root, layout.menu);
    const state = `${pips}|${open}|${keyLabel}|${layout.menu.w}`;
    if (state === this.state) {
      return;
    }
    this.state = state;
    this.root.classList.toggle('open', open);
    this.root.title = keyLabel.length > 0 ? `Menu (${keyLabel})` : 'Menu';
    const svg = this.root.querySelector('svg');
    if (svg !== null) {
      const size = Math.round(layout.menu.w * 0.56);
      svg.setAttribute('width', String(size));
      svg.setAttribute('height', String(size));
    }
    clear(this.badgeHost);
    if (!open && pips > 0) {
      this.badgeHost.append(badge(badgeText(pips)));
    }
  }
}

/** The tray of pages the menu button fans upward: each page with its glyph, name and key, sealed ones greyed with the reason. */
export class MenuTrayView {
  readonly root: HTMLElement;
  private signature = '';

  constructor(
    private readonly skin: Skin,
    private readonly pick: (pageId: string) => void,
  ) {
    this.root = el('div', { class: 'menu-tray pane-like', id: 'hud-tray', role: 'menu', hidden: '' });
  }

  get open(): boolean {
    return !this.root.hidden;
  }

  update(layout: ClusterLayout, viewW: number, rows: readonly TrayRow[], open: boolean): void {
    this.root.hidden = !open;
    if (!open) {
      return;
    }
    const visible = rows.filter((r) => r.state !== 'hidden');
    const rowH = this.skin.metric('tray_row', 34) * layout.scale;
    const width = this.skin.metric('tray_width', 270) * layout.scale;
    const height = visible.length * rowH + 20;
    const x = Math.min(Math.max(layout.menu.x + layout.menu.w - width, 8), Math.max(8, viewW - width - 8));
    place(this.root, { x, y: Math.max(8, layout.menu.y - height - 8), w: width, h: height });
    this.root.style.setProperty('--row-h', `${rowH}px`);
    const signature = `${traySignature(rows)}|${rowH}`;
    if (signature === this.signature) {
      return;
    }
    this.signature = signature;
    clear(this.root);
    for (const row of visible) {
      const open = row.state === 'open';
      const button = el('button', { class: `tray-row${open ? '' : ' sealed'}${row.showing ? ' showing' : ''}`, type: 'button', tabindex: '-1', role: 'menuitem', 'data-page': row.page.id });
      button.title = open ? row.page.description : `${row.page.name}: ${row.reason ?? 'sealed here'}`;
      button.append(this.skin.icon(row.page.icon, Math.round(rowH * 0.6), 'tray-glyph'), el('span', { class: 'tray-name', text: row.page.name }));
      if (row.pips > 0 && open) {
        button.append(badge(badgeText(row.pips)));
      }
      if (!open) {
        button.append(this.skin.icon('lock', Math.round(rowH * 0.42), 'tray-lock'));
      }
      button.append(el('span', { class: 'tray-key num', text: row.keyLabel }));
      button.addEventListener('pointerdown', (e) => e.stopPropagation());
      button.addEventListener('click', (e) => {
        e.stopPropagation();
        if (open) {
          this.pick(row.page.id);
        }
      });
      this.root.append(button);
    }
  }

  /** Whether a client point is on the tray. */
  covers(x: number, y: number): boolean {
    if (this.root.hidden) {
      return false;
    }
    const r = this.root.getBoundingClientRect();
    return x >= r.left && x < r.right && y >= r.top && y < r.bottom;
  }
}
