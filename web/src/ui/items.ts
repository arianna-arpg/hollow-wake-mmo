import type { ItemInfo } from '../net/protocol';
import { clear, el } from './dom';
import type { Carry, MenuAction } from './leaf';
import { humanize } from './logic/content';
import { css, darken, lerp, lighten, luminance, type Skin } from './skin';
import './widgets';

/**
 * How items look on boards, the doll and the carry, and the card shown while one is hovered: the browser twin of
 * the desktop client's UI/Core/ItemArt.cs, ItemCard.cs and CarryGhost. Every value is as the server sent it.
 */

/** The glyph for an item: its own icon if the set has it, its slot's, its class's family, else none. */
export function itemGlyph(skin: Skin, item: ItemInfo): string | null {
  const set = skin.icons;
  if (item.icon && set.icons[item.icon] !== undefined) {
    return item.icon;
  }
  if (item.slot && set.slots[item.slot] !== undefined) {
    return set.slots[item.slot];
  }
  const cls = item.class ?? '';
  if (cls.startsWith('glyph')) {
    return 'gem';
  }
  if (cls.startsWith('form')) {
    return 'weave';
  }
  if (cls.startsWith('weapon')) {
    return 'blade';
  }
  if (cls.startsWith('shield')) {
    return 'shield';
  }
  // armor.head, jewelry.ring: the part after the family names the slot whose glyph the item wears.
  const dot = cls.indexOf('.');
  if (dot > 0 && set.slots[cls.slice(dot + 1)] !== undefined) {
    return set.slots[cls.slice(dot + 1)];
  }
  return null;
}

/**
 * An item as a tile: on the pane's own inset tinted toward its rarity, framed in the rarity's colour, its glyph
 * (or its initial) in the rarity's ink, its count, a lock when locked, and a warning wash when a requirement is
 * unmet. Sized by the caller (absolute boxes on boards, a fixed square in a slot).
 */
export function itemTile(skin: Skin, item: ItemInfo, glyphSize: number): HTMLElement {
  const rarity = skin.rarity(item.rarity);
  const floor = lerp(skin.surface('inset'), rarity, 0.16);
  const ink = luminance(floor) > 0.5 ? darken(rarity, 0.1) : lighten(rarity, 0.15);
  const tile = el('div', { class: 'item-tile', 'data-uid': String(item.uid) });
  tile.style.setProperty('--tile-floor', css(floor, 0.95));
  tile.style.setProperty('--tile-edge', css(rarity, 0.85));
  tile.style.setProperty('--tile-ink', css(ink));
  const glyph = itemGlyph(skin, item);
  if (glyph !== null) {
    tile.append(skin.icon(glyph, Math.max(10, Math.round(glyphSize)), 'tile-glyph'));
  } else {
    const letter = el('span', { class: 'tile-letter', text: item.name.length > 0 ? item.name[0].toUpperCase() : '?' });
    letter.style.fontSize = `${Math.max(10, Math.round(glyphSize * 0.9))}px`;
    tile.append(letter);
  }
  if (item.count > 1) {
    tile.append(el('span', { class: 'tile-count', text: String(item.count) }));
  }
  if (item.locked === true) {
    tile.append(skin.icon('lock', 11, 'tile-lock'));
  }
  if (item.requirements?.some((r) => !r.met)) {
    tile.classList.add('unmet');
  }
  return tile;
}

const kindOf = (id: string): string => humanize(id.replace(/\./g, '_'));

/** The card shown while an item is hovered, with the worn item beside it for comparison. */
export class ItemCard {
  readonly root: HTMLElement;
  shownUid = '';

  constructor(private readonly skin: Skin) {
    this.root = el('div', { class: 'item-cards', hidden: '' });
  }

  hide(): void {
    if (!this.root.hidden) {
      this.root.hidden = true;
      this.shownUid = '';
    }
  }

  /**
   * Shows the card near a point in interface pixels inside a viewport of the given size.
   * @param extra lines a surface adds (a vendor's price).
   * @param seatName the name of a seated weave by slot, for a stone's fits.
   */
  show(item: ItemInfo, x: number, y: number, viewW: number, viewH: number, worn: ItemInfo | null = null, extra: readonly string[] = [], seatName?: (slot: number) => string): void {
    clear(this.root);
    this.root.append(this.card(item, null, extra, seatName));
    if (worn !== null && String(worn.uid) !== String(item.uid)) {
      this.root.append(this.card(worn, 'Worn', [], seatName));
    }
    this.root.hidden = false;
    this.shownUid = String(item.uid);
    // Measure, then keep it inside the view: right of the pointer, else left of it.
    this.root.style.left = '0px';
    this.root.style.top = '0px';
    const w = this.root.offsetWidth;
    const h = this.root.offsetHeight;
    let left = x + 18;
    if (left + w > viewW - 6) {
      left = x - w - 18;
    }
    const top = Math.min(Math.max(6, y - 20), Math.max(6, viewH - h - 6));
    this.root.style.left = `${Math.max(6, left)}px`;
    this.root.style.top = `${top}px`;
  }

  private card(item: ItemInfo, caption: string | null, extra: readonly string[], seatName?: (slot: number) => string): HTMLElement {
    const s = this.skin;
    const card = el('div', { class: 'item-card' });
    card.style.setProperty('--card-edge', css(s.rarity(item.rarity), 0.8));
    const add = (text: string, className = '', color?: string) => {
      const row = el('div', { class: `card-line${className ? ` ${className}` : ''}`, text });
      if (color) {
        row.style.color = color;
      }
      card.append(row);
      return row;
    };
    if (caption !== null) {
      add(caption, 'muted caption');
    }
    add(item.name, 'card-name', css(s.rarity(item.rarity)));
    const kind = [item.base !== item.name ? kindOf(item.base) : null, item.class ? kindOf(item.class) : null].filter((t): t is string => !!t).join('  ');
    if (kind.length > 0) {
      add(kind, 'muted');
    }
    const meta: string[] = [];
    if (item.level > 0) {
      meta.push(`Item level ${item.level}`);
    }
    if (item.count > 1) {
      meta.push(`${item.count} in the stack`);
    }
    if (item.quality !== undefined && item.quality !== null) {
      meta.push(`Quality ${Math.round(item.quality * 100) / 100}`);
    }
    if (meta.length > 0) {
      add(meta.join('   '), 'muted');
    }
    const reqs = item.requirements ?? [];
    if (reqs.length > 0) {
      add(`Requires ${reqs.map((r) => r.text).join(', ')}`, reqs.every((r) => r.met) ? 'muted gap' : 'bad gap');
    }
    item.lines.forEach((text, i) => add(text, i === 0 ? 'gap' : ''));
    if (item.form) {
      add(`Teaches ${item.form.name} ${item.form.level}, ${item.form.sockets} socket${item.form.sockets === 1 ? '' : 's'}`, 'accent gap');
    }
    if (item.glyph) {
      add(`Glyph ${item.glyph.name} ${item.glyph.level}`, 'accent gap');
      for (const row of item.glyph.rows ?? []) {
        add(row);
      }
    }
    const fits = item.fits ?? [];
    if (fits.length > 0) {
      add('Fits the seated weaves:', 'muted gap');
      for (const fit of fits) {
        const name = seatName?.(fit.slot) ?? `Seat ${fit.slot + 1}`;
        const [text, cls] = fit.fit === 'fits' ? ['fits', 'good']
          : fit.fit === 'misfit' ? [`dormant: ${fit.reason ?? 'misfit'}`, 'bad']
          : fit.fit === 'full' ? ['sockets full', 'muted']
          : fit.fit === 'no_sockets' ? ['no sockets', 'muted']
          : [fit.fit, 'muted'];
        add(`  ${name}: ${text}`, cls);
      }
    }
    const sockets = item.sockets ?? [];
    if (sockets.length > 0) {
      add(`Sockets: ${sockets.map((k) => k.stone?.name ?? 'empty').join('  ')}`, 'muted gap');
    }
    if (item.flavor) {
      add(item.flavor, 'flavor gap');
    }
    extra.forEach((text, i) => add(text, i === 0 ? 'accent gap' : 'accent'));
    return card;
  }
}

/** The carried item, following the pointer. */
export class CarryGhost {
  readonly root: HTMLElement;
  private uid = '';

  constructor(private readonly skin: Skin) {
    this.root = el('div', { class: 'carry-ghost', hidden: '' });
  }

  /** Follows a carry at a point in interface pixels with a cell size; null hides it. */
  follow(carry: Carry | null, x: number, y: number, cell: number): void {
    if (carry === null) {
      if (!this.root.hidden) {
        this.root.hidden = true;
        this.uid = '';
        clear(this.root);
      }
      return;
    }
    const w = carry.w * cell;
    const h = carry.h * cell;
    const uid = String(carry.item.uid);
    if (uid !== this.uid) {
      this.uid = uid;
      clear(this.root);
      this.root.append(itemTile(this.skin, carry.item, Math.min(w, h) * 0.68));
    }
    this.root.hidden = false;
    this.root.style.width = `${w}px`;
    this.root.style.height = `${h}px`;
    this.root.style.transform = `translate(${x - w / 2}px, ${y - h / 2}px)`;
  }
}

/** A small menu of actions at a point (right-click quick actions), closed by a pick, a click elsewhere or Escape. */
export class QuickMenu {
  readonly root: HTMLElement;

  constructor() {
    this.root = el('div', { class: 'quick-menu', role: 'menu', hidden: '' });
  }

  get open(): boolean {
    return !this.root.hidden;
  }

  show(x: number, y: number, actions: readonly MenuAction[], viewW: number, viewH: number): void {
    if (actions.length === 0) {
      return;
    }
    clear(this.root);
    for (const action of actions) {
      const b = el('button', { class: 'quick-item', type: 'button', role: 'menuitem', tabindex: '-1', text: action.label });
      b.addEventListener('pointerdown', (e) => e.stopPropagation());
      b.addEventListener('click', (e) => {
        e.stopPropagation();
        this.hide();
        action.act();
      });
      this.root.append(b);
    }
    this.root.hidden = false;
    this.root.style.left = '0px';
    this.root.style.top = '0px';
    const w = this.root.offsetWidth;
    const h = this.root.offsetHeight;
    this.root.style.left = `${Math.max(4, Math.min(x, viewW - w - 4))}px`;
    this.root.style.top = `${Math.max(4, Math.min(y, viewH - h - 4))}px`;
  }

  hide(): void {
    this.root.hidden = true;
  }
}
